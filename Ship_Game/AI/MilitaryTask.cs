using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Ship_Game.Debug;
using Ship_Game.Gameplay;

namespace Ship_Game.AI
{
    public class MilitaryTask : IDisposable
    {
        [Serialize(0)] public bool IsCoreFleetTask;
        [Serialize(1)] public bool WaitForCommand;
        [Serialize(2)] public Array<Guid> HeldGoals = new Array<Guid>();
        [Serialize(3)] public int Step;
        [Serialize(4)] public Guid TargetPlanetGuid = Guid.Empty;
        [Serialize(5)] public TaskType type;
        [Serialize(6)] public Vector2 AO;
        [Serialize(7)] public float AORadius;
        [Serialize(8)] public float InitialEnemyStrength;
        [Serialize(9)] public float EnemyStrength;
        [Serialize(10)] public float StartingStrength;
        [Serialize(11)] public float MinimumTaskForceStrength;
        [Serialize(12)] public float TaskTimer;
        [Serialize(13)] public int WhichFleet = -1;
        [Serialize(14)] public bool IsToughNut;
        [Serialize(15)] public int NeededTroopStrength;

        [XmlIgnore] [JsonIgnore] private Planet TargetPlanet;
        [XmlIgnore] [JsonIgnore] private Empire Empire;
        [XmlIgnore] [JsonIgnore] private Array<Ship> TaskForce = new Array<Ship>();
        [XmlIgnore] [JsonIgnore] private Fleet Fleet => Empire.GetFleetsDict()[WhichFleet];

        //This file Refactored by Gretman

        public MilitaryTask()
        {
        }
        public MilitaryTask(AO ao)
        {
            AO = ao.Position;
            AORadius = ao.Radius;
            type = TaskType.CohesiveClearAreaOfEnemies;
            WhichFleet = ao.WhichFleet;
            IsCoreFleetTask = true;
            SetEmpire(ao.GetCoreFleet().Owner);
        }
        public MilitaryTask(Vector2 location, float radius, Array<Goal> GoalsToHold, Empire Owner)
        {
            this.type = MilitaryTask.TaskType.ClearAreaOfEnemies;
            this.AO = location;
            this.AORadius = radius;

            foreach (Goal g in GoalsToHold)
            {
                g.Held = true;
                this.HeldGoals.Add(g.guid);
            }

            this.EnemyStrength = Owner.GetGSAI().ThreatMatrix.PingRadarStr(location, radius, Owner);
            if (InitialEnemyStrength == 0)
                this.InitialEnemyStrength = EnemyStrength;

            this.MinimumTaskForceStrength = this.EnemyStrength *.75f;
            this.Empire = Owner;
        }

        public MilitaryTask(Planet target, Empire Owner)
        {
            this.type = MilitaryTask.TaskType.AssaultPlanet;
            this.TargetPlanet = target;
            this.TargetPlanetGuid = target.guid;
            this.AO = target.Position;
            this.AORadius = 35000f;
            this.Empire = Owner;
        }

        public MilitaryTask(Empire Owner)
        {
            this.Empire = Owner;
        }

        private void GetAvailableShips(AO area, Array<Ship> bombers, Array<Ship> combat, Array<Ship> troopShips, Array<Ship> utility)
        {
            foreach (Ship ship in area.GetOffensiveForcePool().OrderBy(ship => Vector2.Distance(ship.Center, area.Position) >= area.Radius*.5).ThenBy(str => str.BaseStrength))
            {
                if ((ship.shipData.Role == ShipData.RoleName.station || ship.shipData.Role == ShipData.RoleName.platform)
                    || !ship.BaseCanWarp
                    || ship.InCombat
                    || ship.fleet != null
                    || ship.Mothership != null
                    || Empire.GetGSAI().DefensiveCoordinator.DefensiveForcePool.Contains(ship)
                    || ship.GetAI().State !=  AIState.AwaitingOrders
                    || (ship.System!= null && ship.System.CombatInSystem)   )
                    continue;

                if(utility != null && (ship.InhibitionRadius > 0 || ship.hasOrdnanceTransporter || ship.hasRepairBeam || ship.HasRepairModule || ship.HasSupplyBays ))
                {
                    utility.Add(ship);
                }
                else if (bombers != null && ship.BombBays.Count > 0)
                {
                    bombers.Add(ship);
                }
                else if(troopShips !=null && (ship.TroopList.Count >0 && (ship.hasAssaultTransporter || ship.HasTroopBay || ship.GetShipData().Role == ShipData.RoleName.troop)))
                {
                    troopShips.Add(ship);
                }
                else if (combat != null && ship.BombBays.Count <= 0 && ship.BaseStrength > 0)
                {
                    combat.Add(ship);
                }
            }
        }

