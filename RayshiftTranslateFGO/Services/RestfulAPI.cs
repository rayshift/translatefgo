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

        public void SetEndpoint(string endpoint)
        {
            _client.BaseUrl = new Uri($"{endpoint}/api/v1/");

        }

        /// <summary>
        /// Execute request
        /// </summary>
        /// <typeparam name="T">Type of response expected</typeparam>
        /// <param name="request">Request to send</param>
        /// <returns>API response struct</returns>
        private async Task<IRestResponse<T>> ExecuteAsync<T>(RestRequest request) where T : new()
        {
            request.RequestFormat = DataFormat.Json; // doesn't work at all

            var response = await _client.ExecuteAsync<T>(request); // https://github.com/xamarin/xamarin-macios/issues/4380

            return response;
        }

        /// <summary>
        /// Get a handshake response
        /// </summary>
        /// <returns>Handshake API struct</returns>
        public async Task<IRestResponse<HandshakeAPIResponse>> GetHandshakeApiResponse(FGORegion region)
        {
            string endpoint;
            if (Preferences.ContainsKey("AuthKey"))
            {
                endpoint = $"translate/scripts/{(int)region}/" + Preferences.Get("AuthKey", "")  + "?_ts=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }
            else
            {
                endpoint = $"translate/scripts/{(int)region}" + "?_ts=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }
            var request = new RestRequest(endpoint)
            {
                Method = Method.POST
            };

            return await ExecuteAsync<HandshakeAPIResponse>(request);
        }

        public async Task<IRestResponse<VersionAPIResponse>> GetVersionAPIResponse()
        {
            var endpoint = $"translate/version/{ScriptUtil.GetBuild()}?_ts={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            var request = new RestRequest(endpoint)
            {
                Method = Method.POST
            };
            return await ExecuteAsync<VersionAPIResponse>(request);
        }

        /// <summary>
        /// Get script
        /// </summary>
        /// <param name="path">Path to script</param>
        /// <returns></returns>
        public async Task<IRestResponse> GetScript(string path)
        {
            var request = new RestRequest(path);

            var response = await _client.ExecuteAsync(request);

            return response;
        }

        /// <summary>
        /// Get new asset list
        /// </summary>
        /// <param name="assetList">Existing asset list</param>
        /// <param name="bundleId">Bundle ID</param>
        /// <returns></returns>
        public async Task<IRestResponse<AssetListAPIResponse>> SendAssetList(string assetList, int bundleId, FGORegion region)
        {
            var request = new RestRequest("translate/update-asset-list")
            {
                Method = Method.POST
            };

            request.AddHeader("Content-type", "application/json");
            var sendObject = new Dictionary<string, object>()
            {
                {"data", assetList},
                {"group", bundleId},
                {"region", (int)region}
            };
            if (Preferences.ContainsKey("AuthKey"))
            {
                sendObject.Add("key", Preferences.Get("AuthKey", ""));
            }
            request.AddParameter("application/json; charset=utf-8", SimpleJson.SerializeObject(sendObject), ParameterType.RequestBody);

            return await ExecuteAsync<AssetListAPIResponse>(request);
        }

        /// <summary>
        /// Send firebase registration token
        /// </summary>
        /// <param name="token">token</param>
        /// <returns></returns>
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

            return (await ExecuteAsync<BaseAPIResponse>(request)).Data;
        }

        public async Task<BaseAPIResponse> SendSuccess(FGORegion region, int language, TranslationInstallType installType, int groupId, bool success, string errorMessage, bool isAndroid11Install)
        {
            var request = new RestRequest("translate/result")
            {
                Method = Method.POST
            };

            request.AddHeader("Content-type", "application/json");
            var sendObject = new Dictionary<string, object>()
            {

                {"region", (int)region},
                {"successful", success},
                {"error", errorMessage},
                {"language", language},
                {"group", groupId},
                {"installType", (int)installType},
                {"android11Install", isAndroid11Install}
            };

            request.AddParameter("application/json; charset=utf-8", SimpleJson.SerializeObject(sendObject), ParameterType.RequestBody);

            return (await ExecuteAsync<BaseAPIResponse>(request)).Data;
        }
    }
}