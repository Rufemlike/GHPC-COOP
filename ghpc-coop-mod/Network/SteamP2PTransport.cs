using System;
using System.IO;
using Steamworks;
using UnityEngine;

namespace GHPC.Coop.Network
{
    public class SteamP2PTransport : IDisposable
    {
        public bool IsSteamAvailable { get; private set; }
        public bool IsHost { get; private set; }
        public bool IsConnected { get; private set; }
        public string StatusMessage { get; private set; } = "Steam Offline";
        public CSteamID CurrentLobbyId { get; private set; }
        public CSteamID RemotePeerSteamId { get; private set; }

        public string PersonaName { get; private set; } = "Steam User";

        public event Action<VehicleSyncPacket> OnVehicleSyncReceived;
        public event Action<FireWeaponPacket> OnFireWeaponReceived;

        private Callback<LobbyCreated_t> _lobbyCreatedCallback;
        private Callback<LobbyEnter_t> _lobbyEnterCallback;
        private Callback<GameLobbyJoinRequested_t> _lobbyJoinRequestedCallback;
        private Callback<P2PSessionRequest_t> _p2pSessionRequestCallback;
        private Callback<P2PSessionConnectFail_t> _p2pSessionFailedCallback;

        public void Initialize()
        {
            try
            {
                if (!SteamAPI.IsSteamRunning())
                {
                    StatusMessage = "Steam не запущен";
                    IsSteamAvailable = false;
                    return;
                }

                bool initSuccess = false;
                try
                {
                    initSuccess = SteamAPI.Init();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[GHPC.Coop] SteamAPI.Init() exception: {ex.Message}");
                }

                if (initSuccess)
                {
                    IsSteamAvailable = true;
                    try
                    {
                        PersonaName = SteamFriends.GetPersonaName();
                    }
                    catch { }

                    StatusMessage = $"Steam готов ({PersonaName})";

                    _lobbyCreatedCallback = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
                    _lobbyEnterCallback = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
                    _lobbyJoinRequestedCallback = Callback<GameLobbyJoinRequested_t>.Create(OnLobbyJoinRequested);
                    _p2pSessionRequestCallback = Callback<P2PSessionRequest_t>.Create(OnP2PSessionRequest);
                    _p2pSessionFailedCallback = Callback<P2PSessionConnectFail_t>.Create(OnP2PSessionFailed);
                }
                else
                {
                    IsSteamAvailable = false;
                    StatusMessage = "Steam не запущен (запустите Steam)";
                }
            }
            catch (Exception ex)
            {
                IsSteamAvailable = false;
                StatusMessage = $"Ошибка Steam: {ex.Message}";
                Debug.LogWarning($"[GHPC.Coop] Steam initialization error: {ex}");
            }
        }

        public void HostLobby()
        {
            if (!IsSteamAvailable)
            {
                StatusMessage = "Steam не запущен";
                return;
            }
            try
            {
                IsHost = true;
                StatusMessage = "Создание лобби Steam...";
                SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 4);
                Debug.Log("[GHPC.Coop] SteamMatchmaking.CreateLobby called.");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка создания: {ex.Message}";
            }
        }

        public void JoinLobby(CSteamID lobbyId)
        {
            if (!IsSteamAvailable) return;
            try
            {
                IsHost = false;
                StatusMessage = $"Joining lobby {lobbyId}...";
                SteamMatchmaking.JoinLobby(lobbyId);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Join error: {ex.Message}";
            }
        }

        public void OpenInviteOverlay()
        {
            if (!IsSteamAvailable || CurrentLobbyId == CSteamID.Nil) return;
            try
            {
                SteamFriends.ActivateGameOverlayInviteDialog(CurrentLobbyId);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Overlay error: {ex.Message}";
            }
        }

        private void OnLobbyCreated(LobbyCreated_t callback)
        {
            if (callback.m_eResult == EResult.k_EResultOK)
            {
                CurrentLobbyId = new CSteamID(callback.m_ulSteamIDLobby);
                SteamMatchmaking.SetLobbyData(CurrentLobbyId, "ghpc_coop", "1");
                SteamFriends.SetRichPresence("connect", $"+connect_lobby {callback.m_ulSteamIDLobby}");
                StatusMessage = $"Lobby created! Lobby ID: {callback.m_ulSteamIDLobby}";
                Debug.Log($"[GHPC.Coop] Steam Lobby created: {callback.m_ulSteamIDLobby}");
            }
            else
            {
                StatusMessage = $"Failed to create lobby: {callback.m_eResult}";
            }
        }