        private void DoToughNutRequisition()
        {
            float EnemyTroopStr = this.GetEnemyTroopStr();
            if (EnemyTroopStr < 100)
                EnemyTroopStr = 100;

            float EnemyShipStr = this.GetEnemyStrAtTarget();
            IOrderedEnumerable<AO> sorted =
                from ao in this.Empire.GetGSAI().AreasOfOperations
                where ao.GetCoreFleet().FleetTask == null || ao.GetCoreFleet().FleetTask.type != TaskType.AssaultPlanet
                orderby ao.GetOffensiveForcePool().Where(combat=> !combat.InCombat).Sum(strength => strength.BaseStrength) >= this.MinimumTaskForceStrength descending
                orderby Vector2.Distance(this.AO, ao.Position)
                select ao;

            if (sorted.Count<AO>() == 0)
                return;

            Array<Ship> Bombers = new Array<Ship>();
            Array<Ship> EverythingElse = new Array<Ship>();
            Array<Ship> TroopShips = new Array<Ship>();
            Array<Troop> Troops = new Array<Troop>();
            
            foreach (AO area in sorted)
            {
                this.GetAvailableShips(area, Bombers, EverythingElse, TroopShips, EverythingElse);
                foreach (Planet p in area.GetPlanets())
                {
                    if (p.RecentCombat || p.ParentSystem.combatTimer>0)
                        continue;

                    foreach (Troop t in p.TroopsHere)
                    {
                        if (t.GetOwner() != this.Empire)
                            continue;

                        Troops.Add(t);
                    }
                }
            }

            EverythingElse.AddRange(TroopShips);
            Array<Ship> TaskForce = new Array<Ship>();
            float strAdded = 0f;
            float troopStr = 0f;
            int numOfTroops = 0;

            foreach (Ship ship in EverythingElse)
            {
                if (strAdded < EnemyShipStr * 1.65f)
                    break;

                if (ship.HasTroopBay)
                {
                    foreach (ShipModule Hangar in ship.GetHangars())
                    {
                        troopStr += 10;
                        numOfTroops++;
                    }
                }
                TaskForce.Add(ship);
                strAdded += ship.GetStrength();
            }

            Array<Ship> BombTaskForce = new Array<Ship>();
            int numBombs = 0;
            foreach (Ship ship in Bombers)
            {
                if (numBombs >= 20 || BombTaskForce.Contains(ship))
                    continue;

                if (ship.HasTroopBay)
                {
                    foreach (ShipModule Hangar in ship.GetHangars())
                    {
                        troopStr += 10;
                        numOfTroops++;
                    }
                }
                BombTaskForce.Add(ship);
                numBombs += ship.BombBays.Count;
            }

            Array<Troop> PotentialTroops = new Array<Troop>();
            foreach (Troop t in Troops)
            {
                if (troopStr > EnemyTroopStr * 1.5f || numOfTroops > this.TargetPlanet.GetGroundLandingSpots() )
                    break;

                PotentialTroops.Add(t);
                troopStr += (float)t.Strength;
                numOfTroops++;
            }

            if (strAdded > EnemyShipStr * 1.65f)
            {
                if (this.TargetPlanet.Owner == null || this.TargetPlanet.Owner != null && !this.Empire.TryGetRelations(this.TargetPlanet.Owner, out Relationship rel))
                {
                    this.EndTask();
                    return;
                }

                if (this.Empire.GetRelations(this.TargetPlanet.Owner).PreparingForWar)
                {
                    this.Empire.GetGSAI().DeclareWarOn(this.TargetPlanet.Owner, this.Empire.GetRelations(this.TargetPlanet.Owner).PreparingForWarType);
                }

                AO ClosestAO = sorted.First<AO>();
                MilitaryTask assault = new MilitaryTask(this.Empire)
                {
                    AO = this.TargetPlanet.Position,
                    AORadius = 75000f,
                    type = MilitaryTask.TaskType.AssaultPlanet
                };

                ClosestAO.GetCoreFleet().Owner.GetGSAI().TasksToAdd.Add(assault);
                assault.WhichFleet = ClosestAO.WhichFleet;
                ClosestAO.GetCoreFleet().FleetTask = assault;
                assault.IsCoreFleetTask = true;
                assault.Step = 1;
                
                assault.TargetPlanet = this.TargetPlanet;
                ClosestAO.GetCoreFleet().TaskStep = 0;
                ClosestAO.GetCoreFleet().Name = "Doom Fleet";
                foreach (Ship ship in TaskForce)
                {
                    
                    ship.fleet?.RemoveShip(ship);

                    ship.GetAI().OrderQueue.Clear();
                    this.Empire.GetGSAI().DefensiveCoordinator.Remove(ship);
                    

                    ClosestAO.GetCoreFleet().AddShip(ship);
                }

                foreach (Troop t in PotentialTroops)
                {
                    if (t.GetPlanet() == null)
                        continue;

                    Ship launched = t.Launch();
                    ClosestAO.GetCoreFleet().AddShip(launched);
                }

                ClosestAO.GetCoreFleet().AutoArrange();
                if (Bombers.Count > 0 && numBombs > 6)
                {
                    MilitaryTask GlassPlanet = new MilitaryTask(this.Empire)
                    {
                        AO = this.TargetPlanet.Position,
                        AORadius = 75000f,
                        type = MilitaryTask.TaskType.GlassPlanet,
                        TargetPlanet = this.TargetPlanet,
                        WaitForCommand = true
                    };
                    
                    Fleet bomberFleet = new Fleet()
                    {
                        Owner = this.Empire
                    };

                    bomberFleet.Owner.GetGSAI().TasksToAdd.Add(GlassPlanet);
                    GlassPlanet.WhichFleet = this.Empire.GetUnusedKeyForFleet();
                    this.Empire.GetFleetsDict().Add(GlassPlanet.WhichFleet, bomberFleet);
                    bomberFleet.FleetTask = GlassPlanet;
                    bomberFleet.Name = "Bomber Fleet";

                    foreach (Ship ship in BombTaskForce)
                    {
                        ship.GetAI().OrderQueue.Clear();
                        this.Empire.GetGSAI().DefensiveCoordinator.Remove(ship);
                        ship.fleet?.RemoveShip(ship);

                        bomberFleet.AddShip(ship);
                    }
                    bomberFleet.AutoArrange();
                }
                this.Step = 1;
                this.Empire.GetGSAI().TaskList.QueuePendingRemoval(this);
            }
        }

