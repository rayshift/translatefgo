using System;
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
        public static string Sha1(Stream input)
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

            int code;

            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.P)
            {
                code = (int)info.LongVersionCode;
            }
            else
            {
#pragma warning disable 618
                code = info.VersionCode;
#pragma warning restore 618
            }
            return code;
        }

        public static string GetVersionName()
        {
            var context = global::Android.App.Application.Context;
            PackageManager manager = context.PackageManager;
            PackageInfo info = manager.GetPackageInfo(context.PackageName, 0);

            return info.VersionName;

        }
    }

    public class EndEarlyException : Exception
    {
        public EndEarlyException(string message, Exception innerException) : base(message, innerException)
        {
        }
        public EndEarlyException(string message) : base(message)
        {
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