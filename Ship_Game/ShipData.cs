using Ship_Game.Gameplay;
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using System.Runtime.InteropServices;
using Fasterflect;

namespace Ship_Game
{
    public sealed class ShipData
    {
        public bool Animated;
        public string ShipStyle;
        public string EventOnDeath;
        public byte experience;
        public byte Level;
        public string SelectionGraphic = "";
        public string Name;
        public bool HasFixedCost;
        public short FixedCost;
        public bool HasFixedUpkeep;
        public float FixedUpkeep;
        public bool IsShipyard;
        public bool IsOrbitalDefense;
        public string IconPath;
        public CombatState CombatState = CombatState.AttackRuns;
        public float MechanicalBoardingDefense;
        public string Hull;
        public RoleName Role = RoleName.fighter;
        public List<ShipToolScreen.ThrusterZone> ThrusterList;
        public string ModelPath;
        public AIState DefaultAIState;
        // The Doctor: intending to use this for 'Civilian', 'Recon', 'Fighter', 'Bomber' etc.
        public Category ShipCategory = Category.Unclassified;

        // @todo This lookup is expensive and never changes once initialized, find a way to initialize this properly
        public RoleName HullRole => ResourceManager.HullsDict.TryGetValue(Hull, out ShipData role) ? role.Role : Role;
        public ShipData HullData => ResourceManager.HullsDict.TryGetValue(Hull, out ShipData hull) ? hull : null;

        // The Doctor: intending to use this as a user-toggled flag which tells the AI not to build a design as a stand-alone vessel from a planet; only for use in a hangar
        public bool CarrierShip;
        public float BaseStrength;
        public bool BaseCanWarp;
        public List<ModuleSlotData> ModuleSlotList;
        public bool hullUnlockable;
        public bool allModulesUnlocakable = true;
        public bool unLockable;
        //public HashSet<string> EmpiresThatCanUseThis = new HashSet<string>();
        public HashSet<string> techsNeeded;
        public int TechScore;
        //public Dictionary<string, HashSet<string>> EmpiresThatCanUseThis = new Dictionary<string, HashSet<string>>();
        private static readonly string[] RoleArray     = typeof(RoleName).GetEnumNames();
        private static readonly string[] CategoryArray = typeof(Category).GetEnumNames();