        public void EndTask()
        {
            DebugInfoScreen.CanceledMtasksCount++;

            switch (this.type)
            {
                case TaskType.Exploration:
                    {
                        DebugInfoScreen.CanceledMtask1Count++;
                        DebugInfoScreen.CanceledMTask1Name = TaskType.Exploration.ToString();
                        break;
                    }
                case TaskType.AssaultPlanet:
                    {
                        DebugInfoScreen.CanceledMtask2Count++;
                        DebugInfoScreen.CanceledMTask2Name = TaskType.AssaultPlanet.ToString();
                        break;
                    }
                case TaskType.CohesiveClearAreaOfEnemies:
                    {
                        DebugInfoScreen.CanceledMtask3Count++;
                        DebugInfoScreen.CanceledMTask3Name = TaskType.CohesiveClearAreaOfEnemies.ToString();
                        break;
                    }
                    default:
                    {
                        DebugInfoScreen.CanceledMtask4Count++;
                        DebugInfoScreen.CanceledMTask4Name = this.type.ToString();
                        break;
                    }
            }

            if (this.Empire.isFaction)
            {
                this.FactionEndTask();
                return;
            }

            foreach (Guid goalGuid in this.HeldGoals)
            {
                foreach (Goal g in this.Empire.GetGSAI().Goals)
                {
                    if (g.guid != goalGuid)
                        continue;

                    g.Held = false;
                }
            }
            
            AO closestAO = Empire.GetGSAI().AreasOfOperations.FindMin(ao => AO.SqDist(ao.Position));
            
            if (closestAO == null)
            {
                if (this.WhichFleet != -1 && !this.Fleet.IsCoreFleet && this.Empire != Empire.Universe.player)
                {
                    foreach (Ship ship in this.Empire.GetFleetsDict()[this.WhichFleet].Ships)
                    {
                        this.Empire.ForcePoolAdd(ship);
                    }
                }
                return;
            }

            if (this.WhichFleet != -1)
            {
                if (this.IsCoreFleetTask)
                {
                    Fleet.FleetTask = null;
                }
                else
                {
                    if (!this.Empire.GetFleetsDict().ContainsKey(this.WhichFleet))
                    { //what the hell is this for? dictionary doesnt contain the key the foreach below would blow up. 
                        if (!Fleet.IsCoreFleet && this.Empire != Empire.Universe.player)
                        {
                            foreach (Ship ship in this.Empire.GetFleetsDict()[this.WhichFleet].Ships)
                            {
                                this.Empire.ForcePoolAdd(ship);
                            }
                        }
                        return;
                    }

                    for (int index = Fleet.Ships.Count - 1; index >= 0; index--)
                    {
                        Ship ship = Fleet.Ships[index];
                        ship.GetAI().OrderQueue.Clear();
                        ship.GetAI().State = AIState.AwaitingOrders;
                        Fleet.RemoveShip(ship);
                        ship.HyperspaceReturn();
                        ship.isSpooling = false;
                        if (ship.shipData.Role != ShipData.RoleName.troop)
                            ship.GetAI().OrderRebaseToNearest();
                        else
                        {

                            closestAO.AddShip(ship);
                            ship.GetAI().OrderResupplyNearest(false);
                        }
                        
                    }
                    this.TaskForce.Clear();
                    this.Empire.GetGSAI().UsedFleets.Remove(this.WhichFleet);
                    Fleet.Reset();
                }

                if (this.type == TaskType.Exploration)
                {
                    Array<Troop> toLaunch = new Array<Troop>();
                    for (int index = this.TargetPlanet.TroopsHere.Count - 1; index >= 0; index--)
                    {
                        Troop t = this.TargetPlanet.TroopsHere[index];
                        if (t.GetOwner() != this.Empire
                            || this.TargetPlanet.system.CombatInSystem
                            || t.AvailableAttackActions == 0
                            || t.MoveTimer > 0)
                            continue;

                        toLaunch.Add(t);
                    }

                    foreach (Troop t in toLaunch)
                    {
                        Ship troopship = t.Launch();
                        if (troopship == null)
                            continue;

                        troopship.GetAI().OrderRebaseToNearest();
                    }
                    toLaunch.Clear();
                }
            }
            this.Empire.GetGSAI().TaskList.QueuePendingRemoval(this);
        }

        public void EndTaskWithMove()
        {
            foreach (Guid goalGuid in this.HeldGoals)
            {
                foreach (Goal g in this.Empire.GetGSAI().Goals)
                {
                    if (g.guid != goalGuid)
                        continue;

                    g.Held = false;
                }
            }

            AO closestAO = Empire.GetGSAI().AreasOfOperations.FindMin(ao => AO.SqDist(ao.Position));
            if (closestAO == null)
            {
                if (  !this.IsCoreFleetTask && this.WhichFleet != -1 && this.Empire != EmpireManager.Player)
                {
                    foreach (Ship ship in this.Empire.GetFleetsDict()[this.WhichFleet].Ships)
                    {
                        this.Empire.ForcePoolAdd(ship);
                    }
                }
                return;
            }

            if (this.WhichFleet != -1)
            {
                if (this.IsCoreFleetTask)
                {
                    this.Empire.GetFleetsDict()[this.WhichFleet].FleetTask = null;
                    this.Empire.GetFleetsDict()[this.WhichFleet].MoveToDirectly(closestAO.Position, 0f, new Vector2(0f, -1f));
                }
                else
                {
                    foreach (Ship ship in this.Empire.GetFleetsDict()[this.WhichFleet].Ships)
                    {
                        Empire.GetFleetsDict()[WhichFleet].RemoveShip(ship);
                        closestAO.AddShip(ship);
                        closestAO.TurnsToRelax = 0;
                    }

                    this.TaskForce.Clear();
                    this.Empire.GetGSAI().UsedFleets.Remove(this.WhichFleet);
                    this.Empire.GetFleetsDict()[this.WhichFleet].Reset();
                }
            }
            this.Empire.GetGSAI().TaskList.QueuePendingRemoval(this);
        }

