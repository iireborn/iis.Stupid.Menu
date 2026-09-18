using iiMenu.Classes.Menu;
using iiMenu.Menu;
using ExitGames.Client.Photon;
using GorillaNetworking;
using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using Valve.Newtonsoft.Json.Linq;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using GJoinType = GorillaNetworking.JoinType;

namespace iiMenu.Managers
{
    public static class IiServersManager
    {
        public const string ApiUrl = "https://gtag.useless.best/v1/api/iiservers";
        public const string Room1 = "II_BAN1";
        public const string Room2 = "II_BAN2";

        public static bool IsOnIiServers { get; private set; }

        private static string savedAppIdRealtime;
        private static string savedAppVersion;
        private static string savedFixedRegion;
        private static string savedServer;
        private static int savedPort;
        private static bool hasSaved;
        private static AuthenticationValues savedAuthValues;
        private static AuthenticationValues savedNetClientAuth;

        private static string iiAppId;
        private static string iiAppVersion = "1.0";
        private static string iiRegion = "us";
        private static string iiMotd;
        private static bool iiEnabled = true;
        private static bool fetching;
        private static bool connectRoutineRunning;

        public static string StatusText => IsOnIiServers ? "<color=green>iiSERVERS</color>" : "<color=grey>OFFICIAL</color>";
        public static string Motd => string.IsNullOrEmpty(iiMotd) ? "II_BAN1 / II_BAN2 (10 each, 20 CCU)" : iiMotd;

        public static void ShutdownForGameExit()
        {
            try
            {
                RestoreOfficialSettings();
                RestoreCustomAuth();
            }
            catch (Exception e)
            {
                LogManager.LogError($"iiServers shutdown restore failed: {e.Message}");
            }
            finally
            {
                IsOnIiServers = false;
            }
        }

        public static void EnterIiServers()
        {
            RefreshIiServersButtons();
            Buttons.CurrentCategoryName = "iiServers";
        }

