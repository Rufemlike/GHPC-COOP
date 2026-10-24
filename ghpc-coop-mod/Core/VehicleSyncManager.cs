using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GHPC.Coop.Network;
using GHPC.Player;
using GHPC.Weapons;
using UnityEngine;

namespace GHPC.Coop.Core
{
    public class VehicleSyncManager : MonoBehaviour
    {
        public static VehicleSyncManager Instance { get; private set; }

        public CoopNetwork Network { get; set; }
        public SteamP2PTransport SteamTransport { get; set; }

        public GHPC.Unit LocalPlayerUnit { get; set; }

        public bool IsConnected => (Network != null && Network.IsConnected) || (SteamTransport != null && SteamTransport.IsConnected);
        public bool UseSteam => SteamTransport != null && SteamTransport.IsConnected;

        private readonly Dictionary<string, GHPC.Unit> _unitCache = new Dictionary<string, GHPC.Unit>();
        private readonly Dictionary<string, RemoteVehicleState> _remoteStates = new Dictionary<string, RemoteVehicleState>();

        private float _syncTimer;
        private const float SyncInterval = 0.05f; // 20 Hz

        private class RemoteVehicleState
        {
            public Vector3 TargetPosition;
            public Quaternion TargetRotation;
            public Vector3 Velocity;
            public float TurretYaw;
            public float GunPitch;
            public float LastUpdateTime;
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void ScanUnits()
        {
            _unitCache.Clear();
            var units = FindObjectsOfType<GHPC.Unit>();
            foreach (var unit in units)
            {
                if (unit == null) continue;
                string id = GetUnitIdentifier(unit);
                _unitCache[id] = unit;
            }
        }

        public string GetUnitIdentifier(GHPC.Unit unit)
        {
            if (unit == null) return "";
            if (!string.IsNullOrEmpty(unit.UniqueName)) return unit.UniqueName;
            return unit.name;
        }

        public List<GHPC.Unit> GetFriendlyUnits()
        {
            ScanUnits();
            var list = new List<GHPC.Unit>();
            foreach (var kvp in _unitCache)
            {
                var unit = kvp.Value;
                if (unit != null && unit.Allegiance == GHPC.Faction.Blue) // Blue = NATO / Friendly
                {
                    list.Add(unit);
                }
            }
            return list;
        }

        public bool IsUnitPlayerControlled(GHPC.Unit unit)
        {
            if (unit == null) return false;
            if (unit == LocalPlayerUnit) return true;

            string id = GetUnitIdentifier(unit);
            if (!string.IsNullOrEmpty(id) && _remoteStates.TryGetValue(id, out var state))
            {
                return (UnityEngine.Time.time - state.LastUpdateTime) < 10f;
            }
            return false;
        }

        public void SendPacket(byte[] data)
        {
            if (UseSteam)
            {
                SteamTransport.SendPacket(data);
            }
            else if (Network != null && Network.IsConnected)
            {
                Network.SendPacket(data);
            }
        }

        private void Update()
        {
            // Always pump network callbacks regardless of connection state
            try
            {
                Network?.Update();
                SteamTransport?.Update();
                AutoloaderStabilizer.Update();
            }
            catch { }

            if (!IsConnected) return;

            // Find current player unit if not explicitly assigned
            if (LocalPlayerUnit == null && PlayerInput.Instance != null)
            {
                LocalPlayerUnit = PlayerInput.Instance.CurrentPlayerUnit;
            }

            // Send local vehicle sync at 20 Hz
            _syncTimer += Time.deltaTime;
            if (_syncTimer >= SyncInterval && LocalPlayerUnit != null)
            {
                _syncTimer = 0f;
                SendLocalVehicleSync();
            }

            // Interpolate remote vehicles
            InterpolateRemoteVehicles();
        }

        private void SendLocalVehicleSync()
        {
            var packet = new VehicleSyncPacket
            {
                UnitId = GetUnitIdentifier(LocalPlayerUnit),
                Position = LocalPlayerUnit.RootTransform.position,
                Rotation = LocalPlayerUnit.RootTransform.rotation,
                Velocity = LocalPlayerUnit.Velocity,
                TurretYaw = 0f,
                GunPitch = 0f
            };

            // Read turret & gun platform angles if available
            var platforms = LocalPlayerUnit.AimablePlatforms;
            if (platforms != null && platforms.Length > 0)
            {
                if (platforms[0] != null)
                    packet.TurretYaw = platforms[0].LocalRotation.eulerAngles.y;

                if (platforms.Length > 1 && platforms[1] != null)
                    packet.GunPitch = platforms[1].LocalRotation.eulerAngles.x;
            }

            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                packet.Serialize(writer);
                SendPacket(ms.ToArray());
            }
        }

