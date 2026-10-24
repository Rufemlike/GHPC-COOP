# GHPC-Coop

**GHPC-Coop** is an unofficial co-op multiplayer mod for [Gunner, HEAT, PC!](https://gunnerheatpc.com/) (Unity 2022.3 Mono / 64-bit).

---

## 🌟 What's New in v0.2.7.1

- **Reliable Mission Autoload & Continuous Room Heartbeat (`VehicleSyncManager.cs` & `SteamP2PTransport.cs`):**
  - Replicated steffenbk's robust mission sync model: host broadcasts active mission metadata with continuous 1-second heartbeat pulses over reliable Steam P2P (`EP2PSend.k_EP2PSendReliable`).
  - Automatic fallback scanning across all theaters via `Resources.LoadAll` guarantees clients load instantly even from cold menu states.
  - Handshake triggers immediate mission synchronization.
- **Automatic Mission Sync & Client Autoload (`MissionSyncPatches.cs` & `VehicleSyncManager.cs`):**
  - **Zero-touch mission launch:** When the host selects and starts any mission in the game (Instant Action, Campaign, Dynamic Missions), **all connected clients are automatically loaded into the same mission and map**!
  - **Late-join support:** When a client joins a host who is already in battle, the host automatically sends the current mission metadata and terrain index, seamlessly transitioning the guest into the fight.
  - Synchronizes theater key, mission scene, daytime/nighttime, and spawning faction.
- **Player-Only Tactical 3D Nameplates (`TacticalNameplates.cs`):**
  - AI bots, wingmen, and platoon commanders are now cleanly filtered out — **markers appear strictly above tanks controlled by real connected human players**.
  - Crisp in-world markers drawn directly above teammates' tanks: `PlayerName [VehicleModel] (Distance)`.
  - **Smart Occlusion (LoS Raycasting):** Markers automatically hide behind ridges, terrain, buildings, and tree cover.
  - **Dynamic Scale & Alpha Falloff:** Clean readability from 1m up to 2500m without cluttering your gunner optics.
- **Dedicated In-Game Settings Tab (`CoopArrowMenu.cs`):**
  - Dropdown menu now features a dedicated **`[Настройки]` (Settings)** tab.
  - Toggle 3D Player Nameplates on/off on the fly (`[Ники игроков в 3D: ВКЛ/ВЫКЛ]`).
  - Toggle marker above your own vehicle (`[Метка над своим танком: ВКЛ/ВЫКЛ]`).
  - Quick hotkey guide and control references.
- **Dual-Stack IPv6 + IPv4 Networking (`CoopNetwork.cs`):**
  - Host runs a dual-stack socket (`IPAddress.IPv6Any`) accepting both **IPv6** and **IPv4** peers simultaneously on port `7777`.
  - Client supports direct connection to IPv6 addresses (e.g. `2a00:...`), IPv4, or hostnames/DDNS domains with automatic DNS resolution.
  - Allows direct peer connections across ISPs with native IPv6 without needing NAT traversal, Hamachi, or Radmin VPN.
- **Fully Portable 1-Click Patcher & Uninstaller (`Patcher.exe`):**
  - Single-file self-contained executable (requires zero installed .NET SDKs or runtimes on target PCs).
  - Steam registry auto-discovery, common library drive scanning, and interactive drag-and-drop path fallback.
  - Full UTF-8 terminal output and guaranteed prompt pause before exit.
- **Autoloader Stabilizer (`AutoloaderStabilizer.cs`):**
  - Resolves Soviet carousel autoloader lockups on T-72M1, T-80B, and BMP-1 during multiplayer.
  - Ensures continuous cycling and loading without desynchronizing the gun breech state.

---

## 📦 Installation

### Quick Install (Automated Installer)
1. Download [`GHPC-Coop-Mod-v0.2.7.1.zip`](releases/GHPC-Coop-Mod-v0.2.7.1.zip).
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