        public void Evaluate(Empire e)
        {  
            this.Empire = e;
            switch (this.type)
            {
                case MilitaryTask.TaskType.ClearAreaOfEnemies:
                    {
                        if      (Step == 0) this.RequisitionForces();
                        else if (Step == 1) this.ExecuteAndAssess();
                        break;
                    }
                case MilitaryTask.TaskType.AssaultPlanet:
                    {
                        if (Step == 0) this.RequisitionAssaultForces();
                        else
                        {
                            if (this.Empire.GetFleetsDict().TryGetValue(this.WhichFleet, out Fleet fleet))
                            {
                                if (fleet.Ships.Count != 0)
                                    break;
                            }

                            this.EndTask();
                        }
                        break;
                    }
                case MilitaryTask.TaskType.CorsairRaid:
                    {
                        if (this.Step != 0)
                            break;

                        this.Empire.GetFleetsDict()[1].Reset();
                        foreach (Ship shiptoadd in (Array<Ship>)this.Empire.GetShips())
                        {
                            if (shiptoadd.shipData.Role != ShipData.RoleName.platform)
                                this.Empire.GetFleetsDict()[1].AddShip(shiptoadd);
                        }

                        if (this.Empire.GetFleetsDict()[1].Ships.Count <= 0)
                            break;

                        this.Empire.GetFleetsDict()[1].Name = "Corsair Raiders";
                        this.Empire.GetFleetsDict()[1].AutoArrange();
                        this.Empire.GetFleetsDict()[1].FleetTask = this;
                        this.WhichFleet = 1;
                        this.Step = 1;
                        this.Empire.GetFleetsDict()[1].FormationWarpTo(this.TargetPlanet.Position, 0.0f, Vector2.Zero);
                        break;
                    }
                case MilitaryTask.TaskType.CohesiveClearAreaOfEnemies:
                    {
                        if      (Step == 0) this.RequisitionForces();
                        else if (Step == 1) this.ExecuteAndAssess();
                        break;
                    }
                case MilitaryTask.TaskType.Exploration:
                    {
                        if (this.Step == 0) this.RequisitionExplorationForce();
                        break;
                    }
                case MilitaryTask.TaskType.DefendSystem:
                    {
                        if      (Step == 0) this.RequisitionDefenseForce();
                        else if (Step == 1)
                        {
                            if (this.Empire.GetFleetsDict().ContainsKey(this.WhichFleet))
                            {
                                if (this.Empire.GetFleetsDict()[this.WhichFleet].Ships.Count != 0)
                                    break;
                            }
                            this.EndTask();
                        }
                        break;
                    }
                case MilitaryTask.TaskType.DefendClaim:
                    {
                        switch (this.Step)
                        {
                            case 0:
                                {
                                    if (this.TargetPlanet.Owner != null)
                                    {
                                        Empire.TryGetRelations(TargetPlanet.Owner, out Relationship rel);

                                        if (rel != null && (!rel.AtWar && !rel.PreparingForWar))
                                            this.EndTask();
                                    }
                                    if (!RequisitionClaimForce())
                                        return;
                                    this.Step = 1;
                                    break;
                                }                                
                            case 1:
                                {
                                    if (this.Empire.GetFleetsDict().ContainsKey(this.WhichFleet))
                                    {
                                        if (this.Empire.GetFleetsDict()[this.WhichFleet].Ships.Count == 0)
                                        {
                                            this.EndTask();
                                            return;
                                        }

                                        if (this.TargetPlanet.Owner != null) // &&(this.empire.GetFleetsDict().ContainsKey(this.WhichFleet)))
                                        {
                                        this.Empire.TryGetRelations(TargetPlanet.Owner, out Relationship rel);
                                            if (rel != null && (rel.AtWar || rel.PreparingForWar))
                                            {
                                                if (Vector2.Distance(this.Empire.GetFleetsDict()[this.WhichFleet].FindAveragePosition(), this.TargetPlanet.Position) < this.AORadius)
                                                    this.Step = 2;

                                                return;
                                            }
                                        }
                                    }
                                    else
                                        this.EndTask();

                                    if (this.TargetPlanet.Owner == null)
                                        return;

                                    this.EndTask();
                                    return;
                                }

                            case 2:
                                {
                                    if (this.Empire.GetFleetsDict().ContainsKey(this.WhichFleet))
                                    {
                                        if (this.Empire.GetFleetsDict()[this.WhichFleet].Ships.Count == 0)
                                        {
                                            this.EndTask();
                                            return;
                                        }
                                    }
                                    else
                                    {
                                        this.EndTask();
                                        return;
                                    }

                                    if (this.TargetPlanet.Owner == null)
                                    {
                                        this.EndTask();
                                        return;
                                    }

                                    Empire.TryGetRelations(TargetPlanet.Owner, out Relationship rel);
                                    if (rel != null && !(rel.AtWar || rel.PreparingForWar))
                                        this.EndTask();

                                    if (this.TargetPlanet.Owner == null || this.TargetPlanet.Owner == this.Empire)
                                        this.EndTask();

                                    return;
                                }
                            default:
                                return;
                        }
                        break;
                    }
            }
        }

        private void ExecuteAndAssess()
        {
            if (this.WhichFleet == -1)
            {
                this.Step = 0;
                return;
            }

            if (this.type == TaskType.Exploration ||this.type ==TaskType.AssaultPlanet)
            {
                float groundstrength = this.GetTargetPlanet().GetGroundStrengthOther(this.Empire);
                float ourGroundStrength = this.GetTargetPlanet().GetGroundStrength(this.Empire);

                if (ourGroundStrength > 0)
                {
                    if (this.type == TaskType.Exploration)
                    {
                        Planet p = this.GetTargetPlanet();
                        if (p.BuildingList.Where(relic => !string.IsNullOrEmpty(relic.EventTriggerUID)).Count() > 0)
                            return;
                    }
                    else if (this.type == TaskType.AssaultPlanet)
                    {
                        if (groundstrength > 0)
                            return;
                    }
                }
            } 
            
            if (this.Empire.GetFleetsDict()[this.WhichFleet].FleetTask == null )
            {
                this.EndTask();
                return;
            }
            
            float currentStrength = 0f;
            foreach (Ship ship in this.Empire.GetFleetsDict()[this.WhichFleet].Ships)
            {
                if (!ship.Active || ship.InCombat && this.Step < 1 || ship.GetAI().State == AIState.Scrap)
                {
                    this.Empire.GetFleetsDict()[this.WhichFleet].Ships.QueuePendingRemoval(ship);
                    if (ship.Active && ship.GetAI().State != AIState.Scrap)
                    {
                        if (ship.fleet != null)
                            this.Empire.GetFleetsDict()[this.WhichFleet].Ships.QueuePendingRemoval(ship);
                        
                        this.Empire.ForcePoolAdd(ship);
                    }
                    else if (ship.GetAI().State == AIState.Scrap)
                    {
                        if (ship.fleet != null)
                            this.Empire.GetFleetsDict()[this.WhichFleet].Ships.QueuePendingRemoval(ship);
                    }
                }
                else
                {
                    currentStrength += ship.GetStrength();
                }
            }

            this.Empire.GetFleetsDict()[this.WhichFleet].Ships.ApplyPendingRemovals();
            float currentEnemyStrength = 0f;

            foreach (KeyValuePair<Guid, ThreatMatrix.Pin> pin in this.Empire.GetGSAI().ThreatMatrix.Pins)
            {
                if (Vector2.Distance(this.AO, pin.Value.Position) >= this.AORadius || pin.Value.Ship == null)
                    continue;

                Empire pinEmp = EmpireManager.GetEmpireByName(pin.Value.EmpireName);

                if (pinEmp == this.Empire || !pinEmp.isFaction && !this.Empire.GetRelations(pinEmp).AtWar )
                    continue;

                currentEnemyStrength += pin.Value.Strength;
            }

            if (currentStrength < 0.15f * this.StartingStrength && currentEnemyStrength > currentStrength)
            {
                this.EndTask();
                return;
            }

            if (currentEnemyStrength == 0f || currentStrength == 0f)
                this.EndTask();
        }

