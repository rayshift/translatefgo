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

        }
        public struct TranslationUpdateDetails
        {
            public int AppVer { get; set; }
            public string ReadableVer { get; set; }
            public string UpdateTitle { get; set; }
            public List<string> UpdateChanges { get; set; }
            public bool Required { get; set; }
        }
    }
}