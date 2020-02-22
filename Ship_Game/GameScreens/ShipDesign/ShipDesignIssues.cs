﻿using Microsoft.Xna.Framework.Graphics;
using Ship_Game.Ships;

namespace Ship_Game.ShipDesignIssues
{
    public class ShipDesignIssues
    {
        public readonly ShipData Hull;
        public Array<DesignIssueDetails> CurrentDesignIssues { get; }
        public WarningLevel CurrentWarningLevel { get; private set; }

        public ShipDesignIssues(ShipData hull)
        {
            Hull = hull;
            CurrentDesignIssues = new Array<DesignIssueDetails>();
        }

        void AddDesignIssue(DesignIssueType type, WarningLevel severity)
        {
            DesignIssueDetails details = new DesignIssueDetails(type, severity);
            CurrentDesignIssues.Add(details);
            UpdateCurrentWarningLevel(details.Severity);
        }

        void UpdateCurrentWarningLevel(WarningLevel level)
        {
            if (level > CurrentWarningLevel)
                CurrentWarningLevel = level;
        }

        bool LargeCraft => Hull.HullRole == ShipData.RoleName.freighter || Hull.HullRole == ShipData.RoleName.destroyer
                           || Hull.HullRole == ShipData.RoleName.cruiser || Hull.HullRole == ShipData.RoleName.capital;

        bool Stationary => Hull.HullRole == ShipData.RoleName.station || Hull.HullRole ==  ShipData.RoleName.platform;
        bool Civilian   => Hull.Role == ShipData.RoleName.colony || Hull.Role == ShipData.RoleName.freighter
                           || Hull.Role == ShipData.RoleName.construction || Hull.Role == ShipData.RoleName.scout;

        public void Reset()
        {
            CurrentDesignIssues.Clear();
            CurrentWarningLevel = WarningLevel.None;
        }

        public void CheckIssueNoCommand(int numCommand)
        {
            if (Hull.Role != ShipData.RoleName.platform && numCommand == 0)
                AddDesignIssue(DesignIssueType.NoCommand, WarningLevel.Critical);
        }

        public void CheckIssueBackupCommand(int numCommand, int size)
        {
            if (Hull.Role != ShipData.RoleName.platform && numCommand == 1 && size >= 500)
                AddDesignIssue(DesignIssueType.BackUpCommand, WarningLevel.Major);
        }

        public void  CheckIssueUnpoweredModules(bool unpoweredModules)
        {
            if (unpoweredModules)
                AddDesignIssue(DesignIssueType.UnpoweredModules, WarningLevel.Major);
        }

        public void CheckIssueOrdnance(float ordnanceUsed, float ordnanceRecovered, float ammoTime, int size)
        {
            if ((ordnanceUsed - ordnanceRecovered).LessOrEqual(0))
                return;  // Inf ammo

            if (ammoTime < 5)
            {
                AddDesignIssue(DesignIssueType.NoOrdnance, WarningLevel.Critical);
            }
            else
            {
                int goodAmmoTime = LargeCraft ? 50 : 25;
                if (ammoTime < goodAmmoTime)
                    AddDesignIssue(DesignIssueType.LowOrdnance, WarningLevel.Minor);
            }
        }

        public void CheckIssuePowerRecharge(float recharge)
        {
            if (recharge.Less(0))
                AddDesignIssue(DesignIssueType.NegativeRecharge, WarningLevel.Critical);
        }

        public void CheckIssueLowWarpTime(float warpDraw, float ftlTime, float warpSpeed)
        {
            if (warpSpeed.AlmostZero() || warpDraw.GreaterOrEqual(0) || ftlTime > 900)
                return;

            WarningLevel severity = ftlTime < 60 ? WarningLevel.Critical : WarningLevel.Major;
            AddDesignIssue(DesignIssueType.LowWarpTime, severity);
        }

        public void CheckIssueNoWarp(float speed, float warpSpeed)
        {
            if (speed.AlmostZero())
                return; 

            if (warpSpeed.LessOrEqual(0))
            {
                WarningLevel severity = LargeCraft ? WarningLevel.Critical : WarningLevel.Informative;
                AddDesignIssue(DesignIssueType.NoWarp, severity);
            }
            else if (warpSpeed.Less(10000))
            {
                AddDesignIssue(DesignIssueType.SlowWarp, WarningLevel.Major);
            }
            else if (warpSpeed.Less(20000))
            {
                AddDesignIssue(DesignIssueType.SlowWarp, WarningLevel.Minor);
            }
        }