        public static void RefreshIiServersButtons()
        {
            int cat = Buttons.GetCategory("iiServers");
            if (cat < 0) return;
            string status = IsOnIiServers ? "<color=green>ON iiSERVERS</color>" : "<color=grey>ON OFFICIAL</color>";
            string motdOverlap = Motd.Length > 32 ? Motd.Substring(0, 32) + "..." : Motd;
            Buttons.buttons[cat] = new ButtonInfo[]
            {
                new ButtonInfo { buttonText = "Exit iiServers", method = () => Buttons.CurrentCategoryName = "Room Mods", isTogglable = false, toolTip = "Back to Room Mods." },
                new ButtonInfo { buttonText = "Connect to iiServers", enableMethod = Connect, disableMethod = Disconnect, toolTip = "Live swap to your private Photon Cloud. ON = fetch https://gtag.useless.best/v1/api/iiservers (AppId/AppVersion/Region) -> disconnect official -> reconnect -> auto-join II_BAN1 else II_BAN2 (10/10, 20 CCU). OFF = restore official AppId/Version and reconnect. Banned users bypass PlayFab." },
                new ButtonInfo { buttonText = "Join II_BAN1", method = () => JoinSpecific(Room1), isTogglable = false, toolTip = "Joins II_BAN1 (10 max). Only works while on iiServers. If full, try II_BAN2." },
                new ButtonInfo { buttonText = "Join II_BAN2", method = () => JoinSpecific(Room2), isTogglable = false, toolTip = "Joins II_BAN2 (10 max). Second banned lobby." },
                new ButtonInfo { buttonText = "iiServers Status", overlapText = $"Status <color=grey>[</color>{StatusText}<color=grey>]</color> <color=grey>{motdOverlap}</color>", isTogglable = false, toolTip = $"API: {ApiUrl} | {Motd} | II_BAN1 + II_BAN2 = 20 CCU. Empty rooms are destroyed by Photon (0 CCU).", label = true },
                new ButtonInfo { buttonText = "Refresh iiServers Config", method = () => { if (CoroutineManager.instance != null) CoroutineManager.instance.StartCoroutine(FetchConfigRoutine((ok) => { RefreshIiServersButtons(); NotificationManager.SendNotification(ok ? $"<color=grey>[</color><color=green>iiSERVERS</color><color=grey>]</color> Config refreshed: {(string.IsNullOrEmpty(iiAppId) ? "no AppId yet" : iiAppId.Substring(0, Math.Min(8, iiAppId.Length)) + "...")} {iiAppVersion} {iiRegion}" : "<color=grey>[</color><color=red>iiSERVERS</color><color=grey>]</color> Refresh failed."); })); }, isTogglable = false, toolTip = "Re-fetches AppId/AppVersion/Region from your API without reconnecting." },
            };
            try { var f = typeof(Buttons).GetField("cacheGetIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static); var d = f?.GetValue(null) as System.Collections.IDictionary; d?.Clear(); } catch { }
        }

        public static void Connect()
        {
            if (IsOnIiServers)
            {
                NotificationManager.SendNotification("<color=grey>[</color><color=green>iiSERVERS</color><color=grey>]</color> Already on iiServers.");
                return;
            }
            if (connectRoutineRunning)
            {
                NotificationManager.SendNotification("<color=grey>[</color><color=yellow>iiSERVERS</color><color=grey>]</color> Connection already in progress.");
                return;
            }
            if (CoroutineManager.instance == null)
            {
                NotificationManager.SendNotification("<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> CoroutineManager not ready.");
                try { Buttons.GetIndex("Connect to iiServers").enabled = false; } catch { }
                return;
            }
            connectRoutineRunning = true;
            CoroutineManager.instance.StartCoroutine(ConnectRoutineGuarded());
        }

        private static IEnumerator ConnectRoutineGuarded()
        {
            try { yield return ConnectRoutine(); }
            finally { connectRoutineRunning = false; }
        }

        public static void Disconnect()
        {
            if (!IsOnIiServers)
            {
                NotificationManager.SendNotification("<color=grey>[</color><color=grey>OFFICIAL</color><color=grey>]</color> Already on official.");
                return;
            }
            if (CoroutineManager.instance == null) return;
            CoroutineManager.instance.StartCoroutine(DisconnectRoutine());
        }

        private static RoomConfig BuildIiRoomConfig()
        {
            string gm = "CASUAL";
            string queue = "DEFAULT";
            string lang = "English";
            string platform = "STANDALONE";
            string fan = "false";

            try
            {
                var trigger = PhotonNetworkController.Instance?.currentJoinTrigger ?? GorillaComputer.instance?.GetJoinTriggerForZone("forest");
                string full = null;
                try { full = trigger?.GetFullDesiredGameModeString(); } catch { }
                if (!string.IsNullOrEmpty(full))
                    gm = full;
            }
            catch { }

            try { queue = GorillaComputer.instance?.currentQueue ?? "DEFAULT"; if (string.IsNullOrEmpty(queue)) queue = "DEFAULT"; } catch { }
            try { lang = LocalisationManager.CurrentLanguage.ToString(); } catch { }
            try { platform = PhotonNetworkController.Instance?.platformTag ?? "STANDALONE"; if (string.IsNullOrEmpty(platform)) platform = "STANDALONE"; } catch { }
            try { fan = GorillaTagScripts.SubscriptionManager.IsLocalSubscribed() ? "true" : "false"; } catch { }

            Hashtable props = new Hashtable
            {
                { "gameMode", gm },
                { "platform", platform },
                { "queueName", queue },
                { "language", lang },
                { "fan_club", fan }
            };

            return new RoomConfig
            {
                createIfMissing = true,
                isJoinable = true,
                isPublic = true,
                MaxPlayers = 10,
                CustomProps = props
            };
        }

        private static RoomOptions BuildIiRoomOptions()
        {
            RoomConfig config = BuildIiRoomConfig();
            return new RoomOptions
            {
                MaxPlayers = 10,
                IsVisible = config.isPublic,
                IsOpen = config.isJoinable,
                CustomRoomProperties = config.CustomProps,
                CustomRoomPropertiesForLobby = new[] { "gameMode", "platform", "queueName", "language", "fan_club" }
            };
        }

        private static IEnumerator ConnectRoutine()
        {
            NotificationManager.SendNotification("<color=grey>[</color><color=cyan>iiSERVERS</color><color=grey>]</color> Checking iiServers config...");
            bool fetched = false;
            yield return FetchConfigRoutine((ok) => fetched = ok);
            if (!fetched || string.IsNullOrEmpty(iiAppId))
            {
                NotificationManager.SendNotification("<color=grey>[</color><color=red>iiSERVERS</color><color=grey>]</color> No AppId from API. Set https://gtag.useless.best/v1/api/iiservers to {\"appId\":\"...\",\"appVersion\":\"1.0\",\"region\":\"us\"} and try again.");
                LogManager.LogError("iiServers: fetch failed or missing appId");
                try { Buttons.GetIndex("Connect to iiServers").enabled = false; } catch { }
                RefreshIiServersButtons();
                yield break;
            }
            if (!iiEnabled)
            {
                NotificationManager.SendNotification("<color=grey>[</color><color=yellow>iiSERVERS</color><color=grey>]</color> iiServers is disabled by API (enabled=false).");
                try { Buttons.GetIndex("Connect to iiServers").enabled = false; } catch { }
                yield break;
            }

            try { Buttons.GetIndex("Connect to iiServers").enabled = true; } catch { }

            SaveOriginalIfNeeded();

            NotificationManager.SendNotification($"<color=grey>[</color><color=cyan>iiSERVERS</color><color=grey>]</color> Leaving official and swapping to iiServers ({(string.IsNullOrEmpty(iiRegion) ? "us" : iiRegion)})...", 5000);

            try { NetworkSystem.Instance.ReturnToSinglePlayer(); } catch { }
            try { PhotonNetwork.Disconnect(); } catch { }

            float deadline = Time.time + 15f;
            while (Time.time < deadline && (PhotonNetwork.IsConnected || PhotonNetwork.NetworkClientState != ClientState.Disconnected))
                yield return null;
            deadline = Time.time + 12f;
            while (Time.time < deadline && NetworkSystem.Instance != null && NetworkSystem.Instance.netState != NetSystemState.Idle)
                yield return null;
            if (PhotonNetwork.IsConnected || PhotonNetwork.NetworkClientState != ClientState.Disconnected)
            {
                LogManager.LogError($"iiServers pre-swap still connected state={PhotonNetwork.NetworkClientState} netState={NetworkSystem.Instance?.netState}");
                try { PhotonNetwork.Disconnect(); } catch { }
                yield return new WaitForSeconds(0.8f);
            }
            yield return new WaitForSeconds(0.4f);

            try
            {
                var settings = PhotonNetwork.PhotonServerSettings.AppSettings;
                settings.AppIdRealtime = iiAppId;
                settings.AppVersion = string.IsNullOrEmpty(iiAppVersion) ? "1.0" : iiAppVersion;
                settings.FixedRegion = string.IsNullOrEmpty(iiRegion) ? "us" : iiRegion.ToLowerInvariant();
                LogManager.Log($"iiServers: swapped AppSettings -> {iiAppId.Substring(0, Math.Min(8, iiAppId.Length))}... {settings.AppVersion} {settings.FixedRegion}");
            }
            catch (Exception e)
            {
                NotificationManager.SendNotification($"<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> Could not swap AppSettings: {e.Message}");
                LogManager.LogError($"iiServers swap failed: {e}");
                yield break;
            }

            NotificationManager.SendNotification($"<color=grey>[</color><color=cyan>iiSERVERS</color><color=grey>]</color> Connecting to iiServers {(string.IsNullOrEmpty(iiRegion) ? "" : $"({iiRegion})")}...", 5000);

            try { PhotonNetwork.ConnectUsingSettings(); } catch (Exception e) { LogManager.LogError($"iiServers ConnectUsingSettings failed: {e.Message}"); }

            deadline = Time.time + 14f;
            while (Time.time < deadline && !PhotonNetwork.IsConnectedAndReady)
                yield return null;

            if (!PhotonNetwork.IsConnectedAndReady)
            {
                string err = PhotonNetwork.NetworkClientState.ToString();
                NotificationManager.SendNotification($"<color=grey>[</color><color=red>iiSERVERS</color><color=grey>]</color> Could not connect to iiServers ({err}). Check AppId is PUN (not Fusion) and CCU not full.");
                LogManager.LogError($"iiServers connect timeout state={PhotonNetwork.NetworkClientState} server={PhotonNetwork.Server} cloudRegion={PhotonNetwork.CloudRegion}");
                if (!string.IsNullOrEmpty(iiMotd))
                    NotificationManager.SendNotification($"<color=grey>[</color><color=cyan>MOTD</color><color=grey>]</color> {iiMotd}", 6000);
                IsOnIiServers = false;
                RestoreOfficialSettings();
                RestoreCustomAuth();
                try { PhotonNetwork.Disconnect(); } catch { }
                yield return new WaitForSeconds(0.5f);
                try { PhotonNetwork.ConnectUsingSettings(); } catch (Exception restoreError) { LogManager.LogError($"iiServers official rollback failed: {restoreError.Message}"); }
                RefreshIiServersButtons();
                yield break;
            }

            IsOnIiServers = true;
            RefreshIiServersButtons();
            NotificationManager.SendNotification($"<color=grey>[</color><color=green>iiSERVERS</color><color=grey>]</color> Connected! Joining {Room1} / {Room2}...", 4000);
            if (!string.IsNullOrEmpty(iiMotd))
                NotificationManager.SendNotification($"<color=grey>[</color><color=cyan>MOTD</color><color=grey>]</color> {iiMotd}", 6000);

            yield return JoinIiServersRooms();
        }

        private static IEnumerator DisconnectRoutine()
        {
            NotificationManager.SendNotification("<color=grey>[</color><color=cyan>iiSERVERS</color><color=grey>]</color> Restoring official servers...");

            try { NetworkSystem.Instance.ReturnToSinglePlayer(); } catch { }
            try { PhotonNetwork.Disconnect(); } catch { }

            float deadline = Time.time + 10f;
            while (Time.time < deadline && PhotonNetwork.IsConnected)
                yield return null;
            deadline = Time.time + 8f;
            while (Time.time < deadline && NetworkSystem.Instance != null && NetworkSystem.Instance.netState != NetSystemState.Idle)
                yield return null;
            yield return new WaitForSeconds(0.5f);

            try
            {
                if (hasSaved)
                {
                    var s = PhotonNetwork.PhotonServerSettings.AppSettings;
                    s.AppIdRealtime = savedAppIdRealtime;
                    s.AppVersion = savedAppVersion;
                    s.FixedRegion = savedFixedRegion;
                    if (!string.IsNullOrEmpty(savedServer)) s.Server = savedServer; else s.Server = "";
                    if (savedPort != 0) s.Port = savedPort;
                    LogManager.Log($"iiServers: restored official {savedAppIdRealtime?.Substring(0, Math.Min(8, savedAppIdRealtime.Length))}... {savedAppVersion} {savedFixedRegion}");
                }
            }
            catch (Exception e) { LogManager.LogError($"iiServers restore failed: {e.Message}"); }

            RestoreCustomAuth();

            yield return new WaitForSeconds(0.2f);
            try { PhotonNetwork.ConnectUsingSettings(); } catch (Exception e) { LogManager.LogError($"iiServers official reconnect failed: {e.Message}"); }

            deadline = Time.time + 12f;
            while (Time.time < deadline && !PhotonNetwork.IsConnectedAndReady)
                yield return null;

            IsOnIiServers = false;
            RefreshIiServersButtons();
            if (PhotonNetwork.IsConnectedAndReady)
                NotificationManager.SendNotification("<color=grey>[</color><color=green>OFFICIAL</color><color=grey>]</color> Back on official servers.");
            else
                NotificationManager.SendNotification("<color=grey>[</color><color=yellow>OFFICIAL</color><color=grey>]</color> Restored settings. Reconnecting...", 6000);
        }

        private static IEnumerator JoinIiServersRooms()
        {
            float connectedDeadline = Time.time + 10f;
            while (Time.time < connectedDeadline && !PhotonNetwork.IsConnectedAndReady)
                yield return null;
            if (!PhotonNetwork.IsConnectedAndReady)
            {
                NotificationManager.SendNotification("<color=grey>[</color><color=red>iiSERVERS</color><color=grey>]</color> Not connected, cannot join room.");
                yield break;
            }

            NotificationManager.SendNotification($"<color=grey>[</color><color=cyan>iiSERVERS</color><color=grey>]</color> Joining {Room1}...", 3000);
            bool joined = false;

            try
            {
                PhotonNetworkController.Instance.currentJoinType = GJoinType.Solo;
                PhotonNetwork.JoinOrCreateRoom(Room1, BuildIiRoomOptions(), TypedLobby.Default);
            }
            catch (Exception e)
            {
                LogManager.LogError($"iiServers JoinOrCreateRoom {Room1} failed: {e.Message}");
            }

            float wait = Time.time + 9f;
            while (Time.time < wait && !PhotonNetwork.InRoom)
                yield return null;

            if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.Name == Room1) { joined = true; NotificationManager.SendNotification($"<color=grey>[</color><color=green>iiSERVERS</color><color=grey>]</color> Joined {Room1}!", 4000); yield break; }
            if (PhotonNetwork.InRoom) { joined = true; yield break; }

            NotificationManager.SendNotification($"<color=grey>[</color><color=yellow>iiSERVERS</color><color=grey>]</color> {Room1} full, trying {Room2}...", 3000);
            bool left = false;
            if (PhotonNetwork.InRoom)
            {
                try { PhotonNetwork.LeaveRoom(); left = true; } catch { }
                if (left) yield return new WaitForSeconds(0.8f);
            }
            else
            {
                try { NetworkSystem.Instance.ReturnToSinglePlayer(); } catch { }
                yield return new WaitForSeconds(0.4f);
            }

            try
            {
                PhotonNetworkController.Instance.currentJoinType = GJoinType.Solo;
                PhotonNetwork.JoinOrCreateRoom(Room2, BuildIiRoomOptions(), TypedLobby.Default);
            }
            catch (Exception e)
            {
                LogManager.LogError($"iiServers JoinOrCreateRoom {Room2} failed: {e.Message}");
            }

            wait = Time.time + 9f;
            while (Time.time < wait && !PhotonNetwork.InRoom)
                yield return null;

            if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.Name == Room2) { NotificationManager.SendNotification($"<color=grey>[</color><color=green>iiSERVERS</color><color=grey>]</color> Joined {Room2}!", 4000); joined = true; }
            else if (PhotonNetwork.InRoom) joined = true;

            if (!joined)
            {
                string state = PhotonNetwork.NetworkClientState.ToString();
                NotificationManager.SendNotification($"<color=grey>[</color><color=red>iiSERVERS</color><color=grey>]</color> Both {Room1}/{Room2} full or CCU 20/20 ({state}). Try again soon.", 7000);
                LogManager.Log($"iiServers both rooms failed state={state} inRoom={PhotonNetwork.InRoom}");
            }
        }

