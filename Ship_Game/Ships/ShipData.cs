using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Xml.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using SgMotion;
using SgMotion.Controllers;
using Ship_Game.AI;
using Ship_Game.Gameplay;
using SynapseGaming.LightingSystem.Rendering;

namespace Ship_Game.Ships
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
        public Array<ShipToolScreen.ThrusterZone> ThrusterList;
        public string ModelPath;
        public AIState DefaultAIState;
        // The Doctor: intending to use this for 'Civilian', 'Recon', 'Fighter', 'Bomber' etc.
        public Category ShipCategory = Category.Unclassified;

        // @todo This lookup is expensive and never changes once initialized, find a way to initialize this properly        

        // The Doctor: intending to use this as a user-toggled flag which tells the AI not to build a design as a stand-alone vessel from a planet; only for use in a hangar
        public bool CarrierShip;
        public float BaseStrength;
        public bool BaseCanWarp;
        [XmlArray(ElementName = "ModuleSlotList")] public ModuleSlotData[] ModuleSlots;
        public bool hullUnlockable;
        public bool allModulesUnlocakable = true;
        public bool unLockable;
        //public HashSet<string> EmpiresThatCanUseThis = new HashSet<string>();
        public HashSet<string> techsNeeded = new HashSet<string>();
        public int TechScore;

        //public Map<string, HashSet<string>> EmpiresThatCanUseThis = new Map<string, HashSet<string>>();
        private static readonly string[] RoleArray     = typeof(RoleName).GetEnumNames();
        private static readonly string[] CategoryArray = typeof(Category).GetEnumNames();
        [XmlIgnore] [JsonIgnore] public RoleName HullRole => HullData?.Role ?? Role;
        [XmlIgnore] [JsonIgnore] public ShipData HullData { get; internal set; }

        public void SetHullData(ShipData shipData)
        {            
            shipData.HullData = ResourceManager.HullsDict.TryGetValue(shipData.Hull, out ShipData hull) ? hull : shipData;
        }

        public override string ToString() { return Name; }

        public ShipData()
        {
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct CThrusterZone
        {
            public readonly float X, Y, Scale;
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
        private static extern unsafe CShipDataParser* CreateShipDataParser(
            [MarshalAs(UnmanagedType.LPWStr)] string filename);

        [DllImport("SDNative.dll")]
        private static extern unsafe void DisposeShipDataParser(CShipDataParser* parser);
        
        // Added by RedFox - manual parsing of ShipData, because this is the slowest part 
        // in loading, the brunt work is offloaded to C++ and then copied back into C#
        public static unsafe ShipData Parse(FileInfo info)
        {
            CShipDataParser* s = null;
            try
            {
                s = CreateShipDataParser(info.FullName); // @note This will never throw                
                if (!s->ErrorMessage.Empty)
                {
                    Log.Error($"Ship Load error in {info.FullName} : {s->ErrorMessage.AsString}");
                    throw new InvalidDataException(s->ErrorMessage.AsString);
                }

                var ship = new ShipData
                {
                    Animated       = s->Animated != 0,
                    ShipStyle      = s->ShipStyle.AsInternedOrNull,
                    EventOnDeath   = s->EventOnDeath.AsInternedOrNull,
                    experience     = s->Experience,
                    Level          = s->Level,
                    Name           = s->Name.AsInterned,
                    HasFixedCost   = s->HasFixedCost != 0,
                    FixedCost      = s->FixedCost,
                    HasFixedUpkeep = s->HasFixedUpkeep != 0,
                    FixedUpkeep    = s->FixedUpkeep,
                    IsShipyard     = s->IsShipyard != 0,
                    IconPath       = s->IconPath.AsInterned,
                    Hull           = s->Hull.AsInterned,
                    ModelPath      = s->ModelPath.AsInterned,
                    CarrierShip    = s->CarrierShip != 0,
                    BaseStrength   = s->BaseStrength,
                    BaseCanWarp    = s->BaseCanWarp != 0,
                    hullUnlockable = s->HullUnlockable != 0,
                    unLockable     = s->UnLockable != 0,
                    TechScore      = s->TechScore,
                    IsOrbitalDefense          = s->IsOrbitalDefense != 0,
                    SelectionGraphic          = s->SelectionGraphic.AsInterned,
                    allModulesUnlocakable     = s->AllModulesUnlockable != 0,
                    MechanicalBoardingDefense = s->MechanicalBoardingDefense
                };
                Enum.TryParse(s->Role.AsString,          out ship.Role);
                Enum.TryParse(s->CombatState.AsString,    out ship.CombatState);
                Enum.TryParse(s->ShipCategory.AsString,   out ship.ShipCategory);
                Enum.TryParse(s->DefaultAIState.AsString, out ship.DefaultAIState);

                // @todo Remove SDNative.ModuleSlot conversion
                ship.ModuleSlots = new ModuleSlotData[s->ModuleSlotsLen];
                for (int i = 0; i < s->ModuleSlotsLen; ++i)
                {
                    CModuleSlot* msd = &s->ModuleSlots[i];
                    var slot = new ModuleSlotData();
                    slot.Position           = new Vector2(msd->PosX, msd->PosY);
                    slot.InstalledModuleUID = msd->InstalledModuleUID.AsInternedOrNull;
                    slot.HangarshipGuid     = msd->HangarshipGuid.Empty ? Guid.Empty : new Guid(msd->HangarshipGuid.AsString);
                    slot.Health             = msd->Health;
                    slot.ShieldPower        = msd->ShieldPower;
                    slot.Facing             = msd->Facing;
                    Enum.TryParse(msd->Restrictions.AsString, out slot.Restrictions);
                    slot.Orientation        = msd->State.AsInterned;                    
                    slot.SlotOptions        = msd->SlotOptions.AsInterned;   
                    
                    ship.ModuleSlots[i] = slot;
                }

                int slotCount = ship.ModuleSlots.Length;
              
                if (ship.ModuleSlots.Length != slotCount)
                    Log.Warning($"Ship {ship.Name} loaded with errors ");
                // @todo Remove conversion to List
                ship.ThrusterList = new Array<ShipToolScreen.ThrusterZone>(s->ThrustersLen);
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
                    ship.techsNeeded.Add(s->Techs[i].AsInterned);
                ship.SetHullData(ship);                
                
                return ship;
            }           
            catch (Exception e)
            {
                Log.ErrorDialog(e, $"Failed to parse ShipData '{info.FullName}'");
                throw;
            }
            finally
            {
                DisposeShipDataParser(s);
            }
        }

        public ShipData GetClone()
        {
            return (ShipData)MemberwiseClone();
        }

        public string GetRole()
        {
            return RoleArray[(int)Role -1];
        }

        public static string GetRole(RoleName role)
        {
            int roleNum = (int)role - 1;
            return RoleArray[roleNum];
        }


        public string GetCategory()
        {
            return CategoryArray[(int)ShipCategory];
        }

        public void LoadModel() => LoadModel(out SceneObject shipSO, out AnimationController shipMeshAnim, true);

        public void LoadModel(out SceneObject shipSO, out AnimationController shipMeshAnim, bool justLoad = false)
        {
            //Log.Info($"loading model for {Name}");
            
            //long mem1 = GC.GetTotalMemory(true);
            shipSO = ResourceManager.GetSceneMesh(ModelPath, Animated, justLoad);
            shipMeshAnim = null;
            

            if (Animated)
            {
                SkinnedModel skinned = ResourceManager.GetSkinnedModel(ModelPath);
                if (!justLoad)
                {
                    shipMeshAnim = new AnimationController(skinned.SkeletonBones);
                    shipMeshAnim.StartClip(skinned.AnimationClips["Take 001"]);
                }
            }
            ResourceManager.CompactLargeObjectHeap();
            
            //long mem2 = GC.GetTotalMemory(true);
            //var memoryUsed = mem2 - mem1;
            //Log.Info($"Memory used {memoryUsed}");
            

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
            disabled = 1,
            platform,
            station,
            construction,
            colony,
            supply,
            freighter,
            troop,
            troopShip,
            support,
            bomber,
            carrier,
            fighter,
            scout,            
            gunboat,
            drone,
            corvette,
            frigate,
            destroyer,
            cruiser,            
            capital,
            prototype
        }
    }
}