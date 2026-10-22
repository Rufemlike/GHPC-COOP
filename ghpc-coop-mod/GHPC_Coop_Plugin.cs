using System;
using System.Runtime.InteropServices;
using BepInEx;
using HarmonyLib;
using GHPC.Coop.Core;
using GHPC.Coop.Network;
using GHPC.Coop.UI;
using UnityEngine;

namespace GHPC.Coop
{
    [BepInPlugin("com.coopanything.ghpc", "GHPC Co-op Mod", "0.2.3")]
    public class GHPC_Coop_Plugin : BaseUnityPlugin
    {
        private Harmony _harmony;
        private CoopNetwork _network;
        private SteamP2PTransport _steamTransport;
        private VehicleSyncManager _syncManager;
        private CoopArrowMenu _arrowMenu;
        private GameObject _guiGO;

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private bool _wasRightDown = false;
        private bool _wasLeftDown = false;
        private bool _wasF9Down = false;

        private void Awake()
        {
            Logger.LogInfo("[GHPC.Coop] =========================================");
            Logger.LogInfo("[GHPC.Coop] Initializing GHPC Co-op Mod v0.2.3...");

            // 1. Harmony Patches
            try
            {
                _harmony = new Harmony("com.coopanything.ghpc");
                _harmony.PatchAll();
                Logger.LogInfo("[GHPC.Coop] Harmony patches applied successfully.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[GHPC.Coop] Harmony patch error: {ex.Message}");
            }

            // 2. Direct UDP Network
            try
            {
                _network = new CoopNetwork();
                Logger.LogInfo("[GHPC.Coop] Direct UDP Network initialized.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[GHPC.Coop] UDP Network error: {ex.Message}");
            }

            // 3. Steam Transport
            try
            {
                _steamTransport = new SteamP2PTransport();
                _steamTransport.Initialize();
                Logger.LogInfo($"[GHPC.Coop] Steam Transport: {_steamTransport.StatusMessage}");
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[GHPC.Coop] Steam Transport skipped: {ex.Message}");
            }

            // 4. Vehicle Sync Manager
            try
            {
                _syncManager = gameObject.AddComponent<VehicleSyncManager>();
                _syncManager.Network = _network;
                _syncManager.SteamTransport = _steamTransport;

                if (_network != null)
                {
                    _network.OnVehicleSyncReceived += _syncManager.HandleRemoteSync;
                    _network.OnFireWeaponReceived += _syncManager.HandleRemoteFire;
                }

                if (_steamTransport != null)
                {
                    _steamTransport.OnVehicleSyncReceived += _syncManager.HandleRemoteSync;
                    _steamTransport.OnFireWeaponReceived += _syncManager.HandleRemoteFire;
                }

                Logger.LogInfo("[GHPC.Coop] VehicleSyncManager registered.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[GHPC.Coop] VehicleSyncManager error: {ex.Message}");
            }

            // 5. Connect to existing native UI or create fallback GUI
            EnsureGUIObject();

            Logger.LogInfo("[GHPC.Coop] Subsystems initialized!");
            Logger.LogInfo("[GHPC.Coop] =========================================");
        }

        private void EnsureGUIObject()
        {
            if (CoopArrowMenu.Instance != null)
            {
                _arrowMenu = CoopArrowMenu.Instance;
                return;
            }

            if (_guiGO == null)
            {
                _guiGO = new GameObject("GHPC_Coop_GUI");
                _guiGO.hideFlags = HideFlags.None;
                DontDestroyOnLoad(_guiGO);

                _arrowMenu = _guiGO.AddComponent<CoopArrowMenu>();
                _arrowMenu.Network = _network;
                _arrowMenu.SteamTransport = _steamTransport;
                _arrowMenu.IsOpen = true;

                Logger.LogInfo("[GHPC.Coop] >>> Dedicated GHPC_Coop_GUI created with HideFlags.None (IsOpen=TRUE)! <<<");
            }
        }

        private void Update()
        {
            try
            {
                // Ensure GUI object is always alive and never collected on scene load
                EnsureGUIObject();

                _network?.Update();
                _steamTransport?.Update();

                // Hardware keyboard polling via Windows API
                bool isRightDown = (GetAsyncKeyState(0x27) & 0x8000) != 0;
                bool isLeftDown  = (GetAsyncKeyState(0x25) & 0x8000) != 0;
                bool isF9Down    = (GetAsyncKeyState(0x78) & 0x8000) != 0;

                if (_arrowMenu != null)
                {
                    if (isRightDown && !_wasRightDown)
                    {
                        Logger.LogInfo("[GHPC.Coop] Right Arrow -> Open Co-op menu");
                        _arrowMenu.IsOpen = true;
                    }
                    else if (isLeftDown && !_wasLeftDown)
                    {
                        Logger.LogInfo("[GHPC.Coop] Left Arrow -> Collapse Co-op menu");
                        _arrowMenu.IsOpen = false;
                    }
                    else if (isF9Down && !_wasF9Down)
                    {
                        _arrowMenu.IsOpen = !_arrowMenu.IsOpen;
                        Logger.LogInfo($"[GHPC.Coop] F9 -> Co-op menu is now {(_arrowMenu.IsOpen ? "OPEN" : "COLLAPSED")}");
                    }
                }

                _wasRightDown = isRightDown;
                _wasLeftDown = isLeftDown;
                _wasF9Down = isF9Down;
            }
            catch { }
        }

        private void OnDestroy()
        {
            _network?.Dispose();
            _steamTransport?.Dispose();
            _harmony?.UnpatchSelf();
            if (_guiGO != null) Destroy(_guiGO);
        }
    }
}
