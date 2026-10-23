using System;
using System.Collections.Generic;
using System.IO;
using Steamworks;
using GHPC.Coop.Core;
using GHPC.Coop.Network;
using GHPC.Player;
using UnityEngine;

namespace GHPC.Coop.UI
{
    /// <summary>
    /// Clean, gray drop-down IMGUI menu anchored directly to the top-left corner (Iron Nest style).
    /// Fixed position at (10, 10), drops down when opened, toggleable via F9 or Arrow keys.
    /// </summary>
    public class CoopArrowMenu : MonoBehaviour
    {
        public static CoopArrowMenu Instance { get; private set; }

        public CoopNetwork Network { get; set; }
        public SteamP2PTransport SteamTransport { get; set; }

        // Open by default so the user immediately sees the co-op menu in top-right
        public bool IsOpen { get; set; } = true;

        private const float PosY = 12f;
        private const float PanelWidth = 320f;
        private const float PanelHeight = 380f;

        private float GetPosX(float elementWidth) => Mathf.Max(10f, Screen.width - elementWidth - 15f);

        private int _currentTab = 0; // 0: Steam, 1: LAN/IP, 2: Взвод, 3: Инфо
        private readonly string[] _tabNames = new string[] { "Steam", "LAN/IP", "Взвод", "Инфо" };

        private string _ipInput = "127.0.0.1";
        private string _portInput = "7777";
        private string _lobbyInput = "";
        private float _copiedAt = -999f;
        private Vector2 _platoonScroll = Vector2.zero;
        private bool _wasCursorLocked = false;
        private bool _loggedFirstGUI = false;

        private List<GHPC.Unit> _cachedUnits = new List<GHPC.Unit>();
        private float _lastUnitScan = -999f;

