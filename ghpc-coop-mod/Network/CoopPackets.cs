using System;
using System.IO;
using UnityEngine;

namespace GHPC.Coop.Network
{
    public enum PacketType : byte
    {
        Handshake = 1,
        Welcome = 2,
        VehicleSync = 3,
        FireWeapon = 4,
        AssignUnit = 5,
        ChatMessage = 6
    }

    public struct VehicleSyncPacket
    {
        public string UnitId;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Velocity;
        public float TurretYaw;
        public float GunPitch;

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)PacketType.VehicleSync);
            writer.Write(UnitId ?? "");
            writer.Write(Position.x);
            writer.Write(Position.y);
            writer.Write(Position.z);
            writer.Write(Rotation.x);
            writer.Write(Rotation.y);
            writer.Write(Rotation.z);
            writer.Write(Rotation.w);
            writer.Write(Velocity.x);
            writer.Write(Velocity.y);
            writer.Write(Velocity.z);
            writer.Write(TurretYaw);
            writer.Write(GunPitch);
        }

        public static VehicleSyncPacket Deserialize(BinaryReader reader)
        {
            return new VehicleSyncPacket
            {
                UnitId = reader.ReadString(),
                Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                Rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                Velocity = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                TurretYaw = reader.ReadSingle(),
                GunPitch = reader.ReadSingle()
            };
        }
    }

    public struct FireWeaponPacket
    {
        public string UnitId;
        public int WeaponIndex;
        public Vector3 AimVector;

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)PacketType.FireWeapon);
            writer.Write(UnitId ?? "");
            writer.Write(WeaponIndex);
            writer.Write(AimVector.x);
            writer.Write(AimVector.y);
            writer.Write(AimVector.z);
        }

        public static FireWeaponPacket Deserialize(BinaryReader reader)
        {
            return new FireWeaponPacket
            {
                UnitId = reader.ReadString(),
                WeaponIndex = reader.ReadInt32(),
                AimVector = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle())
            };
        }
    }
}
