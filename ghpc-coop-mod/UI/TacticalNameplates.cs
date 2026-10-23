using System;
using System.Collections.Generic;
using UnityEngine;
using GHPC.Player;

namespace GHPC.Coop.UI
{
    /// <summary>
    /// Tactical in-world 3D Nameplates drawn above teammates' tanks.
    /// Includes distance calculation, smart occlusion check (hills/walls), and scale falloff.
    /// Adapted and optimized from GHPC-Coop-BK Nameplates.
    /// </summary>
    public class TacticalNameplates : MonoBehaviour
    {
        public static TacticalNameplates Instance { get; private set; }

        public bool Enabled { get; set; } = true;
        public bool ShowSelf { get; set; } = false;

        private readonly Dictionary<string, string> _remotePlayerNames = new Dictionary<string, string>();
        private GUIStyle _plateStyle;
        private GUIStyle _strokeStyle;
        private UnityEngine.Camera _mainCam;
        private float _nextCamCheck = 0f;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void SetPlayerName(string unitId, string playerName)
        {
            if (string.IsNullOrEmpty(unitId)) return;
            _remotePlayerNames[unitId] = playerName ?? "Teammate";
        }

        public void ClearNames()
        {
            _remotePlayerNames.Clear();
        }

        private void InitStyles()
        {
            if (_plateStyle == null)
            {
                _plateStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    richText = false,
                    wordWrap = false,
                    fontSize = 15,
                    fontStyle = FontStyle.Bold
                };
                _plateStyle.normal.textColor = new Color(0.92f, 0.95f, 1.0f, 0.95f);
            }

            if (_strokeStyle == null)
            {
                _strokeStyle = new GUIStyle(_plateStyle);
                _strokeStyle.normal.textColor = new Color(0.05f, 0.05f, 0.05f, 0.85f);
            }
        }

        private UnityEngine.Camera GetCamera()
        {
            if (_mainCam != null && _mainCam.isActiveAndEnabled) return _mainCam;
            if (Time.time < _nextCamCheck) return _mainCam;

            _nextCamCheck = Time.time + 0.5f;
            _mainCam = GHPC.Camera.CameraManager.MainCam;
            if (_mainCam == null) _mainCam = UnityEngine.Camera.main;
            return _mainCam;
        }

        private static bool IsLineOfSightBlocked(Vector3 origin, Vector3 end, Transform targetRoot, Transform observerRoot)
        {
            var delta = end - origin;
            float dist = delta.magnitude;
            if (dist < 0.1f) return false;

            Ray ray = new Ray(origin, delta / dist);
            var hits = Physics.RaycastAll(ray, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                var hitTransform = hits[i].transform;
                if (targetRoot != null && hitTransform.IsChildOf(targetRoot)) continue;
                if (observerRoot != null && hitTransform.IsChildOf(observerRoot)) continue;
                return true; // Occluded by terrain or scenery
            }
            return false;
        }

        public void RenderNameplates()
        {
            if (!Enabled) return;
            if (UnityEngine.Event.current == null || UnityEngine.Event.current.type != EventType.Repaint) return;

            var player = PlayerInput.Instance;
            if (player == null || player.CurrentPlayerUnit == null) return;

            var localUnit = player.CurrentPlayerUnit;
            var cam = GetCamera();
            if (cam == null) return;

            InitStyles();

            var sync = GHPC.Coop.Core.VehicleSyncManager.Instance;
            if (sync == null) return;

            var friendlyUnits = sync.GetFriendlyUnits();
            if (friendlyUnits == null || friendlyUnits.Count == 0) return;

            Vector3 camPos = cam.transform.position;

            for (int i = 0; i < friendlyUnits.Count; i++)
            {
                var unit = friendlyUnits[i];
                if (unit == null || !unit.gameObject.activeInHierarchy) continue;
                if (unit.Destroyed || unit.Abandoned || unit.UnitIncapacitated) continue;

                bool isSelf = (unit == localUnit);
                if (isSelf && !ShowSelf) continue;

                string unitId = sync.GetUnitIdentifier(unit);
                string playerName;
                if (isSelf)
                {
                    playerName = "ВЫ";
                }
                else if (!_remotePlayerNames.TryGetValue(unitId, out playerName) || string.IsNullOrEmpty(playerName))
                {
                    // Fallback to vehicle friendly name or callsign
                    playerName = !string.IsNullOrEmpty(unit.FriendlyName) ? unit.FriendlyName : unit.name;
                }

                // 3D anchor position directly above the turret / vehicle roof
                Vector3 worldAnchor = unit.transform.position + Vector3.up * 3.2f;
                float dist = Vector3.Distance(camPos, worldAnchor);

                // Max visibility range: 2500m
                if (dist > 2500f || dist < 1.0f) continue;

                // Screen projection
                Vector3 screenPoint = cam.WorldToScreenPoint(worldAnchor);
                if (screenPoint.z <= 0) continue; // Behind camera

                float screenX = screenPoint.x;
                float screenY = Screen.height - screenPoint.y; // Invert Unity Y to GUI Y

                if (screenX < 20 || screenX > Screen.width - 20 || screenY < 20 || screenY > Screen.height - 20)
                    continue;

                // Raycast occlusion check against terrain/obstacles
                Vector3 midPoint = unit.transform.position + Vector3.up * 1.5f;
                if (!isSelf && IsLineOfSightBlocked(camPos, midPoint, unit.RootTransform, localUnit.RootTransform) &&
                               IsLineOfSightBlocked(camPos, worldAnchor, unit.RootTransform, localUnit.RootTransform))
                {
                    continue; // Hidden behind ridge or building
                }

                // Formatting: [T-72M1] Player (240m)
                string tag = !string.IsNullOrEmpty(unit.FriendlyName) ? unit.FriendlyName : "Unit";
                string label = $"{playerName} [{tag}] ({Mathf.RoundToInt(dist)}m)";

                // Scale falloff with distance
                float scale = Mathf.Clamp(Screen.height / 1080f, 0.75f, 1.4f);
                float t = Mathf.Clamp01(dist / 2000f);
                int fontSize = Mathf.RoundToInt(Mathf.Lerp(16, 12, t) * scale);
                float alpha = Mathf.Lerp(0.95f, 0.45f, t);

                _plateStyle.fontSize = fontSize;
                _strokeStyle.fontSize = fontSize;
                _plateStyle.normal.textColor = new Color(0.92f, 0.96f, 1.0f, alpha);
                _strokeStyle.normal.textColor = new Color(0.05f, 0.05f, 0.08f, alpha * 0.9f);

                float width = 300f;
                float height = 26f;
                Rect rect = new Rect(screenX - width * 0.5f, screenY - height * 0.5f, width, height);

                // Draw contrast drop-shadow outline
                GUI.Label(new Rect(rect.x + 1, rect.y, rect.width, rect.height), label, _strokeStyle);
                GUI.Label(new Rect(rect.x - 1, rect.y, rect.width, rect.height), label, _strokeStyle);
                GUI.Label(new Rect(rect.x, rect.y + 1, rect.width, rect.height), label, _strokeStyle);
                GUI.Label(new Rect(rect.x, rect.y - 1, rect.width, rect.height), label, _strokeStyle);

                // Main label
                GUI.Label(rect, label, _plateStyle);
            }
        }
    }
}