        private void Awake()
        {
            Instance = this;
            Debug.Log("[GHPC.Coop] CoopArrowMenu Awake on GameObject: " + gameObject.name);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void OpenMenu()
        {
            IsOpen = true;
        }

        public void CloseMenu()
        {
            IsOpen = false;
        }

        public void ToggleMenu()
        {
            IsOpen = !IsOpen;
        }

        private void Update()
        {
            // Pump Steam transport callbacks
            try
            {
                SteamTransport?.Update();
            }
            catch { }

            // Keyboard navigation:
            // [F9] -> Toggle
            // [→] (Right Arrow) -> Open
            // [←] (Left Arrow) or [Esc] -> Collapse
            if (Input.GetKeyDown(KeyCode.RightArrow) && !IsOpen)
            {
                IsOpen = true;
            }
            else if ((Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Escape)) && IsOpen)
            {
                IsOpen = false;
            }
            else if (Input.GetKeyDown(KeyCode.F9))
            {
                IsOpen = !IsOpen;
            }

            // Mouse cursor management
            if (IsOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                _wasCursorLocked = true;
            }
            else if (_wasCursorLocked)
            {
                _wasCursorLocked = false;
                if (PlayerInput.Instance != null && PlayerInput.Instance.CurrentPlayerUnit != null)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }

        public void OnGUI()
        {
            if (!_loggedFirstGUI)
            {
                _loggedFirstGUI = true;
                Debug.Log("[GHPC.Coop] >>> CoopArrowMenu OnGUI is rendering in top-right corner! <<<");
                try
                {
                    string path = Path.Combine(Path.GetDirectoryName(Application.dataPath), "coop_gui_active.txt");
                    File.WriteAllText(path, $"Coop GUI active at {DateTime.Now:yyyy-MM-dd HH:mm:ss} on screen {Screen.width}x{Screen.height}\n");
                }
                catch { }
            }

            GUI.depth = -99999;

            // 0. Render Tactical 3D Nameplates over friendly tanks in world space
            try
            {
                TacticalNameplates.Instance?.RenderNameplates();
            }
            catch { }

            // 1. Collapsed: A neat button pinned to the top-right corner
            if (!IsOpen)
            {
                DrawCollapsedHeader();
                return;
            }

            // 2. Expanded: Drop-down from top-right corner
            DrawExpandedDropdown();
        }

        private void DrawCollapsedHeader()
        {
            float posX = GetPosX(185f);
            Rect headerRect = new Rect(posX, PosY, 185f, 26f);
            GUI.Box(headerRect, string.Empty);

            if (GuiButton(new Rect(headerRect.x + 2f, headerRect.y + 2f, headerRect.width - 4f, headerRect.height - 4f), "► СЕТЬ (CO-OP) [F9]"))
            {
                IsOpen = true;
            }
        }

        private void DrawExpandedDropdown()
        {
            float posX = GetPosX(PanelWidth);

            // Dropdown header pinned at top-right
            Rect headerRect = new Rect(posX, PosY, PanelWidth, 26f);
            GUI.Box(headerRect, string.Empty);

            if (GuiButton(new Rect(headerRect.x + 2f, headerRect.y + 2f, headerRect.width - 4f, headerRect.height - 4f), "▼ СЕТЬ (CO-OP) [F9] — СВЕРНУТЬ"))
            {
                IsOpen = false;
            }

            // Dropdown panel body directly below the header
            Rect bodyRect = new Rect(posX, PosY + 28f, PanelWidth, PanelHeight);
            GUI.Box(bodyRect, string.Empty);

            float startX = bodyRect.x + 10f;
            float y = bodyRect.y + 8f;
            float w = bodyRect.width - 20f;

            // Title
            DrawBoldLabel(new Rect(startX, y, w, 20f), "GHPC Co-op Mod v0.2.4", 13);
            y += 24f;

            // Tabs Bar
            float tabW = w / _tabNames.Length;
            for (int i = 0; i < _tabNames.Length; i++)
            {
                Rect r = new Rect(startX + i * tabW, y, tabW - 2f, 24f);
                string title = (_currentTab == i) ? $"[{_tabNames[i]}]" : _tabNames[i];
                if (GuiButton(r, title))
                {
                    _currentTab = i;
                    if (_currentTab == 2) RefreshFriendlyUnits();
                }
            }

            y += 30f;

            // Tab Content
            switch (_currentTab)
            {
                case 0:
                    DrawSteamTab(startX, ref y, w);
                    break;
                case 1:
                    DrawLanTab(startX, ref y, w);
                    break;
                case 2:
                    DrawPlatoonTab(startX, ref y, w);
                    break;
                case 3:
                    DrawInfoTab(startX, ref y, w);
                    break;
            }
        }

        private void DrawSteamTab(float x, ref float y, float w)
        {
            bool steamReady = SteamTransport != null && SteamTransport.IsSteamAvailable;
            string status = SteamTransport != null ? SteamTransport.StatusMessage : "Steam не запущен";

            GUI.Label(new Rect(x, y, w, 20f), $"Статус: {status}");
            y += 24f;

            if (!steamReady)
            {
                GUI.Label(new Rect(x, y, w, 44f), "Steam не активен.\nДля игры по LAN / Radmin используйте 'LAN/IP'.");
                y += 48f;

                if (GuiButton(new Rect(x, y, w, 26f), "Повторить проверку Steam"))
                {
                    SteamTransport?.Initialize();
                }
                y += 32f;
                return;
            }

            bool inLobby = SteamTransport.CurrentLobbyId != CSteamID.Nil;

            if (!inLobby)
            {
                if (GuiButton(new Rect(x, y, w * 0.5f - 3f, 28f), "Создать лобби"))
                {
                    SteamTransport.HostLobby();
                }

                if (GuiButton(new Rect(x + w * 0.5f + 3f, y, w * 0.5f - 3f, 28f), "Войти по ID"))
                {
                    if (ulong.TryParse(_lobbyInput.Trim(), out ulong id))
                    {
                        SteamTransport.JoinLobby(new CSteamID(id));
                    }
                }

                y += 34f;

                GUI.Label(new Rect(x, y, w, 20f), "ID лобби друга (Steam):");
                y += 22f;

                _lobbyInput = GUI.TextField(new Rect(x, y, w - 80f, 24f), _lobbyInput ?? "");
                if (GuiButton(new Rect(x + w - 76f, y, 76f, 24f), "Вставить"))
                {
                    _lobbyInput = GUIUtility.systemCopyBuffer != null ? GUIUtility.systemCopyBuffer.Trim() : "";
                }

                y += 32f;
            }
            else
            {
                string lobbyStr = SteamTransport.CurrentLobbyId.m_SteamID.ToString();
                bool copied = (Time.realtimeSinceStartup - _copiedAt < 1.8f);

                GUI.Label(new Rect(x, y, w - 85f, 20f), $"ID лобби: {lobbyStr}");
                if (GuiButton(new Rect(x + w - 80f, y, 80f, 22f), copied ? "Скопировано!" : "Копировать"))
                {
                    GUIUtility.systemCopyBuffer = lobbyStr;
                    _copiedAt = Time.realtimeSinceStartup;
                }

                y += 28f;

                if (GuiButton(new Rect(x, y, w, 28f), "Пригласить друга (Shift+Tab)"))
                {
                    SteamTransport.OpenInviteOverlay();
                }

                y += 34f;

                if (GuiButton(new Rect(x, y, w, 26f), "Отключиться / Покинуть лобби"))
                {
                    SteamTransport.Disconnect();
                }

                y += 32f;
            }
        }

        private void DrawLanTab(float x, ref float y, float w)
        {
            string status = Network != null ? Network.StatusMessage : "Сеть не активна";
            bool isConnected = Network != null && Network.IsConnected;

            GUI.Label(new Rect(x, y, w, 20f), $"Статус UDP: {status}");
            y += 24f;

            // Host Port
            GUI.Label(new Rect(x, y, 70f, 22f), "Порт:");
            _portInput = GUI.TextField(new Rect(x + 75f, y, 65f, 22f), _portInput ?? "7777");

            if (!isConnected)
            {
                if (GuiButton(new Rect(x + 148f, y, w - 148f, 24f), "Запустить хост"))
                {
                    if (int.TryParse(_portInput, out int p)) Network?.StartHost(p);
                }
            }
            else
            {
                if (GuiButton(new Rect(x + 148f, y, w - 148f, 24f), "Остановить"))
                {
                    Network?.Stop();
                }
            }

            y += 32f;

            // Client IP
            GUI.Label(new Rect(x, y, 70f, 22f), "IP хоста:");
            _ipInput = GUI.TextField(new Rect(x + 75f, y, 130f, 22f), _ipInput ?? "127.0.0.1");

            if (!isConnected)
            {
                if (GuiButton(new Rect(x + 212f, y, w - 212f, 24f), "Войти"))
                {
                    if (int.TryParse(_portInput, out int p)) Network?.Connect(_ipInput.Trim(), p);
                }
            }

            y += 34f;

            GUI.Label(new Rect(x, y, w, 40f), "Для игры через интернет без белого IP\nиспользуйте Radmin VPN или ZeroTier.");
            y += 44f;
        }

        private void RefreshFriendlyUnits()
        {
            _cachedUnits.Clear();
            var sync = VehicleSyncManager.Instance;
            if (sync != null)
            {
                sync.ScanUnits();
                _cachedUnits = sync.GetFriendlyUnits();
            }
            _lastUnitScan = Time.time;
        }

        private void DrawPlatoonTab(float x, ref float y, float w)
        {
            var sync = VehicleSyncManager.Instance;
            if (sync == null)
            {
                GUI.Label(new Rect(x, y, w, 20f), "Синхронизатор техники не активен.");
                return;
            }

            string curName = sync.LocalPlayerUnit != null
                ? (!string.IsNullOrEmpty(sync.LocalPlayerUnit.FriendlyName) ? sync.LocalPlayerUnit.FriendlyName : sync.LocalPlayerUnit.name)
                : "Не выбран (запустите бой)";

            GUI.Label(new Rect(x, y, w, 20f), $"Текущий: {curName}");
            y += 24f;

            if (GuiButton(new Rect(x, y, w * 0.65f - 4f, 26f), "Следующий танк [TAB]"))
            {
                CycleNextVehicle();
            }

            if (GuiButton(new Rect(x + w * 0.65f + 2f, y, w * 0.35f - 2f, 26f), "Обновить"))
            {
                RefreshFriendlyUnits();
            }

            y += 32f;

            if (Time.time - _lastUnitScan > 5f && (_cachedUnits == null || _cachedUnits.Count == 0))
            {
                RefreshFriendlyUnits();
            }

            if (_cachedUnits == null || _cachedUnits.Count == 0)
            {
                GUI.Label(new Rect(x, y, w, 36f), "Техника не найдена.\nЗапустите миссию или быстрый бой.");
                y += 40f;
            }
            else
            {
                float scrollHeight = 135f;
                _platoonScroll = GUI.BeginScrollView(new Rect(x, y, w, scrollHeight), _platoonScroll, new Rect(0, 0, w - 20f, _cachedUnits.Count * 26f));

                for (int i = 0; i < _cachedUnits.Count; i++)
                {
                    var u = _cachedUnits[i];
                    if (u == null) continue;
                    string name = !string.IsNullOrEmpty(u.FriendlyName) ? u.FriendlyName : u.name;
                    bool isCur = (sync.LocalPlayerUnit == u);

                    GUI.Label(new Rect(0, i * 26f, w - 90f, 22f), $"{(isCur ? "► " : "  ")}{i + 1}. {name}");
                    if (!isCur && GuiButton(new Rect(w - 85f, i * 26f, 65f, 22f), "Сесть"))
                    {
                        SwitchToVehicle(u);
                    }
                }

                GUI.EndScrollView();
                y += scrollHeight + 10f;
            }
        }

        private void DrawInfoTab(float x, ref float y, float w)
        {
            GUI.Label(new Rect(x, y, w, 160f),
                "Управление окном:\n" +
                "• Привязано к правому верхнему углу экрана\n" +
                "• [— Свернуть] — свернуть в аккуратную плашку\n" +
                "• [→] (стрелка вправо) — развернуть выпадающее меню\n" +
                "• [←] (стрелка влево) / [Esc] — свернуть\n" +
                "• [F9] — переключить скрыть / показать\n\n" +
                "Курсор мыши автоматически доступен при открытом меню.\n" +
                "Синхронизируются: позиция танка, башня, орудие и стрельба.");
            y += 165f;
        }

        private void CycleNextVehicle()
        {
            var sync = VehicleSyncManager.Instance;
            if (sync == null) return;
            if (_cachedUnits == null || _cachedUnits.Count == 0) RefreshFriendlyUnits();
            if (_cachedUnits == null || _cachedUnits.Count == 0) return;

            int cur = _cachedUnits.IndexOf(sync.LocalPlayerUnit);
            int next = (cur + 1) % _cachedUnits.Count;
            SwitchToVehicle(_cachedUnits[next]);
        }

        private void SwitchToVehicle(GHPC.Unit target)
        {
            var sync = VehicleSyncManager.Instance;
            if (sync == null || target == null) return;

            sync.LocalPlayerUnit = target;
            if (PlayerInput.Instance != null)
            {
                PlayerInput.Instance.SetPlayerUnit(target);
                target.SetNoPlayerControlAllowed(false);
            }
        }

        private static void DrawBoldLabel(Rect r, string text, int fontSize = 13)
        {
            GUIStyle label = GUI.skin.label;
            int oldSize = label.fontSize;
            FontStyle oldStyle = label.fontStyle;
            label.fontSize = fontSize;
            label.fontStyle = FontStyle.Bold;
            GUI.Label(r, text);
            label.fontSize = oldSize;
            label.fontStyle = oldStyle;
        }

        private static bool GuiButton(Rect r, string label)
        {
            bool clicked = false;
            try
            {
                UnityEngine.Event cur = UnityEngine.Event.current;
                if (cur != null && cur.type == EventType.MouseDown && cur.button == 0 && r.Contains(cur.mousePosition))
                {
                    clicked = true;
                    try { cur.Use(); } catch { }
                }
            }
            catch { }

            GUI.Button(r, label);
            return clicked;
        }
    }
}
