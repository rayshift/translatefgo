using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Android.Content.Res;
using Android.OS;
using Newtonsoft.Json;
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
            RefreshEndpoint();
            _assets = Android.App.Application.Context.Assets;
            
        }

        public void SetEndpoint(string endpoint)
        {
            _client.BaseUrl = new Uri($"{endpoint}/api/v1/");

        }

        public void RefreshEndpoint()
        {
            _client = new RestClient($"{EndpointURL.EndPoint}/api/v1/");
            _client.AddHandler("application/json", () => new RestSharp.Serializers.NewtonsoftJson.JsonNetSerializer());
#if DEBUG
            ServicePointManager.ServerCertificateValidationCallback +=
                (sender, certificate, chain, sslPolicyErrors) => true;
            //_client.Proxy = new WebProxy("");
#endif
            var userAgent = Java.Lang.JavaSystem.GetProperty("http.agent");
            _client.UserAgent = $"TranslateFGO {ScriptUtil.GetBuild()} {userAgent}";
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
        public async Task<IRestResponse<HandshakeAPIResponse>> GetHandshakeApiResponse(FGORegion region, string assetStorage = null)
        {
            if (EndpointURL.NeedsRefresh)
            {
                EndpointURL.NeedsRefresh = false;
                RefreshEndpoint();
            }

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
            request.AddHeader("Content-type", "application/json");
            var sendObject = new Dictionary<string, object>();
            if (!string.IsNullOrWhiteSpace(assetStorage))
            {
                sendObject.Add("assetstorage", assetStorage);
            }

            if (Guid.TryParse(Preferences.Get(EndpointURL.GetLinkedAccountKey(), null), out var userToken))
            {
                sendObject.Add("accountToken", userToken);
            }
            sendObject.Add("translateVerCode", ScriptUtil.GetBuild());
            request.AddParameter("application/json; charset=utf-8", SimpleJson.SerializeObject(sendObject), ParameterType.RequestBody);

            return await ExecuteAsync<HandshakeAPIResponse>(request);
        }

        public async Task<IRestResponse<ArtAPIResponse>> GetArtAPIResponse(string assetStorageJP, string assetStorageNA)
        {
            if (EndpointURL.NeedsRefresh)
            {
                EndpointURL.NeedsRefresh = false;
                RefreshEndpoint();
            }

            
            var endpoint = $"translate/art" + "?_ts=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var request = new RestRequest(endpoint)
            {
                Method = Method.POST
            };
            request.AddHeader("Content-type", "application/json");
            var sendObject = new Dictionary<string, object>();
            if (!string.IsNullOrWhiteSpace(assetStorageJP))
            {
                sendObject.Add("assetstoragejp", assetStorageJP);
            }
            if (!string.IsNullOrWhiteSpace(assetStorageNA))
            {
                sendObject.Add("assetstoragena", assetStorageNA);
            }

            if (Guid.TryParse(Preferences.Get(EndpointURL.GetLinkedAccountKey(), null), out var userToken))
            {
                sendObject.Add("accountToken", userToken);
            }
            request.AddParameter("application/json; charset=utf-8", SimpleJson.SerializeObject(sendObject), ParameterType.RequestBody);

            return await ExecuteAsync<ArtAPIResponse>(request);
        }

        /// <summary>
        /// Get version API response
        /// </summary>
        /// <returns></returns>
        public async Task<IRestResponse<VersionAPIResponse>> GetVersionAPIResponse()
        {
            if (EndpointURL.NeedsRefresh)
            {
                EndpointURL.NeedsRefresh = false;
                RefreshEndpoint();
            }

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
        /// <param name="auth"></param>
        /// <returns></returns>
        public async Task<IRestResponse> GetScript(string path, bool auth = false)
        {
            if (EndpointURL.NeedsRefresh)
            {
                EndpointURL.NeedsRefresh = false;
                RefreshEndpoint();
            }

            var request = new RestRequest(path);
            _client.FollowRedirects = true;

            if (auth)
            {
                request.Method = Method.POST;
                request.AddHeader("Content-type", "application/json");
                var sendObject = new Dictionary<string, object>();

                if (Guid.TryParse(Preferences.Get(EndpointURL.GetLinkedAccountKey(), null), out var userToken))
                {
                    sendObject.Add("accountToken", userToken);
                }
                request.AddParameter("application/json; charset=utf-8", SimpleJson.SerializeObject(sendObject), ParameterType.RequestBody);
            }

            var response = await _client.ExecuteAsync(request);

            return response;
        }

        public async Task<(int, string)> GetManualLinkToken(string code)
        {
            if (EndpointURL.NeedsRefresh)
            {
                EndpointURL.NeedsRefresh = false;
                RefreshEndpoint();
            }

            var request = new RestRequest($"translate/link-code/{code}")
            {
                Method = Method.GET
            };


            var result = await ExecuteAsync<BaseAPIResponse>(request);

            return (result?.Data?.Status ?? 500, result?.Data?.Message ?? "server error");
        }

        /// <summary>
        /// Get new asset list
        /// </summary>
        /// <param name="assetList">Existing asset list</param>
        /// <param name="bundleId">Bundle ID</param>
        /// <returns></returns>
        public async Task<IRestResponse<AssetListAPIResponse>> SendAssetList(string assetList, int bundleId, FGORegion region)
        {
            if (EndpointURL.NeedsRefresh)
            {
                EndpointURL.NeedsRefresh = false;
                RefreshEndpoint();
            }

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
        /// Asset list for art
        /// </summary>
        /// <param name="assetList"></param>
        /// <param name="servantIds"></param>
        /// <param name="region"></param>
        /// <param name="disable"></param>
        /// <returns></returns>
        public async Task<IRestResponse<AssetListAPIResponse>> SendAssetList(string assetList, List<int> servantIds, FGORegion region, bool disable=false)
        {
            if (EndpointURL.NeedsRefresh)
            {
                EndpointURL.NeedsRefresh = false;
                RefreshEndpoint();
            }

            var request = new RestRequest("translate/update-asset-list")
            {
                Method = Method.POST
            };

            request.AddHeader("Content-type", "application/json");
            var sendObject = new Dictionary<string, object>()
            {
                {"data", assetList},
                {"servantList", servantIds},
                {"region", (int)region},
                {"art", true},
                {"disable", disable}
            };
            if (Preferences.ContainsKey("AuthKey"))
            {
                sendObject.Add("key", Preferences.Get("AuthKey", ""));
            }
            request.AddParameter("application/json; charset=utf-8", JsonConvert.SerializeObject(sendObject), ParameterType.RequestBody);

            return await ExecuteAsync<AssetListAPIResponse>(request);
        }

        /// <summary>
        /// Get extra assets
        /// </summary>
        /// <param name="uploadGuid"></param>
        /// <param name="userGuid"></param>
        /// <param name="bundleId"></param>
        /// <param name="region"></param>
        /// <returns></returns>
        public async Task<IRestResponse<ExtraAssetAPIResponse>> GetExtraAssets(Guid uploadGuid, Guid? userGuid, int bundleId,
            FGORegion region)
        {
            if (EndpointURL.NeedsRefresh)
            {
                EndpointURL.NeedsRefresh = false;
                RefreshEndpoint();
            }

            var request = new RestRequest("translate/get-extra-assets")
            {
                Method = Method.POST
            };

            request.AddHeader("Content-type", "application/json");
            var sendObject = new Dictionary<string, object>()
            {
                {"uploadKey", uploadGuid.ToString()},
                {"accountToken", userGuid?.ToString() ?? Guid.Empty.ToString()},
                {"group", bundleId},
                {"region", (int)region}
            };
            if (Preferences.ContainsKey("AuthKey"))
            {
                sendObject.Add("key", Preferences.Get("AuthKey", ""));
            }
            request.AddParameter("application/json; charset=utf-8", SimpleJson.SerializeObject(sendObject), ParameterType.RequestBody);
            request.Timeout = 1000 * 120;
            request.ReadWriteTimeout = 1000 * 120;
            return await ExecuteAsync<ExtraAssetAPIResponse>(request);
        }

        public async Task<IRestResponse<ExtraStartAssetAPIResponse>> StartGetExtraAssets(Guid uploadGuid, Guid? userGuid,
            int bundleId, FGORegion region)
        {
            if (EndpointURL.NeedsRefresh)
            {
                EndpointURL.NeedsRefresh = false;
                RefreshEndpoint();
            }

            var request = new RestRequest("translate/start-extra-assets")
            {
                Method = Method.POST
            };

            request.AddHeader("Content-type", "application/json");
            var sendObject = new Dictionary<string, object>()
            {
                {"uploadKey", uploadGuid.ToString()},
                {"accountToken", userGuid?.ToString() ?? Guid.Empty.ToString()},
                {"group", bundleId},
                {"region", (int)region}
            };
            if (Preferences.ContainsKey("AuthKey"))
            {
                sendObject.Add("key", Preferences.Get("AuthKey", ""));
            }
            request.AddParameter("application/json; charset=utf-8", SimpleJson.SerializeObject(sendObject), ParameterType.RequestBody);

            return await ExecuteAsync<ExtraStartAssetAPIResponse>(request);
        }

        public async Task<IRestResponse<ExtraAssetPollAPIResponse>> PollGetExtraAssets(Guid pollGuid)
        {
            if (EndpointURL.NeedsRefresh)
            {
                EndpointURL.NeedsRefresh = false;
                RefreshEndpoint();
            }

            var request = new RestRequest("translate/poll-extra-assets")
            {
                Method = Method.POST
            };

            request.AddHeader("Content-type", "application/json");
            var sendObject = new Dictionary<string, object>()
            {
                {"uploadKey", pollGuid.ToString()},
            };
            if (Preferences.ContainsKey("AuthKey"))
            {
                sendObject.Add("key", Preferences.Get("AuthKey", ""));
            }
            request.AddParameter("application/json; charset=utf-8", SimpleJson.SerializeObject(sendObject), ParameterType.RequestBody);

            return await ExecuteAsync<ExtraAssetPollAPIResponse>(request);
        }

        /// <summary>
        /// Get an async upload token
        /// </summary>
        /// <param name="size"></param>
        /// <param name="pieceCount"></param>
        /// <returns></returns>
        public async Task<IRestResponse<AsyncUploadStartResponse>> BeginAsyncUploadRequest(int size, int pieceCount)
        {
            if (EndpointURL.NeedsRefresh)
            {
                EndpointURL.NeedsRefresh = false;
                RefreshEndpoint();
            }

            var request = new RestRequest("util/start-async-upload")
            {
                Method = Method.POST
            };

            request.AddHeader("Content-type", "application/json");
            var sendObject = new Dictionary<string, object>()
            {
                {"size", size},
                {"pieceCount", pieceCount}
            };

            request.AddParameter("application/json; charset=utf-8", SimpleJson.SerializeObject(sendObject), ParameterType.RequestBody);

            return await ExecuteAsync<AsyncUploadStartResponse>(request);
        }

        /// <summary>
        /// Get username and donor status from token
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<IRestResponse<AccountLinkTestResponse>> GetLinkedUserDetails(Guid token)
        {
            if (EndpointURL.NeedsRefresh)
            {
                EndpointURL.NeedsRefresh = false;
                RefreshEndpoint();
            }

            var endpoint = $"translate/token-check/{token}?_ts={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            var request = new RestRequest(endpoint)
            {
                Method = Method.POST
            };
            return await ExecuteAsync<AccountLinkTestResponse>(request);
        }

        /// <summary>
        /// Send async piece
        /// </summary>
        /// <param name="data">Async piece</param>
        /// <returns></returns>
        public async Task<IRestResponse<BaseAPIResponse>> SendAsyncPiece(AsyncUploadPieceData data)
        {
            if (EndpointURL.NeedsRefresh)
            {
                EndpointURL.NeedsRefresh = false;
                RefreshEndpoint();
            }

            var request = new RestRequest("util/upload-async-piece")
            {
                Method = Method.POST
            };

            request.AddHeader("Content-type", "application/json");
            var sendObject = data;

            request.AddParameter("application/json; charset=utf-8", SimpleJson.SerializeObject(sendObject), ParameterType.RequestBody);

            return await ExecuteAsync<BaseAPIResponse>(request);
        }

        /// <summary>
        /// Send firebase registration token
        /// </summary>
        /// <param name="token">token</param>
        /// <returns></returns>
        public async Task<BaseAPIResponse> SendRegistrationToken(string token)
        {
            if (EndpointURL.NeedsRefresh)
            {
                EndpointURL.NeedsRefresh = false;
                RefreshEndpoint();
            }

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

        public async Task<BaseAPIResponse> SendSuccess(FGORegion region, int language, TranslationInstallType installType, int groupId, bool success, string errorMessage, bool isAndroid11Install, bool isArt = false)
        {
            if (EndpointURL.NeedsRefresh)
            {
                EndpointURL.NeedsRefresh = false;
                RefreshEndpoint();
            }

            var request = new RestRequest("translate/result")
            {
                Method = Method.POST
            };

            if (!Preferences.ContainsKey("InstallID"))
            {
                Preferences.Set("InstallID", Guid.NewGuid().ToString());
            }

            request.AddHeader("Content-type", "application/json");
            var sendObject = new Dictionary<string, object>()
            {

                {"region", (int)region},
                {"successful", success},
                {"error", errorMessage},
                {"language", language},
                {"group", groupId},
                {"installType", (int)installType},
                {"android11Install", isAndroid11Install},
                {"guid", Preferences.Get("InstallID", "")},
                {"art", isArt}
            };

            request.AddParameter("application/json; charset=utf-8", SimpleJson.SerializeObject(sendObject), ParameterType.RequestBody);

            return (await ExecuteAsync<BaseAPIResponse>(request)).Data;
        }
    }
}