        public void FactionEndTask()
        {
            if (this.WhichFleet != -1)
            {
                if (!this.IsCoreFleetTask)
                {
                    if (!this.Empire.GetFleetsDict().ContainsKey(this.WhichFleet))
                        return;

                    foreach (Ship ship in this.Empire.GetFleetsDict()[this.WhichFleet].Ships)
                    {
                        ship.GetAI().OrderQueue.Clear();
                        ship.GetAI().State = AIState.AwaitingOrders;
                        Empire.GetFleetsDict()[WhichFleet].RemoveShip(ship);
                        ship.HyperspaceReturn();
                        ship.isSpooling = false;

                        if (ship.shipData.Role != ShipData.RoleName.troop)
                        {
                            ship.GetAI().OrderResupplyNearest(false);
                        }
                        else
                        {
                            ship.GetAI().OrderRebaseToNearest();
                        }
                    }
                    this.TaskForce.Clear();
                    this.Empire.GetGSAI().UsedFleets.Remove(this.WhichFleet);
                    this.Empire.GetFleetsDict()[this.WhichFleet].Reset();
                }

                if (this.type == MilitaryTask.TaskType.Exploration)
                {
                    Array<Troop> toLaunch = new Array<Troop>();
                    foreach (Troop t in this.TargetPlanet.TroopsHere)
                    {
                        if (t.GetOwner() != this.Empire)
                            continue;

                        toLaunch.Add(t);
                    }

                    foreach (Troop t in toLaunch)
                    {
                        Ship troopship = t.Launch();

                        if (troopship == null)
                            continue;

                        troopship.GetAI().OrderRebaseToNearest();
                    }
                    toLaunch.Clear();
                }
            }
            this.Empire.GetGSAI().TaskList.QueuePendingRemoval(this);
        }

        private float GetEnemyStrAtTarget()
        {
            return GetEnemyStrAtTarget(1000);
        }

        private float GetEnemyStrAtTarget(float standardMinimum)
        {		                        
            float MinimumEscortStrength = 1000;
            if (this.TargetPlanet.Owner == null)
                return standardMinimum;

            this.TargetPlanet.Owner.GetGSAI().DefensiveCoordinator.DefenseDict.TryGetValue(this.TargetPlanet.ParentSystem, out SystemCommander scom);
            float importance = 1;

            if (scom != null)
                importance = 1 + scom.RankImportance * .01f;

            float distance = 250000 * importance;            
            MinimumEscortStrength = this.Empire.GetGSAI().ThreatMatrix.PingRadarStr(this.AO, distance,this.Empire);
            standardMinimum *= importance;
            if (MinimumEscortStrength < standardMinimum)
                MinimumEscortStrength = standardMinimum;

            return MinimumEscortStrength;
        }

        private float GetEnemyTroopStr()
        {
            return this.TargetPlanet.GetGroundStrengthOther(this.Empire);
        }

        public Planet GetTargetPlanet()
        {
            return this.TargetPlanet;
        }
        private Array<Troop> GetTroopsOnPlanets(Array<Troop> potentialTroops, Vector2 rallyPoint)
        {            
            var defenseDict = Empire.GetGSAI().DefensiveCoordinator.DefenseDict;
            var troopSystems = Empire.GetOwnedSystems().OrderBy(troopSource => defenseDict[troopSource].RankImportance)
                .ThenBy(dist => dist.Position.SqDist(rallyPoint));
            foreach(SolarSystem system in troopSystems)
            {
                int rank = (int)defenseDict[system].RankImportance;
                foreach (Planet planet in system.PlanetList)
                {                    
                    if (planet.Owner != Empire) continue;
                    if (planet.RecentCombat) continue;
                    int extra = rank;
                    foreach(Troop troop in planet.TroopsHere)
                    {
                        if (troop.GetOwner() != Empire) continue;
                        extra--;

                        if (extra < 0)
                            potentialTroops.Add(troop);
                    }
                }
                if (potentialTroops.Count > 100)
                    break;
            }

            return potentialTroops;
        }
        private int CountShipTroopAndStrength(Array<Ship> potentialAssaultShips,  out float ourStrength)
        {
            ourStrength = 0;
            int troopCount = 0;
            foreach (Ship ship in potentialAssaultShips)
            {
                int hangars = 0;
                foreach (ShipModule hangar in ship.GetHangars())
                {
                    if (hangar.IsTroopBay)
                        hangars++;
                }

                foreach (Troop t in ship.TroopList)
                {
                    ourStrength += t.Strength;
                    troopCount++;
                    hangars--;
                    if (hangars <= 0)
                        break;
                }
            }
            return troopCount;
        }
        private Array<Ship> GetShipsFromDefense(float tfstrength, float MinimumEscortStrength)
        {
            Array<Ship> elTaskForce = new Array<Ship>();
            if (!this.Empire.isFaction && this.Empire.data.DiplomaticPersonality.Territorialism < 50 && tfstrength < MinimumEscortStrength)
            {
                if (!this.IsCoreFleetTask)
                    foreach (var kv in this.Empire.GetGSAI().DefensiveCoordinator.DefenseDict
                        .OrderByDescending(system => system.Key.CombatInSystem ? 1 : 2 * system.Key.Position.SqDist(TargetPlanet.Position))
                        .ThenByDescending(ship => (ship.Value.GetOurStrength() - ship.Value.IdealShipStrength) < 1000)


                    )
                    {
                        var ships = kv.Value.GetShipList;

                        for (int index = 0; index < ships.Length; index++)
                        {
                            Ship ship = ships[index];
                            if (ship.GetAI().BadGuysNear || ship.fleet != null || tfstrength >= MinimumEscortStrength ||
                                ship.GetStrength() <= 0f
                                || ship.shipData.Role == ShipData.RoleName.troop || ship.hasAssaultTransporter ||
                                ship.HasTroopBay
                                || ship.Mothership != null
                            )
                                continue;

                            tfstrength = tfstrength + ship.GetStrength();
                            elTaskForce.Add(ship);
                            this.Empire.GetGSAI().DefensiveCoordinator.Remove(ship);
                        }
                    }
            }
            return elTaskForce;
        }