        public void CheckIssueNoSpeed(float speed)
        {
            if (speed.Greater(0) || Stationary)
                return;

            AddDesignIssue(DesignIssueType.NoSpeed, WarningLevel.Critical);
        }

        public void CheckTargetExclusions(bool hasWeapons, bool canTargetFighters, bool  canTargetCorvettes, bool canTargetCapitals)
        {
            if (!hasWeapons)
                return;

            WarningLevel severity = LargeCraft ? WarningLevel.Major : WarningLevel.Critical;
            if (!canTargetCapitals)
                AddDesignIssue(DesignIssueType.CantTargetFighters, severity);

            if (!canTargetCorvettes)
                AddDesignIssue(DesignIssueType.CantTargetCorvettes, severity);

            severity = LargeCraft ? WarningLevel.Critical : WarningLevel.Minor;
            if (!canTargetCapitals)
                AddDesignIssue(DesignIssueType.CantTargetCapitals, severity);
        }

        public void CheckTruePD(int size, int pointDefenseValue)
        {
            int threshold = (size / 60);
            if (size < 500 || pointDefenseValue > threshold)
                return;

            WarningLevel severity = pointDefenseValue < threshold / 2 ? WarningLevel.Major
                                                                      : WarningLevel.Minor;

            AddDesignIssue(DesignIssueType.LowPdValue, severity);
        }

        public void CheckWeaponPowerTime(bool hasEnergyWeapons, bool excessPowerConsumed, float weaponPowerTime)
        {
            if (!hasEnergyWeapons || !excessPowerConsumed)
                return;

            WarningLevel severity = WarningLevel.None;
            if (weaponPowerTime < 2)       severity = WarningLevel.Critical;
            else if (weaponPowerTime < 5)  severity = WarningLevel.Major;
            else if (weaponPowerTime < 10) severity = WarningLevel.Minor;
            else if (weaponPowerTime < 20) severity = WarningLevel.Informative;

            if (severity > WarningLevel.None)
                AddDesignIssue(DesignIssueType.LowWeaponPowerTime, severity);
        }

        public Color CurrentWarningColor => IssueColor(CurrentWarningLevel);

        public static Color IssueColor(WarningLevel severity)
        {
            switch (severity)
            {
                default:
                case WarningLevel.None:        return Color.DarkGray;
                case WarningLevel.Informative: return Color.Green;
                case WarningLevel.Minor:       return Color.Yellow;
                case WarningLevel.Major:       return Color.Orange;
                case WarningLevel.Critical:    return Color.Red;
            }
        }

        public string DesignIssuesButtonText
        {
            get
            {
                string text;
                switch (CurrentWarningLevel)
                {
                    default:
                    case WarningLevel.None:        text = "No Issues";                     break;
                    case WarningLevel.Informative: text = new LocalizedText(2546).Text; break;
                    case WarningLevel.Minor:       text = new LocalizedText(2547).Text; break;
                    case WarningLevel.Major:       text = new LocalizedText(2548).Text; break;
                    case WarningLevel.Critical:    text = new LocalizedText(2549).Text; break;
                }

                text += $" ({CurrentDesignIssues.Count})";
                return text;
            }
        }

    }

    public enum DesignIssueType
    {
        NoCommand,
        BackUpCommand,
        UnpoweredModules,
        NoOrdnance,
        LowOrdnance,
        LowWarpTime,
        NoWarp,
        SlowWarp,
        NegativeRecharge,
        NoSpeed,
        CantTargetFighters,
        CantTargetCorvettes,
        CantTargetCapitals,
        LowPdValue,
        LowWeaponPowerTime
    }

    public enum WarningLevel
    {
        None,
        Informative,
        Minor,
        Major,
        Critical
    }

    public struct DesignIssueDetails
    {
        public readonly DesignIssueType Type;
        public readonly WarningLevel Severity;
        public readonly Color Color;
        public readonly string Title;
        public readonly string Problem;
        public readonly string Remediation;
        public readonly SubTexture Texture;

