/*
 * ii's Stupid Menu  Mods/Experimental.cs
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

using ExitGames.Client.Photon;
using GorillaLocomotion;
using GorillaNetworking;
using GorillaTagScripts.VirtualStumpCustomMaps;
using iiMenu.Classes.Menu;
using iiMenu.Extensions;
using iiMenu.Managers;
using iiMenu.Menu;
using iiMenu.Patches.Menu;
using iiMenu.Utilities;
using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using static iiMenu.Menu.Main;
using static iiMenu.Utilities.RandomUtilities;
using static iiMenu.Utilities.RigUtilities;
using Console = iiMenu.Classes.Menu.Console;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace iiMenu.Mods
{
    public static class Experimental
    {
        public static void FixDuplicateButtons()
        {
            int duplicateButtons = 0;
            List<string> previousNames = new List<string>();
            foreach (ButtonInfo[] buttonn in Buttons.buttons)
            {
                foreach (ButtonInfo button in buttonn)
                {
                    if (previousNames.Contains(button.buttonText))
                    {
                        string buttonText = button.overlapText ?? button.buttonText;
                        button.overlapText = buttonText;
                        button.buttonText += "X";
                        duplicateButtons++;
                    }
                    previousNames.Add(button.buttonText);
                }
            }
            NotificationManager.SendNotification("<color=grey>[</color><color=green>SUCCESS</color><color=grey>]</color> Successfully fixed " + duplicateButtons + " broken buttons.");
        }

        private static readonly Dictionary<Renderer, Material> oldMats = new Dictionary<Renderer, Material>();
        public static void BetterFPSBoost()
        {
            foreach (Renderer v in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                try
                {
                    if (v.material.shader.name == "GorillaTag/UberShader")
                    {
                        oldMats.Add(v, v.material);
                        Material replacement = new Material(Shader.Find("GorillaTag/UberShader"))
                        {
                            color = v.material.color
                        };
                        v.material = replacement;
                    }
                } catch (Exception exception) { LogManager.LogError(string.Format("mat error {1} - {0}", exception.Message, exception.StackTrace)); }
            }
        }

        public static void DisableBetterFPSBoost()
        {
            foreach (KeyValuePair<Renderer, Material> v in oldMats)
                v.Key.material = v.Value;
        }

        public static void OnlySerializeNecessary()
        {
            SerializePatch.OverrideSerialization = () =>
            {
                SendSerialize(GorillaTagger.Instance.myVRRig.GetView);
                SendSerialize(GorillaTagger.Instance.myVRRig.reliableView);
                return false;
            };
        }

        public static void DumpSoundData()
        {
            string text = "Handtap Sound Data\n(from GorillaLocomotion.GTPlayer.Instance.materialData)";
            int i = 0;
            foreach (GTPlayer.MaterialData oneshot in GTPlayer.Instance.materialData)
            {
                try
                {
                    text += "\n====================================\n";
                    text += i + " ; " + oneshot.matName + " ; " + oneshot.slidePercent + "% ; " + (oneshot.audio == null ? "none" : oneshot.audio.name);
                }
                catch { LogManager.Log("Failed to log sound"); }
                i++;
            }
            text += "\n====================================\n";
            text += "Text file generated with ii's Stupid Menu";
            string fileName = $"{PluginInfo.BaseDirectory}/SoundData.txt";

            File.WriteAllText(fileName, text);

            string filePath = FileUtilities.GetGamePath() + "/" + fileName;
            Process.Start(filePath);
        }

        public static void DumpCosmeticData()
        {
            string text = "Cosmetic Data\n(from CosmeticsController.instance.allCosmetics)";
            foreach (CosmeticsController.CosmeticItem hat in CosmeticsController.instance.allCosmetics)
            {
                try
                {
                    text += "\n====================================\n";
                    text += hat.itemName + " ; " + hat.displayName + " (override " + hat.overrideDisplayName + ") ; " + hat.cost + "SR ; canTryOn = " + hat.canTryOn;
                }
                catch { LogManager.Log("Failed to log hat"); }
            }
            text += "\n====================================\n";
            text += "Text file generated with ii's Stupid Menu";
            string fileName = $"{PluginInfo.BaseDirectory}/CosmeticData.txt";

            File.WriteAllText(fileName, text);

            string filePath = FileUtilities.GetGamePath() + "/" + fileName;
            Process.Start(filePath);
        }

        public static void DecryptableCosmeticData()
        {
            string text = "";
            foreach (CosmeticsController.CosmeticItem hat in CosmeticsController.instance.allCosmetics)
            {
                try
                {
                    text += hat.itemName + ";;" + hat.overrideDisplayName + ";;" + hat.cost + "\n";
                }
                catch { LogManager.Log("Failed to log hat"); }
            }
            string fileName = $"{PluginInfo.BaseDirectory}/DecryptableCosmeticData.txt";

            File.WriteAllText(fileName, text);

            string filePath = FileUtilities.GetGamePath() + "/" + fileName;
            Process.Start(filePath);
        }

        public static void DumpRPCData()
        {
            string text = "RPC Data\n(from PhotonNetwork.PhotonServerSettings.RpcList)";
            int i = 0;
            foreach (string name in PhotonNetwork.PhotonServerSettings.RpcList)
            {
                try
                {
                    text += "\n====================================\n";
                    text += i + " ; " + name;
                }
                catch { LogManager.Log("Failed to log RPC"); }
                i++;
            }
            text += "\n====================================\n";
            text += "Text file generated with ii's Stupid Menu";
            string fileName = $"{PluginInfo.BaseDirectory}/RPCData.txt";

            File.WriteAllText(fileName, text);

            string filePath = FileUtilities.GetGamePath() + "/" + fileName;
            Process.Start(filePath);
        }

        public static void BlankPage()
        {
            Buttons.buttons[Buttons.GetCategory("Temporary Category")] = Array.Empty<ButtonInfo>();
            Buttons.CurrentCategoryName = "Temporary Category";
        }

        public static void CopyCustomGamemodeScript()
        {
            NotificationManager.SendNotification("<color=grey>[</color><color=green>SUCCESS</color><color=grey>]</color> Copied map script to your clipboard.", 5000);
            GUIUtility.systemCopyBuffer = CustomGameMode.LuaScript;
        }

        public static void CopyCustomMapID()
        {
            string id = CustomMapManager.currentRoomMapModId._id.ToString();
            NotificationManager.SendNotification("<color=grey>[</color><color=green>SUCCESS</color><color=grey>]</color> " + id, 5000);
            GUIUtility.systemCopyBuffer = id;
        }
        
        public static int restartIndex;
        public static float restartDelay;
        public static Vector3 restartPosition;
        public static string restartRoom;
        public static void SafeRestartGame()
        {
            string restartDataPath = $"{PluginInfo.BaseDirectory}/RestartData.txt";
            switch (restartIndex)
            {
                case 0:
                    if (File.Exists(restartDataPath))
                    {
                        string data = File.ReadAllText(restartDataPath);
                        restartRoom = data.Split(";")[0];
                        List<string> positionData = data.Split(";")[1].Split(",").ToList();
                        restartPosition = new Vector3(float.Parse(positionData[0]), float.Parse(positionData[1]), float.Parse(positionData[2]));
                        restartIndex = 3;
                    }
                    else
                    {
                        restartRoom = PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom.Name : "";
                        restartPosition = GTPlayer.Instance.transform.position;
                        restartIndex = 1;
                    }
                    restartDelay = Time.time + 6f;
                    break;
                case 1:
                    Settings.SavePreferences();
                    File.WriteAllText(restartDataPath, restartRoom + $";{restartPosition.x},{restartPosition.y},{restartPosition.z}");
                    restartIndex = 2;
                    break;
                case 2:
                    if (File.Exists(restartDataPath) && Time.time > restartDelay)
                    {
                        Important.RestartGame();
                        restartIndex = 4;
                    }
                    break;
                case 3:
                    if (!PhotonNetwork.InRoom && restartRoom != "")
                    {
                        if (Important.queueCoroutine == null && Time.time > restartDelay)
                            Important.QueueRoom(restartRoom);
                    }
                    else
                    {
                        TeleportPlayer(restartPosition);
                        File.Delete(restartDataPath);
                        NotificationManager.SendNotification("<color=grey>[</color><color=green>SUCCESS</color><color=grey>]</color> Restarted game with information.");
                        restartIndex = 4;
                        Buttons.GetIndex("Safe Restart Game").enabled = false;
                        Settings.SavePreferences();
                    }
                    break;
            }
        }

    }
}
