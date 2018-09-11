﻿using System;
using Ship_Game.Ships;
using Microsoft.Xna.Framework.Graphics;

namespace Ship_Game.AI
{
    public static class ShipBuilder
    {
        public static void PickRoles(ref float numShips, float desiredShips, ShipData.RoleName role, Map<ShipData.RoleName, float>
            rolesPicked)
        {
            if (numShips >= desiredShips)
                return;
            rolesPicked.Add(role, numShips / desiredShips);
        }

        public static string PickFromCandidates(ShipData.RoleName role, Empire empire, int maxSize = 0, 
                      ShipModuleType targetModule = ShipModuleType.Dummy, ShipData.HangarOptions designation = ShipData.HangarOptions.General)
        {
            // The AI will pick ships to build based on their Strength and game difficulty level 
            // instead of techs needed. This allows it to choose the toughest ships to build. This is notmalized by ship total slots
            // so ships with more slots of the same role wont get priority (bigger ships also cost more to build and maintain.
            return PickFromCandidatesByStrength(role, empire, maxSize, targetModule, designation);
        }

        private struct MinMaxStrength
        {
            private readonly float Min;
            private readonly float Max;

            public MinMaxStrength(float maxStrength, Empire empire)
            {
                if (empire.isPlayer) // always select the best for player
                {
                    Min = maxStrength * 0.9f;
                    Max = maxStrength;
                }
                else // for AI, set the range based on difficulty
                {
                    switch (CurrentGame.Difficulty)
                    {
                        case UniverseData.GameDifficulty.Easy:
                            Min = maxStrength * 0.3f;
                            Max = maxStrength * 0.8f;
                            break;
                        case UniverseData.GameDifficulty.Normal:
                            Min = maxStrength * 0.7f;
                            Max = maxStrength;
                            break;
                        case UniverseData.GameDifficulty.Hard:
                            Min = maxStrength * 0.8f;
                            Max = maxStrength;
                            break;
                        case UniverseData.GameDifficulty.Brutal:
                        default:
                            Min = maxStrength * 0.9f;
                            Max = maxStrength;
                            break;
                    }
                }
            }

            public bool InRange(float strength) => strength.InRange(Min, Max);

            public override string ToString() => $"[{Min.String(2)} .. {Max.String(2)}]";
        }

        private static void Debug(string message)
        {
            Log.DebugInfo(ConsoleColor.Blue, message);
        }

        private static Array<Ship> ShipsWeCanBuild(Empire empire)
        {
            var ships = new Array<Ship>(empire.ShipsWeCanBuild.Count);
            foreach (string shipWeCanBuild in empire.ShipsWeCanBuild)
            {
                if (ResourceManager.GetShipTemplate(shipWeCanBuild, out Ship template))
                    ships.Add(template);
            }
            return ships;
        }

        private static string PickFromCandidatesByStrength(ShipData.RoleName role, Empire empire, int maxSize, 
                                                           ShipModuleType targetModule,
                                                           ShipData.HangarOptions designation)
        {
            Ship[] potentialShips = ShipsWeCanBuild(empire).FilterBy(
                ship => ship.DesignRole == role
                && (maxSize <= 0 || ship.SurfaceArea <= maxSize)
                && (designation == ShipData.HangarOptions.General || designation == ship.shipData.HangarDesignation)
            );

            if (potentialShips.Length == 0)
                return "";

            float maxStrength = potentialShips.Max(ship => ship.NormalizedStrength);
            var levelAdjust = new MinMaxStrength(maxStrength, empire);

            Ship[] bestShips = potentialShips.FilterBy(ship => levelAdjust.InRange(ship.NormalizedStrength));
            if (targetModule != ShipModuleType.Dummy)
                bestShips = bestShips.FilterBy(ship => ship.AnyModulesOf(targetModule));

            if (bestShips.Length == 0)
                return "";

            Ship pickedShip = RandomMath.RandItem(bestShips);

            if (Empire.Universe?.showdebugwindow == true)
            {
                Debug($"    Sorted Ship List ({bestShips.Length})");
                foreach (Ship loggedShip in bestShips)
                {
                    Debug($"    -- Name: {loggedShip.Name}, Strength: {loggedShip.NormalizedStrength}");
                }
                Debug($"    Chosen Role: {pickedShip.DesignRole}  Chosen Hull: {pickedShip.shipData.Hull}\n" +
                      $"    Strength: {pickedShip.NormalizedStrength}\n" +
                      $"    Name: {pickedShip.Name}. Range: {levelAdjust}");
            }
            return pickedShip.Name;
        }

        public static string PickShipToRefit(Ship oldShip, Empire empire)
        {
            Ship[] ships = ShipsWeCanBuild(empire).FilterBy(s => s.shipData.Hull == oldShip.shipData.Hull
                                                              && s.BaseStrength >= oldShip.BaseStrength
                                                              && s.Name != oldShip.Name);
            if (ships.Length == 0)
                return "";

            Ship picked = RandomMath.RandItem(ships);
            Log.Info(ConsoleColor.DarkCyan, $"{empire.Name} Refit: {oldShip.Name}, Stength: {oldShip.BaseStrength} refit to --> {picked.Name}, Strength: {picked.BaseStrength}");
            return picked.Name;
        }

        public static float GetModifiedStrength(int shipSize, int numWeaponSlots, float offense, float defense,
            ShipData.RoleName role, float rotationSpeed)
        {
            float weaponRatio = (float)numWeaponSlots / shipSize;
            float modifiedStrength;
            if (defense > offense && weaponRatio < 0.2f)
                modifiedStrength = offense * 2;
            else
                modifiedStrength = offense + defense;

            modifiedStrength += modifiedStrength * rotationSpeed / 100f;
            return modifiedStrength;
        }

        public static Color GetHangarTextColor(string shipName)
        {
            DynamicHangarOptions dynamicHangarType = GetDynamicHangarOptions(shipName);
            switch (dynamicHangarType)
            {
                case DynamicHangarOptions.DynamicLaunch:  return Color.Gold;
                case DynamicHangarOptions.DynamicInterceptor: return Color.Cyan;
                case DynamicHangarOptions.DynamicAntiShip:  return Color.OrangeRed;
                default:                                  return Color.White;
            }
        }

        public static DynamicHangarOptions GetDynamicHangarOptions(string compare)
        {
            return Enum.TryParse(compare, out DynamicHangarOptions result) ? result : DynamicHangarOptions.Static;
        }

        public static bool IsDynamicHangar(string compare)
        {
            if (Enum.TryParse(compare, out DynamicHangarOptions result))
                return result != DynamicHangarOptions.Static;

            return false;
        }
    }
    
    public enum DynamicHangarOptions
    {
        Static,
        DynamicLaunch,
        DynamicInterceptor,
        DynamicAntiShip
    }
}
