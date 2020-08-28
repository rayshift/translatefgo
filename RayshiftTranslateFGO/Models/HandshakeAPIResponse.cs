using System.Collections.Generic;
using RayshiftTranslateFGO.Util;
using RayshiftTranslateFGO.Views;

namespace RayshiftTranslateFGO.Models
{
    public class HandshakeAPIResponse: BaseAPIResponse
    {
        public new HandshakeResponse Response { get; set; }
    }

    /// <summary>
    /// Handshake response
    /// </summary>
    public struct HandshakeResponse
    {
        public string Region { get; set; }
        public string AppVer { get; set; }
        public string UpdateVer { get; set; }

        public List<TranslationList> Translations { get; set; }

        private string _endpoint;

        public string Endpoint
        {
            get => _endpoint;
            set
            {
                _endpoint = value;
                if (!string.IsNullOrEmpty(value))
                {
                    EndpointURL.EndPoint = value;
                }
            }
        }
    }

    /// <summary>
    /// Translation Handshake List (From RayshiftWeb.Controllers.TranslateAPIController)
    /// </summary>
    public class TranslationHandshakeList
    {
        public string GameSHA1 { get; set; }
        public string TranslatedSHA1 { get; set; }
        public string DownloadURL { get; set; }
        public long LastModified { get; set; }
        public TranslationFileStatus Status { get; set; }
        public long Size { get; set; }
    }

    public class TranslationList
    {
        public string Name { get; set; }
        public int Language { get; set; }
        public int Group { get; set; }
        public Dictionary<string, TranslationHandshakeList> Scripts { get; set; }
        public long TotalSize { get; set; }
    }
}