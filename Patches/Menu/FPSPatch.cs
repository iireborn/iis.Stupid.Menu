/*
 * ii's Stupid Menu  Patches/Menu/FPSPatch.cs
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

﻿using HarmonyLib;

namespace iiMenu.Patches.Menu
{
    // VRRig.PackCompetitiveData no longer carries FPS data as of the latest game update.
    // Player stats now serialize through GTPlayerStats.DelayedUpdate instead.
    [HarmonyPatch(typeof(GTPlayerStats), nameof(GTPlayerStats.DelayedUpdate))]
    public class FPSPatch
    {
        public static bool enabled;
        public static int spoofFPSValue;

        public static bool Prefix()
        {
            if (!enabled)
                return true;

            GTPlayerStats.FPS = (short)spoofFPSValue;
            return false;
        }
    }
}
