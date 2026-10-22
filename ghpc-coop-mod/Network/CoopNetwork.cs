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

        public void StartHost(int port = 7777)
        {
            Stop();
            try
            {
                _udpClient = new UdpClient(port);
                IsHost = true;
                _isRunning = true;
                StatusMessage = $"Hosting on port {port}. Waiting for peers...";
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
                _udpClient = new UdpClient();
                _remoteEndPoint = new IPEndPoint(IPAddress.Parse(hostIp), port);
                _udpClient.Connect(_remoteEndPoint);
                IsHost = false;
                _isRunning = true;
                StatusMessage = $"Connecting to {hostIp}:{port}...";
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
            IPEndPoint senderEp = new IPEndPoint(IPAddress.Any, 0);
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
