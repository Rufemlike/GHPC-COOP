# GHPC-Coop

**GHPC-Coop** is an unofficial co-op multiplayer mod for [Gunner, HEAT, PC!](https://gunnerheatpc.com/) (Unity 2022.3 Mono / 64-bit).

---

## 🌟 Key Features (v0.2.4)

- **Clean Top-Right Dropdown UI:**
  - Anchored firmly to the top-right corner of the screen — completely clears in-game compass, radio messages, vehicle status, and native menu text.
  - Collapsed state: Compact button `► СЕТЬ (CO-OP) [F9]`.
  - Expanded state: Drops down right below the header `▼ СЕТЬ (CO-OP) [F9] — СВЕРНУТЬ`.
  - The native game main menu is left 100% clean and vanilla (no broken cloned buttons or stretched animation glitches).
  - Automatic mouse cursor release (`Cursor.lockState = None`) while the menu is open, restored seamlessly during gameplay.
  - Quick hotkeys: `[F9]` to toggle, `[→]` to expand dropdown, `[←]` or `[Esc]` to collapse.

- **High Performance & 0 FPS Drop:**
  - Single-pass native rendering: completely eliminated duplicate `OnGUI()` invocations.
  - Removed per-frame scene hierarchy searches (`FindObjectsOfType`).
  - Friendly unit platoon list is cached and refreshed on-demand.

- **Dual Multiplayer Networking:**
  - **Steam P2P (Lobbies & Invites):**
    - Seamless lobby creation and lookup with active `SteamAPI` callback pumping.
    - Copy lobby ID in one click or invite directly via Steam Overlay (`Shift+Tab`).
  - **Direct UDP / LAN (Port 7777):**
    - Play over Local Network, Radmin VPN, or ZeroTier without needing Steam.

- **Platoon Control & Vehicle Synchronization (20 Hz):**
  - Chassis world position, orientation quaternion, and linear velocity.
  - Turret horizontal traverse azimuth and main gun vertical elevation.
  - Weapon firing replication with ballistics.
  - Hotkey switching between platoon vehicles (`TAB`) or via the platoon tab.

---

## 📦 Installation

### Quick Install (Pre-patched)
1. Download [`GHPC-Coop-Mod-v0.2.4.zip`](releases/GHPC-Coop-Mod-v0.2.4.zip).
2. Ensure GHPC is closed.
3. Extract the archive into your GHPC root directory (where `GHPC.exe` is located).
4. Allow replacing `GHPC_Data/Managed/Assembly-CSharp.dll` when prompted (a backup is recommended).
5. Launch `GHPC.exe`. The `► СЕТЬ (CO-OP) [F9]` button will appear in the top-left corner!

---

## 🎮 How to Play

### Steam P2P
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

# Apply IL patch to game Assembly-CSharp.dll
dotnet run --project Patcher
```
