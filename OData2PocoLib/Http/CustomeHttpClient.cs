﻿using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using OData2Poco.InfraStructure.Logging;

namespace OData2Poco.Http
{
    internal class CustomeHttpClient
    {
        public static ILog Logger = PocoLogger.Default;
        readonly OdataConnectionString _odataConnectionString;
        readonly DelegatingHandler _delegatingHandler;
        public Uri ServiceUri { get; set; }
        public HttpResponseMessage Response;
        private HttpClient _client;
        public CustomeHttpClient(OdataConnectionString odataConnectionString)
        {
            _odataConnectionString = odataConnectionString;
            ServiceUri = new Uri(_odataConnectionString.ServiceUrl);
        }
        public CustomeHttpClient(OdataConnectionString odataConnectionString, DelegatingHandler dh)
            : this(odataConnectionString)
        {
            _delegatingHandler = dh;
        }
        private async Task SetHttpClient()
        {
           
            CredentialCache credentials = new CredentialCache();
            switch (_odataConnectionString.Authenticate)
            {
                case AuthenticationType.Ntlm:
                    Logger.Trace("Authenticating with NTLM");
                    credentials.Add(ServiceUri, "NTLM",
                        new NetworkCredential(_odataConnectionString.UserName, _odataConnectionString.Password, _odataConnectionString.Domain));
                    break;
                case AuthenticationType.Digest:
                    Logger.Trace("Authenticating with Digest");
                    credentials.Add(ServiceUri, "Digest",
                        new NetworkCredential(_odataConnectionString.UserName, _odataConnectionString.Password, _odataConnectionString.Domain));
                    break;
            }

            //UseDefaultCredentials for NTLM support in windows
            var handler = new HttpClientHandler()
            {
                UseDefaultCredentials = true,
                Credentials = credentials,
            };

            if (!string.IsNullOrEmpty(_odataConnectionString.Proxy))
            {
                Logger.Trace($"Using Proxy: '{_odataConnectionString.Proxy}'");
                handler.UseProxy = true;
                handler.Proxy = new WebProxy(_odataConnectionString.Proxy);
            }

            if (_delegatingHandler != null)
            {
                Logger.Trace($"Attaching Delegating Handler");
                _delegatingHandler.InnerHandler = handler;
                _client = new HttpClient(_delegatingHandler);
            }
            else
                _client = new HttpClient(handler);
            var auth = new Authenticator(_client);
            //authenticate
            await auth.Authenticate(_odataConnectionString);
            _client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json; charset=utf-8");
            _client.DefaultRequestHeaders.Add("User-Agent", "OData2Poco");
        }

        internal async Task<string> ReadMetaDataAsync()
        {
            await SetHttpClient();
            string url = ServiceUri.AbsoluteUri.TrimEnd('/') + "/$metadata";
            using (HttpResponseMessage response = await _client.GetAsync(url))
            {
                Response = response;
                if (!response.IsSuccessStatusCode)
                    throw new HttpRequestException(
                        $"Http Error {(int)response.StatusCode}: {response.ReasonPhrase}");
                var content = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrEmpty(content))
                    throw new HttpRequestException(
                        $"Http Error {(int)response.StatusCode}: {response.ReasonPhrase}");
                return content;
            }
        }
    }

}
