/*
 * ii's Stupid Menu  Managers/TranslationManager.cs
 * A mod menu for Gorilla Tag with over 1000+ mods
 *
 * Copyright (C) 2026  Goldentrophy Software
 * https://github.com/iiDk-the-actual/iis.Stupid.Menu
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using Valve.Newtonsoft.Json;
using Valve.Newtonsoft.Json.Linq;
using static iiMenu.Menu.Main;

namespace iiMenu.Managers
{
    public class TranslationManager
    {
        public static readonly Dictionary<string, float> waitingForTranslate = new Dictionary<string, float>();
        public static readonly Dictionary<string, string> translateCache = new Dictionary<string, string>();
        private static readonly Dictionary<string, float> nextRetryTime = new Dictionary<string, float>();
        private static readonly HashSet<string> loggedFailures = new HashSet<string>();
        private const float TranslationRetryDelay = 30f;
        private const float TranslationRequestInterval = 1f;
        private static float nextRequestTime;

        /// <summary>
        /// Target language for translation. Format: "en", "fr", "de", "jp"
        /// </summary>
        public static string language;

        public static void ResetLanguageState()
        {
            waitingForTranslate.Clear();
            translateCache.Clear();
            nextRetryTime.Clear();
            loggedFailures.Clear();
        }

        public static string TranslateText(string input, Action<string> onTranslated = null)
        {
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(language) || language == "en")
                return input;

            if (translateCache.TryGetValue(input, out var text))
                return text;

            if (waitingForTranslate.TryGetValue(input, out float retryAt))
            {
                if (Time.time < retryAt)
                    return input;

                waitingForTranslate.Remove(input);
            }

            if (nextRetryTime.TryGetValue(input, out float nextRetry) && Time.time < nextRetry)
                return input;

            if (Time.time < nextRequestTime)
                return input;

            nextRequestTime = Time.time + TranslationRequestInterval;
            waitingForTranslate[input] = Time.time + TranslationRetryDelay;
            CoroutineManager.instance?.StartCoroutine(GetTranslation(input, onTranslated));
            return input;
        }

        public static IEnumerator GetTranslation(string text, Action<string> onTranslated = null)
        {
            if (string.IsNullOrEmpty(text))
                yield break;

            if (translateCache.TryGetValue(text, out var cached))
            {
                waitingForTranslate.Remove(text);
                nextRetryTime.Remove(text);
                onTranslated?.Invoke(cached);
                yield break;
            }

            if (string.IsNullOrEmpty(language) || language == "en")
            {
                waitingForTranslate.Remove(text);
                nextRetryTime.Remove(text);
                onTranslated?.Invoke(text);
                yield break;
            }

            string fileName = GetSHA256(text) + ".txt";
            string directoryPath = $"{PluginInfo.BaseDirectory}/TranslationData{language.ToUpper()}";

            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);

            string filePath = Path.Combine(directoryPath, fileName);
            string translation = null;

            if (!File.Exists(filePath))
            {
                string cleanText = Regex.Replace(text, @"([""'$`\\])", "\\$1");
                cleanText = cleanText[..Mathf.Min(cleanText.Length, 4096)];

                string cleanLang = Regex.Replace(language, @"[^a-zA-Z0-9]", "");
                cleanLang = cleanLang[..Mathf.Min(cleanLang.Length, 6)];

                string url =
                    "https://translate.googleapis.com/translate_a/single" +
                    "?client=gtx" +
                    "&sl=auto" +
                    $"&tl={cleanLang}" +
                    "&dt=t" +
                    $"&q={UnityWebRequest.EscapeURL(cleanText)}";

                using UnityWebRequest request = UnityWebRequest.Get(url);

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var parsed = JsonConvert.DeserializeObject<List<object>>(request.downloadHandler.text);
                        var sentences = parsed[0] as JArray;

                        StringBuilder sb = new StringBuilder();

                        foreach (var sentence in sentences)
                            sb.Append(sentence[0]);

                        translation = sb.ToString();

                        File.WriteAllText(filePath, translation);
                    }
                    catch (Exception e)
                    {
                        LogTranslationFailure(text, $"parse error: {e.Message}");
                    }
                }
                else
                    LogTranslationFailure(text, request.error);
            }
            else
                translation = File.ReadAllText(filePath);

            waitingForTranslate.Remove(text);

            if (string.IsNullOrEmpty(translation))
                yield break;

            nextRetryTime.Remove(text);
            translateCache[text] = translation;
            onTranslated?.Invoke(translation);
        }

        private static void LogTranslationFailure(string text, string error)
        {
            waitingForTranslate.Remove(text);
            nextRetryTime[text] = Time.time + TranslationRetryDelay;

            string key = $"{language}:{GetSHA256(text)}";
            if (loggedFailures.Add(key))
                LogManager.LogError($"Translation request failed: {error}");
        }
    }
}
