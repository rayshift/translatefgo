using System.Collections.Generic;

namespace RayshiftTranslateFGO.Models
{
    public class AssetListAPIResponse : BaseAPIResponse
    {
        public new Dictionary<string, string> Response { get; set; }
    }
}