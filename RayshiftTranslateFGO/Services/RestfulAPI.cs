using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Android.Content.Res;
using RayshiftTranslateFGO.Models;
using RayshiftTranslateFGO.Util;
using RestSharp;
using RestSharp.Serialization.Json;
using Xamarin.Essentials;

namespace RayshiftTranslateFGO.Services
{
    /// <summary>
    /// Restful API for TranslateFGO
    /// </summary>
    public class RestfulAPI
    {
        private RestClient _client;
        public bool Mock = false;
        private AssetManager _assets;

        /// <summary>
        /// Restful API
        /// </summary>
        public RestfulAPI()
        {
#if DEBUG
            _client = new RestClient($"{EndpointURL.EndPoint}/api/v1/");
            ServicePointManager.ServerCertificateValidationCallback +=
                (sender, certificate, chain, sslPolicyErrors) => true;

            //_client.Proxy = new WebProxy("");
#else
            _client = new RestClient($"{EndpointURL.EndPoint}/api/v1/");
#endif
            var userAgent = Java.Lang.JavaSystem.GetProperty("http.agent");
            _client.UserAgent = $"TranslateFGO {ScriptUtil.GetBuild()} {userAgent}";
            _assets = Android.App.Application.Context.Assets;
            
        }

        /// <summary>
        /// Execute request
        /// </summary>
        /// <typeparam name="T">Type of response expected</typeparam>
        /// <param name="request">Request to send</param>
        /// <returns>API response struct</returns>
        private async Task<T> ExecuteAsync<T>(RestRequest request) where T : new()
        {
            if (Mock) {
                var mockResponse = new RestResponse<T>();
                var typeName = typeof(T).Name;

                using StreamReader sr = new StreamReader(_assets.Open($"Mock/{typeName}.txt"));
                mockResponse.Content = await sr.ReadToEndAsync();

                // Deserialize mock content
                JsonDeserializer deserializer = new RestSharp.Serialization.Json.JsonDeserializer();
                return deserializer.Deserialize<T>(mockResponse);
            }
            request.RequestFormat = DataFormat.Json; // doesn't work at all

            var response = await _client.ExecuteAsync<T>(request); // https://github.com/xamarin/xamarin-macios/issues/4380

            return response.Data;
        }

        /// <summary>
        /// Get a handshake response
        /// </summary>
        /// <returns>Handshake API struct</returns>
        public async Task<HandshakeAPIResponse> GetHandshakeApiResponse()
        {
            string endpoint;
            if (Preferences.ContainsKey("AuthKey"))
            {
                endpoint = "translate/handshake/" + Preferences.Get("AuthKey", "")  + "?_ts=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }
            else
            {
                endpoint = "translate/handshake" + "?_ts=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }
            var request = new RestRequest(endpoint)
            {
                Method = Method.POST
            };

            return await ExecuteAsync<HandshakeAPIResponse>(request);
        }

        /// <summary>
        /// Get script
        /// </summary>
        /// <param name="path">Path to script</param>
        /// <returns></returns>
        public async Task<byte[]> GetScript(string path)
        {
            var request = new RestRequest(path);

            var response = await _client.ExecuteAsync(request);

            return response.RawBytes;
        }

        /// <summary>
        /// Get new asset list
        /// </summary>
        /// <param name="assetList">Existing asset list</param>
        /// <param name="bundleId">Bundle ID</param>
        /// <returns></returns>
        public async Task<AssetListAPIResponse> SendAssetList(string assetList, int bundleId)
        {
            var request = new RestRequest("translate/assetlist")
            {
                Method = Method.POST
            };

            request.AddHeader("Content-type", "application/json");
            var sendObject = new Dictionary<string, object>()
            {
                {"data", assetList},
                {"group", bundleId}
            };
            if (Preferences.ContainsKey("AuthKey"))
            {
                sendObject.Add("key", Preferences.Get("AuthKey", ""));
            }
            request.AddParameter("application/json; charset=utf-8", SimpleJson.SerializeObject(sendObject), ParameterType.RequestBody);

            return await ExecuteAsync<AssetListAPIResponse>(request);
        }

        public async Task<BaseAPIResponse> SendRegistrationToken(string token)
        {
            var request = new RestRequest("translate/firebasetoken")
            {
                Method = Method.POST
            };

            request.AddHeader("Content-type", "application/json");
            var sendObject = new Dictionary<string, object>()
            {
                {"token", token}
            };

            request.AddParameter("application/json; charset=utf-8", SimpleJson.SerializeObject(sendObject), ParameterType.RequestBody);

            return await ExecuteAsync<BaseAPIResponse>(request);
        }
    }
}