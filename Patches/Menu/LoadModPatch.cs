using GorillaTagScripts.VirtualStumpCustomMaps;
using HarmonyLib;
using iiMenu.Managers;
using iiMenu.Mods.CustomMaps;
using Modio.Mods;

namespace iiMenu.Patches.Menu
{
    [HarmonyPatch(typeof(CustomMapManager), nameof(CustomMapManager.LoadMap))]
    public static class LoadModPatch
    {
        public static void Prefix(ModId modId)
        {
            try
            {
                Manager.UpdateCustomMapsTab(modId);
            }
            catch (System.Exception exception)
            {
                LogManager.LogError($"Custom-map menu update before load failed: {exception.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(CustomMapManager), nameof(CustomMapManager.UnloadMap))]
    public static class UnloadModPatch
    {
        public static void Prefix(bool returnToSinglePlayerIfInPublic)
        {
            try
            {
                Manager.UpdateCustomMapsTab();
            }
            catch (System.Exception exception)
            {
                LogManager.LogError($"Custom-map menu update before unload failed: {exception.Message}");
            }
        }
    }
}