        public void HandleRemoteSync(VehicleSyncPacket packet)
        {
            if (string.IsNullOrEmpty(packet.UnitId)) return;

            // Don't apply sync to our own locally controlled vehicle
            if (LocalPlayerUnit != null && GetUnitIdentifier(LocalPlayerUnit) == packet.UnitId) return;

            if (!_remoteStates.TryGetValue(packet.UnitId, out var state))
            {
                state = new RemoteVehicleState();
                _remoteStates[packet.UnitId] = state;
            }

            state.TargetPosition = packet.Position;
            state.TargetRotation = packet.Rotation;
            state.Velocity = packet.Velocity;
            state.TurretYaw = packet.TurretYaw;
            state.GunPitch = packet.GunPitch;
            state.LastUpdateTime = Time.time;

            if (_unitCache.TryGetValue(packet.UnitId, out var unit) && unit != null)
            {
                unit.SetNoPlayerControlAllowed(false);
            }
        }

        private void InterpolateRemoteVehicles()
        {
            float lerpFactor = Mathf.Clamp01(Time.deltaTime * 15f);

            foreach (var kvp in _remoteStates)
            {
                string id = kvp.Key;
                var state = kvp.Value;

                if (!_unitCache.TryGetValue(id, out var unit) || unit == null) continue;

                // Smooth position & rotation
                unit.RootTransform.position = Vector3.Lerp(unit.RootTransform.position, state.TargetPosition, lerpFactor);
                unit.RootTransform.rotation = Quaternion.Slerp(unit.RootTransform.rotation, state.TargetRotation, lerpFactor);

                // Smooth aim platforms
                var platforms = unit.AimablePlatforms;
                if (platforms != null && platforms.Length > 0)
                {
                    var turret = platforms[0];
                    if (turret != null)
                    {
                        var curEuler = turret.LocalRotation.eulerAngles;
                        var targetRot = Quaternion.Euler(curEuler.x, state.TurretYaw, curEuler.z);
                        turret.LocalRotation = Quaternion.Slerp(turret.LocalRotation, targetRot, lerpFactor);
                    }

                    if (platforms.Length > 1)
                    {
                        var gun = platforms[1];
                        if (gun != null)
                        {
                            var curEuler = gun.LocalRotation.eulerAngles;
                            var targetRot = Quaternion.Euler(state.GunPitch, curEuler.y, curEuler.z);
                            gun.LocalRotation = Quaternion.Slerp(gun.LocalRotation, targetRot, lerpFactor);
                        }
                    }
                }
            }
        }

        public void HandleRemoteFire(FireWeaponPacket packet)
        {
            if (string.IsNullOrEmpty(packet.UnitId)) return;
            if (_unitCache.TryGetValue(packet.UnitId, out var unit) && unit != null)
            {
                var wm = unit.WeaponsManager;
                if (wm != null && wm.Weapons != null && packet.WeaponIndex < wm.Weapons.Length)
                {
                    var wsi = wm.Weapons[packet.WeaponIndex];
                    if (wsi != null && wsi.Weapon != null)
                    {
                        WeaponFirePatch.IsRemoteFire = true;
                        try
                        {
                            wsi.Weapon.Fire();
                        }
                        finally
                        {
                            WeaponFirePatch.IsRemoteFire = false;
                        }
                    }
                }
            }
        }

        public void BroadcastCurrentMission()
        {
            try
            {
                var state = GHPC.State.PersistentDataManager.GetStateData();
                if (state == null || state.MetaData == null) return;

                var packet = new MissionLoadPacket
                {
                    TheaterKey = state.TheaterKey ?? "",
                    MissionSceneName = state.MetaData.MissionSceneReference.Name ?? "",
                    MissionName = state.MetaData.MissionName ?? "",
                    IsDaytime = SceneController.IsDaytime,
                    Faction = (int)SceneController.TargetSpawningFaction,
                    IsFlexMission = state.MetaData.IsFlexMission
                };

                using (var ms = new MemoryStream())
                using (var writer = new BinaryWriter(ms))
                {
                    packet.Serialize(writer);
                    SendPacket(ms.ToArray());
                }

                Debug.Log($"[GHPC.Coop] Broadcasted MissionLoadPacket: {packet.TheaterKey} / {packet.MissionName} ({packet.MissionSceneName})");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GHPC.Coop] BroadcastCurrentMission failed: {ex.Message}");
            }
        }

