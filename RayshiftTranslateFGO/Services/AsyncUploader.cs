using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dasync.Collections;
using RayshiftTranslateFGO.Models;
using RayshiftTranslateFGO.Util;

namespace RayshiftTranslateFGO.Services
{
    public class AsyncUploader
    {
        protected Dictionary<int, byte[]> Pieces = new Dictionary<int, byte[]>();
        public Guid Token;
        protected RestfulAPI API;

        public int Percent = 0;
        public int Stage = 0;
        private readonly object _percentWriteLock = new object();

        public AsyncUploader()
        {
            API = new RestfulAPI();
            Percent = 0;
            Stage = 0;
        }

        private async Task<ScriptInstallStatus> Prepare(MemoryStream file)
        {
            Stage = 1;
            try
            {
                var pieceCount = (int)Math.Ceiling((double)(int)file.Length / ((double)1024 * 1024));

                var token = await API.BeginAsyncUploadRequest((int)file.Length, pieceCount);

                if (!token.IsSuccessful || token.Data.Status != 200)
                {
                    throw new Exception(
                        $"Error {token.Data.Status}: {token.Data.Message}\n{token.ErrorMessage}");
                }

                Token = token.Data.Response["data"].guid;

                file.Seek(0, SeekOrigin.Begin);

                for (int i = 0; i < pieceCount; i++)
                {
                    var count = (int)Math.Min(1024 * 1024, file.Length - file.Position);
                    byte[] buffer = new byte[count];
                    var read = await file.ReadAsync(buffer, 0, count);
                    if (read == 0) throw new Exception("Read 0 bytes.");
                    Pieces.Add(i, buffer);
                }

                Percent = 10;

                return new ScriptInstallStatus()
                {
                    IsSuccessful = true
                };
            }
            catch (Exception ex)
            {
                return new ScriptInstallStatus()
                {
                    IsSuccessful = false,
                    ErrorMessage = String.Format(UIFunctions.GetResourceString("ChunkUploadFailed")) + "\n\n" + ex.ToString()
                };
            }
        }

        private async Task UploadChunk(int chunk, bool retryAllowed = true)
        {
            var resp = await API.SendAsyncPiece(new AsyncUploadPieceData
            {
                guid = Token,
                data = Convert.ToBase64String(Pieces[chunk]),
                piece = chunk,
                size = Pieces[chunk].Length
            });

            if (!resp.IsSuccessful || resp.Data.Status != 200)
            {
                if (retryAllowed)
                {
                    await UploadChunk(chunk, false);
                }
                else
                {
                    throw new Exception(
                        $"\n\nError {resp.Data.Status}: {resp.Data.Message}\n{resp.ErrorMessage}");
                }
            }
        }

        public async Task<ScriptInstallStatus> UploadAllChunks()
        {
            int totalChunks = Pieces.Count;
            int completeChunks = 0;
            try
            {
                await Pieces.ParallelForEachAsync(async pair =>
                    {
                        try
                        {
                            await UploadChunk(pair.Key);
                            Interlocked.Increment(ref completeChunks);
                            lock (_percentWriteLock)
                            {
                                Percent = (int)Math.Round(((float)completeChunks/(float)totalChunks)*100*0.9) + 10;
                            }
                        }
                        catch (Exception ex)
                        {
                            throw new EndEarlyException(String.Format(UIFunctions.GetResourceString("ChunkUploadFailed")), ex);
                        }
                    },
                    maxDegreeOfParallelism: 8);
            }
            catch (EndEarlyException ex)
            {
                return new ScriptInstallStatus()
                {
                    IsSuccessful = false,
                    ErrorMessage = ex.InnerException != null ? ex.ToString() + "\n\n" + ex.InnerException.ToString() : ex.ToString()
                };
            }

            return new ScriptInstallStatus()
            {
                IsSuccessful = true
            };
        }

