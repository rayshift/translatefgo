using System;
using System.Collections.Generic;
using System.IO;
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

        public AsyncUploader()
        {
            API = new RestfulAPI();
        }

        private async Task<ScriptInstallStatus> Prepare(MemoryStream file)
        {
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
            try
            {
                await Pieces.ParallelForEachAsync(async pair =>
                    {
                        try
                        {
                            await UploadChunk(pair.Key);
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
            var apiResult = await API.GetExtraAssets(Token, userToken, installId, region);

            if (!apiResult.IsSuccessful)
            {
                return new ExtraAssetReturn()
                {
                    IsSuccessful = false,
                    ErrorMessage = string.Format(UIFunctions.GetResourceString("InstallExtraAPIFailure"), installId, apiResult.StatusCode, apiResult.Data?.Message)
                };
            }

            return new ExtraAssetReturn()
            {
                IsSuccessful = true,
                Data = Convert.FromBase64String(apiResult.Data.Response["data"])
            };

        }

        public class ExtraAssetReturn: ScriptInstallStatus
        {
            public byte[] Data { get; set; }
        }

    }
}