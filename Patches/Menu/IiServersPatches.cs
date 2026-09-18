using HarmonyLib;
using GorillaNetworking;
using iiMenu.Managers;

namespace iiMenu.Patches.Menu
{
    [HarmonyPatch(typeof(PhotonNetworkController), nameof(PhotonNetworkController.AttemptToJoinPublicRoom))]
    public class BlockIiServersPublicJoinPatch
    {
        static bool Prefix()
        {
            if (IiServersManager.IsOnIiServers)
                return false;
            return true;
        }
    }

    [HarmonyPatch(typeof(PhotonNetworkController), nameof(PhotonNetworkController.AttemptToJoinPublicRoomAsync))]
    public class BlockIiServersPublicJoinAsyncPatch
    {
        static bool Prefix()
        {
            if (IiServersManager.IsOnIiServers)
                return false;
            return true;
        }
    }

    [HarmonyPatch(typeof(PhotonNetworkController), nameof(PhotonNetworkController.AttemptToJoinRankedPublicRoom))]
    public class BlockIiServersRankedJoinPatch
    {
        static bool Prefix()
        {
            if (IiServersManager.IsOnIiServers)
                return false;
            return true;
        }
    }

    [HarmonyPatch(typeof(PhotonNetworkController), nameof(PhotonNetworkController.AttemptToJoinRankedPublicRoomAsync))]
    public class BlockIiServersRankedJoinAsyncPatch
    {
        static bool Prefix()
        {
            if (IiServersManager.IsOnIiServers)
                return false;
            return true;
        }
    }
}