        private Array<Ship> AddShipsLimited(Array<Ship> shipList, float strengthLimit, float tfStrength, out float currentStrength)
        {
            Array<Ship> added = new Array<Ship>();
            foreach (Ship ship in shipList)
            {               
                tfStrength += ship.GetStrength();
                added.Add(ship);
                if (tfStrength > strengthLimit)
                    break;
            }
            currentStrength = tfStrength;
            return added;
        }
        private bool DeclareWar()
        {
            if (this.Empire.GetRelations(this.TargetPlanet.Owner).PreparingForWar)
            {
                this.Empire.GetGSAI().DeclareWarOn(this.TargetPlanet.Owner, this.Empire.GetRelations(this.TargetPlanet.Owner).PreparingForWarType);
                return true;
            }
            return false;
        }
        private void CreateFleet(Array<Ship> elTaskForce, Array<Ship> potentialAssaultShips, 
            Array<Troop> potentialTroops,float EnemyTroopStrength, AO closestAO,  Array<Ship> potentialBombers = null, string fleetName = "Invasion Fleet")
        {
   
            
            int landingSpots = this.TargetPlanet.GetGroundLandingSpots();
            if (potentialBombers != null)
            {
                int bombs = 0;
                foreach (Ship ship in potentialBombers)
                {
                    bombs += ship.BombBays.Count;

                    if (elTaskForce.Contains(ship))
                        continue;

                    elTaskForce.Add(ship);
                    if (bombs > 25 - landingSpots)
                        break;
                }
            }
           
            
            Fleet newFleet = new Fleet()
            {
                Owner = this.Empire,
                Name = fleetName
            };

            int FleetNum = FindFleetNumber();
            float ForceStrength = 0f;

            foreach (Ship ship in potentialAssaultShips)
            {
                if (ForceStrength > EnemyTroopStrength * 1.25f )
                    break;

                newFleet.AddShip(ship);
                ForceStrength += ship.PlanetAssaultStrength;
                this.Empire.GetGSAI().DefensiveCoordinator.Remove(ship);
                
            }

            foreach (Troop t in potentialTroops)
            {
                if (ForceStrength > EnemyTroopStrength * 1.25f )
                    break;

                if (t.GetPlanet() != null && t.GetPlanet().ParentSystem.combatTimer <= 0 && !t.GetPlanet().RecentCombat)
                {
                    if (t.GetOwner() != null)
                    {
                        newFleet.AddShip(t.Launch());
                        ForceStrength += t.Strength;
                        
                    }
                }
            }

            Empire.GetFleetsDict()[FleetNum] = newFleet;
            Empire.GetGSAI().UsedFleets.Add(FleetNum);
            WhichFleet = FleetNum;
            newFleet.FleetTask = this;
            foreach (Ship ship in elTaskForce)
            {
                newFleet.AddShip(ship);
                ship.GetAI().OrderQueue.Clear();
                ship.GetAI().State = AIState.AwaitingOrders;
               
                closestAO.RemoveShip(ship);
                if (ship.GetAI().SystemToDefend != null)
                    this.Empire.GetGSAI().DefensiveCoordinator.Remove(ship);
            }
            newFleet.AutoArrange();
            Step = 1;


        }

        private void RequisitionAssaultForces()
        {

            if (this.TargetPlanet.Owner == null || !Empire.IsEmpireAttackable(TargetPlanet.Owner))
            {
                this.EndTask();
                return;
            }
            if (this.IsToughNut)
            {
                this.DoToughNutRequisition();
                return;
            }
            int landingSpots = this.TargetPlanet.GetGroundLandingSpots();
            AO closestAO = FindClosestAO();

            if (closestAO == null || closestAO.GetOffensiveForcePool().Count < 5)
                return;

            if (this.Empire.GetRelations(this.TargetPlanet.Owner).Treaty_Peace)
            {
                this.Empire.GetRelations(this.TargetPlanet.Owner).PreparingForWar = false;
                this.EndTask();
                return;
            }

            float EnemyTroopStrength = this.TargetPlanet.GetGroundStrengthOther(this.Empire) ;

            if (EnemyTroopStrength < 100f)
                EnemyTroopStrength = 100f;
            
            Array<Ship> potentialAssaultShips = new Array<Ship>();
            Array<Troop> potentialTroops = new Array<Troop>();
            Array<Ship> potentialCombatShips = new Array<Ship>();
            Array<Ship> potentialBombers = new Array<Ship>();
            Array<Ship> potentialUtilityShips = new Array<Ship>();
            this.GetAvailableShips(closestAO, potentialBombers, potentialCombatShips, potentialAssaultShips, potentialUtilityShips);
            Planet rallypoint = Empire.RallyPoints?.FindMin(p => p.Position.SqDist(this.AO));
            if (rallypoint == null)
                return;

            potentialTroops = GetTroopsOnPlanets(potentialTroops,rallypoint.Position);
            int troopCount = potentialTroops.Count();
            troopCount += CountShipTroopAndStrength(potentialAssaultShips, out float ourAvailableStrength);

            bool GoodToGo = false;

            foreach (Troop t in potentialTroops)
            ourAvailableStrength = ourAvailableStrength + t.Strength;

            float MinimumEscortStrength = this.GetEnemyStrAtTarget();

            // I'm unsure on ball-park figures for ship strengths. Given it used to build up to 1500, sticking flat +300 on seems a good start
            //updated. Now it will use 1/10th of the current military strength escort strength needed is under 1000
            //well thats too much. 1/10th can be huge. moved it into the getenemy strength logic with some adjustments. now it looks at the enemy empires importance of the planet. 
            //sort of cheating but as it would be much the same calculation as the attacking empire would use.... hrmm.
            // actually i think the raw importance value could be used to create an importance for that planet. interesting... that could be very useful in many areas. 

            this.MinimumTaskForceStrength = MinimumEscortStrength;
            BatchRemovalCollection<Ship> elTaskForce = new BatchRemovalCollection<Ship>();
            float tfstrength = 0f;
            elTaskForce.AddRange(AddShipsLimited(potentialCombatShips, MinimumEscortStrength, tfstrength, out float tempStrength));
            tfstrength += tempStrength;

            elTaskForce.AddRange(AddShipsLimited(potentialUtilityShips, MinimumEscortStrength * 1.5f, tfstrength, out  tempStrength));
            tfstrength += tempStrength;

            elTaskForce.AddRange(GetShipsFromDefense(tfstrength, MinimumEscortStrength));
            if (tfstrength >= MinimumTaskForceStrength)
            {
                if (ourAvailableStrength >= EnemyTroopStrength && landingSpots > 8 && troopCount >= 10 )
                {
                    DeclareWar();
                    CreateFleet(elTaskForce, potentialAssaultShips, potentialTroops, EnemyTroopStrength, closestAO);
                    return;
                }
                if (landingSpots < 10 && potentialBombers.Count > 10 -landingSpots)
                {
                    DeclareWar();
                    CreateFleet(elTaskForce, potentialAssaultShips, potentialTroops, EnemyTroopStrength, closestAO, potentialBombers);
                    return;
                }
                if (landingSpots >0 )
                {
                    DeclareWar();
                    CreateFleet(elTaskForce, potentialAssaultShips, potentialTroops, EnemyTroopStrength * 2, closestAO);
                    return;
                }
            }
            else
            if (tfstrength <= MinimumTaskForceStrength)
            {
                if (TargetPlanet.Owner == null || TargetPlanet.Owner != null && !Empire.TryGetRelations(TargetPlanet.Owner, out Relationship rel2))
                {
                    EndTask();
                    return;
                }

                Fleet closestCoreFleet = closestAO.GetCoreFleet();
                if (closestCoreFleet.FleetTask != null && closestCoreFleet.GetStrength() > MinimumTaskForceStrength)
                {
                    var clearArea = new MilitaryTask(closestCoreFleet.Owner)
                    {
                        AO       = TargetPlanet.Position,
                        AORadius = 75000f,
                        type     = TaskType.ClearAreaOfEnemies
                    };

                    closestCoreFleet.Owner.GetGSAI().TasksToAdd.Add(clearArea);
                    clearArea.WhichFleet       = closestAO.WhichFleet;
                    closestCoreFleet.FleetTask = clearArea;
                    clearArea.IsCoreFleetTask  = true;
                    closestCoreFleet.TaskStep  = 1;
                    clearArea.Step             = 1;

                    if (Empire.GetRelations(TargetPlanet.Owner).PreparingForWar)
                        Empire.GetGSAI().DeclareWarOn(TargetPlanet.Owner, Empire.GetRelations(TargetPlanet.Owner).PreparingForWarType);
                }
                return;
            }
            if (landingSpots < 10) IsToughNut = true;

            NeededTroopStrength = (int)(EnemyTroopStrength - ourAvailableStrength);
        }       
        