        public ShipData()
        {
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct CThrusterZone
        {
            public readonly float X, Y, Scale;
        }
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private unsafe struct CStrView
        {
            readonly sbyte* Str;
            readonly int Len;

            public string AsString
            {
                get
                {
                    if (Str == null)
                        return "";
                    return Len != 0 ? new string(Str, 0, Len) : string.Empty;
                }
            }
            //public string AsString => Len != 0 ? new string(Str, 0, Len) : string.Empty;
            public bool Empty => Len == 0;
            public override string ToString() { return AsString; }
        }
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct CModuleSlot
        {
            public readonly float PosX, PosY, Health, ShieldPower, Facing;
            public readonly CStrView InstalledModuleUID;
            public readonly CStrView HangarshipGuid;
            public readonly CStrView State;
            public readonly CStrView Restrictions;
            public readonly CStrView SlotOptions;
        }
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private unsafe struct CShipDataParser
        {
            public readonly CStrView Name;
            public readonly CStrView Hull;
            public readonly CStrView ShipStyle;
            public readonly CStrView EventOnDeath;
            public readonly CStrView SelectionGraphic;
            public readonly CStrView IconPath;
            public readonly CStrView ModelPath;
            public readonly CStrView DefaultAIState;
            public readonly CStrView Role;
            public readonly CStrView CombatState;
            public readonly CStrView ShipCategory;

            public readonly int TechScore;
            public readonly float BaseStrength;
            public readonly float FixedUpkeep;
            public readonly float MechanicalBoardingDefense;
            public readonly byte Experience;
            public readonly byte Level;
            public readonly short FixedCost;
            public readonly byte Animated;
            public readonly byte HasFixedCost;
            public readonly byte HasFixedUpkeep;
            public readonly byte IsShipyard;
            public readonly byte CarrierShip;
            public readonly byte BaseCanWarp;
            public readonly byte IsOrbitalDefense;
            public readonly byte HullUnlockable;
            public readonly byte UnLockable;
            public readonly byte AllModulesUnlockable;

            public readonly CThrusterZone* Thrusters;
            public readonly int ThrustersLen;
            public readonly CModuleSlot* ModuleSlots;
            public readonly int ModuleSlotsLen;
            public readonly CStrView* Techs;
            public readonly int TechsLen;

            public readonly CStrView ErrorMessage;
        }

        [DllImport("SDNative.dll")]
        private static extern IntPtr CreateShipDataParser(
            [MarshalAs(UnmanagedType.LPWStr)] string filename);

        [DllImport("SDNative.dll")]
        private static extern void DisposeShipDataParser(IntPtr parser);

        // Added by RedFox - manual parsing of ShipData, because this is the slowest part 
        // in loading, the brunt work is offloaded to C++ and then copied back into C#
        public static unsafe ShipData Parse(FileInfo info)
        {
            IntPtr pParser = CreateShipDataParser(info.FullName);
            try
            {
                CShipDataParser* s = (CShipDataParser*)pParser;
                if (!s->ErrorMessage.Empty)
                    throw new InvalidDataException(s->ErrorMessage.AsString);

                ShipData ship = new ShipData()
                {
                    Animated       = s->Animated != 0,
                    ShipStyle      = s->ShipStyle.AsString,
                    EventOnDeath   = s->EventOnDeath.AsString,
                    experience     = s->Experience,
                    Level          = s->Level,
                    Name           = s->Name.AsString,
                    HasFixedCost   = s->HasFixedCost != 0,
                    FixedCost      = s->FixedCost,
                    HasFixedUpkeep = s->HasFixedUpkeep != 0,
                    FixedUpkeep    = s->FixedUpkeep,
                    IsShipyard     = s->IsShipyard != 0,
                    IconPath       = s->IconPath.AsString,
                    Hull           = s->Hull.AsString,
                    ModelPath      = s->ModelPath.AsString,
                    CarrierShip    = s->CarrierShip != 0,
                    BaseStrength   = s->BaseStrength,
                    BaseCanWarp    = s->BaseCanWarp != 0,
                    hullUnlockable = s->HullUnlockable != 0,
                    unLockable     = s->UnLockable != 0,
                    TechScore      = s->TechScore,
                    IsOrbitalDefense = s->IsOrbitalDefense != 0,
                    SelectionGraphic = s->SelectionGraphic.AsString,
                    allModulesUnlocakable = s->AllModulesUnlockable != 0,
                    MechanicalBoardingDefense = s->MechanicalBoardingDefense
                };
                Enum.TryParse(s->Role.AsString, out ship.Role);
                Enum.TryParse(s->CombatState.AsString, out ship.CombatState);
                Enum.TryParse(s->ShipCategory.AsString, out ship.ShipCategory);
                Enum.TryParse(s->DefaultAIState.AsString, out ship.DefaultAIState);

                // @todo Remove conversion to List
                // @todo Remove SDNative.ModuleSlotData conversion
                ship.ModuleSlotList = new List<ModuleSlotData>(s->ModuleSlotsLen);
                for (int i = 0; i < s->ModuleSlotsLen; ++i)
                {
                    CModuleSlot* msd = &s->ModuleSlots[i];
                    ModuleSlotData slot = new ModuleSlotData
                    {
                        Position = new Vector2(msd->PosX, msd->PosY),
                        InstalledModuleUID = msd->InstalledModuleUID.AsString,
                        HangarshipGuid = msd->HangarshipGuid.Empty 
                                       ? Guid.Empty : new Guid(msd->HangarshipGuid.AsString),
                        Health = msd->Health,
                        Shield_Power = msd->ShieldPower,
                        facing = msd->Facing,
                        SlotOptions = msd->SlotOptions.AsString
                    };
                    Enum.TryParse(msd->State.AsString, out slot.state);
                    Enum.TryParse(msd->Restrictions.AsString, out slot.Restrictions);
                    ship.ModuleSlotList.Add(slot);
                }

                // @todo Remove conversion to List
                ship.ThrusterList = new List<ShipToolScreen.ThrusterZone>(s->ThrustersLen);
                for (int i = 0; i < s->ThrustersLen; ++i)
                {
                    CThrusterZone* zone = &s->Thrusters[i];
                    ship.ThrusterList.Add(new ShipToolScreen.ThrusterZone
                    {
                        Position = new Vector2(zone->X, zone->Y),
                        Scale = zone->Scale
                    });
                }

                // @todo Remove conversion to HashSet
                ship.techsNeeded = new HashSet<string>();
                for (int i = 0; i < s->TechsLen; ++i)
                    ship.techsNeeded.Add(s->Techs[i].AsString);
                return ship;
            }
            finally
            {
                DisposeShipDataParser(pParser);
            }
        }

        public ShipData GetClone()
        {
            return (ShipData)MemberwiseClone();
        }

        public string GetRole()
        {
            return RoleArray[(int)Role];
        }

        public string GetCategory()
        {
            return CategoryArray[(int)ShipCategory];
        }

        public enum Category
        {
            Unclassified,
            Civilian,
            Recon,
            Combat,
            Bomber,
            Fighter,            
            Kamikaze
        }

        public enum RoleName
        {
            disabled,
            platform,
            station,
            construction,
            supply,
            freighter,
            troop,
            fighter,
            scout,
            gunboat,
            drone,
            corvette,
            frigate,
            destroyer,
            cruiser,
            carrier,
            capital,
            prototype
        }
    }
}