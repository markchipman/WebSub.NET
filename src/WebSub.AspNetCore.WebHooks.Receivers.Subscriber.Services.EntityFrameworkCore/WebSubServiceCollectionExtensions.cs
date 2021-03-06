﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WebSub.WebHooks.Receivers.Subscriber.Services;
using WebSub.WebHooks.Receivers.Subscriber.Services.EntityFrameworkCore;

namespace Microsoft.AspNetCore.Builder
{
    /// <summary>
    /// The <see cref="IServiceCollection"/> extensions for adding WebSub related services that uses Entity Framework Core.
    /// </summary>
    public static class WebSubServiceCollectionExtensions
    {
        /// <summary>
        /// Registers <see cref="IWebSubSubscriptionsStore"/> service implementation that uses Entity Framework Core.
        /// </summary>
        /// <param name="services">The collection of service descriptors.</param>
        /// <returns>The collection of service descriptors.</returns>
        public static IServiceCollection AddEntityFrameworkWebSubSubscriptionStore<TContext>(this IServiceCollection services) where TContext : WebSubDbContext
        {
            services.TryAddScoped<IWebSubSubscriptionsStore, WebSubSubscriptionsStore<TContext>>();

            return services;
        }
    }
}