        public async Task<ExtraAssetReturn> GetExtraAssets(MemoryStream buffer, Guid userToken, int installId, FGORegion region)
        {
            // prepare
            var prepareResult = await this.Prepare(buffer);

            buffer.Close();
            if (!prepareResult.IsSuccessful) return new ExtraAssetReturn()
            {
                IsSuccessful = prepareResult.IsSuccessful,
                ErrorMessage = prepareResult.ErrorMessage
            };

            // upload all chunks
            var chunkResult = await this.UploadAllChunks();
            if (!chunkResult.IsSuccessful) return new ExtraAssetReturn()
            {
                IsSuccessful = prepareResult.IsSuccessful,
                ErrorMessage = prepareResult.ErrorMessage
            };

            // grab API response
            /*var apiResult = await API.GetExtraAssets(Token, userToken, installId, region);

            if (!apiResult.IsSuccessful)
            {
                return new ExtraAssetReturn()
                {
                    IsSuccessful = false,
                    ErrorMessage = string.Format(UIFunctions.GetResourceString("InstallExtraAPIFailure"), installId, apiResult.StatusCode, apiResult.Data?.Message)
                };
            }*/

            Stage += 1;
            Percent = 0;
            var startResult = await API.StartGetExtraAssets(Token, userToken, installId, region);

            if (!startResult.IsSuccessful)
            {
                return new ExtraAssetReturn()
                {
                    IsSuccessful = false,
                    ErrorMessage = string.Format(UIFunctions.GetResourceString("InstallExtraAPIFailure"), installId, startResult.StatusCode, startResult.Data?.Message)
                };
            }

            // if it's available immediately

            if (!string.IsNullOrWhiteSpace(startResult.Data.Response.DownloadUrl))
            {
                return await DownloadAndReturnMasterData(startResult.Data.Response.DownloadUrl, installId);
            }

            // otherwise poll until it is with timeout

            var pollRetriesWithNoProgress = 0;
            while (pollRetriesWithNoProgress < 100)
            {
                var pollGuid = startResult.Data.Response.PollToken;

                var pollResult = await API.PollGetExtraAssets(pollGuid);

                if (!pollResult.IsSuccessful)
                {
                    return new ExtraAssetReturn()
                    {
                        IsSuccessful = false,
                        ErrorMessage = string.Format(UIFunctions.GetResourceString("InstallExtraAPIFailure"), installId, pollResult.StatusCode, pollResult.Data?.Message)
                    };
                }

                if (!string.IsNullOrWhiteSpace(pollResult.Data.Response.DownloadUrl))
                {
                    return await DownloadAndReturnMasterData(pollResult.Data.Response.DownloadUrl, installId);
                }

                if (pollResult.Data.Response.PercentStatus == Percent)
                {
                    pollRetriesWithNoProgress += 1;
                }
                else
                {
                    Percent = pollResult.Data.Response.PercentStatus;
                    pollRetriesWithNoProgress = 0;
                }

                await Task.Delay(1000);
            }

            return new ExtraAssetReturn()
            {
                IsSuccessful = false,
                ErrorMessage = string.Format(UIFunctions.GetResourceString("InstallExtraAPIFailure"), installId, 500, "Timed out, no progress made after 100 seconds.")
            };
        }

        public async Task<ExtraAssetReturn> DownloadAndReturnMasterData(string downloadUrl, int installId)
        {
            Stage += 1;
            Percent = 50;
            var downloadResponse = await API.GetScript(downloadUrl, false);

            if (!downloadResponse.IsSuccessful)
            {
                return new ExtraAssetReturn()
                {
                    IsSuccessful = false,
                    ErrorMessage = string.Format(UIFunctions.GetResourceString("InstallExtraAPIFailure"), installId, downloadResponse.StatusCode, downloadResponse.ErrorMessage)
                };
            }

            Percent = 100;

            var downloadedScript = downloadResponse.Content;

            if (string.IsNullOrWhiteSpace(downloadedScript))
            {
                return new ExtraAssetReturn()
                {
                    IsSuccessful = false,
                    ErrorMessage = String.Format(UIFunctions.GetResourceString("InstallEmptyFileFailure"),
                        installId, downloadUrl)
                };
            }

            return new ExtraAssetReturn()
            {
                IsSuccessful = true,
                Data = Convert.FromBase64String(downloadedScript)
            };
        }


        public class ExtraAssetReturn: ScriptInstallStatus
        {
            public byte[] Data { get; set; }
        }

    }
}