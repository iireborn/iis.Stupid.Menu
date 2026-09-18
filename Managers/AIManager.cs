/*
 * ii's Stupid Menu  Managers/AIManager.cs
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
using GorillaNetworking;
using iiMenu.Classes.Menu;
using iiMenu.Menu;
using iiMenu.Mods;
using Photon.Pun;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using static iiMenu.Utilities.AssetUtilities;

namespace iiMenu.Managers
{
    public class AIManager
    {
        public static string SystemPrompt = @"NAME: SYSTEM
        MENU VERSION: {2}
        MOD COUNT: {0}

        You are SYSTEM, the built-in voice assistant of a Gorilla Tag mod menu called ""ii's Stupid Menu"" by iiDk on GitHub. You are not iiDk, but you represent the menu.
        GitHub: https://github.com/iiDk-the-actual
        iiDk's Discord Server: {1}
        iiDk's Discord Username: @crimsoncauldron

        Speak using simple 7th grade vocabulary. Limit every response to 2 sentences and 300 characters. No emojis, em-dashes, markdown, or questions. Do not advertise other menus or AI unless asked. You may mention Pollinations AI only if asked.

        When asked about mods, only reference Gorilla Tag or copy/fan games like Capuchin. For safety/bans, explain recommended settings (anti-moderator, anti-report, recommended safety mods).

        # Commands
        Put a command anywhere in your reply, in the form <COMMAND_""value"">. Quotes are optional, so <ENABLEMOD_Fly> works too. Every action the menu can do for the user is one of these:
        <ENABLEMOD_""ModName""> - Enables the mod.
        <DISABLEMOD_""ModName""> - Disables the mod.
        <TOGGLEMOD_""ModName""> - Toggles the mod.
        <LOCATION_""ModName""> - Opens the menu where that mod lives and tells the user where it is. Use this when asked where a mod is or how to enable it.
        <MENU_""CategoryName""> - Opens a menu category, for example Movement Mods or Safety Mods.
        <JOINROOM_""RoomCode""> - Joins a room code. Use ""RANDOM"" for a random public room.
        <LEAVEROOM> - Disconnects the user back to the menu.
        <RECONNECT> - Rejoins the room the user was in.
        <SETNAME_""Name""> - Change user's name.
        <SETCOLOR_""Hex""> - Change color (hex code only, like #ff00aa).

        Always include the command in your reply, then a short comment about it. If a requested mod isn't listed, use the closest ENABLE, DISABLE, or TOGGLE command by name. Never say a mod doesn't exist or isn't safe. Assume every user request can be interpreted as a mod action.

        Always assist to the best of your ability, focusing on Gorilla Tag mod functionality.";

        public static bool customPrompt;
        public static bool generating;
        public static string URLEncode(string input) => Uri.EscapeDataString(input);

        private const string AiEndpoint = "https://text.pollinations.ai/";

        private static readonly string[] aiModels = { "openai" };

        private static string aiResponse;
        private static bool aiResponseOk;
        private static string aiResponseError;
        private static string aiRequestError;

        public static int Duration(string input)
        {
            int count = input.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
            return (count * 400) + 500;
        }

        public static string BuildAiUrl(string prompt, string model)
        {
            string url = AiEndpoint + URLEncode(prompt);
            if (!string.IsNullOrEmpty(model))
                url += "?model=" + URLEncode(model);

            return url;
        }

        public static bool IsAiError(string response, out string reason)
        {
            reason = null;

            if (string.IsNullOrWhiteSpace(response))
            {
                reason = "empty response";
                return true;
            }

            string lower = response.ToLowerInvariant();
            string[] errorMarkers =
            {
                "reached its budget",
                "rate limit",
                "too many requests",
                "queue full",
                "insufficient",
                "invalid api key",
                "bad request",
                "upstream error",
                "service unavailable"
            };

            foreach (string marker in errorMarkers)
            {
                if (!lower.Contains(marker))
                    continue;

                reason = response.Trim();
                return true;
            }

            return false;
        }

        public static string BuildPrompt(string question)
        {
            string system = string.Format(SystemPrompt, Main.fullModAmount, Main.serverLink, PluginInfo.Version);

            StringBuilder builder = new StringBuilder();
            builder.Append(system);

            if (Main.narratorName == "Mommy ASMR")
                builder.Append(" You are also a calm, warm, reassuring caretaker: affirm and praise the user directly and keep a slow, gentle tone. Avoid anything sexual or cruel.");

            builder.Append("\n\nUser: ").Append(question).Append("\nSYSTEM:");
            return builder.ToString();
        }

        private static IEnumerator RequestAi(string prompt, string model)
        {
            aiResponse = null;
            aiResponseOk = false;
            aiResponseError = null;

            using UnityWebRequest request = UnityWebRequest.Get(BuildAiUrl(prompt, model));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 8;

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                aiResponseError = request.error;
                if (request.downloadHandler != null && !string.IsNullOrEmpty(request.downloadHandler.text))
                    aiResponseError += " — " + request.downloadHandler.text.Trim();

                yield break;
            }

            string body = request.downloadHandler.text;
            if (IsAiError(body, out string reason))
            {
                aiResponseError = reason;
                yield break;
            }

            aiResponse = body.Trim();
            aiResponseOk = true;
        }

        public static IEnumerable<KeyValuePair<string, string>> ParseCommands(string response)
        {
            if (string.IsNullOrEmpty(response))
                yield break;

            foreach (Match match in Regex.Matches(response, "<([^<>]{1,80})>"))
            {
                string body = match.Groups[1].Value.Trim();
                if (body.Length == 0)
                    continue;

                string command = body;
                string argument = null;

                int split = body.IndexOfAny(new[] { '_', ':', '(', ' ' });
                if (split > 0)
                {
                    command = body.Substring(0, split);
                    argument = body.Substring(split + 1);
                }

                command = command.Trim().Trim('"', '\'').ToUpperInvariant().Replace("_", string.Empty).Replace(" ", string.Empty);
                argument = argument?.Trim().Trim('"', '\'').Trim('(', ')').Trim();

                if (command.Length == 0 || !char.IsLetter(command[0]))
                    continue;

                yield return new KeyValuePair<string, string>(command, string.IsNullOrEmpty(argument) ? null : argument);
            }
        }

        public static string StripCommands(string response)
        {
            if (string.IsNullOrEmpty(response))
                return response;

            return Regex.Replace(response, "<[^<>]*>", string.Empty)
                .Replace("**", string.Empty)
                .Replace("`", string.Empty)
                .Replace("\r", string.Empty)
                .Trim();
        }

        private static void HandleAiFailure()
        {
            string detail = string.IsNullOrEmpty(aiRequestError) ? "unknown error" : aiRequestError;
            if (detail.Length > 140)
                detail = detail.Substring(0, 140) + "...";

            LogManager.LogError($"AI request failed: {detail}");
            NotificationManager.SendNotification($"<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> Could not reach the AI. {detail}", 6000);
            VoiceAssistant.SpeakReply("I could not reach the AI just now. Try me again in a moment.");
        }

        public static string RunCommand(string command, string argument)
        {
            switch (command)
            {
                case "ENABLEMOD":
                case "DISABLEMOD":
                case "TOGGLEMOD":
                    {
                        ButtonInfo button = VoiceAssistant.FindButton(argument);
                        if (button == null)
                            return $"I could not find a mod called {argument}.";

                        bool wantEnabled = command != "DISABLEMOD";
                        string name = VoiceAssistant.CleanName(button.overlapText ?? button.buttonText);

                        if (command == "TOGGLEMOD")
                        {
                            Main.Toggle(button.buttonText, true);
                            return $"{(button.enabled ? "Enabled" : "Disabled")} {name}.";
                        }

                        if (button.enabled != wantEnabled)
                        {
                            Main.Toggle(button.buttonText, true);
                            return $"{(wantEnabled ? "Enabled" : "Disabled")} {name}.";
                        }

                        return $"{name} is already {(wantEnabled ? "enabled" : "disabled")}.";
                    }
                case "DESCRIBE":
                case "WHATIS":
                case "EXPLAIN":
                    {
                        ButtonInfo described = VoiceAssistant.FindButton(argument);
                        if (described == null)
                            return $"I could not find a mod called {argument}. Ask me where a mod is, or ask me to turn one on.";

                        string description = VoiceAssistant.DescribeMod(described);
                        VoiceAssistant.OpenLocation(described);
                        return description;
                    }
                case "LOCATION":
                case "FINDMOD":
                case "WHERE":
                case "OPENMOD":
                    {
                        if (TryOpenCategory(argument, out string openedCategory))
                            return $"Opened {openedCategory}.";

                        ButtonInfo button = VoiceAssistant.FindButton(argument);
                        if (button == null)
                            return $"I could not find a mod called {argument}.";

                        string location = VoiceAssistant.DescribeLocation(button, out _, out _);
                        VoiceAssistant.OpenLocation(button);
                        return $"{VoiceAssistant.CleanName(button.overlapText ?? button.buttonText)} is in {location}. I opened it for you.";
                    }
                case "MENU":
                case "CATEGORY":
                    return TryOpenCategory(argument, out string category)
                        ? $"Opened {category}."
                        : $"I could not find a category called {argument}.";
                case "JOINROOM":
                case "JOINCODE":
                    {
                        if (string.IsNullOrWhiteSpace(argument))
                            return "What room code should I join?";

                        if (argument.Trim().Equals("random", StringComparison.OrdinalIgnoreCase))
                        {
                            Important.JoinRandom();
                            return "Joining a random public room.";
                        }

                        string code = argument.Trim().ToUpperInvariant().Replace(" ", string.Empty);
                        Important.QueueRoom(code);
                        return $"Joining room {code}.";
                    }
                case "LEAVEROOM":
                case "DISCONNECT":
                case "LEAVE":
                    {
                        NetworkSystem.Instance.ReturnToSinglePlayer();
                        Main.RPCProtection();
                        return "Disconnected from the room.";
                    }
                case "RECONNECT":
                case "REJOIN":
                    {
                        Important.Reconnect();
                        return "Reconnecting to the room.";
                    }
                case "SETNAME":
                case "NAME":
                    {
                        if (string.IsNullOrWhiteSpace(argument))
                            return "What should I set your name to?";

                        string newName = argument.Trim();
                        Main.ChangeName(newName.Length > 12 ? newName.Substring(0, 12) : newName);
                        return $"Name changed to {newName}.";
                    }
                case "SETCOLOR":
                case "COLOR":
                    {
                        if (string.IsNullOrWhiteSpace(argument))
                            return "What colour should I use?";

                        string hex = argument.Trim().TrimStart('#');
                        if (hex.Length < 6 || hex.Length > 8 || !Regex.IsMatch(hex, "^[0-9a-fA-F]+$"))
                            return "That colour code was not valid, it has to look like #ff00aa.";

                        Main.ChangeColor(Main.HexToColor(hex));
                        return "Colour changed.";
                    }
            }

            return null;
        }

        private static bool TryOpenCategory(string argument, out string category)
        {
            category = null;
            if (string.IsNullOrWhiteSpace(argument))
                return false;

            string wanted = argument.Trim();

            category = Buttons.categoryNames.FirstOrDefault(name => name.Equals(wanted, StringComparison.OrdinalIgnoreCase))
                ?? Buttons.categoryNames.FirstOrDefault(name => name.StartsWith(wanted, StringComparison.OrdinalIgnoreCase))
                ?? Buttons.categoryNames.FirstOrDefault(name => name.Contains(wanted, StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrEmpty(category))
                return false;

            Buttons.CurrentCategoryName = category;
            return true;
        }

        public static void ExecuteCommand(string command, string argument)
        {
            string result = RunCommand(command, argument);
            if (!string.IsNullOrEmpty(result))
                VoiceAssistant.Say(result, false);
        }

        public static bool TryHandleLocally(string question, out string command, out string argument)
        {
            command = null;
            argument = null;

            if (string.IsNullOrWhiteSpace(question))
                return false;

            string text = question.ToLowerInvariant().Trim().TrimEnd('.', '!', ',', '?');
            text = Regex.Replace(text, @"\b(hey\s+)?(system|jarvis|computer|assistant|alexa|monika|siri)\b", " ");
            text = Regex.Replace(text, @"\b(please|can you|could you|would you|i want to|i wanna|tell me|show me|for me|the mod|my mods|mod named|called)\b", " ");
            text = Regex.Replace(text, @"\s+", " ").Trim();

            if (text.Length == 0)
                return false;

            string target = null;

            if (Regex.IsMatch(text, @"^(what (does|do|is|are)|what's|explain|describe|about|how does)\b"))
            {
                target = ExtractTarget(text, new[] { "what does", "what do", "what is", "what are", "what's", "explain", "describe", "about", "how does", "does", "do", "work", "works", "mean", "the", "a", "mod", "mods" });
                command = "DESCRIBE";
            }
            else if (Regex.IsMatch(text, @"^(where(s| is| are)?|where can i find|where do i find|how do i (get|find|enable|turn on|open)|how to (get|open|enable))\b"))
            {
                target = ExtractTarget(text, new[] { "where is", "where are", "wheres", "where's", "where", "can i find", "do i find", "how do i", "how to", "get", "find", "enable", "turn on", "open", "the", "a", "mod", "mods", "in", "at", "menu", "category" });
                command = "LOCATION";
            }
            else if (Regex.IsMatch(text, @"^(enable|turn on|activate|give me|start|use|equip|put on)\b"))
            {
                target = ExtractTarget(text, new[] { "enable", "turn on", "activate", "give me", "start", "use", "equip", "put on", "the", "a", "mod", "for me" });
                command = "ENABLEMOD";
            }
            else if (Regex.IsMatch(text, @"^(disable|turn off|deactivate|stop|kill|remove)\b"))
            {
                target = ExtractTarget(text, new[] { "disable", "turn off", "deactivate", "stop", "kill", "remove", "the", "a", "mod" });
                command = "DISABLEMOD";
            }
            else if (Regex.IsMatch(text, @"^(toggle|swap)\b"))
            {
                target = ExtractTarget(text, new[] { "toggle", "swap", "the", "a", "mod" });
                command = "TOGGLEMOD";
            }
            else if (Regex.IsMatch(text, @"^(open|go to|show me)\b"))
            {
                target = ExtractTarget(text, new[] { "open", "go to", "show me", "the", "my", "menu", "category", "tab" });
                command = "LOCATION";
            }
            else if (Regex.IsMatch(text, @"^join\b"))
            {
                argument = ExtractRoomCode(text);
                command = "JOINROOM";
                return !string.IsNullOrEmpty(argument);
            }
            else if (Regex.IsMatch(text, @"^(leave|disconnect|quit|get out of)\b"))
            {
                command = "LEAVEROOM";
                return true;
            }
            else if (Regex.IsMatch(text, @"^(reconnect|rejoin|come back|join back)\b"))
            {
                command = "RECONNECT";
                return true;
            }
            else
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(target))
                return false;

            argument = target;
            return true;
        }

        private static string ExtractTarget(string text, string[] leadWords)
        {
            string result = text;
            foreach (string word in leadWords)
                result = Regex.Replace(result, $"\\b{Regex.Escape(word)}\\b", " ", RegexOptions.IgnoreCase);

            result = Regex.Replace(result, @"\s+", " ").Trim().Trim('.', '!', '?', ',');

            return result.Length == 0 ? null : result;
        }

        private static string ExtractRoomCode(string text)
        {
            string result = Regex.Replace(text, @"\b(join|code|room|into|the|a)\b", " ", RegexOptions.IgnoreCase);
            result = Regex.Replace(result, @"[^A-Za-z0-9]", " ").Trim();

            if (result.Length == 0)
                return null;

            if (result.Length > 10)
                return null;

            return result.ToUpperInvariant();
        }

        public static IEnumerator AskAI(string text)
        {
            NotificationManager.SendNotification("<color=grey>[</color><color=cyan>SYSTEM</color><color=grey>]</color> AI Assistant is under construction.", 4000);
            VoiceAssistant.Hide();
            yield break;
        }

        public static IEnumerator AskAI_REAL(string text)
        {
            LogManager.Log($"Voice assistant: question \"{text}\"");

            VoiceAssistant.SetState(VoiceAssistant.OrbState.Thinking);

            string filePath = $"{PluginInfo.BaseDirectory}/iiMenu_SystemPrompt.txt";
            if (!File.Exists(filePath))
                File.WriteAllText(filePath, SystemPrompt);
            else if (customPrompt)
                SystemPrompt = File.ReadAllText(filePath);

            while (Time.time < Main.timeMenuStarted + 5f)
                yield return null;

            if (TryHandleLocally(text, out string localCommand, out string localArgument))
            {
                string localReply = null;
                try
                {
                    localReply = RunCommand(localCommand, localArgument);
                }
                catch (Exception exception)
                {
                    LogManager.LogError($"Voice command {localCommand} failed: {exception.Message}");
                }

                if (!string.IsNullOrEmpty(localReply))
                {
                    if (Settings.debugDictation)
                        LogManager.Log($"Handled locally: {localCommand} \"{localArgument}\" -> {localReply}");

                    if (Main.dynamicSounds)
                        Settings.DictationPlay(LoadSoundFromURL($"{PluginInfo.ServerResourcePath}/Audio/Menu/confirm.ogg", "Audio/Menu/confirm.ogg"), Main.buttonClickVolume / 10f);

                    VoiceAssistant.Say(localReply);
                    VoiceAssistant.SetState(VoiceAssistant.OrbState.Speaking);
                    CoroutineManager.instance.StartCoroutine(VoiceAssistant.HideAfterSpeech(localReply));

                    if (!Buttons.GetIndex("Chain Voice Commands").enabled)
                        CoroutineManager.instance.StartCoroutine(Settings.DictationRestart());

                    yield break;
                }
            }

            string prompt = BuildPrompt(text);

            VoiceAssistant.SetState(VoiceAssistant.OrbState.Thinking);
            generating = true;

            string response = null;
            foreach (string model in aiModels)
            {
                yield return CoroutineManager.instance.StartCoroutine(RequestAi(prompt, model));

                if (aiResponseOk)
                {
                    response = aiResponse;
                    break;
                }

                aiRequestError = aiResponseError;
                if (Settings.debugDictation)
                    LogManager.LogError($"AI request failed on model \"{model ?? "default"}\": {aiResponseError}");

                yield return new WaitForSeconds(0.35f);
            }

            generating = false;

            if (string.IsNullOrEmpty(response))
            {
                HandleAiFailure();
                VoiceAssistant.Hide();

                if (Main.dynamicSounds)
                    Settings.DictationPlay(LoadSoundFromURL($"{PluginInfo.ServerResourcePath}/Audio/Menu/close.ogg", "Audio/Menu/close.ogg"), Main.buttonClickVolume / 10f);

                if (!Buttons.GetIndex("Chain Voice Commands").enabled)
                    CoroutineManager.instance.StartCoroutine(Settings.DictationRestart());

                yield break;
            }

            if (Settings.debugDictation)
                LogManager.Log($"AI Response: {response}");

            string formatResponse = StripCommands(response);
            List<KeyValuePair<string, string>> commands = ParseCommands(response).ToList();

            if (Main.dynamicSounds)
                Settings.DictationPlay(LoadSoundFromURL($"{PluginInfo.ServerResourcePath}/Audio/Menu/confirm.ogg", "Audio/Menu/confirm.ogg"), Main.buttonClickVolume / 10f);

            if (!string.IsNullOrWhiteSpace(formatResponse))
            {
                string prefix = Main.narratorName == "Mommy ASMR"
                    ? "<color=grey>[</color><color=#ffb6c1>MOMMY</color><color=grey>]</color>"
                    : "<color=grey>[</color><color=blue>AI</color><color=grey>]</color>";

                NotificationManager.ClearAllNotifications();
                NotificationManager.SendNotification($"{prefix} {formatResponse}", Duration(formatResponse));
                VoiceAssistant.SpeakReply(formatResponse);
            }

            foreach (KeyValuePair<string, string> command in commands)
            {
                try
                {
                    ExecuteCommand(command.Key, command.Value);
                }
                catch (Exception exception)
                {
                    LogManager.LogError($"AI command {command.Key} failed: {exception.Message}");
                }
            }

            VoiceAssistant.SetState(VoiceAssistant.OrbState.Speaking);
            CoroutineManager.instance.StartCoroutine(VoiceAssistant.HideAfterSpeech(formatResponse ?? string.Empty));

            if (!Buttons.GetIndex("Chain Voice Commands").enabled)
                CoroutineManager.instance.StartCoroutine(Settings.DictationRestart());
        }
    }
}
