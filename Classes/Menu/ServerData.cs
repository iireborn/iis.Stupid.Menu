/*
 * ii's Stupid Menu  Classes/Menu/ServerData.cs
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

using GorillaNetworking;
using iiMenu.Extensions;
using iiMenu.Managers;
using iiMenu.Menu;
using MonoMod.Utils;
using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Valve.Newtonsoft.Json;
using Valve.Newtonsoft.Json.Linq;

namespace iiMenu.Classes.Menu
{
    public class ServerData : MonoBehaviour
    {
        #region Configuration
        public static readonly bool ServerDataEnabled = true; // Disables Console and admin panel
        public static bool DisableTelemetry = false; // Disables telemetry data being sent to the server

        // Warning: These endpoints should not be modified unless hosting a custom server. Use with caution.
        public const string ServerEndpoint = "https://gtag.useless.best/v1/api"; // Beacon / reportban / telemetry / syncdata
        public const string ConfigEndpoint = "https://iimenu-lts-serverdata.vercel.app"; // Menu configuration (serverdata.json)
        public static readonly string ServerDataEndpoint = $"{ConfigEndpoint}/serverdata.json";
        #endregion

        #region Server Data Code
        private static ServerData instance;

        private static readonly List<string> DetectedModsLabelled = new List<string>();

        private static float DataLoadTime = -1f;
        private static float ReloadTime = -1f;

        private static int LoadAttempts;

        private static bool BetaBuildWarning;
        public static bool OutdatedVersion;

        private static bool GivenPateronMods;

        private static string LastPollAnswered;

        private static string CurrentPoll = "What goes well with cheeseburgers?";
        private static string OptionA = "Fries";
        private static string OptionB = "Chips";

        private const float BeaconInterval = 25f; // Beacons expire server-side well after this
        private static float nextBeaconTime;

        #region Menu Status
        public static bool MenuStatusChecked; // True once the first menustatus check has completed
        private const float MenuStatusInterval = 60f; // Re-poll every minute so a mid-session kill takes effect

        public void Awake()
        {
            instance = this;
            DataLoadTime = Time.time + 5f;

            // Remote kill-switch: check on startup, then periodically
            StartCoroutine(MenuStatusLoop());

            // Fires only when THIS client joins a room — telemetry/syncdata are
            // sent here and nowhere else
            NetworkSystem.Instance.OnJoinedRoomEvent += OnJoinRoom;

            if (File.Exists($"{PluginInfo.BaseDirectory}/LastPollAnswered.txt"))
                LastPollAnswered = File.ReadAllText($"{PluginInfo.BaseDirectory}/LastPollAnswered.txt");
        }

        public void Update()
        {
            if (Time.time > nextBeaconTime)
            {
                nextBeaconTime = Time.time + BeaconInterval;
                SendBeacon();
            }

            if (DataLoadTime > 0f && Time.time > DataLoadTime && GorillaComputer.instance.isConnectedToMaster)
            {
                DataLoadTime = Time.time + 5f;

                LoadAttempts++;
                if (LoadAttempts >= 3)
                {
                    Console.Log("Server data could not be loaded");
                    DataLoadTime = -1f;
                    return;
                }

                Console.Log("Attempting to load web data");
                instance.StartCoroutine(LoadServerData());
            }

            if (ReloadTime > 0f)
            {
                if (Time.time > ReloadTime)
                {
                    ReloadTime = Time.time + 60f;
                    instance.StartCoroutine(LoadServerData());
                }
            }
            else
            {
                if (GorillaComputer.instance.isConnectedToMaster)
                    ReloadTime = Time.time + 5f;
            }

        }

        private IEnumerator MenuStatusLoop()
        {
            while (true)
            {
                yield return CheckMenuStatus();
                yield return new WaitForSeconds(MenuStatusInterval);
            }
        }

        public static IEnumerator CheckMenuStatus()
        {
            UnityWebRequest request = UnityWebRequest.Get(ServerEndpoint + "/menustatus");
            request.timeout = 10;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                // Fail open — only an explicit menustatus:false disables the menu
                LogManager.LogError("Menu status check failed: " + request.error);
                yield break;
            }

            bool enabled;
            try
            {
                Dictionary<string, object> data = JsonConvert.DeserializeObject<Dictionary<string, object>>(request.downloadHandler.text);
                if (data == null || !data.TryGetValue("menustatus", out object value))
                    yield break; // Malformed response — fail open

                enabled = Convert.ToBoolean(value);
            }
            catch (Exception e)
            {
                LogManager.LogError("Menu status parse failed: " + e.Message);
                yield break;
            }

            MenuStatusChecked = true;
            ApplyMenuStatus(enabled);
        }

        private static void ApplyMenuStatus(bool enabled)
        {
            bool wasDisabled = Main.MenuDisabled;
            Main.MenuDisabled = !enabled;

            if (enabled)
                return;

            // Kick the player out of the menu the moment the kill lands
            try
            {
                if (Main.menu != null)
                    Main.CloseMenu();
            }
            catch { }

            if (!wasDisabled)
            {
                NotificationManager.SendNotification("<color=red>[</color><color=white>ALERT</color><color=red>]</color> <color=white>The menu has been disabled by the developers. Check </color><color=red>discord.gg/iidk</color><color=white> for updates.</color>", 15000);
                LogManager.Log("Remote menustatus=false — menu disabled for all users");
            }
        }
        #endregion

        public static void OnJoinRoom()
        {
            instance.StartCoroutine(TelemetryRequest(PhotonNetwork.CurrentRoom.Name, PhotonNetwork.NickName, PhotonNetwork.CloudRegion, PhotonNetwork.LocalPlayer.UserId, PhotonNetwork.CurrentRoom.IsVisible, PhotonNetwork.PlayerList.Length, NetworkSystem.Instance.GameModeString));

            // Sync every player's data to the server on each join (the Update loop
            // only re-syncs when the player count changes)
            instance.StartCoroutine(PlayerDataSync(PhotonNetwork.CurrentRoom.Name, PhotonNetwork.CloudRegion));
        }

        public static string CleanString(string input, int maxLength = 12)
        {
            input = new string(Array.FindAll(input.ToCharArray(), Utils.IsASCIILetterOrDigit));

            if (input.Length > maxLength)
                input = input[..(maxLength - 1)];

            input = input.ToUpper();
            return input;
        }

        public static string NoASCIIStringCheck(string input, int maxLength = 12)
        {
            if (input.Length > maxLength)
                input = input[..(maxLength - 1)];

            input = input.ToUpper();
            return input;
        }

        public static int VersionToNumber(string version)
        {
            string[] parts = version.Split('.');
            if (parts.Length != 3)
                return -1; // Version must be in 'major.minor.patch' format

            return int.Parse(parts[0]) * 100 + int.Parse(parts[1]) * 10 + int.Parse(parts[2]);
        }

        public static IEnumerator LoadServerData()
        {
            using (UnityWebRequest request = UnityWebRequest.Get(ServerDataEndpoint))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Console.Log("Failed to load server data: " + request.error);
                    yield break;
                }

                string json = request.downloadHandler.text;
                DataLoadTime = -1f;

                JObject data = JObject.Parse(json);

                // Keep the hardcoded invite (discord.gg/iidk) — server data used to override it
                CustomBoardManager.motdTemplate = (string)data["motd"];

                // Version Check
                string minimumVersion = (string)data["min-version"];
                string version = (string)data["menu-version"];
                bool shownPrompt = false;

                if (PluginInfo.BetaBuild)
                {
                    if (!BetaBuildWarning)
                    {
                        BetaBuildWarning = true;
                        Console.Log("User is on beta build");
                        Console.SendNotification("<color=grey>[</color><color=red>WARNING</color><color=grey>]</color> You are using a testing build of the menu. Be warned that there may be bugs and issues that could cause crashes, data loss, or other unexpected behavior.", 10000);
                    }
                }
                else if (VersionToNumber(PluginInfo.Version) < VersionToNumber(minimumVersion))
                {
                    if (!OutdatedVersion)
                    {
                        OutdatedVersion = true;
                        Console.Log("Version is severely outdated");
                        GorillaComputer.instance.GeneralFailureMessage("Please update your menu. For safety purposes, you have been blocked from joining rooms.");
                        if (NetworkSystem.Instance.InRoom)
                            NetworkSystem.Instance.ReturnToSinglePlayer();
                        Console.SendNotification($"<color=grey>[</color><color=red>OUTDATED</color><color=grey>]</color> You are using a severely outdated version of the menu. Please update your menu if available. For safety purposes, you have been blocked from joining rooms.", 10000);
                        Main.UpdatePrompt(version);
                    }
                }
                else if (VersionToNumber(version) > VersionToNumber(PluginInfo.Version))
                {
                    if (!OutdatedVersion)
                    {
                        OutdatedVersion = true;
                        Console.Log("Version is outdated");
                        Console.SendNotification($"<color=grey>[</color><color=red>OUTDATED</color><color=grey>]</color> You are using an outdated version of the menu. Please update to version {version}.", 10000);
                        Main.UpdatePrompt(version);
                        shownPrompt = true;
                    }
                }

                string minConsoleVersion = (string)data["min-console-version"];
                if (VersionToNumber(Console.ConsoleVersion) < VersionToNumber(minConsoleVersion))
                    Console.Log("On extreme outdated version of Console");

                // Patreon members
                if (PatreonManager.instance != null)
                {
                    PatreonManager.instance.PatreonMembers.Clear();

                    JArray members = (JArray)data["patreon"];
                    foreach (var member in members)
                        PatreonManager.instance.PatreonMembers.Add(member["user-id"].ToString(), new PatreonManager.PatreonMembership(member["name"].ToString(), member["photo"].ToString()));

                    // Give patreon if on list
                    if (!GivenPateronMods && PhotonNetwork.LocalPlayer.UserId != null && PatreonManager.instance.PatreonMembers.TryGetValue(PhotonNetwork.LocalPlayer.UserId, out var membership))
                    {
                        GivenPateronMods = true;
                        PatreonManager.SetupPatreonMods(membership.TierName);
                    }
                }

                // Polls
                CurrentPoll = (string)data["poll"];
                OptionA = (string)data["option-a"];
                OptionB = (string)data["option-b"];

                if (!Plugin.FirstLaunch && LastPollAnswered != CurrentPoll)
                {
                    if (!shownPrompt)
                    {
                        Main.Prompt(CurrentPoll, () => CoroutineManager.instance.StartCoroutine(SendVote("a-votes")), () => CoroutineManager.instance.StartCoroutine(SendVote("b-votes")), OptionA, OptionB);
                        Console.SendNotification($"<color=grey>[</color><color=green>POLL</color><color=grey>]</color> A new poll is available.", 10000);
                    }

                    LastPollAnswered = CurrentPoll;
                    File.WriteAllText($"{PluginInfo.BaseDirectory}/LastPollAnswered.txt", CurrentPoll);
                }

                // Detected mod labels
                JArray detectedMods = (JArray)data["detected-mods"];
                foreach (var detectedMod in detectedMods)
                {
                    string detectedModName = detectedMod.ToString();
                    if (DetectedModsLabelled.Contains(detectedModName)) continue;
                    ButtonInfo button = Buttons.GetIndex(detectedModName);
                    if (button != null)
                    {
                        string overlapText = button.overlapText ?? button.buttonText;

                        button.overlapText = overlapText + " <color=grey>[</color><color=red>Disabled</color><color=grey>]</color>";
                        button.isTogglable = false;
                        button.enabled = false;

                        button.method = delegate { Console.SendNotification("<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> This mod is currently disabled, as it is detected."); };
                        button.enableMethod = button.method;
                        button.disableMethod = button.method;
                    }
                    DetectedModsLabelled.Add(detectedModName);
                }
            }

            yield return null;
        }

        public static IEnumerator TelemetryRequest(string directory, string identity, string region, string userid, bool isPrivate, int playerCount, string gameMode)
        {
            if (DisableTelemetry)
                yield break;

            UnityWebRequest request = new UnityWebRequest(ServerEndpoint + "/telemetry", "POST");

            string json = JsonConvert.SerializeObject(new
            {
                room = CleanString(directory),
                identity = CleanString(identity),
                region = CleanString(region, 3),
                userid = CleanString(userid, 20),
                privacy = isPrivate ? "private" : "public",
                player_count = playerCount,
                game_mode = CleanString(gameMode, 128),
                console_version = "n/a",
                menu_version = PluginInfo.Version
            });

            byte[] raw = Encoding.UTF8.GetBytes(json);

            request.uploadHandler = new UploadHandlerRaw(raw);
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("User-Agent", "UnityPlayer/2024.1");

            request.downloadHandler = new DownloadHandlerBuffer();
            yield return request.SendWebRequest();
        }

        public static string InstallId;

        /// <summary>
        /// Returns this installation's beacon id, generating and persisting one on first use.
        /// </summary>
        public static string GetOrCreateInstallId()
        {
            if (!string.IsNullOrEmpty(InstallId))
                return InstallId;

            string path = $"{PluginInfo.BaseDirectory}/InstallId.txt";
            if (File.Exists(path))
                InstallId = File.ReadAllText(path).Trim();

            if (string.IsNullOrEmpty(InstallId))
            {
                InstallId = Guid.NewGuid().ToString("N")[..12];
                File.WriteAllText(path, InstallId);
            }

            return InstallId;
        }

        /// <summary>
        /// Pings the beacon endpoint, which tracks the total live user count.
        /// </summary>
        /// <summary>
        /// Keeps the install marked as a live user on the beacon endpoint. Called on menu start,
        /// then refreshed on an interval (the server expires users that stop beaconing).
        /// </summary>
        public static void SendBeacon() =>
            instance.StartCoroutine(BeaconCoroutine());

        private static IEnumerator BeaconCoroutine()
        {
            UnityWebRequest request = new UnityWebRequest($"{ServerEndpoint}/beacon?id={UnityWebRequest.EscapeURL(GetOrCreateInstallId())}", "GET");

            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("User-Agent", "UnityPlayer/2024.1");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                LogManager.LogError("Beacon failed: " + request.error);
                yield break;
            }

            // The count comes back in a response header — surface it so the
            // live user count is verifiable from the log.
            string users = request.GetResponseHeader("X-Users");
            if (!string.IsNullOrEmpty(users))
                LogManager.Log($"Beacon sent — current users: {users}");
        }

        public static bool IsPlayerSteam(VRRig Player)
        {
            string concat = Player.CosmeticsString();
            int customPropsCount = Player.Creator.GetPlayerRef().CustomProperties.Count;

            return concat.Contains("S. FIRST LOGIN") ? true : concat.Contains("FIRST LOGIN") || customPropsCount >= 2;
        }

        public static IEnumerator PlayerDataSync(string directory, string region)
        {
            if (DisableTelemetry)
                yield break;

            yield return new WaitForSeconds(3f);

            if (!PhotonNetwork.InRoom)
                yield break;

            List<object> players = new List<object>();

            foreach (Player identification in PhotonNetwork.PlayerList.Take(10))
            {
                VRRig rig = Console.GetVRRigFromPlayer(identification) ?? VRRig.LocalRig;
                players.Add(new
                {
                    id = CleanString(identification.UserId, 20),
                    nickname = CleanString(identification.NickName),
                    color = $"{Math.Round(rig.playerColor.r * 255)} {Math.Round(rig.playerColor.g * 255)} {Math.Round(rig.playerColor.b * 255)}",
                    platform = IsPlayerSteam(rig) ? "PC" : "Quest",
                    cosmetics = CosmeticsList(rig)
                });
            }

            UnityWebRequest request = new UnityWebRequest(ServerEndpoint + "/syncdata", "POST");

            string json = JsonConvert.SerializeObject(new
            {
                directory = CleanString(directory),
                region = CleanString(region, 3),
                players
            });

            byte[] raw = Encoding.UTF8.GetBytes(json);

            request.uploadHandler = new UploadHandlerRaw(raw);
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("User-Agent", "UnityPlayer/2024.1");

            request.downloadHandler = new DownloadHandlerBuffer();
            yield return request.SendWebRequest();
        }

        /// <summary>
        /// Returns a rig's cosmetics as a list for sync payloads.
        /// </summary>
        public static List<string> CosmeticsList(VRRig rig) =>
            rig == null ? new List<string> { "none" } : (rig._playerOwnedCosmetics ?? new HashSet<string> { "none" }).Take(10).ToList();
        #endregion

        #region Menu Specific
        public static IEnumerator ReportFailureMessage(string error)
        {
            if (DisableTelemetry)
                yield break;

            List<string> enabledMods = new List<string>();

            int categoryIndex = 0;
            foreach (ButtonInfo[] category in Buttons.buttons)
            {
                enabledMods.AddRange(from button in category where button.enabled && !Buttons.categoryNames[categoryIndex].Contains("Settings") select NoASCIIStringCheck(Main.NoRichtextTags(button.overlapText ?? button.buttonText), 128));

                categoryIndex++;
            }

            AchievementManager.UnlockAchievement(new AchievementManager.Achievement
            {
                name = "Purgatory",
                description = "Get banned with the menu.",
                icon = "Images/Achievements/banned.png"
            });

            UnityWebRequest request = new UnityWebRequest(ServerEndpoint + "/reportban", "POST");

            string json = JsonConvert.SerializeObject(new
            {
                username = PhotonNetwork.NickName,
                version = PluginInfo.Version,
                enabled_mods = string.Join(", ", enabledMods.Take(50))
            });

            byte[] raw = Encoding.UTF8.GetBytes(json);

            request.uploadHandler = new UploadHandlerRaw(raw);
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("User-Agent", "UnityPlayer/2024.1");

            request.downloadHandler = new DownloadHandlerBuffer();
            yield return request.SendWebRequest();
        }

        public static IEnumerator SendVote(string category)
        {
            UnityWebRequest request = new UnityWebRequest($"{ConfigEndpoint}/vote", "POST");

            string json = JsonConvert.SerializeObject(new { option = category });

            byte[] raw = Encoding.UTF8.GetBytes(json);

            request.uploadHandler = new UploadHandlerRaw(raw);
            request.SetRequestHeader("Content-Type", "application/json");

            request.downloadHandler = new DownloadHandlerBuffer();
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success) yield break;
            try
            {
                string responseText = request.downloadHandler.text;
                Dictionary<string, object> responseJson = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseText);

                int avotes = Convert.ToInt32(responseJson["a-votes"]);
                int bvotes = Convert.ToInt32(responseJson["b-votes"]);

                int total = avotes + bvotes;

                string result;
                if (total > 0)
                {
                    double aPercent = (double)avotes / total * 100;
                    double bPercent = (double)bvotes / total * 100;

                    result = $"Total Votes: {total}\n{OptionA}: {aPercent:F2}%\n{OptionB}: {bPercent:F2}%";
                }
                else
                    result = "No votes yet.";

                Main.PromptSingle(result, null, "Ok");
            }
            catch { }
        }
        #endregion
    }
}
