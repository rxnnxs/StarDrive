﻿using System;
using System.Collections.Generic;
using Ship_Game.Commands.Goals;
using Ship_Game.Gameplay;
using Ship_Game.Ships;

namespace Ship_Game.Universe.SolarBodies
{
    public class SBProduction
    {
        private readonly Planet Ground;

        private Array<PlanetGridSquare> TilesList => Ground.TilesList;
        private Empire Owner => Ground.Owner;        
        private Array<Building> BuildingList => Ground.BuildingList;        
        private SolarSystem ParentSystem => Ground.ParentSystem;        
        //public IReadOnlyList<QueueItem> ConstructionQ => ConstructionQueue;
        public BatchRemovalCollection<QueueItem> ConstructionQueue = new BatchRemovalCollection<QueueItem>();
        private int CrippledTurns => Ground.CrippledTurns;
        private bool RecentCombat => Ground.RecentCombat;
        private float MineralRichness => Ground.MineralRichness;
        private float Population => Ground.Population;
        private float Consumption => Ground.Consumption;
        private float ShipBuildingModifier => Ground.ShipBuildingModifier;
        private float Fertility => Ground.Fertility;
        private SpaceStation Station => Ground.Station;        
        private Planet.GoodState PS => Ground.PS;
        //private Planet.GoodState FS => Ground.FS;
        private bool PSexport => Ground.PSexport;
        private Planet.ColonyType colonyType => Ground.colonyType;
        private float NetProductionPerTurn => Ground.NetProductionPerTurn;
        private bool GovernorOn => Ground.GovernorOn;

        private float ProductionHere
        {
            get => Ground.ProductionHere;
            set => Ground.ProductionHere = value;
        }

        public SBProduction(Planet planet)
        {
            Ground = planet;
        }

        public bool ApplyStoredProduction(int Index)
        {

            if (CrippledTurns > 0 || RecentCombat || (ConstructionQueue.Count <= 0 || Owner == null))//|| this.Owner.Money <=0))
                return false;
            if (Owner != null && !Owner.isPlayer && Owner.data.Traits.Cybernetic > 0)
                return false;

            QueueItem item = ConstructionQueue[Index];
            float amountToRush = GetMaxProductionPotential(); //for debug help
            float amount = Math.Min(ProductionHere, amountToRush);
            if (Empire.Universe.Debug && Owner.isPlayer)
                amount = float.MaxValue;
            if (amount < 1)
            {
                return false;
            }
            ProductionHere -= amount;
            ApplyProductiontoQueue(amount, Index);

            return true;
        }

        public float GetMaxProductionPotential()
        {
            float num1 = 0.0f;
            float num2 = MineralRichness * Population / 1000;
            for (int index = 0; index < BuildingList.Count; ++index)
            {
                Building building = BuildingList[index];
                if (building.PlusProdPerRichness > 0.0)
                    num1 += building.PlusProdPerRichness * MineralRichness;
                num1 += building.PlusFlatProductionAmount;
                if (building.PlusProdPerColonist > 0.0)
                    num2 += building.PlusProdPerColonist;
            }
            float num3 = num2 + num1 * Population / 1000;
            float num4 = num3;
            if (Owner.data.Traits.Cybernetic > 0)
                return num4 + Owner.data.Traits.ProductionMod * num4 - Consumption;
            return num4 + Owner.data.Traits.ProductionMod * num4;
        }

