using iiMenu.Classes.Menu;
using iiMenu.Menu;
using iiMenu.Mods;
using iiMenu.Utilities;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using Valve.Newtonsoft.Json.Linq;

namespace iiMenu.Managers
{
    public static class ExternalModsManager
    {
        public class ExternalMod
        {
            public string DisplayName;
            public string Repo;
            public string Description;
            public string ExpectedFile;
        }

        public static readonly ExternalMod[] Mods = new ExternalMod[]
        {
            new ExternalMod { DisplayName = "Utilla", Repo = "iireborn/Utilla", Description = "Backend for custom maps/cosmetics. Required by most mods.", ExpectedFile = "Utilla.dll" },
            new ExternalMod { DisplayName = "WalkSim Fixed", Repo = "iireborn/Walksim-Fixed", Description = "Fixed WalkSimulator for current build.", ExpectedFile = "WalkSimulator.dll" },
            new ExternalMod { DisplayName = "TooMuchInfo", Repo = "iireborn/TooMuchInfo", Description = "Shows player/room info.", ExpectedFile = "TooMuchInfo.dll" },
            new ExternalMod { DisplayName = "LibrePad Updated", Repo = "iireborn/LibrePad-Updated", Description = "Updated LibrePad build.", ExpectedFile = "LibrePad.dll" },
        };

        private static string PluginsFolder => FileUtilities.GetGamePath() + "/BepInEx/plugins";

        public static void EnterExternalMods()
        {
            RefreshExternalModsButtons();
            Buttons.CurrentCategoryName = "External Mods";
        }

        public static void RefreshExternalModsButtons()
        {
            int cat = Buttons.GetCategory("External Mods");
            if (cat < 0) return;

            var list = new System.Collections.Generic.List<ButtonInfo>();
            list.Add(new ButtonInfo { buttonText = "Exit External Mods", method = () => Buttons.CurrentCategoryName = "Main", isTogglable = false, toolTip = "Back to main." });
            list.Add(new ButtonInfo { buttonText = "Restart Gorilla Tag", method = () => Important.RestartGame(), isTogglable = false, toolTip = "Restarts Gorilla Tag so newly installed mods load. Required after installing." });

            foreach (ExternalMod mod in Mods)
            {
                ExternalMod captured = mod;
                string installed = IsInstalled(captured) ? "<color=green>INSTALLED</color>" : "<color=red>NOT INSTALLED</color>";
                string overlap = $"{captured.DisplayName} <color=grey>[</color>{installed}<color=grey>]</color>";
                list.Add(new ButtonInfo
                {
                    buttonText = $"Install {captured.DisplayName}",
                    overlapText = overlap,
                    method = () => DownloadLatest(captured),
                    isTogglable = false,
                    toolTip = $"{captured.Description} Repo: {captured.Repo} - Always grabs the latest release from GitHub and drops the .dll into BepInEx/plugins. Then restart."
                });
            }

            list.Add(new ButtonInfo { buttonText = "Open Plugins Folder", method = () => System.Diagnostics.Process.Start(PluginsFolder), isTogglable = false, toolTip = "Opens BepInEx/plugins in Explorer." });
            list.Add(new ButtonInfo { buttonText = "Refresh List", method = () => RefreshExternalModsButtons(), isTogglable = false, toolTip = "Refreshes installed status." });

            Buttons.buttons[cat] = list.ToArray();
        }

