﻿using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WebSub.Net.Http.Subscriber.Discovery
{
    internal class WebSubDiscoverer : IWebSubDiscoverer
    {
        #region Fields
        private const string LINK_HEADER = "Link";

        private readonly HttpClient _httpClient;
        #endregion

        #region Constructors
        public WebSubDiscoverer(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }
        #endregion

        #region Methods
        public async Task<WebSubDiscovery> DiscoverAsync(string requestUri, CancellationToken cancellationToken)
        {
            HttpResponseMessage discoveryResponse = await _httpClient.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (discoveryResponse.StatusCode == HttpStatusCode.OK)
            {
                if (discoveryResponse.Headers.Contains(LINK_HEADER))
                {
                    WebSubDiscovery webSubDiscovery = WebLinkParser.ParseWebLinkHeaders(discoveryResponse.Headers.GetValues(LINK_HEADER));
                    if (RequiredUrlsIdentified(webSubDiscovery))
                    {
                        return webSubDiscovery;
                    }
                }
            }

            throw new WebSubDiscoveryException("The discovery mechanism haven't identified required URLs.", discoveryResponse);
        }

        private static bool RequiredUrlsIdentified(WebSubDiscovery webSubDiscovery)
        {
            return !String.IsNullOrWhiteSpace(webSubDiscovery.TopicUrl) && (webSubDiscovery.HubsUrls != null) && (webSubDiscovery.HubsUrls.Count > 0);
        }
        #endregion
    }
}