        public void ApplyProductiontoQueue(float howMuch, int whichItem)
        {
            if (CrippledTurns > 0 || RecentCombat || howMuch <= 0.0)
            {
                if (howMuch > 0 && CrippledTurns <= 0)
                    ProductionHere += howMuch;
                return;
            }
            float cost = 0;
            if (ConstructionQueue.Count > 0 && ConstructionQueue.Count > whichItem)
            {
                QueueItem item = ConstructionQueue[whichItem];
                cost = item.Cost;
                if (item.isShip)
                    cost *= ShipBuildingModifier;
                //cost -= item.productionTowards;
                item.productionTowards += howMuch;
                float remainder = item.productionTowards - cost;
                ProductionHere += Math.Max(0, remainder);                                
            }
            else ProductionHere += howMuch;

            for (int index1 = 0; index1 < ConstructionQueue.Count; ++index1)
            {
                QueueItem queueItem = ConstructionQueue[index1];

                //Added by gremlin remove exess troops from queue 
                if (queueItem.isTroop)
                {

                    int space = 0;
                    foreach (PlanetGridSquare tilesList in TilesList)
                    {
                        if (tilesList.TroopsHere.Count >= tilesList.number_allowed_troops || tilesList.building != null 
                            && (tilesList.building == null || tilesList.building.CombatStrength != 0))
                        {
                            continue;
                        }
                        space++;
                    }

                    if (space < 1)
                    {
                        if (queueItem.productionTowards == 0)
                        {
                            ConstructionQueue.Remove(queueItem);
                        }
                        else
                        {
                            ProductionHere += queueItem.productionTowards;
                            if (queueItem.pgs != null)
                                queueItem.pgs.QItem = null;
                            ConstructionQueue.Remove(queueItem);
                        }
                    }
                }

                if (queueItem.isBuilding && queueItem.productionTowards >= queueItem.Cost)
                {
                    bool dupBuildingWorkaround = false;
                    if (queueItem.Building.Name != "Biospheres")
                        foreach (Building dup in BuildingList)
                        {
                            if (dup.Name == queueItem.Building.Name)
                            {
                                ProductionHere += queueItem.productionTowards;
                                ConstructionQueue.QueuePendingRemoval(queueItem);
                                dupBuildingWorkaround = true;
                            }
                        }
                    if (!dupBuildingWorkaround)
                    {
                        Building building = ResourceManager.CreateBuilding(queueItem.Building.Name);
                        if (queueItem.IsPlayerAdded)
                            building.IsPlayerAdded = queueItem.IsPlayerAdded;
                        BuildingList.Add(building);
                        Ground.Fertility -= building.MinusFertilityOnBuild;
                        Ground.Fertility = Math.Max(Fertility, 0);
                        if (queueItem.pgs != null)
                        {
                            if (queueItem.Building != null && queueItem.Building.Name == "Biospheres")
                            {
                                queueItem.pgs.Habitable = true;
                                queueItem.pgs.Biosphere = true;
                                queueItem.pgs.building = null;
                                queueItem.pgs.QItem = null;
                            }
                            else
                            {
                                queueItem.pgs.building = building;
                                queueItem.pgs.QItem = null;
                            }
                        }
                        if (queueItem.Building.Name == "Space Port")
                        {
                            Station.planet = Ground;
                            Station.ParentSystem = ParentSystem;
                            Station.LoadContent(Empire.Universe.ScreenManager);
                            Ground.HasShipyard = true;
                        }
                        if (queueItem.Building.AllowShipBuilding)
                            Ground.HasShipyard = true;
                        if (building.EventOnBuild != null && Owner != null && Owner == Empire.Universe.PlayerEmpire)
                            Empire.Universe.ScreenManager.AddScreen(new EventPopup(Empire.Universe, Empire.Universe.PlayerEmpire, ResourceManager.EventsDict[building.EventOnBuild], ResourceManager.EventsDict[building.EventOnBuild].PotentialOutcomes[0], true));
                        ConstructionQueue.QueuePendingRemoval(queueItem);
                    }
                }
                else if (queueItem.isShip && !ResourceManager.ShipsDict.ContainsKey(queueItem.sData.Name))
                {
                    ConstructionQueue.QueuePendingRemoval(queueItem);
                    ProductionHere += queueItem.productionTowards;
                }
                else if (queueItem.isShip && queueItem.productionTowards >= queueItem.Cost * ShipBuildingModifier)
                {
                    Ship shipAt;
                    if (queueItem.isRefit)
                        shipAt = Ship.CreateShipAt(queueItem.sData.Name, Owner, Ground, true, !string.IsNullOrEmpty(queueItem.RefitName) ? queueItem.RefitName : queueItem.sData.Name, queueItem.sData.Level);
                    else
                        shipAt = Ship.CreateShipAt(queueItem.sData.Name, Owner, Ground, true);
                    ConstructionQueue.QueuePendingRemoval(queueItem);

                    if (queueItem.sData.Role == ShipData.RoleName.station || queueItem.sData.Role == ShipData.RoleName.platform)
                    {
                        int num = Ground.Shipyards.Count / 9;
                        shipAt.Position = Ground.Center + MathExt.PointOnCircle(Ground.Shipyards.Count * 40, 2000 + 2000 * num * Ground.Scale);
                        shipAt.Center = shipAt.Position;
                        shipAt.TetherToPlanet(Ground);
                        Ground.Shipyards.Add(shipAt.guid, shipAt);
                    }
                    if (queueItem.Goal != null)
                    {
                        if (queueItem.Goal is BuildConstructionShip)
                        {
                            shipAt.AI.OrderDeepSpaceBuild(queueItem.Goal);
                            shipAt.isConstructor = true;
                            shipAt.VanityName = "Construction Ship";
                        }
                        else if (!(queueItem.Goal is BuildDefensiveShips) 
                            && !(queueItem.Goal is BuildOffensiveShips) 
                            && !(queueItem.Goal is FleetRequisition))
                        {
                            ++queueItem.Goal.Step;
                        }
                        else
                        {
                            if (Owner != Empire.Universe.PlayerEmpire)
                                Owner.ForcePoolAdd(shipAt);
                            queueItem.Goal.ReportShipComplete(shipAt);
                        }
                    }
                    else if ((queueItem.sData.Role != ShipData.RoleName.station || queueItem.sData.Role == ShipData.RoleName.platform)
                        && Owner != Empire.Universe.PlayerEmpire)
                        Owner.ForcePoolAdd(shipAt);
                }
                else if (queueItem.isTroop && queueItem.productionTowards >= queueItem.Cost)
                {
                    if (ResourceManager.CreateTroop(queueItem.troopType, Owner).AssignTroopToTile(Ground))
                    {
                        if (queueItem.Goal != null)
                            ++queueItem.Goal.Step;
                        ConstructionQueue.QueuePendingRemoval(queueItem);
                    }
                }
            }
            ConstructionQueue.ApplyPendingRemovals();
        }

