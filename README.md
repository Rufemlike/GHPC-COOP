# GHPC-Coop

**GHPC-Coop** is an unofficial co-op multiplayer mod for [Gunner, HEAT, PC!](https://gunnerheatpc.com/) (Unity 2022.3 Mono / 64-bit).

---

## 🌟 What's New in v0.2.5

- **Tactical 3D Nameplates (`TacticalNameplates.cs`):**
  - Crisp in-world markers drawn directly above teammates' tanks: `PlayerName [VehicleModel] (Distance)`.
  - **Smart Occlusion (LoS Raycasting):** Markers automatically hide behind ridges, terrain, buildings, and tree cover.
  - **Dynamic Scale & Alpha Falloff:** Clean readability from 1m up to 2500m without cluttering your gunner optics.
- **Autoloader Stabilizer (`AutoloaderStabilizer.cs`):**
  - Resolves Soviet carousel autoloader lockups on T-72M1, T-80B, and BMP-1 during multiplayer.
  - Ensures continuous cycling and loading without desynchronizing the gun breech state.
- **Interactive 1-Click Patcher & Uninstaller (`Patcher.exe`):**
  - Run `Patcher.exe` directly from the game root:
    - **`[1]`** Install / update the mod into the game.
    - **`[2]`** Completely uninstall the mod and restore 100% vanilla game files from backup.
- **Top-Right Dropdown Menu:**
  - Anchored firmly to the top-right corner of the screen (`Screen.width - 335px`), completely clearing compass, radio chat, and vehicle status.
  - Dual networking: Steam P2P (invites & lobby codes) + Direct UDP / LAN (port `7777`).

---

## 📦 Installation

### Quick Install (Automated Installer)
1. Download [`GHPC-Coop-Mod-v0.2.5.zip`](releases/GHPC-Coop-Mod-v0.2.5.zip).
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
