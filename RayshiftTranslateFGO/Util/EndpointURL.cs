using System.Net;

namespace RayshiftTranslateFGO.Util
{
    public class EndpointURL
    {
        public static string EndPoint
        {
            get => _endPoint;
            set
            {
                _endPoint = value;
                NeedsRefresh = true;
            }
        }

        private static string _endPoint = "https://rayshift.io";
        public static bool NeedsRefresh = false;

        public static string OldEndPoint = "";

        public static readonly string DefaultEndPoint = "https://rayshift.io";

        public static string GetLinkedAccountKey()
        {
            if (EndpointURL.EndPoint != EndpointURL.DefaultEndPoint)
            {
                return "LinkedRayshiftKey_" + EndpointURL.EndPoint;
            }
            else
            {
                return "LinkedRayshiftKey";
            }
        }
    }
}