        public static void JoinSpecific(string room)
        {
            if (!IsOnIiServers)
            {
                NotificationManager.SendNotification("<color=grey>[</color><color=red>iiSERVERS</color><color=grey>]</color> Connect to iiServers first (toggle ON).");
                return;
            }
            if (!PhotonNetwork.IsConnectedAndReady)
            {
                NotificationManager.SendNotification("<color=grey>[</color><color=red>iiSERVERS</color><color=grey>]</color> Not connected to iiServers yet.");
                return;
            }
            if (CoroutineManager.instance == null) return;
            CoroutineManager.instance.StartCoroutine(JoinSpecificRoutine(room));
        }

        private static IEnumerator JoinSpecificRoutine(string room)
        {
            if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.Name == room)
            {
                NotificationManager.SendNotification($"<color=grey>[</color><color=green>iiSERVERS</color><color=grey>]</color> Already in {room}.");
                yield break;
            }
            bool left2 = false;
            try { if (PhotonNetwork.InRoom) { PhotonNetwork.LeaveRoom(); left2 = true; } } catch { }
            if (left2) yield return new WaitForSeconds(0.8f);
            NotificationManager.SendNotification($"<color=grey>[</color><color=cyan>iiSERVERS</color><color=grey>]</color> Joining {room}...", 3000);
            try
            {
                PhotonNetworkController.Instance.currentJoinType = GJoinType.Solo;
                PhotonNetwork.JoinOrCreateRoom(room, BuildIiRoomOptions(), TypedLobby.Default);
            }
            catch (Exception e)
            {
                LogManager.LogError($"iiServers JoinOrCreateRoom {room} failed: {e.Message}");
            }
            float wait = Time.time + 9f;
            while (Time.time < wait && !PhotonNetwork.InRoom)
                yield return null;
            if (PhotonNetwork.InRoom) NotificationManager.SendNotification($"<color=grey>[</color><color=green>iiSERVERS</color><color=grey>]</color> Joined {room}!", 4000);
            else NotificationManager.SendNotification($"<color=grey>[</color><color=red>iiSERVERS</color><color=grey>]</color> Could not join {room} (full/CCU).", 5000);
        }

