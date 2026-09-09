/*
 * ii's Stupid Menu  Patches/Menu/WalkSimCursorPatch.cs
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

using HarmonyLib;
using iiMenu.Managers;
using iiMenu.Menu;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.XR;

namespace iiMenu.Patches.Menu
{
    /// <summary>
    /// WalkSimulator's HeadDriver locks and hides the cursor whenever its rig
    /// components enable (which happens constantly), defeating the desktop menu's
    /// free cursor. While the menu is open without a headset, force its lock off.
    /// </summary>
    public static class WalkSimCursorPatch
    {
        /// <summary>
        /// HeadDriver lives in WalkSimulator.Rigging in WalkSimulator 2.x,
        /// but older forks of the mod used WalkSim.WalkSim.Rigging. Both are
        /// tried so the patch works regardless of which fork is installed.
        /// </summary>
        private static readonly string[] HeadDriverTypeNames =
        {
            "WalkSimulator.Rigging.HeadDriver",
            "WalkSim.WalkSim.Rigging.HeadDriver"
        };

        private static bool installed;
        private static int installAttempts;
        private const int MaxInstallAttempts = 5;
        private const float RetryInterval = 5f;
        private static float nextRetryTime;

        /// <summary>
        /// Attempts to patch WalkSimulator's cursor lock immediately.
        /// Safe to call repeatedly — the work only happens once (or on retries
        /// via <see cref="EnsureInstalled"/> if WalkSimulator wasn't loaded yet).
        /// </summary>
        public static void Install() => EnsureInstalled(force: true);

        /// <summary>
        /// Cheap per-frame retry hook. Resolves to a single field check once the
        /// patch is installed (or attempts are exhausted), so it costs nothing
        /// in the steady state.
        /// </summary>
        public static void EnsureInstalled(bool force = false)
        {
            if (installed)
                return;

            if (!force)
            {
                if (installAttempts >= MaxInstallAttempts || Time.time < nextRetryTime)
                    return;
            }

            installAttempts++;
            nextRetryTime = Time.time + RetryInterval;

            try
            {
                // Resolve silently instead of via AccessTools.TypeByName, which
                // logs a HarmonyX warning for every name it fails to find.
                Type headDriver = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(TryGetTypes)
                    .FirstOrDefault(type => HeadDriverTypeNames.Contains(type.FullName));

                if (headDriver == null)
                    return; // WalkSimulator isn't loaded (yet) — retried later

                PatchHandler.ApplyPatch(headDriver, "set_LockCursor",
                    prefix: AccessTools.Method(typeof(WalkSimCursorPatch), nameof(LockCursorPrefix)));

                installed = true;
                LogManager.Log("WalkSimulator cursor lock neutralized while the desktop menu is open");
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to patch WalkSimulator cursor lock: {ex.Message}");
            }
        }

        private static Type[] TryGetTypes(System.Reflection.Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch { return Array.Empty<Type>(); }
        }

        private static void LockCursorPrefix(ref bool value)
        {
            // While the keyboard-opened menu owns the cursor, never let it be locked or hidden
            if (value && Main.MenuWantsCursor)
                value = false;
        }
    }
}
