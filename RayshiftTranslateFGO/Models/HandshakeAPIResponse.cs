using System;
using System.Collections.Generic;
using RayshiftTranslateFGO.Services;
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
    public class HandshakeResponse
    {
        public string Region { get; set; }
        public string AppVer { get; set; }

        public ScriptLiveUpdate LiveStatus { get; set; }

        public List<TranslationList> Translations { get; set; }
        public HandshakeAssetStatus AssetStatus { get; set; } = HandshakeAssetStatus.Missing;
        private string _endpoint;

        public string Endpoint
        {
            get => _endpoint;
            set
            {
                _endpoint = value;
                if (!string.IsNullOrEmpty(value))
                {
                    EndpointURL.OldEndPoint = EndpointURL.EndPoint;
                    EndpointURL.EndPoint = value;
                }
                else if (!string.IsNullOrEmpty(EndpointURL.OldEndPoint))
                {
                    EndpointURL.EndPoint = EndpointURL.OldEndPoint;
                    EndpointURL.OldEndPoint = "";
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
        public FGORegion Region { get; set; }
        public bool Hidden { get; set; } = false;
    }

    public class ScriptLiveUpdate
    {
        public bool Enabled { get; set; }
        public string Title { get; set; }
        public string CurrentRelease { get; set; }
        public DateTime NextReleaseDate { get; set; }
        public string PercentDone { get; set; }
    }

    public enum HandshakeAssetStatus
    {
        Missing = 0,
        UpToDate = 1,
        UpdateRequired = 2,
        TimeTraveler = 4,
        Unrecognized = 8,
        Corrupt = 16
    }
}