        private void OnLobbyEntered(LobbyEnter_t callback)
        {
            CurrentLobbyId = new CSteamID(callback.m_ulSteamIDLobby);
            CSteamID owner = SteamMatchmaking.GetLobbyOwner(CurrentLobbyId);

            if (!IsHost)
            {
                RemotePeerSteamId = owner;
                IsConnected = true;
                string ownerName = "Host";
                try { ownerName = SteamFriends.GetFriendPersonaName(owner); } catch { }
                StatusMessage = $"Connected to {ownerName}";

                // Send Handshake via P2P
                using (var ms = new MemoryStream())
                using (var writer = new BinaryWriter(ms))
                {
                    writer.Write((byte)PacketType.Handshake);
                    writer.Write("Hello from Steam Client");
                    byte[] data = ms.ToArray();
                    SendPacket(data);
                }
            }
            else
            {
                StatusMessage = "Host lobby ready. Invite friends via Shift+Tab.";
            }
        }

        private void OnLobbyJoinRequested(GameLobbyJoinRequested_t callback)
        {
            JoinLobby(callback.m_steamIDLobby);
        }

        private void OnP2PSessionRequest(P2PSessionRequest_t callback)
        {
            SteamNetworking.AcceptP2PSessionWithUser(callback.m_steamIDRemote);
            RemotePeerSteamId = callback.m_steamIDRemote;
            IsConnected = true;
            string peerName = "Peer";
            try { peerName = SteamFriends.GetFriendPersonaName(callback.m_steamIDRemote); } catch { }
            StatusMessage = $"Peer connected: {peerName}";
            Debug.Log($"[GHPC.Coop] Accepted P2P session from {callback.m_steamIDRemote}");
        }

        private void OnP2PSessionFailed(P2PSessionConnectFail_t callback)
        {
            Debug.LogWarning($"[GHPC.Coop] P2P Session failed with {callback.m_steamIDRemote}: {callback.m_eP2PSessionError}");
            StatusMessage = $"P2P Error: {callback.m_eP2PSessionError}";
        }

        public void SendPacket(byte[] data)
        {
            if (!IsSteamAvailable || RemotePeerSteamId == CSteamID.Nil) return;
            try
            {
                SteamNetworking.SendP2PPacket(RemotePeerSteamId, data, (uint)data.Length, EP2PSend.k_EP2PSendUnreliableNoDelay);
            }
            catch { }
        }

        private float _lastSteamCheck = 0f;

        public void Update()
        {
            if (!IsSteamAvailable)
            {
                if (Time.time - _lastSteamCheck > 2f)
                {
                    _lastSteamCheck = Time.time;
                    Initialize();
                }
                return;
            }

            try
            {
                SteamAPI.RunCallbacks();

                while (SteamNetworking.IsP2PPacketAvailable(out uint msgSize))
                {
                    byte[] buffer = new byte[msgSize];
                    if (SteamNetworking.ReadP2PPacket(buffer, msgSize, out uint bytesRead, out CSteamID remoteId))
                    {
                        if (RemotePeerSteamId == CSteamID.Nil)
                        {
                            RemotePeerSteamId = remoteId;
                            IsConnected = true;
                        }

                        ProcessPacket(buffer);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GHPC.Coop] Steam update error: {ex.Message}");
            }
        }

        private void ProcessPacket(byte[] raw)
        {
            if (raw == null || raw.Length == 0) return;
            try
            {
                using (var ms = new MemoryStream(raw))
                using (var reader = new BinaryReader(ms))
                {
                    var packetType = (PacketType)reader.ReadByte();
                    switch (packetType)
                    {
                        case PacketType.VehicleSync:
                            var sync = VehicleSyncPacket.Deserialize(reader);
                            OnVehicleSyncReceived?.Invoke(sync);
                            break;

                        case PacketType.FireWeapon:
                            var fire = FireWeaponPacket.Deserialize(reader);
                            OnFireWeaponReceived?.Invoke(fire);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GHPC.Coop] Steam packet parsing error: {ex.Message}");
            }
        }

        public void Disconnect()
        {
            try
            {
                if (CurrentLobbyId != CSteamID.Nil)
                {
                    SteamMatchmaking.LeaveLobby(CurrentLobbyId);
                    CurrentLobbyId = CSteamID.Nil;
                }
                if (RemotePeerSteamId != CSteamID.Nil)
                {
                    SteamNetworking.CloseP2PSessionWithUser(RemotePeerSteamId);
                    RemotePeerSteamId = CSteamID.Nil;
                }
            }
            catch { }
            IsConnected = false;
            IsHost = false;
            StatusMessage = "Disconnected";
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
