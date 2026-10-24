using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace GHPC.Coop.Network
{
    public class CoopNetwork : IDisposable
    {
        private UdpClient _udpClient;
        private Thread _receiveThread;
        private volatile bool _isRunning;
        private IPEndPoint _remoteEndPoint;
        private readonly ConcurrentQueue<byte[]> _inbox = new ConcurrentQueue<byte[]>();

        public bool IsHost { get; private set; }
        public bool IsConnected { get; private set; }
        public string StatusMessage { get; private set; } = "Disconnected";

        public event Action<VehicleSyncPacket> OnVehicleSyncReceived;
        public event Action<FireWeaponPacket> OnFireWeaponReceived;
        public event Action<string> OnUnitAssigned;
        public event Action<MissionLoadPacket> OnMissionLoadReceived;

        public void StartHost(int port = 7777)
        {
            Stop();
            try
            {
                // Dual-stack IPv6 socket that accepts both IPv4 and IPv6 connections
                Socket socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
                try
                {
                    socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
                }
                catch
                {
                    // Fallback to pure IPv4 if OS does not support dual-stack
                    socket.Dispose();
                    socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                }

                IPEndPoint localEp = socket.AddressFamily == AddressFamily.InterNetworkV6 
                    ? new IPEndPoint(IPAddress.IPv6Any, port) 
                    : new IPEndPoint(IPAddress.Any, port);

                socket.Bind(localEp);
                _udpClient = new UdpClient { Client = socket };

                IsHost = true;
                _isRunning = true;
                StatusMessage = $"Hosting on port {port} (IPv4 + IPv6 Dual-Stack). Waiting for peers...";
                _receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
                _receiveThread.Start();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Host failed: {ex.Message}";
                Debug.LogError($"[GHPC.Coop] Host error: {ex}");
            }
        }

        public void Connect(string hostIp, int port = 7777)
        {
            Stop();
            try
            {
                IPAddress targetAddress;
                if (!IPAddress.TryParse(hostIp.Trim(), out targetAddress))
                {
                    // Support DNS resolving (e.g. DDNS domain or hostname)
                    var addresses = Dns.GetHostAddresses(hostIp.Trim());
                    if (addresses == null || addresses.Length == 0)
                        throw new ArgumentException("Could not resolve host IP or domain.");
                    targetAddress = addresses[0];
                }

                _remoteEndPoint = new IPEndPoint(targetAddress, port);
                _udpClient = new UdpClient(_remoteEndPoint.AddressFamily);
                _udpClient.Connect(_remoteEndPoint);

                IsHost = false;
                _isRunning = true;
                string ipFamily = targetAddress.AddressFamily == AddressFamily.InterNetworkV6 ? "IPv6" : "IPv4";
                StatusMessage = $"Connecting to [{targetAddress}]:{port} ({ipFamily})...";
                _receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
                _receiveThread.Start();

                // Send Handshake
                using (var ms = new MemoryStream())
                using (var writer = new BinaryWriter(ms))
                {
                    writer.Write((byte)PacketType.Handshake);
                    writer.Write("GHPC_Client_Hello");
                    byte[] data = ms.ToArray();
                    _udpClient.Send(data, data.Length);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Connect failed: {ex.Message}";
                Debug.LogError($"[GHPC.Coop] Connect error: {ex}");
            }
        }

        public void SendPacket(byte[] data)
        {
            if (_udpClient == null || !_isRunning) return;
            try
            {
                if (IsHost && _remoteEndPoint != null)
                {
                    _udpClient.Send(data, data.Length, _remoteEndPoint);
                }
                else if (!IsHost)
                {
                    _udpClient.Send(data, data.Length);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GHPC.Coop] Send error: {ex.Message}");
            }
        }

        private void ReceiveLoop()
        {
            IPEndPoint senderEp = _udpClient != null && _udpClient.Client.AddressFamily == AddressFamily.InterNetworkV6
                ? new IPEndPoint(IPAddress.IPv6Any, 0)
                : new IPEndPoint(IPAddress.Any, 0);

            while (_isRunning && _udpClient != null)
            {
                try
                {
                    byte[] data = _udpClient.Receive(ref senderEp);
                    if (IsHost && _remoteEndPoint == null)
                    {
                        _remoteEndPoint = senderEp;
                        IsConnected = true;
                        StatusMessage = $"Peer connected: {senderEp.Address}";

                        // Send Welcome
                        using (var ms = new MemoryStream())
                        using (var writer = new BinaryWriter(ms))
                        {
                            writer.Write((byte)PacketType.Welcome);
                            writer.Write("Welcome to GHPC Host!");
                            byte[] resp = ms.ToArray();
                            _udpClient.Send(resp, resp.Length, _remoteEndPoint);
                        }

                        if (IsHost)
                        {
                            GHPC.Coop.Core.VehicleSyncManager.Instance?.BroadcastCurrentMission();
                        }
                    }
                    _inbox.Enqueue(data);
                }
                catch (SocketException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[GHPC.Coop] Receive loop error: {ex.Message}");
                }
            }
        }

        public void Update()
        {
            while (_inbox.TryDequeue(out byte[] raw))
            {
                if (raw == null || raw.Length == 0) continue;
                try
                {
                    using (var ms = new MemoryStream(raw))
                    using (var reader = new BinaryReader(ms))
                    {
                        var packetType = (PacketType)reader.ReadByte();
                        switch (packetType)
                        {
                            case PacketType.Welcome:
                                IsConnected = true;
                                string msg = reader.ReadString();
                                StatusMessage = $"Connected to Host! ({msg})";
                                break;

                            case PacketType.VehicleSync:
                                var sync = VehicleSyncPacket.Deserialize(reader);
                                OnVehicleSyncReceived?.Invoke(sync);
                                break;

                            case PacketType.FireWeapon:
                                var fire = FireWeaponPacket.Deserialize(reader);
                                OnFireWeaponReceived?.Invoke(fire);
                                break;

                            case PacketType.AssignUnit:
                                string assignedUnitId = reader.ReadString();
                                OnUnitAssigned?.Invoke(assignedUnitId);
                                break;

                            case PacketType.MissionLoad:
                                var mission = MissionLoadPacket.Deserialize(reader);
                                OnMissionLoadReceived?.Invoke(mission);
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[GHPC.Coop] Packet parsing error: {ex.Message}");
                }
            }
        }

        public void Stop()
        {
            _isRunning = false;
            IsConnected = false;
            try { _udpClient?.Close(); } catch { }
            _udpClient = null;
            _remoteEndPoint = null;
            StatusMessage = "Disconnected";
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
