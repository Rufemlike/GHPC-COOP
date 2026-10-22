using System;
using System.IO;
using GHPC.Coop.Network;
using GHPC.Coop.UI;
using GHPC.UI;
using UnityEngine;

namespace GHPC.Coop.Core
{
    /// <summary>
    /// Native Game Entry Point directly called by patched Assembly-CSharp.dll
    /// Runs 100% natively inside the game engine without relying on BepInEx lifecycle.
    /// </summary>
    public static class Bootstrapper
    {
        private static bool _initialized = false;
        public static CoopNetwork Network { get; private set; }
        public static SteamP2PTransport SteamTransport { get; private set; }
        public static VehicleSyncManager SyncManager { get; private set; }
        public static CoopArrowMenu ArrowMenu { get; private set; }

        private static GameObject _nativeHostObject;

        public static void EnsureSubsystems()
        {
            if (_initialized) return;
            _initialized = true;

            Debug.Log("[GHPC.Coop] =========================================");
            Debug.Log("[GHPC.Coop] >>> BOOTSTRAPPER: Initializing Native Game Co-op Integration! <<<");

            try
            {
                // Create a native persistent GameObject
                _nativeHostObject = new GameObject("GHPC_Coop_NativeHost");
                _nativeHostObject.hideFlags = HideFlags.None;
                UnityEngine.Object.DontDestroyOnLoad(_nativeHostObject);

                // 1. UDP Network
                Network = new CoopNetwork();
                Debug.Log("[GHPC.Coop] Native UDP Network initialized.");

                // 2. Steam Transport
                SteamTransport = new SteamP2PTransport();
                SteamTransport.Initialize();
                Debug.Log($"[GHPC.Coop] Native Steam Transport: {SteamTransport.StatusMessage}");

                // 3. Vehicle Sync Manager
                SyncManager = _nativeHostObject.AddComponent<VehicleSyncManager>();
                SyncManager.Network = Network;
                SyncManager.SteamTransport = SteamTransport;

                if (Network != null)
                {
                    Network.OnVehicleSyncReceived += SyncManager.HandleRemoteSync;
                    Network.OnFireWeaponReceived += SyncManager.HandleRemoteFire;
                }

                if (SteamTransport != null)
                {
                    SteamTransport.OnVehicleSyncReceived += SyncManager.HandleRemoteSync;
                    SteamTransport.OnFireWeaponReceived += SyncManager.HandleRemoteFire;
                }

                // 4. Menu / UI controller
                ArrowMenu = _nativeHostObject.AddComponent<CoopArrowMenu>();
                ArrowMenu.Network = Network;
                ArrowMenu.SteamTransport = SteamTransport;
                ArrowMenu.IsOpen = true; // Open by default!

                // Write confirmation file
                try
                {
                    string path = Path.Combine(Path.GetDirectoryName(Application.dataPath), "coop_native_active.txt");
                    File.WriteAllText(path, $"Native Bootstrapper active at {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
                }
                catch { }

                Debug.Log("[GHPC.Coop] >>> Native Co-op Subsystems successfully initialized! <<<");
                Debug.Log("[GHPC.Coop] =========================================");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GHPC.Coop] Bootstrapper error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public static void OnMenuStart(BuildMenuController controller)
        {
            Debug.Log("[GHPC.Coop] >>> Native BuildMenuController.Start() invoked! <<<");
            EnsureSubsystems();
            // Note: MenuButtonInjector is intentionally removed so the native menu stays 100% vanilla and clean!
        }

        public static void OnMenuGUI(BuildMenuController controller)
        {
            EnsureSubsystems();
            // CoopArrowMenu is a MonoBehaviour and renders its own OnGUI automatically.
            // Leaving this method empty prevents duplicate OnGUI rendering and frame drops.
        }

        public static void OnSceneControllerStart(SceneController controller)
        {
            Debug.Log("[GHPC.Coop] >>> Native SceneController.Start() invoked! <<<");
            EnsureSubsystems();
        }

        public static void OnSceneControllerGUI(SceneController controller)
        {
            EnsureSubsystems();
            // CoopArrowMenu renders its own OnGUI automatically.
            // Leaving this empty avoids FindObjectOfType calls that destroy in-game FPS.
        }
    }
}