        public void ApplyAllStoredProduction(int Index)
        {
            if (CrippledTurns > 0 || RecentCombat || (ConstructionQueue.Count <= 0 || Owner == null)) //|| this.Owner.Money <= 0))
                return;

            float amount = Empire.Universe.Debug ? float.MaxValue : ProductionHere;
            ProductionHere = 0f;
            ApplyProductiontoQueue(amount, Index);

        }

        public void ApplyProductionTowardsConstruction()
        {
            if (CrippledTurns > 0 || RecentCombat)
                return;
         
            float maxp = GetMaxProductionPotential() * (1 - Ground.FarmerPercentage); 
            if (maxp < 5)
                maxp = 5;
            float StorageRatio = 0;
            float take10Turns = 0;
            StorageRatio = ProductionHere / Ground.MaxStorage;
            take10Turns = maxp * StorageRatio;


            if (!PSexport)
                take10Turns *= (StorageRatio < .75f ? PS == Planet.GoodState.EXPORT ? .5f : PS == Planet.GoodState.STORE ? .25f : 1 : 1);
            if (!GovernorOn || colonyType == Planet.ColonyType.Colony)
            {
                take10Turns = NetProductionPerTurn; ;
            }
            float normalAmount = take10Turns;

            normalAmount = ProductionHere.Clamp(0, normalAmount);
            ProductionHere -= normalAmount;

            ApplyProductiontoQueue(normalAmount, 0);
            ProductionHere += NetProductionPerTurn > 0.0f ? NetProductionPerTurn : 0.0f;

            //fbedard: apply all remaining production on Planet with no governor
            if (PS != Planet.GoodState.EXPORT && colonyType == Planet.ColonyType.Colony && Owner.isPlayer)
            {
                normalAmount = ProductionHere;
                ProductionHere = 0f;
                ApplyProductiontoQueue(normalAmount, 0);
            }
        }