        public void BroadcastMissionFromParam(string missionParam)
        {
            try
            {
                // First try standard StateData
                var state = GHPC.State.PersistentDataManager.GetStateData();
                if (state != null && state.MetaData != null && !string.IsNullOrEmpty(state.MetaData.MissionSceneReference.Name))
                {
                    BroadcastCurrentMission();
                    return;
                }

                // Fallback: parse parameter "terrainIndex,sceneName"
                if (string.IsNullOrEmpty(missionParam)) return;
                var parts = missionParam.Split(',');
                if (parts.Length < 2) return;

                string sceneName = parts[1].Trim();
                var theaters = Resources.FindObjectsOfTypeAll<GHPC.Mission.Data.MissionTheaterScriptable>();
                if (theaters == null) return;

                foreach (var t in theaters)
                {
                    if (t == null || t.Missions == null) continue;
                    var meta = t.Missions.FirstOrDefault(m => m != null && !m.IsCategory && m.MissionSceneReference.Name == sceneName);
                    if (meta != null)
                    {
                        var packet = new MissionLoadPacket
                        {
                            TheaterKey = t.Key,
                            MissionSceneName = sceneName,
                            MissionName = meta.MissionName,
                            IsDaytime = SceneController.IsDaytime,
                            Faction = (int)SceneController.TargetSpawningFaction,
                            IsFlexMission = meta.IsFlexMission
                        };

                        using (var ms = new MemoryStream())
                        using (var writer = new BinaryWriter(ms))
                        {
                            packet.Serialize(writer);
                            SendPacket(ms.ToArray());
                        }

                        Debug.Log($"[GHPC.Coop] Broadcasted MissionLoadPacket from param: {t.Key} / {meta.MissionName} ({sceneName})");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GHPC.Coop] BroadcastMissionFromParam failed: {ex.Message}");
            }
        }

        public void HandleRemoteMissionLoad(MissionLoadPacket packet)
        {
            if (string.IsNullOrEmpty(packet.MissionSceneName)) return;

            var curScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (curScene.name == packet.MissionSceneName)
            {
                Debug.Log($"[GHPC.Coop] Already in mission scene: {curScene.name}");
                return;
            }

            Debug.Log($"[GHPC.Coop] Autoloading host mission: {packet.TheaterKey} / {packet.MissionName} ({packet.MissionSceneName})");

            try
            {
                var theaters = Resources.FindObjectsOfTypeAll<GHPC.Mission.Data.MissionTheaterScriptable>();
                var theater = theaters?.FirstOrDefault(t => t != null && t.Key == packet.TheaterKey);
                if (theater == null || theater.Missions == null)
                {
                    Debug.LogError($"[GHPC.Coop] Theater not found: {packet.TheaterKey}");
                    return;
                }

                var meta = theater.Missions.FirstOrDefault(m => m != null && !m.IsCategory && m.MissionSceneReference.Name == packet.MissionSceneName);
                if (meta == null)
                {
                    Debug.LogError($"[GHPC.Coop] Mission not found in theater: {packet.MissionSceneName}");
                    return;
                }

                int terrainIndex = UnityEngine.SceneManagement.SceneUtility.GetBuildIndexByScenePath(theater.TerrainSceneReference.Path);
                if (terrainIndex < 0)
                {
                    Debug.LogError($"[GHPC.Coop] Terrain index not found: {theater.TerrainSceneReference.Path}");
                    return;
                }

                GHPC.State.PersistentDataManager.PersistMissionInfo(
                    new GHPC.State.Data.MissionInfoData(meta.MissionName, "Co-op Host Mission"), 
                    meta, 
                    theater.Key);

                GHPC.Mission.DynamicMissionComposer.CampaignMode = false;
                GHPC.Mission.DynamicMissionComposer.DynamicMission = packet.IsFlexMission;
                GHPC.Mission.DynamicMissionComposer.NeedsFlexMissionSetup = packet.IsFlexMission;
                GHPC.Mission.DynamicMissionComposer.CurrentMissionNameKey = packet.MissionSceneName;

                SceneController.IsDaytime = packet.IsDaytime;
                SceneController.AllowTimeChoice = false;
                SceneController.TargetSpawningFaction = (GHPC.Faction)packet.Faction;

                var sc = UnityEngine.Object.FindObjectOfType<SceneController>();
                if (sc != null)
                {
                    sc.LoadSceneByMission(terrainIndex + "," + packet.MissionSceneName);
                }
                else
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene(terrainIndex);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GHPC.Coop] Error autoloading host mission: {ex}");
            }
        }
    }
}
