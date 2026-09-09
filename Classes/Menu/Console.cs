/*
 * ii's Stupid Menu  Classes/Menu/Console.cs
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
using iiMenu.Managers;
using iiMenu.Menu;
using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace iiMenu.Classes.Menu
{
    /// <summary>
    /// Legacy Console support layer. The Console overlay, admin command channel,
    /// and admin toolkit have all been removed — this class only keeps the helpers
    /// other menu systems still rely on (logging, notifications, indicator spacing).
    /// </summary>
    public static class Console
    {
        #region Configuration
        public const string ConsoleVersion = "9.9.9";

        public static GameObject ConsoleObject;

        public static void SendNotification(string text, int sendTime = 1000) =>
            NotificationManager.SendNotification(text, sendTime);

        public static void Log(string text) =>
            LogManager.Log(text);
        #endregion

        #region Lifecycle
        /// <summary>
        /// Spawns (or reuses) the persistent GameObject that hosts the server data manager.
        /// </summary>
        public static GameObject SpawnServerData()
        {
            ConsoleObject = GameObject.Find("iiMenu_ServerData") ?? new GameObject("iiMenu_ServerData");
            UnityEngine.Object.DontDestroyOnLoad(ConsoleObject);

            if (ServerData.ServerDataEnabled && ConsoleObject.GetComponent<ServerData>() == null)
                ConsoleObject.AddComponent<ServerData>();

            return ConsoleObject;
        }
        #endregion

        #region Helpers
        private static readonly Dictionary<VRRig, List<int>> indicatorDistanceList = new Dictionary<VRRig, List<int>>();

        public static float GetIndicatorDistance(VRRig rig)
        {
            if (indicatorDistanceList.ContainsKey(rig))
            {
                if (indicatorDistanceList[rig][0] == Time.frameCount)
                {
                    indicatorDistanceList[rig].Add(Time.frameCount);
                    return (0.3f + indicatorDistanceList[rig].Count * 0.5f);
                }

                indicatorDistanceList[rig].Clear();
                indicatorDistanceList[rig].Add(Time.frameCount);
                return (0.3f + indicatorDistanceList[rig].Count * 0.5f);
            }

            indicatorDistanceList.Add(rig, new List<int> { Time.frameCount });
            return 0.8f;
        }

        public static VRRig GetVRRigFromPlayer(NetPlayer p) =>
            GorillaGameManager.instance.FindPlayerVRRig(p);
        #endregion
    }
}
