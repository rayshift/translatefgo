using System;
using System.Collections.Generic;
using Android.Util;
using Newtonsoft.Json;
using RayshiftTranslateFGO.Models;
using Xamarin.Forms;

namespace RayshiftTranslateFGO.Util
{
    public class InstallerUtil
    {
        public static Tuple<string, Color> GenerateStatusString(IDictionary<string, TranslationHandshakeList> translationList)
        {
            int translatedScripts = 0;
            int missingScripts = 0;
            int notModified = 0;
            int differentTranslation = 0;

            foreach (var item in translationList)
            {
                var status = item.Value.Status;
                switch (status)
                {
                    case TranslationFileStatus.UpdateAvailable:
                        return new Tuple<string, Color>(AppResources.StatusUpdateAvailable, Color.Coral);
                    case TranslationFileStatus.NotModified:
                        notModified += 1;
                        break;
                    case TranslationFileStatus.DifferentTranslation: // Different translation installed
                        return new Tuple<string, Color>(AppResources.StatusNotInstalled, Color.Crimson);
                    case TranslationFileStatus.Missing: // Some scripts are missing
                        missingScripts += 1;
                        break;
                    case TranslationFileStatus.Translated:
                        translatedScripts += 1;
                        break;
                    case TranslationFileStatus.Invalid: // Player hasn't launched their game yet
                        return new Tuple<string, Color>(AppResources.StatusMismatch, Color.Crimson);
                    case TranslationFileStatus.Default: // This shouldn't happen
                    default:
                        return new Tuple<string, Color>(AppResources.StatusErrorTryReinstall, Color.Crimson);
                }
            }

            if (translatedScripts == translationList.Count) // Installed
            {
                return new Tuple<string, Color>(AppResources.StatusInstalled, Color.LimeGreen);
            }

            if (missingScripts == translationList.Count || notModified == translationList.Count || differentTranslation == translationList.Count) // Nothing installed
            {
                return new Tuple<string, Color>(AppResources.StatusNotInstalled, Color.Crimson);
            }

            if (translatedScripts > 0 && Math.Abs(notModified - translatedScripts) == translationList.Count) // One or more scripts have changed, requiring an update
            {
                return new Tuple<string, Color>(AppResources.StatusUpdateRequired, Color.Crimson);
            }

            return new Tuple<string, Color>(AppResources.StatusUnknown, Color.Crimson);
        }

        public static string PeriodOfTimeOutput(TimeSpan tspan, int level = 0, string ago = " ago")
        {
            string how_long_ago = ago.Trim();
            if (level >= 2) return how_long_ago;
            if (tspan.Days > 1)
                how_long_ago = $"{tspan.Days} days{ago}";
            else if (tspan.Days == 1)
                how_long_ago =
                    $"1 day {PeriodOfTimeOutput(new TimeSpan(tspan.Hours, tspan.Minutes, tspan.Seconds), level + 1, ago)}";
            else if (tspan.Hours >= 1)
                how_long_ago =
                    $"{tspan.Hours} {((tspan.Hours > 1) ? "hours" : "hour")} {PeriodOfTimeOutput(new TimeSpan(0, tspan.Minutes, tspan.Seconds), level + 1, ago)}";
            else if (tspan.Minutes >= 1)
                how_long_ago =
                    $"{tspan.Minutes} {((tspan.Minutes > 1) ? "minutes" : "minute")} {PeriodOfTimeOutput(new TimeSpan(0, 0, tspan.Seconds), level + 1, ago)}";
            else if (tspan.Seconds >= 1)
                how_long_ago = $"{tspan.Seconds} {((tspan.Seconds > 1) ? "seconds" : "second")}{ago}";
            return how_long_ago;
        }
    }
}