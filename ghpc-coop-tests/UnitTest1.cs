using System;
using System.IO;
using System.Threading;
using GHPC.Coop.Network;
using UnityEngine;
using Xunit;

namespace GHPC.Coop.Tests
{
    public class NetworkPacketTests
    {
        [Fact]
        public void Test_VehicleSyncPacket_Roundtrip()
        {
            var original = new VehicleSyncPacket
            {
                UnitId = "M1IP_Abrams_Alpha_1",
                Position = new Vector3(123.45f, 67.89f, -987.65f),
                Rotation = new Quaternion(0.1f, 0.2f, 0.3f, 0.9f),
                Velocity = new Vector3(10f, 0f, 5f),
                TurretYaw = 45.5f,
                GunPitch = -2.5f
            };

            byte[] bytes;
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                original.Serialize(writer);
                bytes = ms.ToArray();
            }

            Assert.NotEmpty(bytes);

            VehicleSyncPacket deserialized;
            using (var ms = new MemoryStream(bytes))
            using (var reader = new BinaryReader(ms))
            {
                var type = (PacketType)reader.ReadByte();
                Assert.Equal(PacketType.VehicleSync, type);
                deserialized = VehicleSyncPacket.Deserialize(reader);
            }

            Assert.Equal(original.UnitId, deserialized.UnitId);
            Assert.Equal(original.Position.x, deserialized.Position.x, 3);
            Assert.Equal(original.Position.y, deserialized.Position.y, 3);
            Assert.Equal(original.Position.z, deserialized.Position.z, 3);
            Assert.Equal(original.Rotation.x, deserialized.Rotation.x, 3);
            Assert.Equal(original.Rotation.y, deserialized.Rotation.y, 3);
            Assert.Equal(original.Rotation.z, deserialized.Rotation.z, 3);
            Assert.Equal(original.Rotation.w, deserialized.Rotation.w, 3);
            Assert.Equal(original.TurretYaw, deserialized.TurretYaw, 2);
            Assert.Equal(original.GunPitch, deserialized.GunPitch, 2);
        }

        [Fact]
        public void Test_FireWeaponPacket_Roundtrip()
        {
            var original = new FireWeaponPacket
            {
                UnitId = "T72M1_NVA_Platoon_2",
                WeaponIndex = 1,
                AimVector = new Vector3(0.5f, 0.1f, 0.8f).normalized
            };

            byte[] bytes;
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                original.Serialize(writer);
                bytes = ms.ToArray();
            }

            Assert.NotEmpty(bytes);

            FireWeaponPacket deserialized;
            using (var ms = new MemoryStream(bytes))
            using (var reader = new BinaryReader(ms))
            {
                var type = (PacketType)reader.ReadByte();
                Assert.Equal(PacketType.FireWeapon, type);
                deserialized = FireWeaponPacket.Deserialize(reader);
            }

            Assert.Equal(original.UnitId, deserialized.UnitId);
            Assert.Equal(original.WeaponIndex, deserialized.WeaponIndex);
            Assert.Equal(original.AimVector.x, deserialized.AimVector.x, 3);
            Assert.Equal(original.AimVector.y, deserialized.AimVector.y, 3);
            Assert.Equal(original.AimVector.z, deserialized.AimVector.z, 3);
        }

        [Fact]
        public void Test_Udp_Host_Client_Loopback()
        {
            int testPort = 17777;
            using (var host = new CoopNetwork())
            using (var client = new CoopNetwork())
            {
                host.StartHost(testPort);
                Assert.True(host.IsHost);

                client.Connect("127.0.0.1", testPort);

                // Wait for handshake
                for (int i = 0; i < 20; i++)
                {
                    Thread.Sleep(50);
                    host.Update();
                    client.Update();
                    if (host.IsConnected && client.IsConnected) break;
                }

                Assert.True(host.IsConnected, "Host should detect client connection");
                Assert.True(client.IsConnected, "Client should receive welcome packet");

                // Test vehicle sync transmission
                bool packetReceived = false;
                VehicleSyncPacket receivedPacket = default;

                host.OnVehicleSyncReceived += (p) =>
                {
                    packetReceived = true;
                    receivedPacket = p;
                };

                var sync = new VehicleSyncPacket
                {
                    UnitId = "LoopbackTank",
                    Position = new Vector3(10, 20, 30),
                    Rotation = Quaternion.identity,
                    Velocity = Vector3.zero,
                    TurretYaw = 90f,
                    GunPitch = 5f
                };

                using (var ms = new MemoryStream())
                using (var writer = new BinaryWriter(ms))
                {
                    sync.Serialize(writer);
                    client.SendPacket(ms.ToArray());
                }

                for (int i = 0; i < 20; i++)
                {
                    Thread.Sleep(50);
                    host.Update();
                    if (packetReceived) break;
                }

                Assert.True(packetReceived, "Host should receive vehicle sync packet from client");
                Assert.Equal("LoopbackTank", receivedPacket.UnitId);
                Assert.Equal(10f, receivedPacket.Position.x);
                Assert.Equal(90f, receivedPacket.TurretYaw);
            }
        }
    }
}
