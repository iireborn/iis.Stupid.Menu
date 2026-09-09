/*
 * ii's Stupid Menu  Patches/Safety/IncrementRPCPatches.cs
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
using Photon.Pun;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace iiMenu.Patches.Safety
{
    public class IncrementRPCPatches
    {
        [HarmonyPatch(typeof(VRRig), nameof(VRRig.IncrementRPC), typeof(PhotonMessageInfoWrapped), typeof(string))]
        public class NoIncrementRPC
        {
            private static bool Prefix(PhotonMessageInfoWrapped info, string sourceCall) =>
                false;
        }

        [HarmonyPatch(typeof(MonkeAgent), nameof(MonkeAgent.IncrementRPCCall), typeof(PhotonMessageInfo), typeof(string))]
        public class NoIncrementRPCCall
        {
            private static bool Prefix(PhotonMessageInfo info, string callingMethod = "") =>
                false;
        }

        [HarmonyPatch(typeof(MonkeAgent), nameof(MonkeAgent.IncrementRPCCallLocal))]
        public class NoIncrementRPCCallLocal
        {
            private static bool Prefix(PhotonMessageInfoWrapped infoWrapped, string rpcFunction) =>
                false;
        }

        // Belt-and-suspenders: MonkeAgent.IncrementRPCTracker is the new per-sender RPC
        // rate limiter that feeds the "too many rpc calls!" SendReport. The three
        // overloads use by-ref parameters, which attribute patching can't express,
        // so TargetMethods resolves them. Always returns true — RPCs still flow,
        // the counter just never increments. Mirrors the existing tracker patches.
        [HarmonyPatch]
        public class NoIncrementRPCTracker
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                // The overloads are non-public — plain GetMethods() misses them,
                // and an empty result makes Harmony throw "Undefined target method".
                var trackerMethods = typeof(MonkeAgent).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                    .Where(m => m.Name == nameof(MonkeAgent.IncrementRPCTracker) && m.GetParameters().Length == 3);

                foreach (var method in trackerMethods)
                    yield return method;
            }

            private static bool Prefix(ref bool __result)
            {
                __result = true;
                return false;
            }
        }
    }
}
