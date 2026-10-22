using System;
using HarmonyLib;
using HeathenEngineering.SteamworksIntegration;
using HeathenEngineering.SteamworksIntegration.API;
using Steamworks;
using UnityEngine;

namespace GHPC.Coop.Core
{
    [HarmonyPatch(typeof(SteamSettings), "Initialize")]
    public static class SteamSettingsInitPatch
    {
        [HarmonyPrefix]
        public static void Prefix(SteamSettings __instance)
        {
            if (__instance != null)
            {
                __instance.applicationId = new AppId_t(480);
                Debug.Log("[GHPC.Coop] Intercepted SteamSettings: forced AppId to 480 (Spacewar)");
            }
        }
    }

    [HarmonyPatch(typeof(App.Client), "Initialize", new Type[] { typeof(AppData), typeof(InventorySettings), typeof(LeaderboardObject[]), typeof(InputAction[]) })]
    public static class AppClientInitPatch
    {
        [HarmonyPrefix]
        public static void Prefix(ref AppData appId)
        {
            appId = (AppData)new AppId_t(480);
            Debug.Log("[GHPC.Coop] Intercepted App.Client.Initialize: forced AppId to 480 (Spacewar)");
        }
    }

    [HarmonyPatch(typeof(Friends.Client), "GetFriendPersonaName", new Type[] { typeof(UserData) })]
    public static class FriendsGetFriendPersonaNamePatch
    {
        [HarmonyPrefix]
        public static bool Prefix(UserData userId, ref string __result)
        {
            try
            {
                string name = SteamFriends.GetFriendPersonaName((CSteamID)userId);
                if (!string.IsNullOrEmpty(name))
                {
                    __result = name;
                    return false;
                }
            }
            catch { }
            __result = "Player";
            return false;
        }
    }
}