        public static bool IsInstalled(ExternalMod mod)
        {
            try
            {
                if (!Directory.Exists(PluginsFolder)) return false;
                string pathExact = Path.Combine(PluginsFolder, mod.ExpectedFile);
                if (File.Exists(pathExact)) return true;
                string alt = Directory.GetFiles(PluginsFolder, "*.dll", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault(f => Path.GetFileName(f).Equals(mod.ExpectedFile, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(f).IndexOf(mod.DisplayName.Replace(" ", ""), StringComparison.OrdinalIgnoreCase) >= 0);
                return alt != null;
            }
            catch { return false; }
        }

        public static void DownloadLatest(ExternalMod mod)
        {
            if (CoroutineManager.instance == null)
            {
                NotificationManager.SendNotification("<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> CoroutineManager not ready.");
                return;
            }
            CoroutineManager.instance.StartCoroutine(DownloadLatestRoutine(mod));
        }

        private static IEnumerator DownloadLatestRoutine(ExternalMod mod)
        {
            NotificationManager.SendNotification($"<color=grey>[</color><color=cyan>EXTERNAL</color><color=grey>]</color> Checking latest for {mod.DisplayName}...");

            string apiUrl = $"https://api.github.com/repos/{mod.Repo}/releases/latest";
            string json = null;

            using (UnityWebRequest req = UnityWebRequest.Get(apiUrl))
            {
                req.SetRequestHeader("User-Agent", "iis-Stupid-Menu");
                req.SetRequestHeader("Accept", "application/vnd.github+json");
                req.timeout = 15;
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    NotificationManager.SendNotification($"<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> GitHub API failed for {mod.DisplayName}: {req.error}");
                    LogManager.LogError($"ExternalMods: API {apiUrl} -> {req.error} {req.downloadHandler?.text}");
                    yield break;
                }
                json = req.downloadHandler.text;
            }

            string dllUrl = null;
            string dllName = mod.ExpectedFile;
            string tagName = null;

            try
            {
                JObject obj = JObject.Parse(json);
                tagName = (string)obj["tag_name"];
                JArray assets = obj["assets"] as JArray;
                if (assets != null && assets.Count > 0)
                {
                    foreach (JToken a in assets)
                    {
                        string url = (string)a["browser_download_url"];
                        string name = (string)a["name"];
                        if (string.IsNullOrEmpty(url)) continue;
                        bool isDll = name != null && name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
                        if (isDll)
                        {
                            if (name.Equals(mod.ExpectedFile, StringComparison.OrdinalIgnoreCase))
                            {
                                dllUrl = url;
                                dllName = name;
                                break;
                            }
                            if (dllUrl == null)
                            {
                                dllUrl = url;
                                dllName = name;
                            }
                        }
                    }
                    if (dllUrl == null)
                    {
                        foreach (JToken a in assets)
                        {
                            string url = (string)a["browser_download_url"];
                            string name = (string)a["name"];
                            if (url != null && name != null && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                            {
                                NotificationManager.SendNotification($"<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> {mod.DisplayName} latest release has no .dll, only .zip. Extract it manually.");
                                yield break;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                NotificationManager.SendNotification($"<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> Failed to parse GitHub response for {mod.DisplayName}.");
                LogManager.LogError($"ExternalMods parse failed: {e}");
                yield break;
            }

            if (string.IsNullOrEmpty(dllUrl))
            {
                NotificationManager.SendNotification($"<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> No .dll asset found for {mod.DisplayName} {(tagName != null ? $"({tagName})" : "")}.");
                yield break;
            }

            NotificationManager.SendNotification($"<color=grey>[</color><color=cyan>EXTERNAL</color><color=grey>]</color> Downloading {dllName} {(tagName ?? "")}...");

            byte[] data = null;
            using (UnityWebRequest dl = UnityWebRequest.Get(dllUrl))
            {
                dl.SetRequestHeader("User-Agent", "iis-Stupid-Menu");
                dl.downloadHandler = new DownloadHandlerBuffer();
                dl.timeout = 30;
                yield return dl.SendWebRequest();

                if (dl.result != UnityWebRequest.Result.Success)
                {
                    NotificationManager.SendNotification($"<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> Download failed: {dl.error}");
                    LogManager.LogError($"ExternalMods dl {dllUrl} -> {dl.error}");
                    yield break;
                }
                data = dl.downloadHandler.data;
            }

            if (data == null || data.Length < 1024)
            {
                NotificationManager.SendNotification($"<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> Downloaded file too small/corrupt.");
                yield break;
            }

            try
            {
                if (!Directory.Exists(PluginsFolder))
                    Directory.CreateDirectory(PluginsFolder);

                string dest = Path.Combine(PluginsFolder, dllName);
                if (File.Exists(dest))
                {
                    try { File.Delete(dest); } catch { }
                }
                File.WriteAllBytes(dest, data);
                NotificationManager.SendNotification($"<color=grey>[</color><color=green>SUCCESS</color><color=grey>]</color> Installed {mod.DisplayName} {tagName ?? ""} -> {dllName}. Restart Gorilla Tag to load.", 7000);
                LogManager.Log($"ExternalMods installed {mod.DisplayName} {tagName} -> {dest} ({data.Length} bytes)");
                RefreshExternalModsButtons();
            }
            catch (Exception e)
            {
                NotificationManager.SendNotification($"<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> Could not save {dllName}: {e.Message}");
                LogManager.LogError($"ExternalMods save failed: {e}");
            }
        }
    }
}
