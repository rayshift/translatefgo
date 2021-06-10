using System;
using System.Collections.Generic;

namespace RayshiftTranslateFGO.Models
{
    public class VersionAPIResponse: BaseAPIResponse
    {

        public new VersionUpdate Response { get; set; }


        public struct VersionUpdate
        {
            public string Action { get; set; }
            public TranslationUpdateDetails Update { get; set; }
            public TranslationAnnouncements Announcement { get; set; }

        }
        public class TranslationUpdateDetails
        {
            public int AppVer { get; set; }
            public string ReadableVer { get; set; }
            public string UpdateTitle { get; set; }
            public List<string> UpdateChanges { get; set; }
            public bool Required { get; set; }
        }
        public class TranslationAnnouncements
        {
            public int id { get; set; }
            public DateTime Timestamp { get; set; }
            public string Title { get; set; }
            public string ImageUrl { get; set; }
            public string Message { get; set; }
            public bool IsSpecialAnnouncement { get; set; }
            public bool IsActiveAnnouncement { get; set; }
            public string Url { get; set; }
        }
    }
}