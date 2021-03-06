﻿using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.WebHooks.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using WebSub.WebHooks.Receivers.Subscriber;
using WebSub.WebHooks.Receivers.Subscriber.Services;

namespace WebSub.AspNetCore.WebHooks.Receivers.Subscriber.Filters
{
    /// <summary>
    /// An <see cref="IAsyncResourceFilter"/> to verify the topic URL and short-circuit hub intent of subscriber verification request.
    /// </summary>
    internal class WebSubWebHookIntentVerificationFilter : IAsyncResourceFilter, IOrderedFilter
    {
        #region Fields
        private readonly ILogger _logger;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the order value for determining the order of execution of filters. Filters execute in ascending numeric value of the <see cref="Order"/> property.
        /// </summary>
        public int Order => WebHookGetHeadRequestFilter.Order;
        #endregion

        #region Constructor
        /// <summary>
        /// Instantiates a new <see cref="WebSubWebHookIntentVerificationFilter"/> instance.
        /// </summary>
        /// <param name="loggerFactory">The <see cref="ILoggerFactory"/>.</param>
        public WebSubWebHookIntentVerificationFilter(ILoggerFactory loggerFactory)
        {
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }
            _logger = loggerFactory.CreateLogger(GetType());
        }
        #endregion

        #region Methods
        /// <summary>
        /// Called asynchronously before the rest of the pipeline.
        /// </summary>
        /// <param name="context">The <see cref="ResourceExecutingContext"/>.</param>
        /// <param name="next">The <see cref="ResourceExecutionDelegate"/>. Invoked to execute the next resource filter or the remainder of the pipeline.</param>
        /// <returns>A <see cref="Task"/> which will complete when the remainder of the pipeline completes.</returns>
        public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            IQueryCollection requestQuery = context.HttpContext.Request.Query;
            if (HttpMethods.IsGet(context.HttpContext.Request.Method) && requestQuery.ContainsKey(WebSubConstants.MODE_QUERY_PARAMETER_NAME))
            {
                WebSubSubscription subscription = context.HttpContext.Items[WebSubConstants.HTTP_CONTEXT_ITEMS_SUBSCRIPTION_KEY] as WebSubSubscription;
                context.Result = await HandleIntentVerificationAsync(subscription, requestQuery, context.HttpContext.RequestServices);
            }
            else
            {
                await next();
            }
        }

        private async Task<IActionResult> HandleIntentVerificationAsync(WebSubSubscription subscription, IQueryCollection requestQuery, IServiceProvider requestServices)
        {
            IActionResult intentVerificationResult = new NotFoundResult();

            if (subscription != null)
            {
                IWebSubSubscriptionsStore subscriptionsStore = requestServices.GetRequiredService<IWebSubSubscriptionsStore>();
                IWebSubSubscriptionsService subscriptionsService = requestServices.GetService<IWebSubSubscriptionsService>();

                switch (requestQuery[WebSubConstants.MODE_QUERY_PARAMETER_NAME])
                {
                    case WebSubConstants.MODE_DENIED:
                        intentVerificationResult = await HandleSubscribeIntentDenyAsync(subscription, subscriptionsStore, subscriptionsService, requestQuery);
                        break;
                    case WebSubConstants.MODE_SUBSCRIBE:
                        intentVerificationResult = await HandleSubscribeIntentVerificationAsync(subscription, subscriptionsStore, subscriptionsService, requestQuery);
                        break;
                    case WebSubConstants.MODE_UNSUBSCRIBE:
                        intentVerificationResult = await HandleUnsubscribeIntentVerificationAsync(subscription, subscriptionsStore, subscriptionsService, requestQuery);
                        break;
                    default:
                        intentVerificationResult = HandleBadRequest($"A '{WebSubConstants.ReceiverName}' WebHook intent verification request contains unknown '{WebSubConstants.MODE_QUERY_PARAMETER_NAME}' query parameter value.");
                        break;
                }
            }

            return intentVerificationResult;
        }

        private async Task<IActionResult> HandleSubscribeIntentDenyAsync(WebSubSubscription subscription, IWebSubSubscriptionsStore subscriptionsStore, IWebSubSubscriptionsService subscriptionsService, IQueryCollection requestQuery)
        {
            StringValues topicValues = requestQuery[WebSubConstants.TOPIC_QUERY_PARAMETER_NAME];
            if (StringValues.IsNullOrEmpty(topicValues))
            {
                return HandleBadRequest($"A '{WebSubConstants.ReceiverName}' WebHook subscribe intent deny request must contain a '{WebSubConstants.TOPIC_QUERY_PARAMETER_NAME}' query parameter.");
            }
            StringValues reason = requestQuery[WebSubConstants.INTENT_DENY_REASON_QUERY_PARAMETER_NAME];

            subscription.State = WebSubSubscriptionState.SubscribeDenied;
            await subscriptionsStore.UpdateAsync(subscription);

            if (subscriptionsService != null)
            {
                await subscriptionsService.OnSubscribeIntentDenyAsync(subscription, reason, subscriptionsStore);
            }

            _logger.LogInformation("Received a subscribe intent deny request for the '{ReceiverName}' WebHook receiver -- subscription denied, returning confirmation response.", WebSubConstants.ReceiverName);

            return new NoContentResult();
        }

        private async Task<IActionResult> HandleSubscribeIntentVerificationAsync(WebSubSubscription subscription, IWebSubSubscriptionsStore subscriptionsStore, IWebSubSubscriptionsService subscriptionsService, IQueryCollection requestQuery)
        {
            StringValues topicValues = requestQuery[WebSubConstants.TOPIC_QUERY_PARAMETER_NAME];
            if (StringValues.IsNullOrEmpty(topicValues))
            {
                return HandleMissingIntentVerificationParameter(WebSubConstants.TOPIC_QUERY_PARAMETER_NAME);
            }

            StringValues challengeValues = requestQuery[WebSubConstants.INTENT_VERIFICATION_CHALLENGE_QUERY_PARAMETER_NAME];
            if (StringValues.IsNullOrEmpty(challengeValues))
            {
                return HandleMissingIntentVerificationParameter(WebSubConstants.INTENT_VERIFICATION_CHALLENGE_QUERY_PARAMETER_NAME);
            }

            StringValues leaseSecondsValues = requestQuery[WebSubConstants.INTENT_VERIFICATION_LEASE_SECONDS_QUERY_PARAMETER_NAME];
            if (StringValues.IsNullOrEmpty(leaseSecondsValues) || !Int32.TryParse(leaseSecondsValues, out int leaseSeconds))
            {
                return HandleMissingIntentVerificationParameter(WebSubConstants.INTENT_VERIFICATION_LEASE_SECONDS_QUERY_PARAMETER_NAME);
            }

            if (await VerifySubscribeIntentAsync(subscription, subscriptionsStore, subscriptionsService, topicValues, leaseSeconds))
            {
                _logger.LogInformation("Received a subscribe intent verification request for the '{ReceiverName}' WebHook receiver -- verification passed, returning challenge response.", WebSubConstants.ReceiverName);
                return new ContentResult { Content = challengeValues };
            }
            else
            {
                _logger.LogInformation("Received a subscribe intent verification request for the '{ReceiverName}' WebHook receiver -- verification failed, returning challenge response.", WebSubConstants.ReceiverName);
                return new NotFoundResult();
            }
        }

        private async Task<bool> VerifySubscribeIntentAsync(WebSubSubscription subscription, IWebSubSubscriptionsStore subscriptionsStore, IWebSubSubscriptionsService subscriptionsService, string topic, int leaseSeconds)
        {
            bool verified = false;

            if (subscription.State == WebSubSubscriptionState.SubscribeRequested)
            {
                if (subscription.TopicUrl != topic)
                {
                    if (subscriptionsService != null)
                    {
                        await subscriptionsService.OnInvalidSubscribeIntentVerificationAsync(subscription, subscriptionsStore);
                    }
                }
                else if ((subscriptionsService == null) || (await subscriptionsService.OnSubscribeIntentVerificationAsync(subscription, subscriptionsStore)))
                {
                    subscription.State = WebSubSubscriptionState.SubscribeValidated;
                    subscription.VerificationRequestTimeStampUtc = DateTime.UtcNow;
                    subscription.LeaseSeconds = leaseSeconds;
                    await subscriptionsStore.UpdateAsync(subscription);

                    verified = true;
                }
            }

            return verified;
        }

        private async Task<IActionResult> HandleUnsubscribeIntentVerificationAsync(WebSubSubscription subscription, IWebSubSubscriptionsStore subscriptionsStore, IWebSubSubscriptionsService subscriptionsService, IQueryCollection requestQuery)
        {
            StringValues topicValues = requestQuery[WebSubConstants.TOPIC_QUERY_PARAMETER_NAME];
            if (StringValues.IsNullOrEmpty(topicValues))
            {
                return HandleMissingIntentVerificationParameter(WebSubConstants.TOPIC_QUERY_PARAMETER_NAME);
            }

            StringValues challengeValues = requestQuery[WebSubConstants.INTENT_VERIFICATION_CHALLENGE_QUERY_PARAMETER_NAME];
            if (StringValues.IsNullOrEmpty(challengeValues))
            {
                return HandleMissingIntentVerificationParameter(WebSubConstants.INTENT_VERIFICATION_CHALLENGE_QUERY_PARAMETER_NAME);
            }

            if (await VerifyUnsubscribeIntentAsync(subscription, subscriptionsStore, subscriptionsService, topicValues))
            {
                _logger.LogInformation("Received an unsubscribe intent verification request for the '{ReceiverName}' WebHook receiver -- verification passed, returning challenge response.", WebSubConstants.ReceiverName);
                return new ContentResult { Content = challengeValues };
            }
            else
            {
                _logger.LogInformation("Received an unsubscribe intent verification request for the '{ReceiverName}' WebHook receiver -- verification failed, returning challenge response.", WebSubConstants.ReceiverName);
                return new NotFoundResult();
            }
        }

        private async Task<bool> VerifyUnsubscribeIntentAsync(WebSubSubscription subscription, IWebSubSubscriptionsStore subscriptionsStore, IWebSubSubscriptionsService subscriptionsService, string topic)
        {
            bool verified = false;

            if (subscription.State == WebSubSubscriptionState.UnsubscribeRequested)
            {
                if (subscription.TopicUrl != topic)
                {
                    if (subscriptionsService != null)
                    {
                        await subscriptionsService.OnInvalidUnsubscribeIntentVerificationAsync(subscription, subscriptionsStore);
                    }
                }
                else if ((subscriptionsService == null) || (await subscriptionsService.OnUnsubscribeIntentVerificationAsync(subscription, subscriptionsStore)))
                {
                    subscription.State = WebSubSubscriptionState.UnsubscribeValidated;
                    subscription.VerificationRequestTimeStampUtc = DateTime.UtcNow;
                    await subscriptionsStore.UpdateAsync(subscription);

                    verified = true;
                }
            }

            return verified;
        }

        private IActionResult HandleMissingIntentVerificationParameter(string parameterName)
        {
            return HandleBadRequest($"A '{WebSubConstants.ReceiverName}' WebHook intent verification request must contain a '{parameterName}' query parameter.");
        }

        private IActionResult HandleBadRequest(string message)
        {
            _logger.LogWarning(message);

            return new BadRequestObjectResult(message);
        }
        #endregion
    }
}
