using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using Valve.Newtonsoft.Json.Linq;
using iiMenu.Managers;
using iiMenu.Utilities;
using Photon.Voice;
using Photon.Voice.Unity;
using POpusCodec.Enums;
using static iiMenu.Utilities.FileUtilities;

namespace iiMenu.Mods
{
    public static class SoundboardManager
    {
        public const string MyInstantsApiBase = "https://myinstants-api.vercel.app";
        public const string MyInstantsSearchUrl = MyInstantsApiBase + "/search?q=";

        // "recent" is the only endpoint that returns results without a search query.
        // The upstream "best" and "trending" endpoints currently answer with an empty data array,
        // and single-character queries return HTTP 404, so both are guarded below.
        public const string MyInstantsRecentUrl = MyInstantsApiBase + "/recent";
        public const string MyInstantsTrendingUrl = MyInstantsApiBase + "/trending?q=en";
        public const string MyInstantsBestUrl = MyInstantsApiBase + "/best?q=en";
        public const int MyInstantsMinQueryLength = 2;
        public const int MyInstantsMaxResults = 60;

        public static float LocalVolume = 1f;
        public static float MicVolume = 1f;
        public static bool HighQuality = true;
        public static bool MuteWhilePlaying = false;

        private static GameObject previewManager;

        public static void EnsureMicHighQuality()
        {
            try
            {
                var vm = VoiceManager.Get();
                vm.OutputRate = 48000;
                vm.SamplingRate = 48000;
                vm.HighQualityMode = HighQuality;
                vm.SoundboardVolume = MicVolume;
                try
                {
                    var recorder = GorillaTagger.Instance?.myRecorder;
                    if (recorder != null)
                    {
                        try { recorder.SamplingRate = (SamplingRate)48000; } catch { }
                        try { recorder.Bitrate = 51000; } catch { }
                        try { recorder.RestartRecording(true); } catch { }
                    }
                }
                catch { }
            }
            catch { }
        }

        public static void ApplySettings()
        {
            try
            {
                var vm = VoiceManager.Get();
                vm.SoundboardVolume = MicVolume;
                vm.HighQualityMode = HighQuality;
            }
            catch { }
            if (previewManager != null)
            {
                var src = previewManager.GetComponent<AudioSource>();
                if (src != null) src.volume = LocalVolume;
            }
        }

        public static void PlayLocalPreview(AudioClip clip)
        {
            if (clip == null || LocalVolume <= 0.001f) return;
            try
            {
                if (previewManager == null)
                {
                    previewManager = new GameObject("SoundboardPreview");
                    var a = previewManager.AddComponent<AudioSource>();
                    a.spatialBlend = 0f;
                    a.playOnAwake = false;
                }
                var src = previewManager.GetComponent<AudioSource>();
                src.volume = LocalVolume;
                src.clip = clip;
                src.loop = false;
                src.Play();
            }
            catch { }
        }

        public static void StopLocalPreview()
        {
            try
            {
                if (previewManager != null)
                    previewManager.GetComponent<AudioSource>()?.Stop();
            }
            catch { }
        }

        public static Guid InjectMic(AudioClip clip, bool disableMic = false, float volume = -1f)
        {
            if (clip == null || !Photon.Pun.PhotonNetwork.InRoom) return Guid.Empty;
            if (volume < 0f) volume = MicVolume;
            if (volume <= 0.001f) return Guid.Empty;
            try { return VoiceManager.Get().AudioClip(clip, disableMic || MuteWhilePlaying, volume); } catch { return Guid.Empty; }
        }

        public static AudioClip PlayHighQuality(AudioClip clip, bool disableMic = false)
        {
            if (clip == null) return null;
            EnsureMicHighQuality();
            PlayLocalPreview(clip);
            if (Photon.Pun.PhotonNetwork.InRoom)
                InjectMic(clip, disableMic, MicVolume);
            return clip;
        }