        //added by gremlin req claim forces
        private bool RequisitionClaimForce()
        {
            IOrderedEnumerable<AO> sorted =
                from ao in this.Empire.GetGSAI().AreasOfOperations
                orderby ao.GetOffensiveForcePool().Sum(strength => strength.GetStrength()) >= this.MinimumTaskForceStrength
                orderby Vector2.Distance(this.AO, ao.Position)
                select ao;

            if (!sorted.Any())
                return false;

            AO ClosestAO = sorted.First<AO>();
            float tfstrength = 0f;
            BatchRemovalCollection<Ship> elTaskForce = new BatchRemovalCollection<Ship>();
            int shipCount = 0;
            float strengthNeeded = this.EnemyStrength;

            if (strengthNeeded <1)
                strengthNeeded = this.Empire.GetGSAI().ThreatMatrix.PingRadarStr(this.TargetPlanet.Position, 125000, this.Empire);

            if (strengthNeeded < this.Empire.currentMilitaryStrength * .02f)
                strengthNeeded = this.Empire.currentMilitaryStrength * .02f;
            foreach (Ship ship in ClosestAO.GetOffensiveForcePool().OrderBy(str=>str.GetStrength()))
            {
                if (shipCount >= 3 && tfstrength >= strengthNeeded)
                    break;

                if (ship.GetStrength() <= 0f || ship.InCombat || ship.fleet != null)
                    continue;

                shipCount++;
                if (elTaskForce.Contains(ship))
                     Log.Error("eltaskforce already contains ship");
                elTaskForce.Add(ship);
                tfstrength += ship.GetStrength();
            }

            if (shipCount < 3 && tfstrength < strengthNeeded)
                return false;

            this.TaskForce = elTaskForce;
            this.StartingStrength = tfstrength;
            int FleetNum = FindFleetNumber();
            Fleet newFleet = new Fleet();
            foreach (Ship ship in this.TaskForce)
            {
                newFleet.AddShip(ship);
            }

            newFleet.Owner = this.Empire;
            newFleet.Name = "Scout Fleet";
            newFleet.AutoArrange();
            this.Empire.GetFleetsDict()[FleetNum] = newFleet;
            this.Empire.GetGSAI().UsedFleets.Add(FleetNum);
            this.WhichFleet = FleetNum;
            newFleet.FleetTask = this;

            foreach (Ship ship in this.TaskForce)
            {
                ClosestAO.RemoveShip(ship);               
                this.Empire.GetGSAI().DefensiveCoordinator.Remove(ship);
                if(ship.fleet == null  || ship.fleet.IsCoreFleet)
                {
                    Log.Error("ship fleet became null");
                }
            }
            return true;
        }
        private AO FindClosestAO()
        {
            AO closestAO = Empire.GetGSAI().AreasOfOperations?.FindMin(ao => ao.Position.SqDist(AO));

            if (closestAO == null)
                closestAO = Empire.GetGSAI().AreasOfOperations?.FindMax(ao => ao.GetOffensiveForcePool().Count);
  
            return closestAO;
        }
        private void RequisitionDefenseForce()
        {
            float forcePoolStr = this.Empire.GetForcePoolStrength();
            float tfstrength = 0f;
            BatchRemovalCollection<Ship> elTaskForce = new BatchRemovalCollection<Ship>();
            
            foreach (Ship ship in this.Empire.GetForcePool().OrderBy(strength=> strength.GetStrength()))
            {
                if (ship.fleet != null)
                    continue;

                if (tfstrength >= forcePoolStr / 2f)
                    break;

                if (ship.GetStrength() <= 0f || ship.InCombat)
                    continue;

                elTaskForce.Add(ship);
                tfstrength = tfstrength + ship.GetStrength();
            }

            this.TaskForce = elTaskForce;
            this.StartingStrength = tfstrength;
            int FleetNum = FindFleetNumber();
            Fleet newFleet = new Fleet();

            foreach (Ship ship in this.TaskForce)
            {
                newFleet.AddShip(ship);
            }

            newFleet.Owner = this.Empire;
            newFleet.Name = "Defensive Fleet";
            newFleet.AutoArrange();
            this.Empire.GetFleetsDict()[FleetNum] = newFleet;
            this.Empire.GetGSAI().UsedFleets.Add(FleetNum);
            this.WhichFleet = FleetNum;
            newFleet.FleetTask = this;

            foreach (Ship ship in this.TaskForce)
            {
                this.Empire.ForcePoolRemove(ship);
            }
            this.Step = 1;
        }

