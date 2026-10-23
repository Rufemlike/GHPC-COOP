using System;
using System.IO;
using HarmonyLib;
using GHPC.Coop.Network;
using GHPC.Weapons;
using UnityEngine;

namespace GHPC.Coop.Core
{
    [HarmonyPatch(typeof(WeaponSystem), "Fire")]
    public static class WeaponFirePatch
    {
        public static bool IsRemoteFire = false;

        [HarmonyPostfix]
        public static void Postfix(WeaponSystem __instance, bool __result)
        {
            if (!__result || IsRemoteFire) return;

            var sync = VehicleSyncManager.Instance;
            if (sync == null || !sync.IsConnected) return;

            var unit = Traverse.Create(__instance).Field("_unit").GetValue<GHPC.Unit>() 
                       ?? __instance.GetComponentInParent<GHPC.Unit>();

            // Only broadcast if fired by local player's vehicle
            if (sync.LocalPlayerUnit == null || unit != sync.LocalPlayerUnit) return;

            int weaponIndex = 0;
            if (unit != null && unit.WeaponsManager != null)
            {
                var weapons = unit.WeaponsManager.Weapons;
                if (weapons != null)
                {
                    for (int i = 0; i < weapons.Length; i++)
                    {
                        if (weapons[i] != null && weapons[i].Weapon == __instance)
                        {
                            weaponIndex = i;
                            break;
                        }
                    }
                }
            }

            Vector3 aim = __instance.MuzzleIdentity != null ? __instance.MuzzleIdentity.forward : Vector3.forward;

            var packet = new FireWeaponPacket
            {
                UnitId = sync.GetUnitIdentifier(sync.LocalPlayerUnit),
                WeaponIndex = weaponIndex,
                AimVector = aim
            };

            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                packet.Serialize(writer);
                sync.SendPacket(ms.ToArray());
            }
        }
    }
}
