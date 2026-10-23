using System;
using System.Reflection;
using HarmonyLib;
using GHPC.Weapons;
using UnityEngine;

namespace GHPC.Coop.Core
{
    /// <summary>
    /// Recovers and keeps mechanical carousel autoloaders (T-72M1, T-80B, BMP) active
    /// and synchronized in multiplayer so the gun never gets stuck in a half-loaded state.
    /// Adapted from steffenbk AmmoSync.
    /// </summary>
    public static class AutoloaderStabilizer
    {
        private static GHPC.Unit _lastUnit;
        private static WeaponSystem[] _cachedWeapons = new WeaponSystem[0];
        private static float _nextCheckTime;

        public static void Update()
        {
            if (Time.time < _nextCheckTime) return;
            _nextCheckTime = Time.time + 0.35f;

            var player = GHPC.Player.PlayerInput.Instance;
            if (player == null) return;
            var unit = player.CurrentPlayerUnit;
            if (unit == null || unit.Destroyed || unit.Abandoned || unit.UnitIncapacitated) return;

            if (_lastUnit != unit)
            {
                _lastUnit = unit;
                _cachedWeapons = unit.GetComponentsInChildren<WeaponSystem>(true);
            }

            for (int i = 0; i < _cachedWeapons.Length; i++)
            {
                var weapon = _cachedWeapons[i];
                if (weapon == null || weapon.Feed == null) continue;

                var feed = weapon.Feed as AmmoFeed;
                if (feed == null || feed.Carousel == null) continue;

                // Check if autoloader is idle with an empty breech and needs reload initiated
                if (!feed.Reloading && !feed.Cycling && feed.AmmoTypeInBreech == null)
                {
                    try
                    {
                        // Safely trigger carousel feed reload if ready rack has ammunition
                        if (feed.ReadyRack != null && feed.ReserveCount > 0 && feed.ReadyToReload)
                        {
                            feed.Reload();
                        }
                    }
                    catch { }
                }
            }
        }
    }
}