        //added by gremlin Req Exploration forces
        private void RequisitionExplorationForce()
        {
            AO closestAO = FindClosestAO();
            
            if (closestAO == null || closestAO.GetOffensiveForcePool().Count < 3)
                return;

            this.EnemyStrength = 0f;
            foreach (KeyValuePair<Guid, ThreatMatrix.Pin> pin in this.Empire.GetGSAI().ThreatMatrix.Pins)
            {
                if (Vector2.Distance(this.AO, pin.Value.Position) >= this.AORadius || EmpireManager.GetEmpireByName(pin.Value.EmpireName) == this.Empire)
                    continue;

                this.EnemyStrength += pin.Value.Strength;
            }

            this.MinimumTaskForceStrength = this.EnemyStrength + 0.35f * this.EnemyStrength;

            if (this.MinimumTaskForceStrength == 0f)
                this.MinimumTaskForceStrength = closestAO.GetOffensiveForcePool().Sum(strength => strength.GetStrength()) * .2f;

            foreach (var entry in this.Empire.AllRelations)
            {
                if (!entry.Value.AtWar || entry.Key.isFaction || this.MinimumTaskForceStrength <= closestAO.GetOffensiveForcePool().Sum(strength => strength.GetStrength()) * .5f)
                    continue;

                this.EndTask();
                return;
            }
            Array<Troop> potentialTroops = new Array<Troop>();
            potentialTroops = GetTroopsOnPlanets(potentialTroops, closestAO.GetPlanet().Position);
            if (potentialTroops.Count < 4)
            {
                NeededTroopStrength = 20;
                foreach (Troop troop in potentialTroops)
                {
                    NeededTroopStrength -= (int)troop.Strength ;
                    if (NeededTroopStrength > 0)
                        return;
                }
                
                NeededTroopStrength = 0;
            }

            Array<Ship> potentialAssaultShips = new Array<Ship>();
            Array<Ship> potentialCombatShips = new Array<Ship>();
            Array<Ship> potentialBombers = new Array<Ship>();
            Array<Ship> potentialUtilityShips = new Array<Ship>();
            this.GetAvailableShips(closestAO, potentialBombers, potentialCombatShips, potentialAssaultShips, potentialUtilityShips);

            Array<Planet> shipyards = closestAO.GetPlanets().Intersect(Empire.RallyPoints).ToArrayList();

            Planet rallyPoint = shipyards.FindMin(p => p.Position.SqDist(AO));

            if (rallyPoint == null)
            {
                EndTask();
                return;
            }

            float ourAvailableStrength = 0f;
            CountShipTroopAndStrength(potentialAssaultShips, out float troopStrength);
            ourAvailableStrength += troopStrength;
            

            foreach (Troop t in potentialTroops)
                ourAvailableStrength = ourAvailableStrength + (float)t.Strength;


            float tfstrength = 0f;
            Array<Ship> elTaskForce = AddShipsLimited(potentialCombatShips, MinimumTaskForceStrength, tfstrength, out tfstrength);

            if (tfstrength >= this.MinimumTaskForceStrength && ourAvailableStrength >= 20f)
            {
                StartingStrength = tfstrength;
                CreateFleet(elTaskForce, potentialAssaultShips, potentialTroops, EnemyStrength, closestAO,null, "Exploration Force");
                
            }
            

        }

        private void RequisitionForces()
        {
            IOrderedEnumerable<AO> sorted = this.Empire.GetGSAI().AreasOfOperations
                .OrderByDescending(ao => ao.GetOffensiveForcePool().Sum(strength => strength.GetStrength()) >= this.MinimumTaskForceStrength)
                .ThenBy(ao => Vector2.Distance(this.AO, ao.Position));

            if (sorted.Count<AO>() == 0)
                return;

            AO ClosestAO = sorted.First<AO>();
            this.EnemyStrength = this.Empire.GetGSAI().ThreatMatrix.PingRadarStr(this.AO, this.AORadius,this.Empire);

            this.MinimumTaskForceStrength = this.EnemyStrength;
            if (this.MinimumTaskForceStrength == 0f)
            {
                this.EndTask();
                return;
            }

            if (ClosestAO.GetCoreFleet().FleetTask == null && ClosestAO.GetCoreFleet().GetStrength() > this.MinimumTaskForceStrength)
            {
                this.WhichFleet = ClosestAO.WhichFleet;
                ClosestAO.GetCoreFleet().FleetTask = this;
                ClosestAO.GetCoreFleet().TaskStep = 1;
                this.IsCoreFleetTask = true;
                this.Step = 1;
            }
        }

        public void SetEmpire(Empire e)
        {
            this.Empire = e;
        }

        public void SetTargetPlanet(Planet p)
        {
            this.TargetPlanet = p;
            this.TargetPlanetGuid = p.guid;
        }

        public enum TaskType
        {
            ClearAreaOfEnemies,
            Resupply,
            AssaultPlanet,
            CorsairRaid,
            CohesiveClearAreaOfEnemies,
            Exploration,
            DefendSystem,
            DefendClaim,
            DefendPostInvasion,
            GlassPlanet
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~MilitaryTask() { Dispose(false); }

        private void Dispose(bool disposing)
        {
            TaskForce = null;  //Dispose(ref TaskForce);
        }

        private int FindFleetNumber()
        {
            for (int i = 1; i < 10; i++)
            {
                if (this.Empire.GetGSAI().UsedFleets.Contains(i))
                    continue;

                return i;
            }
            return -1;
        }
    }
}