        public void AddBuildingToCQ(Building b)
        {
            AddBuildingToCQ(b, false);
        }

        public void AddBuildingToCQ(Building b, bool PlayerAdded)
        {
            int count            = ConstructionQueue.Count;
            QueueItem qi         = new QueueItem();
            qi.IsPlayerAdded     = PlayerAdded;
            qi.isBuilding        = true;
            qi.Building          = b;
            qi.Cost              = b.Cost * UniverseScreen.GamePaceStatic;
            qi.productionTowards = 0.0f;
            qi.NotifyOnEmpty     = false;
            ResourceManager.BuildingsDict.TryGetValue("Terraformer", out Building terraformer);

            if (terraformer == null)
            {
                foreach (KeyValuePair<string, bool> bdict in Owner.GetBDict())
                {
                    if (!bdict.Value)
                        continue;
                    Building check = ResourceManager.GetBuildingTemplate(bdict.Key);

                    if (check.PlusTerraformPoints <= 0)
                        continue;
                    terraformer = check;
                }
            }
            if (b.AssignBuildingToTile(qi, Ground))
                ConstructionQueue.Add(qi);

            else if (Owner.data.Traits.Cybernetic <= 0 && Owner.GetBDict()[terraformer.Name] && Fertility < 1.0
                && Ground.WeCanAffordThis(terraformer, colonyType))
            {
                bool flag = true;
                foreach (QueueItem queueItem in ConstructionQueue)
                {
                    if (queueItem.isBuilding && queueItem.Building.Name == terraformer.Name)
                        flag = false;
                }
                foreach (Building building in BuildingList)
                {
                    if (building.Name == terraformer.Name)
                        flag = false;
                }
                if (!flag)
                    return;
                AddBuildingToCQ(ResourceManager.CreateBuilding(terraformer.Name), false);
            }
            else
            {
                if (!Owner.GetBDict()["Biospheres"])
                    return;
                Ground.TryBiosphereBuild(ResourceManager.CreateBuilding("Biospheres"), qi);
            }
        }
        public int EstimatedTurnsTillComplete(QueueItem qItem)
        {
            int num = (int)Math.Ceiling((double)(int)((qItem.Cost - qItem.productionTowards) / NetProductionPerTurn));
            if (NetProductionPerTurn > 0.0)
                return num;
            else
                return 999;
        }

        public bool TryBiosphereBuild(Building b, QueueItem qi)
        {
            if (qi.isBuilding == false && Ground.NeedsFood()) //(FarmerPercentage > .5f || NetFoodPerTurn < 0))
                return false;
            Array<PlanetGridSquare> list = new Array<PlanetGridSquare>();
            foreach (PlanetGridSquare planetGridSquare in TilesList)
            {
                if (!planetGridSquare.Habitable && planetGridSquare.building == null && (!planetGridSquare.Biosphere && planetGridSquare.QItem == null))
                    list.Add(planetGridSquare);
            }
            if (b.Name != "Biospheres" || list.Count <= 0) return false;

            int index = (int)RandomMath.RandomBetween(0.0f, list.Count);
            PlanetGridSquare planetGridSquare1 = list[index];
            foreach (PlanetGridSquare planetGridSquare2 in TilesList)
            {
                if (planetGridSquare2 == planetGridSquare1)
                {
                    qi.Building = b;
                    qi.isBuilding = true;
                    qi.Cost = b.Cost;
                    qi.productionTowards = 0.0f;
                    planetGridSquare2.QItem = qi;
                    qi.pgs = planetGridSquare2;
                    qi.NotifyOnEmpty = false;
                    ConstructionQueue.Add(qi);
                    return true;
                }
            }
            return false;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~SBProduction() { Dispose(false); }
        private void Dispose(bool disposing)
        {
            ConstructionQueue?.Dispose(ref ConstructionQueue);
        }
    }
}