        public static IEnumerator DownloadAndPlayMyInstants(string mp3Url, string title)
        {
            mp3Url = NormalizeMp3Url(mp3Url);
            title = HtmlDecode(title);

            if (string.IsNullOrEmpty(mp3Url))
            {
                NotificationManager.SendNotification("<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> That sound has no download link.");
                yield break;
            }

            string safe = FileUtilities.SanitizeFileName(title);
            if (safe.Length > 32) safe = safe.Substring(0, 32);
            string rel = $"Sounds/MyInstants/{safe}.mp3";
            string abs = Path.Combine(FileUtilities.GetGamePath(), PluginInfo.BaseDirectory, rel);
            try { Directory.CreateDirectory(Path.GetDirectoryName(abs)); } catch { }

            AudioClip clip = null;
            if (File.Exists(abs))
            {
                clip = AssetUtilities.LoadSoundFromFile(rel);
            }
            else
            {
                using (var req = UnityWebRequestMultimedia.GetAudioClip(mp3Url, AudioType.MPEG))
                {
                    // myinstants.com 403s default user agents — must look like a browser
                    try { req.SetRequestHeader("User-Agent", AssetUtilities.BrowserUserAgent); } catch { }
                    yield return req.SendWebRequest();
                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        try
                        {
                            clip = DownloadHandlerAudioClip.GetContent(req);
                            // Persist so folder drop-in sees it and it survives restart
                            byte[] bytes = req.downloadHandler?.data;
                            if (clip != null && bytes != null && bytes.Length > 1024)
                                File.WriteAllBytes(abs, bytes);
                        }
                        catch { clip = null; }
                    }

                    // Fallback: our downloader sets a browser UA and caches to disk
                    if (clip == null)
                        clip = AssetUtilities.LoadSoundFromURL(mp3Url, rel);
                }
            }
            if (clip != null) PlayHighQuality(clip, false);
            else NotificationManager.SendNotification("<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> Download failed.");
        }

        public static bool IsValidQuery(string query) =>
            !string.IsNullOrWhiteSpace(query) && query.Trim().Length >= MyInstantsMinQueryLength;

        public static void SearchMyInstants(string query, Action<List<MyInstantEntry>> onDone)
        {
            if (!IsValidQuery(query))
            {
                onDone?.Invoke(new List<MyInstantEntry>());
                return;
            }

            string url = MyInstantsSearchUrl + UnityWebRequest.EscapeURL(query.Trim());
            CoroutineManager.instance.StartCoroutine(FetchEntries(url, onDone));
        }

        public static void FetchTrending(Action<List<MyInstantEntry>> onDone) =>
            CoroutineManager.instance.StartCoroutine(FetchEntries(MyInstantsTrendingUrl, onDone));

        /// <summary>
        /// Fetches and parses one MyInstants endpoint. Failures report a readable reason instead of
        /// a silently empty list, so the menu can show the user what actually went wrong.
        /// </summary>
        public static IEnumerator FetchEntries(string url, Action<List<MyInstantEntry>> onDone, Action<string> onError = null)
        {
            List<MyInstantEntry> list = new List<MyInstantEntry>();

            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 15;
                try { req.SetRequestHeader("User-Agent", AssetUtilities.BrowserUserAgent); } catch { }

                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"request failed (HTTP {(long)req.responseCode})");
                    list = new List<MyInstantEntry>();
                }
                else
                {
                    list = ParseEntries(req.downloadHandler?.text);

                    if (list.Count == 0)
                        onError?.Invoke("MyInstants returned no sounds for that request");
                }
            }

            onDone?.Invoke(list);
        }

        public static List<MyInstantEntry> ParseEntries(string json)
        {
            List<MyInstantEntry> list = new List<MyInstantEntry>();

            if (string.IsNullOrEmpty(json))
                return list;

            try
            {
                JObject jo = JObject.Parse(json);
                JArray data = jo["data"] as JArray;
                if (data == null)
                    return list;

                foreach (JToken item in data)
                {
                    string title = HtmlDecode(item["title"]?.ToString() ?? "sound");
                    string mp3 = NormalizeMp3Url(item["mp3"]?.ToString());

                    if (string.IsNullOrEmpty(mp3))
                        continue;

                    list.Add(new MyInstantEntry
                    {
                        title = title,
                        mp3 = mp3,
                        id = item["id"]?.ToString(),
                        pageUrl = item["url"]?.ToString()
                    });

                    if (list.Count >= MyInstantsMaxResults)
                        break;
                }
            }
            catch { }

            return list;
        }

        /// <summary>
        /// The API returns HTML-escaped titles ("Now&#x27;s your chance"). Decode them so the button
        /// label and the cached file name both read correctly.
        /// </summary>
        public static string HtmlDecode(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            return value
                .Replace("&#x27;", "'")
                .Replace("&#39;", "'")
                .Replace("&quot;", "\"")
                .Replace("&#x2F;", "/")
                .Replace("&lt;", "<")
                .Replace("&gt;", ">")
                .Replace("&amp;", "&");
        }

        public static string NormalizeMp3Url(string mp3)
        {
            if (string.IsNullOrEmpty(mp3))
                return null;

            if (mp3.StartsWith("//"))
                return "https:" + mp3;

            if (mp3.StartsWith("/"))
                return "https://www.myinstants.com" + mp3;

            return mp3;
        }

        public class MyInstantEntry
        {
            public string title;
            public string mp3;
            public string id;
            public string pageUrl;
        }
    }
}
