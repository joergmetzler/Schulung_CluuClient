﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Cluu;
using Cluu.Hosting;
using SampleSolutionWpf.Identity;
using SampleSolutionWpf.Login;

namespace SampleSolutionWpf.Middleware;

internal class CluuWpfMiddleware : ICluuWpfMiddleware
{
    private readonly ICluuIdentityAccessor cluuIdentityAccessor;
    private readonly WindowFactory<CluuLoginWindow> loginWindowFactory;
    private readonly IServiceProvider serviceProvider;
    private readonly IWpfCluuIdentityProvider wpfCluuIdentityProvider;

    public CluuWpfMiddleware(IServiceProvider serviceProvider,
        ICluuIdentityAccessor cluuIdentityAccessor,
        IWpfCluuIdentityProvider wpfCluuIdentityProvider,
        WindowFactory<CluuLoginWindow> loginWindowFactory)
    {
        this.serviceProvider = serviceProvider;
        this.cluuIdentityAccessor = cluuIdentityAccessor;
        this.wpfCluuIdentityProvider = wpfCluuIdentityProvider;
        this.loginWindowFactory = loginWindowFactory;
    }

    async Task ICluuWpfMiddleware.InvokeAsync(Func<CancellationToken, Task> requestDelegate, CancellationToken cancellationToken)
    {
        using (this.serviceProvider.EnterAllCluuScopes())
        {
            var identity = await this.EnsureIdentityAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                using (this.cluuIdentityAccessor.Impersonate(identity))
                {
                    await requestDelegate(cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                // Schweinehack weil Exception noch internal ist.
                if (string.Equals(exception.GetType().FullName, "Cluu.Security.AuthenticationFailedException"))
                {
                    // Maybe token is expired. Refresh login and try again
                    identity = await this.RefreshIdentityAsync(cancellationToken).ConfigureAwait(false);

                    if (identity == null)
                    {
                        throw;
                    }

                    using (this.cluuIdentityAccessor.Impersonate(identity))
                    {
                        await requestDelegate(cancellationToken).ConfigureAwait(false);
                    }
                }
                else
                {
                    throw;
                }
            }
        }
    }

    async Task<TResult> ICluuWpfMiddleware.InvokeAsync<TResult>(Func<CancellationToken, Task<TResult>> requestDelegate, CancellationToken cancellationToken)
    {
        using (this.serviceProvider.EnterAllCluuScopes())
        {
            var identity = await this.EnsureIdentityAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                using (this.cluuIdentityAccessor.Impersonate(identity))
                {
                    return await requestDelegate(cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                // Schweinehack weil Exception noch internal ist.
                if (string.Equals(exception.GetType().FullName, "Cluu.Security.AuthenticationFailedException"))
                {
                    // Maybe token is expired. Refresh login and try again
                    identity = await this.RefreshIdentityAsync(cancellationToken).ConfigureAwait(false);

                    if (identity == null)
                    {
                        throw;
                    }

                    using (this.cluuIdentityAccessor.Impersonate(identity))
                    {
                        return await requestDelegate(cancellationToken).ConfigureAwait(false);
                    }
                }
                else
                {
                    throw;
                }
            }
        }
    }

    private async Task<ICluuIdentity> EnsureIdentityAsync(CancellationToken cancellationToken)
    {
        var identity = await this.wpfCluuIdentityProvider.GetIdentityAsync(cancellationToken).ConfigureAwait(false);

        identity ??= await this.RefreshLoginAsync(cancellationToken).ConfigureAwait(false);

        return identity;
    }

    private async Task<ICluuIdentity> RefreshIdentityAsync(CancellationToken cancellationToken)
    {
        this.wpfCluuIdentityProvider.InvalidateIdentity();

        return await this.wpfCluuIdentityProvider.GetIdentityAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<ICluuIdentity> RefreshLoginAsync(CancellationToken cancellationToken)
    {
        ICluuIdentity identity = null;

        var loginWindow = this.loginWindowFactory();

        if (loginWindow.ShowDialog() == true)
        {
            identity = await this.wpfCluuIdentityProvider.GetIdentityAsync(cancellationToken).ConfigureAwait(false);
        }

        return identity;
    }
}
