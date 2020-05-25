using System.IO;
using System.Security.Cryptography;
using System.Text;
using Android.Content.PM;

namespace RayshiftTranslateFGO.Util
{
    public class ScriptUtil
    {
        /// <summary>
        /// If the app version is valid
        /// </summary>
        /// <param name="appVerRequired">Required minimum app version</param>
        /// <param name="appVerCheck">Actual app version</param>
        /// <returns>Valid</returns>
        public static bool IsValidAppVersion(string appVerRequired, string appVerCheck)
        {
            var splitAppVerRequired = appVerRequired.Split('.');
            var splitAppVerCheck = appVerCheck.Split('.');

            return splitAppVerRequired[0] == splitAppVerCheck[0] && int.Parse(splitAppVerRequired[1]) <= int.Parse(splitAppVerCheck[1]);
        }

        /// <summary>
        /// SHA1
        /// </summary>
        /// <param name="input">Input bytes</param>
        /// <returns>SHA</returns>
        public static string Sha1(FileStream input)
        {
            using SHA1Managed sha1 = new SHA1Managed();
            var hash = sha1.ComputeHash(input);
            var sb = new StringBuilder(hash.Length * 2);

            foreach (byte b in hash)
            {
                // can be "x2" if you want lowercase
                sb.Append(b.ToString("x2"));
            }

            return sb.ToString();
        }

        public static string Sha1(byte[] input)
        {
            using SHA1Managed sha1 = new SHA1Managed();
            var hash = sha1.ComputeHash(input);
            var sb = new StringBuilder(hash.Length * 2);

            foreach (byte b in hash)
            {
                // can be "x2" if you want lowercase
                sb.Append(b.ToString("x2"));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Get build of this app
        /// </summary>
        /// <returns>Build number</returns>
        public static int GetBuild()
        {
            var context = global::Android.App.Application.Context;
            PackageManager manager = context.PackageManager;
            PackageInfo info = manager.GetPackageInfo(context.PackageName, 0);

            return info.VersionCode;
        }
    }
    public enum TranslationFileStatus
    {
        Default = 0,
        NotModified,
        Translated,
        DifferentTranslation,
        Invalid,
        Missing,
        UpdateAvailable
    }
}