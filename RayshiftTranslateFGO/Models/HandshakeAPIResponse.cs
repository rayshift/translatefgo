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
        public LinkedUserInfo AccountStatus { get; set; }
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

    public class ArtAPIResponse: BaseAPIResponse
    {
        public new ArtResponse Response { get; set; }
    }

    public class ArtResponse
    {
        public string AppVer { get; set; }
        public HandshakeAssetStatus JPAssetStatus { get; set; } = HandshakeAssetStatus.Missing;
        public HandshakeAssetStatus NAAssetStatus { get; set; } = HandshakeAssetStatus.Missing;
        public LinkedUserInfo AccountStatus { get; set; }
        public List<ArtUrl> NAArtUrls { get; set; }
        public List<ArtUrl> JPArtUrls { get; set; }
    }

    public class ArtUrl
    {
        // filename : checksum
        public List<ArtDownload> Urls { get; set; } = new List<ArtDownload>();
        public bool Starred { get; set; }
        public bool IsNew { get; set; }
        public long Size { get; set; } // only if new == false
        public bool IsCurrentlyInstalled { get; set; } // currently in assetstorage
        public bool ToBeInstalled { get; set; } = false;
        public List<int> ServantIDs { get; set; } = new List<int>();

    }

    public class ArtDownload
    {
        public string Url { get; set; }
        public string Hash { get; set; }
        public string Filename { get; set; }
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
        public bool HasExtraStage { get; set; } = false;
        public List<string> ExtraStages { get; set; } = new List<string>();
        public bool IsDonorOnly { get; set; } = false;
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