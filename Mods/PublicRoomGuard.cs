/*
 * ii's Stupid Menu  Mods/PublicRoomGuard.cs
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
using iiMenu.Classes.Menu;
using iiMenu.Managers;
using iiMenu.Menu;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace iiMenu.Mods
{
    /// <summary>
    /// Automatically disables every enabled "detected" mod the moment you join a
    /// public room. Prevents the most common ban scenario: leaving a risky mod
    /// enabled and forgetting about it before joining a public lobby.
    /// </summary>
    public static class PublicRoomGuard
    {
        public static bool PublicRoomGuardEnabled;

        private static bool firing;

        public static void OnJoinRoom()
        {
            if (!PublicRoomGuardEnabled || firing || PhotonNetwork.InRoom == false || NetworkSystem.Instance.SessionIsPrivate)
                return;

            firing = true;
            CoroutineManager.instance.StartCoroutine(DisableDetectedMods());
        }

        private static IEnumerator DisableDetectedMods()
        {
            yield return null;

            try
            {
                var detectedButtons = Buttons.buttons
                    .SelectMany(category => category)
                    .Where(button => button.detected)
                    .ToArray();

                var disabled = new System.Collections.Generic.List<string>();

                foreach (var button in detectedButtons)
                {
                    if (button == null || !button.enabled)
                        continue;

                    // Disable through the standard toggle path so disableMethod
                    // hooks, UI state, and dependent fields all unwind correctly.
                    Main.Toggle(button.buttonText);
                    disabled.Add(button.overlapText ?? button.buttonText);
                }

                Settings.SavePreferences();

                if (disabled.Count > 0)
                {
                    string list = string.Join(", ", disabled.Take(5));
                    if (disabled.Count > 5)
                        list += $" (+{disabled.Count - 5} more)";

                    NotificationManager.SendNotification(
                        $"<color=grey>[</color><color=yellow>GUARD</color><color=grey>]</color> Public room detected — disabled {disabled.Count} detected mod(s): {list}");
                }
            }
            finally
            {
                firing = false;
            }
        }
    }
}
