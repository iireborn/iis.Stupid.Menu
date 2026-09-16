using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

namespace AntiIAuth
{
    /// <summary>
    /// A Malware protector for Developers
    /// Drag and drop this into your project(s) and call "AntiIAuthProtection.Initialize(this);" in your Awake() Method.
    /// Made by yours truly, poopoovr.
    /// </summary>
    public static class AntiIAuthProtection
    {
        public const string ANTIIAUTH_MARKER = "antiiauth.protection.instance";
        public static HashSet<string> FetchedURLs = new HashSet<string>();
        public static HashSet<string> FetchedKeywords = new HashSet<string>();
        private static bool _initialized = false;
        
        public static void Initialize(BaseUnityPlugin plugin)
        {
            if (_initialized) return;
            _initialized = true;

            var harmony = new Harmony($"antiiauth.protection.{Assembly.GetExecutingAssembly().GetName().Name}");
            harmony.PatchAll(typeof(Patch_WebRequest_Create_String));
            harmony.PatchAll(typeof(Patch_WebRequest_Create_Uri));
            harmony.PatchAll(typeof(Patch_UnityWebRequest_SendWebRequest));

            plugin.StartCoroutine(FetchBannedURLsRoutine());
        }


        private static IEnumerator FetchBannedURLsRoutine()
        {
            using (UnityWebRequest webRequest = UnityWebRequest.Get("https://gtag.website/bannedurls"))
            {
                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    string json = webRequest.downloadHandler.text;
                    ParseAndAddFetchedData(json);
                    ScanPluginsFolder();
                }
            }
        }

        private static void ParseAndAddFetchedData(string json)
        {
            try
            {
                string urlPattern = "\"bannedURLs\"\\s*:\\s*\\[(.*?)\\]";
                string keywordPattern = "\"bannedKeywords\"\\s*:\\s*\\[(.*?)\\]";

                Match urlMatch = Regex.Match(json, urlPattern, RegexOptions.Singleline);
                if (urlMatch.Success)
                {
                    MatchCollection matches = Regex.Matches(urlMatch.Groups[1].Value, "\"([^\"]+)\"");
                    foreach (Match m in matches)
                    {
                        FetchedURLs.Add(m.Groups[1].Value.ToLowerInvariant());
                    }
                }

                Match keywordMatch = Regex.Match(json, keywordPattern, RegexOptions.Singleline);
                if (keywordMatch.Success)
                {
                    MatchCollection matches = Regex.Matches(keywordMatch.Groups[1].Value, "\"([^\"]+)\"");
                    foreach (Match m in matches)
                    {
                        FetchedKeywords.Add(m.Groups[1].Value.ToLowerInvariant());
                    }
                }
            }
            catch
            {
            }
        }

        public static bool IsUrlBlocked(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;

            string lowerUrl = url.ToLowerInvariant();

            if (lowerUrl.Contains("gtag.website/bannedurls"))
                return false;

            foreach (var fetched in FetchedURLs)
            {
                if (lowerUrl.Contains(fetched))
                {
                    return true;
                }
            }
            return false;
        }

        private static void ScanPluginsFolder()
        {
            try
            {
                string pluginDir = Paths.PluginPath;
                string[] allDlls = Directory.GetFiles(pluginDir, "*.dll", SearchOption.AllDirectories);

                string ourAssemblyPath = Assembly.GetExecutingAssembly().Location;
                bool malwareFound = false;

                foreach (string dllPath in allDlls)
                {
                    if (string.Equals(dllPath, ourAssemblyPath, StringComparison.OrdinalIgnoreCase)) continue;

                    if (ScanFile(dllPath))
                    {
                        malwareFound = true;
                    }
                }

                if (malwareFound)
                {
                    Application.Quit();
                    Environment.Exit(0);
                }
            }
            catch
            {
            
            }
        }

        private static bool ScanFile(string filePath)
        {
            try
            {
                byte[] fileBytes = File.ReadAllBytes(filePath);
                string fileContent = Encoding.ASCII.GetString(fileBytes).ToLowerInvariant();

                if (fileContent.Contains(ANTIIAUTH_MARKER.ToLowerInvariant()))
                    return false;

                foreach (string keyword in FetchedKeywords)
                {
                    if (fileContent.Contains(keyword))
                    {
                        NeutralizeFile(filePath);
                        return true;
                    }
                }
            }
            catch
            {

            }
            return false;
        }

        private static void NeutralizeFile(string filePath)
        {
            try
            {
                string newPath = filePath + ".virus";
                if (File.Exists(newPath))
                {
                    File.Delete(newPath);
                }
                File.Move(filePath, newPath);
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(filePath), "READ_THIS_VIRUS_FOUND.txt"), "If you are wondering why this is here and why your DLLs have been renamed, they have been caught by our virus detection system. Please delete all the affected DLLs and continue to enjoy modding Gorilla Tag without the feeling someone is watching");
            }
            catch
            {

            }
        }

        [HarmonyPatch(typeof(WebRequest), nameof(WebRequest.Create), new Type[] { typeof(string) })]
        internal class Patch_WebRequest_Create_String
        {
            static bool Prefix(string requestUriString, ref WebRequest __result)
            {
                if (IsUrlBlocked(requestUriString))
                {
                    __result = null;
                    return false;    
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(WebRequest), nameof(WebRequest.Create), new Type[] { typeof(Uri) })]
        internal class Patch_WebRequest_Create_Uri
        {
            static bool Prefix(Uri requestUri, ref WebRequest __result)
            {
                if (requestUri != null && IsUrlBlocked(requestUri.ToString()))
                {
                    __result = null;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(UnityWebRequest), nameof(UnityWebRequest.SendWebRequest))]
        internal class Patch_UnityWebRequest_SendWebRequest
        {
            static bool Prefix(UnityWebRequest __instance)
            {
                if (__instance != null && IsUrlBlocked(__instance.url))
                {
                    __instance.Abort();
                    return false;
                }
                return true;
            }
        }
    }
}
