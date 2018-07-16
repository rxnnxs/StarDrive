﻿using System.Collections.Generic;
using System.Reflection;

namespace Ship_Game.Ships
{
    public struct Power
    {
        public float NetSubLightPowerDraw;
        public float NetWarpPowerDraw;

        public static Power Calculate(ShipModule[] modules, Empire empire, ShieldsWarpBehavior behavior = ShieldsWarpBehavior.Fully_Powered)
        {
            float nonShieldPowerDraw = 0f;
            float shieldPowerDraw = 0f;
            float warpPowerDrawBonus = 0f;
            if (modules == null)
                return new Power();

            foreach (ShipModule module in modules)
            {
                if (!module.Active || (!module.Powered && module.PowerDraw > 0f))
                    continue;

                if (module.Is(ShipModuleType.Shield))
                {
                    shieldPowerDraw += module.PowerDraw;
                    if (behavior == ShieldsWarpBehavior.Fully_Powered)
                        warpPowerDrawBonus += module.PowerDrawAtWarp; // FB: include bonuses to warp if shields are on at warp
                }
                else
                {
                    nonShieldPowerDraw += module.PowerDraw;
                    warpPowerDrawBonus += module.PowerDrawAtWarp;
                }
            }
            float subLightPowerDraw = shieldPowerDraw + nonShieldPowerDraw;
            float warpPowerDrainModifier = empire?.data.FTLPowerDrainModifier ?? 1;
            float warpPowerDraw = 0f;
            switch (behavior)
            {
                case ShieldsWarpBehavior.Fully_Powered:
                    {
                        warpPowerDraw = (shieldPowerDraw + nonShieldPowerDraw) * warpPowerDrainModifier + (warpPowerDrawBonus * warpPowerDrainModifier / 2);
                        break;
                    }
                case ShieldsWarpBehavior.Maintained_With_Acticvation:
                    {
                        warpPowerDraw = nonShieldPowerDraw * warpPowerDrainModifier + shieldPowerDraw;
                        break;
                    }
                case ShieldsWarpBehavior.Discharged_With_Acticvation:
                    {
                        warpPowerDraw = nonShieldPowerDraw * warpPowerDrainModifier;
                        break;
                    }
            }

            return new Power
            {
                NetSubLightPowerDraw = subLightPowerDraw,
                NetWarpPowerDraw = warpPowerDraw
            };
        }
    }
    public enum ShieldsWarpBehavior
    {
        Fully_Powered,
        Maintained_With_Acticvation,
        Discharged_With_Acticvation
    }
}