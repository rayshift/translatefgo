using System.Collections.Generic;

namespace RayshiftTranslateFGO.Util
{
    public class AppNames
    {
        public static string[] ValidAppNames = new[]
        {
            "com.aniplex.fategrandorder",
            "com.aniplex.fategrandorder.en",
            "io.rayshift.betterfgo",
            "io.rayshift.betterfgo.en",
        };

        public static Dictionary<string, string> AppDescriptions = new Dictionary<string, string>()
        {
            { "com.aniplex.fategrandorder", "Fate/Grand Order JP" },
            { "com.aniplex.fategrandorder.en", "Fate/Grand Order NA" },
            { "io.rayshift.betterfgo", "BetterFGO JP" },
            { "io.rayshift.betterfgo.en", "BetterFGO NA" }
        };
    }
}