# GHPC-Coop

**GHPC-Coop** is an unofficial co-op multiplayer mod for [Gunner, HEAT, PC!](https://gunnerheatpc.com/) (Unity 2022.3 Mono / 64-bit).

---

## 🌟 What's New in v0.2.7.3

- **Enhanced Steam Invite & Friends Overlay (`SteamP2PTransport.cs`):**
  - Added multi-stage fallback for Steam invites: activates lobby invite dialog with automatic fallback to standard Steam Friends overlay (`SteamFriends.ActivateGameOverlay("Friends")`) if the game overlay dialog is restricted by Steam/Spacewar.
- **Fixed Critical Mission Intercept Patch (`MissionSyncPatches.cs`):**
  - Resolved `Parameter mission not found in SceneController.LoadSceneByMission(string data)` which previously prevented the Harmony prefix hook from intercepting mission starts.
  - Scene transition parameter hook now activates reliably on any mission launch.
- **Improved LAN/IP Host Controls (`CoopArrowMenu.cs`):**
  - Direct state feedback in the LAN tab: displays clear `«Остановить хост»` button and listening socket status.
- **Reliable Mission Autoload & Continuous Room Heartbeat (`VehicleSyncManager.cs` & `SteamP2PTransport.cs`):**
  - Replicated steffenbk's robust mission sync model: host broadcasts active mission metadata with continuous 1-second heartbeat pulses over reliable Steam P2P (`EP2PSend.k_EP2PSendReliable`).
  - Automatic fallback scanning across all theaters via `Resources.LoadAll` guarantees clients load instantly even from cold menu states.
  - Handshake triggers immediate mission synchronization.

---

## 📦 Installation

### Quick Install (Automated Installer)
1. Download [`GHPC-Coop-Mod-v0.2.7.3.zip`](releases/GHPC-Coop-Mod-v0.2.7.3.zip).
2. Ensure GHPC is closed.
3. Extract the contents directly into your GHPC game directory (where `GHPC.exe` is located).
4. Run **`Patcher.exe`** and press **`1`** (or let it auto-install).
5. Launch `GHPC.exe`. The `► СЕТЬ (CO-OP) [F9]` button will appear in the top-right corner!

### How to Uninstall / Restore Vanilla
* Run `Patcher.exe` and press **`2`**. Your game is immediately 100% vanilla.

---

## 🎮 How to Play

### Steam P2P (Recommended)
1. Both players launch Steam and start GHPC.
2. In the top-right corner, click `► СЕТЬ (CO-OP) [F9]` to open the dropdown menu.
3. **Host:** On the **Steam** tab, click **`Создать лобби` (Create Lobby)**.
4. Click **`Копировать`** to copy the lobby code or invite friends via `Shift+Tab`.
5. **Client:** Paste the lobby ID into the text box and click **`Войти по ID` (Join by ID)**.

### Direct IP / LAN
1. **Host:** Click the **`LAN/IP`** tab -> Click **`Запустить хост` (Start Host)** on port `7777`.
2. **Client:** Enter host's IP address (e.g. from Radmin VPN) -> Click **`Войти` (Connect)**.

---

## ⌨️ Controls & Hotkeys

| Key | Action |
|---|---|
| **`[F9]`** | Toggle Co-op dropdown on/off |
| **`[ → ]` (Right Arrow)** | Open / expand dropdown |
| **`[ ← ]` (Left Arrow) / `[Esc]`** | Collapse dropdown |
| **`[TAB]`** | Switch to next platoon vehicle in-game |

---

## 🛠️ Building from Source

Requirements: .NET SDK 8.0/9.0/10.0

```bash
# Build mod DLL
dotnet build ghpc-coop-mod/GHPC.Coop.csproj -c Release

# Build standalone Patcher tool
dotnet publish Patcher/Patcher.csproj -c Release -r win-x64 --self-contained false -o Patcher/publish
```