        public DesignIssueDetails(DesignIssueType issueType, WarningLevel severity)
        {
            Type     = issueType;
            Severity = severity;
            Color    = ShipDesignIssues.IssueColor(severity);
            switch (issueType)
            {
                default:
                case DesignIssueType.NoCommand:
                    Title       = new LocalizedText(2501).Text;
                    Problem     = new LocalizedText(2502).Text;
                    Remediation = new LocalizedText(2503).Text;
                    Texture     = ResourceManager.Texture("NewUI/IssueNoCommand");
                    break;
                case DesignIssueType.BackUpCommand:
                    Title       = new LocalizedText(2504).Text;
                    Problem     = new LocalizedText(2505).Text;
                    Remediation = new LocalizedText(2506).Text;
                    Texture     = ResourceManager.Texture("NewUI/IssueBackupCommand");
                    break;
                case DesignIssueType.UnpoweredModules:
                    Title       = new LocalizedText(2507).Text;
                    Problem     = new LocalizedText(2508).Text;
                    Remediation = new LocalizedText(2509).Text;
                    Texture     = ResourceManager.Texture("NewUI/IssueUnpowered");
                    break;
                case DesignIssueType.NoOrdnance:
                    Title       = new LocalizedText(2510).Text;
                    Problem     = new LocalizedText(2511).Text;
                    Remediation = new LocalizedText(2512).Text;
                    Texture     = ResourceManager.Texture("NewUI/IssueNoOrdnance.png");
                    break;
                case DesignIssueType.LowOrdnance:
                    Title       = new LocalizedText(2513).Text;
                    Problem     = new LocalizedText(2514).Text;
                    Remediation = new LocalizedText(2515).Text;
                    Texture     = ResourceManager.Texture("NewUI/IssueLowOrdnance.png");
                    break;
                case DesignIssueType.LowWarpTime:
                    Title       = new LocalizedText(2516).Text;
                    Problem     = new LocalizedText(2517).Text;
                    Remediation = new LocalizedText(2518).Text;
                    Texture     = ResourceManager.Texture("NewUI/IssueLowWarpTime");
                    break;
                case DesignIssueType.NoWarp:
                    Title       = new LocalizedText(2522).Text;
                    Problem     = new LocalizedText(2523).Text;
                    Remediation = new LocalizedText(2524).Text;
                    Texture     = ResourceManager.Texture("NewUI/IssueNoWarp");
                    break;
                case DesignIssueType.SlowWarp:
                    Title       = new LocalizedText(2525).Text;
                    Problem     = new LocalizedText(2526).Text;
                    Remediation = new LocalizedText(2527).Text;
                    Texture     = ResourceManager.Texture("NewUI/IssueSlowWarp");
                    break;
                case DesignIssueType.NegativeRecharge:
                    Title       = new LocalizedText(2519).Text;
                    Problem     = new LocalizedText(2520).Text;
                    Remediation = new LocalizedText(2521).Text;
                    Texture     = ResourceManager.Texture("NewUI/IssueNegativeRecharge");
                    break;
                case DesignIssueType.NoSpeed:
                    Title       = new LocalizedText(2528).Text;
                    Problem     = new LocalizedText(2529).Text;
                    Remediation = new LocalizedText(2530).Text;
                    Texture     = ResourceManager.Texture("NewUI/IssueNoSublight");
                    break;
                case DesignIssueType.CantTargetFighters:
                    Title       = new LocalizedText(2531).Text;
                    Problem     = new LocalizedText(2532).Text;
                    Remediation = new LocalizedText(2533).Text;
                    Texture     = ResourceManager.Texture("NewUI/IssueNoCommand");
                    break;
                case DesignIssueType.CantTargetCorvettes:
                    Title       = new LocalizedText(2534).Text;
                    Problem     = new LocalizedText(2535).Text;
                    Remediation = new LocalizedText(2536).Text;
                    Texture     = ResourceManager.Texture("NewUI/IssueNoCommand");
                    break;
                case DesignIssueType.CantTargetCapitals:
                    Title       = new LocalizedText(2537).Text;
                    Problem     = new LocalizedText(2538).Text;
                    Remediation = new LocalizedText(2539).Text;
                    Texture     = ResourceManager.Texture("NewUI/IssueNoCommand");
                    break;
                case DesignIssueType.LowPdValue:
                    Title       = new LocalizedText(2540).Text;
                    Problem     = new LocalizedText(2541).Text;
                    Remediation = new LocalizedText(2542).Text;
                    Texture     = ResourceManager.Texture("NewUI/IssueLowPD");
                    break;
                case DesignIssueType.LowWeaponPowerTime:
                    Title       = new LocalizedText(2543).Text;
                    Problem     = new LocalizedText(2544).Text;
                    Remediation = new LocalizedText(2545).Text;
                    Texture     = ResourceManager.Texture("NewUI/IssueLowEnergyWeaponTime");
                    break;
            }
        }
    }
}