        private static IEnumerator FetchConfigRoutine(Action<bool> callback)
        {
            if (fetching) { callback?.Invoke(!string.IsNullOrEmpty(iiAppId)); yield break; }
            fetching = true;
            string json = null;
            using (UnityWebRequest req = UnityWebRequest.Get(ApiUrl))
            {
                req.SetRequestHeader("User-Agent", "iis-Stupid-Menu");
                req.SetRequestHeader("Accept", "application/json");
                req.timeout = 12;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    LogManager.LogError($"iiServers fetch {ApiUrl} -> {req.error} {req.downloadHandler?.text}");
                    fetching = false;
                    callback?.Invoke(false);
                    yield break;
                }
                json = req.downloadHandler.text;
            }

            try
            {
                JObject obj = JObject.Parse(json);
                string appId = (string)obj["appId"] ?? (string)obj["appIdRealtime"] ?? (string)obj["AppId"] ?? (string)obj["AppIdRealtime"];
                string appVersion = (string)obj["appVersion"] ?? (string)obj["AppVersion"] ?? (string)obj["app_version"] ?? "1.0";
                string region = (string)obj["region"] ?? (string)obj["Region"] ?? (string)obj["fixedRegion"] ?? (string)obj["FixedRegion"] ?? "us";
                string motd = (string)obj["motd"] ?? (string)obj["MOTD"] ?? (string)obj["message"] ?? null;
                JToken enabledTok = obj["enabled"] ?? obj["Enabled"];
                bool enabled = enabledTok == null ? true : enabledTok.Type == JTokenType.Boolean ? (bool)enabledTok : true;

                if (!string.IsNullOrEmpty(appId))
                {
                    iiAppId = appId.Trim();
                    iiAppVersion = string.IsNullOrWhiteSpace(appVersion) ? "1.0" : appVersion.Trim();
                    iiRegion = string.IsNullOrWhiteSpace(region) ? "us" : region.Trim();
                    iiMotd = motd;
                    iiEnabled = enabled;
                    LogManager.Log($"iiServers config: {iiAppId.Substring(0, Math.Min(8, iiAppId.Length))}... {iiAppVersion} {iiRegion} enabled={iiEnabled}");
                    fetching = false;
                    callback?.Invoke(true);
                    yield break;
                }
                LogManager.LogError($"iiServers parse: missing appId in {json.Substring(0, Math.Min(300, json.Length))}");
            }
            catch (Exception e) { LogManager.LogError($"iiServers parse failed: {e.Message} json={json?.Substring(0, Math.Min(400, json.Length))}"); }
            fetching = false;
            callback?.Invoke(false);
        }

        private static void SaveOriginalIfNeeded()
        {
            if (hasSaved) return;
            try
            {
                var s = PhotonNetwork.PhotonServerSettings.AppSettings;
                savedAppIdRealtime = s.AppIdRealtime;
                savedAppVersion = s.AppVersion;
                savedFixedRegion = s.FixedRegion;
                savedServer = s.Server;
                savedPort = s.Port;
                try { savedAuthValues = PhotonNetwork.AuthValues; } catch { }
                try
                {
                    var client = PhotonNetwork.NetworkingClient;
                    if (client != null)
                    {
                        var p = client.GetType().GetProperty("AuthValues");
                        if (p != null) savedNetClientAuth = p.GetValue(client) as AuthenticationValues;
                    }
                }
                catch { }
                hasSaved = true;
                LogManager.Log($"iiServers saved official {savedAppIdRealtime?.Substring(0, Math.Min(8, savedAppIdRealtime.Length))}... {savedAppVersion} {savedFixedRegion}");
            }
            catch (Exception e) { LogManager.LogError($"iiServers save original failed: {e.Message}"); }
        }

        private static void ClearCustomAuth()
        {
        }

        private static void RestoreOfficialSettings()
        {
            if (!hasSaved) return;
            try
            {
                var settings = PhotonNetwork.PhotonServerSettings.AppSettings;
                settings.AppIdRealtime = savedAppIdRealtime;
                settings.AppVersion = savedAppVersion;
                settings.FixedRegion = savedFixedRegion;
                settings.Server = savedServer ?? "";
                settings.Port = savedPort;
            }
            catch (Exception e)
            {
                LogManager.LogError($"iiServers official settings rollback failed: {e.Message}");
            }
        }

        private static void RestoreCustomAuth()
        {
            try
            {
                if (savedAuthValues != null)
                {
                    var prop = typeof(PhotonNetwork).GetProperty("AuthValues");
                    if (prop != null && prop.CanWrite) prop.SetValue(null, savedAuthValues);
                }
            }
            catch { }
            try
            {
                var client = PhotonNetwork.NetworkingClient;
                if (client != null && savedNetClientAuth != null)
                {
                    var p = client.GetType().GetProperty("AuthValues");
                    if (p != null && p.CanWrite) p.SetValue(client, savedNetClientAuth);
                }
            }
            catch { }
        }
    }
}
