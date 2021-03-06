﻿using System.Threading.Tasks;
using Lib.AspNetCore.ServerSentEvents;
using WebSub.WebHooks.Receivers.Subscriber;
using WebSub.WebHooks.Receivers.Subscriber.Services;

namespace Demo.AspNetCore.WebSub.Subscriber.Services
{
    internal class ServerSentEventWebSubSubscriptionsService : IWebSubSubscriptionsService
    {
        #region Fields
        private readonly IServerSentEventsService _serverSentEventsService;
        #endregion

        #region Constructor
        public ServerSentEventWebSubSubscriptionsService(IServerSentEventsService serverSentEventsService)
        {
            _serverSentEventsService = serverSentEventsService;
        }
        #endregion

        #region Methods
        public Task OnSubscribeIntentDenyAsync(WebSubSubscription subscription, string reason, IWebSubSubscriptionsStore subscriptionsStore)
        {
            return _serverSentEventsService.SendEventAsync($"OnSubscribeIntentDenyAsync ({subscription.Id})");
        }

        public Task OnInvalidSubscribeIntentVerificationAsync(WebSubSubscription subscription, IWebSubSubscriptionsStore subscriptionsStore)
        {
            return _serverSentEventsService.SendEventAsync($"OnInvalidSubscribeIntentVerificationAsync ({subscription.Id})");
        }

        public async Task<bool> OnSubscribeIntentVerificationAsync(WebSubSubscription subscription, IWebSubSubscriptionsStore subscriptionsStore)
        {
            await _serverSentEventsService.SendEventAsync($"OnSubscribeIntentVerificationAsync ({subscription.Id})");

            return true;
        }

        public Task OnInvalidUnsubscribeIntentVerificationAsync(WebSubSubscription subscription, IWebSubSubscriptionsStore subscriptionsStore)
        {
            return _serverSentEventsService.SendEventAsync($"OnInvalidUnsubscribeIntentVerificationAsync ({subscription.Id})");
        }

        public async Task<bool> OnUnsubscribeIntentVerificationAsync(WebSubSubscription subscription, IWebSubSubscriptionsStore subscriptionsStore)
        {
            await _serverSentEventsService.SendEventAsync($"OnUnsubscribeIntentVerificationAsync ({subscription.Id})");

            return true;
        }
        #endregion
    }
}
