/*
 * ii's Stupid Menu  Managers/VoiceAssistantManager.cs
 * A mod menu for Gorilla Tag with over 1000+ mods
 *
 * Copyright (C) 2026  Goldentrophy Software
 * https://github.com/iireborn/iis.Stupid.Menu
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

using iiMenu.Classes.Menu;
using iiMenu.Menu;
using iiMenu.Mods;
using Photon.Pun;
using System;
using static iiMenu.Utilities.AssetUtilities;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace iiMenu.Managers
{
    public static class VoiceAssistant
    {
        public static readonly string[] WakeWords = { "System", "Jarvis", "Computer", "Assistant", "Alexa", "Monika" };

        public static int wakeWordIndex;
        public static string WakeWord => WakeWords[Mathf.Clamp(wakeWordIndex, 0, WakeWords.Length - 1)];

        public static string WakePhrase => "Hey " + WakeWord;

        public static string Greeting = "Hey there! How can I help?";

        public static bool voiceEnabled = true;
        public static bool greetingEnabled = true;
        public static bool orbEnabled = true;

        private static string WakeWordFilePath => $"{PluginInfo.BaseDirectory}/iiMenu_WakeWord.txt";
        private static string GreetingFilePath => $"{PluginInfo.BaseDirectory}/iiMenu_Greeting.txt";

        public static void Load()
        {
            try
            {
                if (!File.Exists(WakeWordFilePath))
                {
                    File.WriteAllText(WakeWordFilePath, WakeWord + "\n# The wake word the assistant listens for. \"Hey <word>\" also works.\n");
                }
                else
                {
                    string saved = File.ReadAllLines(WakeWordFilePath)
                        .Select(line => line.Trim())
                        .FirstOrDefault(line => line.Length > 0 && !line.StartsWith("#"));

                    if (!string.IsNullOrEmpty(saved))
                    {
                        int index = Array.FindIndex(WakeWords, word => word.Equals(saved, StringComparison.OrdinalIgnoreCase));
                        wakeWordIndex = index < 0 ? 0 : index;
                    }
                }

                if (File.Exists(GreetingFilePath))
                {
                    string saved = File.ReadAllLines(GreetingFilePath)
                        .Select(line => line.Trim())
                        .FirstOrDefault(line => line.Length > 0 && !line.StartsWith("#"));

                    if (!string.IsNullOrEmpty(saved))
                        Greeting = saved;
                }
                else
                {
                    File.WriteAllText(GreetingFilePath, Greeting + "\n# What the assistant says when it hears the wake word.\n");
                }
            }
            catch (Exception exception)
            {
                LogManager.LogError($"Failed to load the voice assistant settings: {exception.Message}");
            }

            RefreshWakeWordButton();
        }

        public static void SaveWakeWord()
        {
            try
            {
                File.WriteAllText(WakeWordFilePath, WakeWord + "\n# The wake word the assistant listens for. \"Hey <word>\" also works.\n");
            }
            catch (Exception exception)
            {
                LogManager.LogError($"Failed to save the wake word: {exception.Message}");
            }
        }

        public static void ChangeWakeWord(bool positive = true)
        {
            if (positive)
                wakeWordIndex++;
            else
                wakeWordIndex--;

            wakeWordIndex %= WakeWords.Length;
            if (wakeWordIndex < 0)
                wakeWordIndex = WakeWords.Length - 1;

            SaveWakeWord();
            RefreshWakeWordButton();

            NotificationManager.SendNotification($"<color=grey>[</color><color=cyan>SYSTEM</color><color=grey>]</color> Wake word set to \"{WakeWord}\" — say \"{WakePhrase}\" or \"{WakeWord}\".");
        }

        private static void RefreshWakeWordButton()
        {
            ButtonInfo button = Buttons.GetIndex("Change Wake Word");
            if (button != null)
                button.overlapText = $"Change Wake Word <color=grey>[</color><color=green>{WakeWord}</color><color=grey>]</color>";
        }

        public static string[] BuildWakeWords(string[] userKeywords)
        {
            List<string> words = new List<string> { WakeWord, WakePhrase, WakeWord.ToLowerInvariant() };

            if (userKeywords != null)
                words.AddRange(userKeywords.Where(keyword => !string.IsNullOrWhiteSpace(keyword)));

            return words
                .Select(word => word.Trim())
                .Where(word => word.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static bool IsWakeWord(string phrase)
        {
            if (string.IsNullOrEmpty(phrase))
                return false;

            string text = phrase.Trim().ToLowerInvariant();
            return text == WakeWord.ToLowerInvariant()
                || text == WakePhrase.ToLowerInvariant()
                || wordsIndexed.Contains(text);
        }

        private static readonly HashSet<string> wordsIndexed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "hey system", "hey jarvis", "hey computer", "hey assistant", "hey alexa",
            "system", "jarvis", "computer", "assistant", "alexa", "monika"
        };

        public enum OrbState { Hidden, Listening, Thinking, Speaking }

        private static OrbState orbState = OrbState.Hidden;
        public static OrbState State => orbState;

        private static GameObject orbRoot;
        private static Transform orbCore;
        private static Transform orbHalo;
        private static Transform[] orbRings;
        private static Quaternion[] orbRingBase;
        private static readonly float[] orbRingSpin = { 26f, -34f, 44f };
        private static Coroutine orbCoroutine;
        private static float orbHiddenAt;

        private static Material MakeMaterial(Color color)
        {
            Shader shader = Shader.Find("GUI/Text Shader")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Universal Render Pipeline/Unlit");

            if (shader == null)
                return null;

            Material material = new Material(shader);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);

            return material;
        }

        private static readonly Color listeningColor = new Color(0.25f, 0.85f, 1f);
        private static readonly Color thinkingColor = new Color(1f, 0.85f, 0.25f);
        private static readonly Color speakingColor = new Color(0.35f, 1f, 0.45f);

        private static Color StateColor(OrbState state) =>
            state == OrbState.Thinking ? thinkingColor :
            state == OrbState.Speaking ? speakingColor :
            listeningColor;

        public static void SetState(OrbState state)
        {
            bool shouldShow = orbEnabled && state != OrbState.Hidden;
            orbState = state;
            PublishStatus();

            if (!shouldShow)
            {
                orbHiddenAt = Time.time;
                return;
            }

            if (CoroutineManager.instance == null)
                return;

            BuildOrb();
            if (orbCoroutine == null)
                orbCoroutine = CoroutineManager.instance.StartCoroutine(OrbUpdate());
        }

        public static void Hide(bool immediate = false)
        {
            orbState = OrbState.Hidden;
            orbHiddenAt = immediate ? 0f : Time.time;
            PublishStatus();
        }

        public static void HideIfListening()
        {
            if (orbState == OrbState.Listening)
                Hide();
        }

        private static void PublishStatus()
        {
            if (orbState == OrbState.Hidden)
            {
                NotificationManager.information.Remove("System");
                NotificationManager.information.Remove("Heard");
                return;
            }

            string status =
                orbState == OrbState.Thinking ? "Thinking..." :
                orbState == OrbState.Speaking ? "Answering..." :
                "Listening...";

            NotificationManager.information["System"] = status;
        }

        public static void ShowHeard(string text)
        {
            lastHeardAt = Time.time;

            unheardFor = 0f;

            if (string.IsNullOrWhiteSpace(text))
            {
                NotificationManager.information.Remove("Heard");
                return;
            }

            NotificationManager.information["Heard"] = text.Length > 28 ? text.Substring(0, 28) + "..." : text;
        }

        private static float lastHeardAt;

        private static float unheardFor;

        private static Coroutine listeningWatchdog;

        private static IEnumerator ListeningWatchdog()
        {
            float patience = 7f;
            unheardFor = 0f;

            while (true)
            {
                yield return null;

                if (orbState != OrbState.Listening)
                    yield break;

                unheardFor += Time.unscaledDeltaTime;

                if (unheardFor < patience)
                    continue;

                Hide();
                Say("I did not catch that. Say the wake word and ask me again.", voiceEnabled);
                Settings.StopDictation();
                yield break;
            }
        }

        private static void BuildOrb()
        {
            if (orbRoot != null)
            {
                orbRoot.SetActive(true);
                return;
            }

            orbRoot = new GameObject("iiMenu Voice Assistant Orb");

            orbCore = MakeOrbSphere("Core", 0.022f, Color.white);
            orbHalo = MakeOrbSphere("Halo", 0.075f, new Color(1f, 1f, 1f, 0.10f));

            float[] radii = { 0.052f, 0.066f, 0.080f };
            float[] alphas = { 0.85f, 0.55f, 0.35f };
            Vector3[] tilts =
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(62f, 24f, 0f),
                new Vector3(-48f, -30f, 0f)
            };

            orbRings = new Transform[radii.Length];
            orbRingBase = new Quaternion[radii.Length];

            for (int i = 0; i < radii.Length; i++)
            {
                orbRings[i] = MakeOrbRing($"Arc {i + 1}", radii[i], 0.0032f, alphas[i]);
                orbRingBase[i] = Quaternion.Euler(tilts[i]);
            }
        }

        private static Transform MakeOrbSphere(string name, float scale, Color color)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            part.name = name;
            Object.Destroy(part.GetComponent<Collider>());

            part.transform.SetParent(orbRoot.transform, false);
            part.transform.localPosition = Vector3.zero;
            part.transform.localScale = Vector3.one * scale;

            Renderer renderer = part.GetComponent<Renderer>();
            Material material = MakeMaterial(color);
            if (material != null)
                renderer.material = material;
            else
                renderer.material.color = color;

            return part.transform;
        }

        private static Transform MakeOrbRing(string name, float radius, float width, float alpha)
        {
            GameObject ringObject = new GameObject(name);
            ringObject.transform.SetParent(orbRoot.transform, false);
            ringObject.transform.localPosition = Vector3.zero;

            LineRenderer line = ringObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 64;
            line.startWidth = width;
            line.endWidth = width;
            line.numCornerVertices = 6;
            line.numCapVertices = 6;
            line.startColor = new Color(listeningColor.r, listeningColor.g, listeningColor.b, alpha);
            line.endColor = line.startColor;

            Material material = MakeMaterial(line.startColor);
            if (material != null)
                line.material = material;

            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = (i / (float)line.positionCount) * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
            }

            ringObject.transform.localRotation = Quaternion.identity;
            return ringObject.transform;
        }

        private static IEnumerator OrbUpdate()
        {
            float spin = 0f;
            float[] alphas = { 0.85f, 0.55f, 0.35f };

            while (true)
            {
                if (orbState == OrbState.Hidden && orbRoot != null && Time.time - orbHiddenAt > 1.25f)
                    break;

                if (orbRoot == null)
                    break;

                if (orbState != OrbState.Hidden && GorillaTagger.Instance != null && GorillaTagger.Instance.headCollider != null)
                {
                    Transform head = GorillaTagger.Instance.headCollider.transform;

                    Vector3 target = head.position + (head.forward * 0.55f) + (head.right * 0.24f) - (head.up * 0.05f);
                    if (!orbRoot.activeSelf)
                    {
                        orbRoot.SetActive(true);
                        orbRoot.transform.position = target;
                    }

                    orbRoot.transform.position = Vector3.Lerp(orbRoot.transform.position, target, Time.deltaTime * 10f);
                    orbRoot.transform.rotation = Quaternion.Slerp(orbRoot.transform.rotation, Quaternion.LookRotation(head.forward, head.up), Time.deltaTime * 8f);

                    bool busy = orbState != OrbState.Listening;
                    spin += Time.deltaTime * (busy ? 110f : 42f);
                    float pulse = 1f + (Mathf.Sin(Time.time * (busy ? 5f : 2.4f)) * (busy ? 0.10f : 0.05f));

                    Color color = StateColor(orbState);
                    float stateAlpha = orbState == OrbState.Thinking ? 0.95f : orbState == OrbState.Speaking ? 0.9f : 0.8f;

                    if (orbRings != null)
                    {
                        for (int i = 0; i < orbRings.Length; i++)
                        {
                            if (orbRings[i] == null)
                                continue;

                            float baseSpin = spin * (1f + (i * 0.22f));
                            float sway = Mathf.Sin((Time.time * 0.6f) + i) * 8f;
                            orbRings[i].localRotation = orbRingBase != null && i < orbRingBase.Length
                                ? orbRingBase[i] * Quaternion.Euler(0f, 0f, baseSpin)
                                : Quaternion.Euler(sway, baseSpin, 0f);

                            LineRenderer line = orbRings[i].GetComponent<LineRenderer>();
                            if (line != null)
                            {
                                line.startColor = new Color(color.r, color.g, color.b, alphas[i] * stateAlpha);
                                line.endColor = line.startColor;
                            }
                        }
                    }

                    if (orbCore != null)
                    {
                        orbCore.localScale = Vector3.one * 0.022f * pulse;

                        Renderer coreRenderer = orbCore.GetComponent<Renderer>();
                        if (coreRenderer != null && coreRenderer.material != null)
                            coreRenderer.material.color = new Color(1f, 1f, 1f, stateAlpha);
                    }

                    if (orbHalo != null)
                    {
                        orbHalo.localScale = Vector3.one * (0.075f * pulse);

                        Renderer haloRenderer = orbHalo.GetComponent<Renderer>();
                        if (haloRenderer != null && haloRenderer.material != null)
                            haloRenderer.material.color = new Color(color.r, color.g, color.b, 0.09f * stateAlpha);
                    }
                }
                else if (orbRoot.activeSelf)
                {
                    orbRoot.SetActive(false);
                }

                yield return null;
            }

            DestroyOrb();
            orbCoroutine = null;
        }

        private static void DestroyOrb()
        {
            if (orbRoot != null)
                Object.Destroy(orbRoot);

            orbRoot = null;
            orbCore = null;
            orbHalo = null;
            orbRings = null;
            orbRingBase = null;
        }

        public static string CleanName(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return raw;

            string clean = raw.Replace("\n", " ");
            int colorTag = clean.IndexOf(" <color", StringComparison.OrdinalIgnoreCase);
            if (colorTag > 0)
                clean = clean.Substring(0, colorTag);

            return clean.Trim();
        }

        public static ButtonInfo FindButton(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            string wanted = name.Trim().Trim('"', '\'', '.', '!', '?');

            ButtonInfo exact = Buttons.GetIndex(wanted);
            if (exact != null)
                return exact;

            var candidates = Buttons.buttons
                .SelectMany((list, categoryIndex) => list.Select(button => new { button, categoryIndex }))
                .Where(entry => !Buttons.categoryNames[entry.categoryIndex].Contains("Settings", StringComparison.OrdinalIgnoreCase))
                .Select(entry => new
                {
                    entry.button,
                    entry.categoryIndex,
                    text = CleanName(entry.button.overlapText ?? entry.button.buttonText)
                })
                .Where(entry => !string.IsNullOrEmpty(entry.text))
                .ToList();

            var best = candidates
                .Where(entry => entry.text.Equals(wanted, StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();

            best ??= candidates
                .Where(entry => entry.text.Contains(wanted, StringComparison.OrdinalIgnoreCase))
                .OrderBy(entry => Math.Abs(entry.text.Length - wanted.Length))
                .FirstOrDefault();

            best ??= candidates
                .Where(entry => wanted.Contains(entry.text, StringComparison.OrdinalIgnoreCase) && entry.text.Length > 2)
                .OrderByDescending(entry => entry.text.Length)
                .FirstOrDefault();

            if (best == null)
            {
                string relaxed = wanted.Replace("_", " ").Replace("-", " ");

                if (relaxed != wanted)
                    return FindButton(relaxed);
            }

            return best?.button;
        }

        public static string DescribeLocation(ButtonInfo button, out int categoryIndex, out int buttonIndex)
        {
            categoryIndex = -1;
            buttonIndex = -1;

            if (button == null)
                return null;

            for (int i = 0; i < Buttons.buttons.Length; i++)
            {
                int index = Array.IndexOf(Buttons.buttons[i], button);
                if (index < 0)
                    continue;

                categoryIndex = i;
                buttonIndex = index;
                break;
            }

            if (categoryIndex < 0)
                return null;

            int pageSize = Mathf.Max(1, Main.PageSize);
            return $"{Buttons.categoryNames[categoryIndex]}, page {(buttonIndex / pageSize) + 1}";
        }

        public static string DescribeMod(ButtonInfo button)
        {
            if (button == null)
                return null;

            string name = CleanName(button.overlapText ?? button.buttonText);
            string location = DescribeLocation(button, out _, out _);

            string kind = button.incremental
                ? "a setting you cycle through"
                : button.isTogglable
                    ? (button.enabled ? "a toggle, and it is on right now" : "a toggle")
                    : "a one time action";

            string sentence = string.IsNullOrEmpty(location)
                ? $"{name} is {kind}."
                : $"{name} is {kind}, in {location}.";

            string tooltip = CleanName(button.toolTip);
            bool usableTooltip = !string.IsNullOrWhiteSpace(tooltip)
                && !tooltip.StartsWith("This button doesn't have a tooltip", StringComparison.OrdinalIgnoreCase);

            if (usableTooltip)
            {
                if (tooltip.Length > 180)
                    tooltip = tooltip.Substring(0, 180).TrimEnd() + "...";

                sentence += $" {tooltip.Trim().TrimEnd('.')}.";
            }

            return sentence;
        }

        public static bool OpenLocation(ButtonInfo button)
        {
            if (button == null)
                return false;

            string location = DescribeLocation(button, out int categoryIndex, out int buttonIndex);
            if (location == null)
                return false;

            Buttons.CurrentCategoryName = Buttons.categoryNames[categoryIndex];

            int pageSize = Mathf.Max(1, Main.PageSize);
            Main.pageNumber = Mathf.Clamp(buttonIndex / pageSize, 0, Mathf.Max(0, Main.LastPage));

            return true;
        }

        public static void SpeakReply(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            bool narrate = Buttons.GetIndex("Narrate Assistant")?.enabled == true;
            bool globalNarrate = Buttons.GetIndex("Global Narrate Assistant")?.enabled == true;

            if (!narrate && !globalNarrate && !voiceEnabled)
                return;

            if (globalNarrate && PhotonNetwork.InRoom)
                Main.SpeakText(text);
            else
                Main.NarrateText(text);
        }

        public static IEnumerator HideAfterSpeech(string text)
        {
            float wait = Mathf.Clamp(AIManager.Duration(text ?? string.Empty) / 1000f, 1.5f, 12f) + 0.5f;
            yield return new WaitForSeconds(wait);

            if (orbState == OrbState.Speaking)
                Hide();
        }

        public static void FlashListening(float seconds = 2.5f)
        {
            if (!orbEnabled || CoroutineManager.instance == null)
                return;

            SetState(OrbState.Listening);
            CoroutineManager.instance.StartCoroutine(HideAfterDelay(seconds));
        }

        private static IEnumerator HideAfterDelay(float seconds)
        {
            yield return new WaitForSeconds(seconds);

            if (orbState == OrbState.Listening)
                Hide();
        }

        public static void Say(string text, bool speak = true)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            NotificationManager.SendNotification($"<color=grey>[</color><color=cyan>SYSTEM</color><color=grey>]</color> {text}", Math.Max(3000, AIManager.Duration(text)));

            if (speak)
                SpeakReply(text);
        }

        public static void PrewarmSounds()
        {
            LoadSoundFromURL($"{PluginInfo.ServerResourcePath}/Audio/Menu/confirm.ogg", "Audio/Menu/confirm.ogg");
            LoadSoundFromURL($"{PluginInfo.ServerResourcePath}/Audio/Menu/close.ogg", "Audio/Menu/close.ogg");
        }

        public static void Greet(bool playGreeting = true)
        {
            SetState(OrbState.Listening);
            ShowHeard(null);

            Settings.DictationPlay(LoadSoundFromURL($"{PluginInfo.ServerResourcePath}/Audio/Menu/confirm.ogg", "Audio/Menu/confirm.ogg"), Main.buttonClickVolume / 10f);

            if (playGreeting && greetingEnabled)
                Say(Greeting, voiceEnabled);

            if (CoroutineManager.instance != null)
            {
                if (listeningWatchdog != null)
                    CoroutineManager.instance.StopCoroutine(listeningWatchdog);

                listeningWatchdog = CoroutineManager.instance.StartCoroutine(ListeningWatchdog());
            }
        }

        public static void ManualWake()
        {
            NotificationManager.SendNotification("<color=grey>[</color><color=cyan>SYSTEM</color><color=grey>]</color> AI Assistant is under construction.", 4000);
            return;
        }

        public static void ManualWake_REAL()
        {
            if (Buttons.GetIndex("AI Assistant")?.enabled != true)
            {
                Say("Turn on AI Assistant in Menu Settings first, then wake me.", false);
                return;
            }

            if (Settings.drec != null)
            {
                Say("Already listening.", false);
                return;
            }

            Greet();
            CoroutineManager.instance.StartCoroutine(Settings.DictationRecognizer());
        }
    }
}
