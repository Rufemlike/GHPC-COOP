using System;
using HarmonyLib;
using UnityEngine;

namespace GHPC.Coop.Core
{
    /// <summary>
    /// Intercepts host loading a mission and broadcasts MissionLoadPacket to all connected clients.
    /// </summary>
    [HarmonyPatch(typeof(GHPC.SceneController), "LoadSceneByMission")]
    public static class SceneControllerLoadPatch
    {
        public static void Prefix(string mission)
        {
            try
            {
                Debug.Log($"[GHPC.Coop] SceneController.LoadSceneByMission called with param: {mission}");
                if (VehicleSyncManager.Instance != null && VehicleSyncManager.Instance.IsConnected)
                {
                    VehicleSyncManager.Instance.BroadcastMissionFromParam(mission);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GHPC.Coop] SceneControllerLoadPatch error: {ex.Message}");
            }
        }
    }
}
