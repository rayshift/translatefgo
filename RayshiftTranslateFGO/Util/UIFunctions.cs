
using System;
using Xamarin.Essentials;

namespace RayshiftTranslateFGO.Util
{
    public class UIFunctions
    {
        public static void SetLocale(string language = "")
        {
            Preferences.Set("Language", language);
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo(language); 
            System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(language);
        }
        public static string GetResourceString(string key)
        {
            return AppResources.ResourceManager.GetString(key);
        }
    }
}