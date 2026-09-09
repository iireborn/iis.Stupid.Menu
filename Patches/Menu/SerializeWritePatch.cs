/*
 * ii's Stupid Menu  Patches/Menu/SerializeWritePatch.cs
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
using iiMenu.Extensions;
using Photon.Pun;
using UnityEngine;

namespace iiMenu.Patches.Menu
{
    /// <summary>
    /// Lets outgoing serialization packets report a spoofed rig position without
    /// the rig's transform ever visibly moving.
    ///
    /// The hook point matters: the rig's position is captured into the outgoing
    /// InputStruct inside PhotonNetwork.OnSerializeWrite (before SerializeWriteShared
    /// ever runs), so swapping the transform inside SerializeWriteShared is too late —
    /// the packet would already contain the real position. Patching OnSerializeWrite
    /// swaps the transform before anything downstream reads it.
    /// </summary>
    [HarmonyPatch(typeof(PhotonNetwork), nameof(PhotonNetwork.OnSerializeWrite))]
    public static class SerializeWritePatch
    {
        /// <summary>
        /// When non-null, the local rig serializes this position instead of its real one.
        /// Only set this inside a try/finally that clears it, and only for the span of a
        /// serialization call — it swaps the transform for the duration of the write.
        /// </summary>
        public static Vector3? positionOverride;

        public static void Prefix(PhotonView view, out Vector3? __state)
        {
            __state = null;

            if (positionOverride == null || VRRig.LocalRig == null)
                return;

            // Only the local network rig's view carries the rig position
            if (GorillaTagger.Instance == null || GorillaTagger.Instance.myVRRig == null || view != GorillaTagger.Instance.myVRRig.GetView)
                return;

            __state = VRRig.LocalRig.transform.position;
            VRRig.LocalRig.transform.position = positionOverride.Value;
        }

        public static void Finalizer(ref Vector3? __state)
        {
            if (__state == null || VRRig.LocalRig == null)
                return;

            VRRig.LocalRig.transform.position = __state.Value;
        }
    }
}
