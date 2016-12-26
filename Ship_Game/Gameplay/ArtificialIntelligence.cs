using Microsoft.Xna.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Algorithms;
using Ship_Game.Commands;

namespace Ship_Game.Gameplay
{
	public sealed class ArtificialIntelligence : IDisposable
	{       
        public bool UseSensorsForTargets =true;
        public bool ClearOrdersNext;
		private Vector2 aiNewDir;
		public static UniverseScreen universeScreen;
		public Ship Owner;
		public GameplayObject Target;
		public AIState State = AIState.AwaitingOrders;
		public CombatState CombatState = CombatState.AttackRuns;
		public Guid OrbitTargetGuid;
		public CombatAI CombatAI = new CombatAI();
		public BatchRemovalCollection<ShipWeight> NearbyShips = new BatchRemovalCollection<ShipWeight>();
        public BatchRemovalCollection<Ship> PotentialTargets = new BatchRemovalCollection<Ship>();
		public Planet resupplyTarget;
		public Planet start;
		public Planet end;
		private SolarSystem SystemToPatrol;
		private Array<Planet> PatrolRoute = new Array<Planet>();
		private int stopNumber;
		private Planet PatrolTarget;
		public SolarSystem SystemToDefend;
		public Guid SystemToDefendGuid;
        public SolarSystem ExplorationTarget;
		public Ship EscortTarget;
		public Guid EscortTargetGuid;
        private float findNewPosTimer;
		private Goal ColonizeGoal;
		private Planet awaitClosest;
        private Vector2 OrbitPos;
		private float DistanceLast;
		public bool HasPriorityOrder;
        public int GotoStep;
		private bool AttackRunStarted;
		private float AttackRunAngle;
		private float runTimer;
		private Vector2 AttackVector = Vector2.Zero;
		public AIState DefaultAIState = AIState.AwaitingOrders;
		private FleetDataNode node;
		public bool HadPO;
		private float ScanForThreatTimer;
		public Vector2 MovePosition;
		private float DesiredFacing;
		private Vector2 FinalFacingVector;
        public SafeQueue<ShipGoal> OrderQueue = new SafeQueue<ShipGoal>();
		public Queue<Vector2> ActiveWayPoints = new Queue<Vector2>();
		public Planet ExterminationTarget;
		public string FoodOrProd;
        public bool hasPriorityTarget;
		public bool Intercepting;
		public Array<Ship> TargetQueue = new Array<Ship>();
		private float TriggerDelay = 0;
		public Guid TargetGuid;
        public Planet ColonizeTarget;
		public bool ReadyToWarp = true;
		public Planet OrbitTarget;
		private float OrbitalAngle = RandomMath.RandomBetween(0f, 360f);
		public bool IgnoreCombat;
		public BatchRemovalCollection<Ship> FriendliesNearby = new BatchRemovalCollection<Ship>();
		public bool BadGuysNear;        
        public bool troopsout = false;
        private float UtilityModuleCheckTimer;
        public object WayPointLocker;
        public Ship TargetShip;
        private bool disposed;
        public Array<Projectile> TrackProjectiles = new Array<Projectile>();
        private static float[] DmgLevel = { 0.25f, 0.85f, 0.65f, 0.45f, 0.45f, 0.45f, 0.0f };  //fbedard: dmg level for repair
                
		public ArtificialIntelligence(Ship owner)
		{
			Owner = owner;
			State = AIState.AwaitingOrders;
            WayPointLocker = new object();
		}
     
        private void AwaitOrders(float elapsedTime)
		{
            if(State != AIState.Resupply)
            HasPriorityOrder = false;            
			if (awaitClosest != null)
			{
				DoOrbit(awaitClosest, elapsedTime);
			}
			else if (Owner.System== null)
			{
				if(SystemToDefend != null)
                {
                    DoOrbit(SystemToDefend.PlanetList[0], elapsedTime);
                    awaitClosest = SystemToDefend.PlanetList[0];
                    return;
                }                
                var sortedList = 
					from solarsystem in Owner.loyalty.GetOwnedSystems()                    
					orderby Vector2.Distance(Owner.Center, solarsystem.Position)
					select solarsystem;
                if (Owner.loyalty.isFaction)
                {
                    sortedList =
                        from solarsystem in Ship.universeScreen.SolarSystemDict.Values
                        orderby Vector2.Distance(Owner.Center, solarsystem.Position) < 800000
                        , Owner.loyalty.GetOwnedSystems().Contains(solarsystem)
                        select solarsystem;
                       
                }
                else
				if (sortedList.Count<SolarSystem>() > 0)
				{
					DoOrbit(sortedList.First<SolarSystem>().PlanetList[0], elapsedTime);
					awaitClosest = sortedList.First<SolarSystem>().PlanetList[0];
					return;
				}
			}
			else
			{
                var closestD = 999999f;
                var closestUS =false;
				foreach (Planet p in Owner.System.PlanetList)
				{
                    if (awaitClosest == null)
                        awaitClosest = p;
                    var us = false;
                    if(Owner.loyalty.isFaction)
                        us = p.Owner != null || p.habitable;
                    else
                        us = p.Owner == Owner.loyalty;
                    if (closestUS && !us)
                        continue;
                    float Distance = Vector2.Distance(Owner.Center, p.Position);
                    if (us == closestUS)
                        if (Distance >= closestD)
                            continue;
				    closestUS = us;
                    closestD = Distance;
                    awaitClosest = p;
				}
                
			}
		}

	    private void AwaitOrdersPlayer(float elapsedTime)
	    {
	        HasPriorityOrder = false;
	        if (Owner.InCombatTimer > elapsedTime * -5 && ScanForThreatTimer < 2 - elapsedTime * 5)
	            ScanForThreatTimer = 0;
	        if (EscortTarget != null)
	        {
	            State = AIState.Escort;
	            return;
	        }
	        if (!HadPO)
	        {
	            if (SystemToDefend != null)
	            {
	                DoOrbit(SystemToDefend.PlanetList[0], elapsedTime);
	                awaitClosest = SystemToDefend.PlanetList[0]; //@TASK change this to use the highest value planet from the sys def ai. 
	                return;
	            }
	            if (awaitClosest != null)
	            {
	                DoOrbit(awaitClosest, elapsedTime);
	                return;
	            }
	            awaitClosest =
	                Owner.loyalty.GetGSAI()
	                    .GetKnownPlanets()
	                    .FindMin(
	                        planet => planet.Position.Distance(Owner.Center) + (Owner.loyalty != planet.Owner ? 300000 : 0));
	            return;
	        }	        
            if (Owner.System?.OwnerList.Contains(Owner.loyalty) ?? false)
	        {
	            HadPO = false;
	            return;
	        }
	        Stop(elapsedTime);
	    }

	    private void Colonize(Planet TargetPlanet)
		{
			if (Owner.Center.OutsideRadius(TargetPlanet.Position, 2000f))
			{
				OrderQueue.RemoveFirst();
				OrderColonization(TargetPlanet);
				State = AIState.Colonize;
				return;
			}
			if (TargetPlanet.Owner != null || !TargetPlanet.habitable)
			{                
                if (ColonizeGoal != null)
				{					
                    ColonizeGoal.Step += 1;
					Owner.loyalty.GetGSAI().Goals.QueuePendingRemoval(ColonizeGoal);
				}
				State = AIState.AwaitingOrders;
				OrderQueue.Clear();
				return;
			}
			ColonizeTarget = TargetPlanet;
			ColonizeTarget.Owner = Owner.loyalty;
			ColonizeTarget.system.OwnerList.Add(Owner.loyalty);
		    if (Owner.loyalty.isPlayer)
		    {
		        if (!Owner.loyalty.AutoColonize)
		        {
		            ColonizeTarget.colonyType = Planet.ColonyType.Colony;
		            ColonizeTarget.GovernorOn = false;
		        }
                else ColonizeTarget.colonyType = Owner.loyalty.AssessColonyNeeds(ColonizeTarget);
                Empire.Universe.NotificationManager.AddColonizedNotification(ColonizeTarget, EmpireManager.Player);
            }
            else ColonizeTarget.colonyType = Owner.loyalty.AssessColonyNeeds(ColonizeTarget);                
		    Owner.loyalty.AddPlanet(ColonizeTarget);
			ColonizeTarget.InitializeSliders(Owner.loyalty);
			ColonizeTarget.ExploredDict[Owner.loyalty] = true;
			var BuildingsAdded = new Array<string>();
			foreach (ModuleSlot slot in Owner.ModuleSlotList)//@TODO create building placement methods in planet.cs that take into account the below logic. 
			{
				if (slot.module == null || slot.module.ModuleType != ShipModuleType.Colony || slot.module.DeployBuildingOnColonize == null || BuildingsAdded.Contains(slot.module.DeployBuildingOnColonize))
				    continue;
			    Building building = ResourceManager.GetBuilding(slot.module.DeployBuildingOnColonize);
				var ok = true;
				if (building.Unique)
				    foreach (Building b in ColonizeTarget.BuildingList)
				    {
				        if (b.Name != building.Name)
				            continue;
				        ok = false;
				        break;
				    }
			    if (!ok)
			        continue;
			    BuildingsAdded.Add(slot.module.DeployBuildingOnColonize);
				ColonizeTarget.BuildingList.Add(building);
				ColonizeTarget.AssignBuildingToTileOnColonize(building);
			}
			Planet colonizeTarget = ColonizeTarget;
			colonizeTarget.TerraformPoints = colonizeTarget.TerraformPoints + Owner.loyalty.data.EmpireFertilityBonus;
			ColonizeTarget.Crippled_Turns = 0;
            string starDate = universeScreen.StarDate.ToString("#.0");
			if (StatTracker.SnapshotsDict.ContainsKey(starDate))
			{
				StatTracker.SnapshotsDict[starDate][EmpireManager.Empires.IndexOf(Owner.loyalty)].Events.Add(
                    string.Concat(Owner.loyalty.data.Traits.Name, " colonized ", ColonizeTarget.Name));
				var nro = new NRO()
				{
					Node = ColonizeTarget.Position,
					Radius = 300000f,
					StarDateMade = universeScreen.StarDate
				};
				StatTracker.SnapshotsDict[starDate][EmpireManager.Empires.IndexOf(Owner.loyalty)].EmpireNodes.Add(nro);
			}
			foreach (Goal g in Owner.loyalty.GetGSAI().Goals)
			{
				if (g.type != GoalType.Colonize || g.GetMarkedPlanet() != ColonizeTarget)
				    continue;
			    Owner.loyalty.GetGSAI().Goals.QueuePendingRemoval(g);
				break;
			}
			Owner.loyalty.GetGSAI().Goals.ApplyPendingRemovals();
			if (ColonizeTarget.system.OwnerList.Count > 1)
			    foreach (Planet p in ColonizeTarget.system.PlanetList)
			    {
			        if (p.Owner == ColonizeTarget.Owner || p.Owner == null)
			            continue;
			        if (p.Owner.TryGetRelations(Owner.loyalty, out Relationship rel) && !rel.Treaty_OpenBorders)
			            p.Owner.DamageRelationship(Owner.loyalty, "Colonized Owned System", 20f, p);
			    }
		    foreach (ModuleSlot slot in Owner.ModuleSlotList)
			{
				if (slot.module.ModuleType != ShipModuleType.Colony)
				    continue;
			    Planet foodHere = ColonizeTarget;
				foodHere.FoodHere = foodHere.FoodHere + slot.module.numberOfFood;
				Planet productionHere = ColonizeTarget;
				productionHere.ProductionHere = productionHere.ProductionHere + slot.module.numberOfEquipment;
				Planet population = ColonizeTarget;
				population.Population = population.Population + slot.module.numberOfColonists;
			}
            var TroopsRemoved = false;
            var PlayerTroopsRemoved = false;

			var toLaunch = new Array<Troop>();
            foreach (Troop t in TargetPlanet.TroopsHere)
			{
                Empire owner = t?.GetOwner();
                if (owner != null && !owner.isFaction && owner.data.DefaultTroopShip != null && owner != ColonizeTarget.Owner && 
                    ColonizeTarget.Owner.TryGetRelations(owner, out Relationship rel) && !rel.AtWar)
				    toLaunch.Add(t);
			}
			foreach (Troop t in toLaunch)
			{
                t.Launch();
                TroopsRemoved = true;
                if (t.GetOwner().isPlayer)
                    PlayerTroopsRemoved = true;
			}
			toLaunch.Clear();
            if (TroopsRemoved)
                if (PlayerTroopsRemoved)
                    universeScreen.NotificationManager.AddTroopsRemovedNotification(ColonizeTarget);
                else if (ColonizeTarget.Owner.isPlayer)
                    universeScreen.NotificationManager.AddForeignTroopsRemovedNotification(ColonizeTarget);
		    Owner.QueueTotalRemoval();
		}

		private void DeRotate()
		{
			if (Owner.yRotation > 0f)
			{
				Ship owner = Owner;
				owner.yRotation = owner.yRotation - Owner.yBankAmount;
				if (Owner.yRotation < 0f)
				{
					Owner.yRotation = 0f;
					return;
				}
			}
			else if (Owner.yRotation < 0f)
			{
				Ship ship = Owner;
				ship.yRotation = ship.yRotation + Owner.yBankAmount;
				if (Owner.yRotation > 0f)
				    Owner.yRotation = 0f;
			}
		}

        private void DoAssaultShipCombat(float elapsedTime)
        {
            DoNonFleetArtillery(elapsedTime);
            if (!Owner.loyalty.isFaction && (Target as Ship).shipData.Role < ShipData.RoleName.drone || Owner.GetHangars().Count == 0)
                return;
            var OurTroopStrength = 0f;
            var OurOutStrength = 0f;
            var tcount = 0;
            for (var i = 0; i < Owner.GetHangars().Count; i++)
            {
                ShipModule s = Owner.GetHangars()[i];
                if (s.IsTroopBay)
                {
                    if (s.GetHangarShip() != null)
                        foreach (Troop st in s.GetHangarShip().TroopList)
                        {
                            OurTroopStrength += st.Strength;
                            if (s.GetHangarShip().GetAI().EscortTarget == Target ||
                                s.GetHangarShip().GetAI().EscortTarget == null
                                || s.GetHangarShip().GetAI().EscortTarget == Owner)
                                OurOutStrength += st.Strength;
                        }
                    if (s.hangarTimer <= 0)
                        tcount++;
                }
            }
            for (var i = 0; i < Owner.TroopList.Count; i++)
            {
                Troop t = Owner.TroopList[i];
                if (tcount <= 0)
                    break;
                OurTroopStrength = OurTroopStrength + (float) t.Strength;
                tcount--;
            }

            if (OurTroopStrength <= 0) return;
            if (Target == null)
                if (!Owner.InCombat &&  Owner.System?.OwnerList.Count > 0)
                {
                    Owner.ScrambleAssaultShips(0);
                    Planet x = Owner.System.PlanetList.FindMinFiltered(
                        filter: p => p.Owner != null && p.Owner != Owner.loyalty || p.RecentCombat,
                        selector: p => Owner.Center.Distance(p.Position));
                }            
            Ship shipTarget = Target as Ship;
            float EnemyStrength = shipTarget?.BoardingDefenseTotal ?? 0;

            if (OurTroopStrength + OurOutStrength > EnemyStrength && (Owner.loyalty.isFaction || shipTarget.GetStrength() > 0f))
            {
                if (OurOutStrength < EnemyStrength)
                    Owner.ScrambleAssaultShips(EnemyStrength);
                for (var i = 0; i < Owner.GetHangars().Count; i++)
                {
                    ShipModule hangar = Owner.GetHangars()[i];
                    if (!hangar.IsTroopBay || hangar.GetHangarShip() == null)
                        continue;
                    hangar.GetHangarShip().GetAI().OrderTroopToBoardShip(shipTarget);
                }
            }

        }
        //aded by gremlin Deveksmod Attackrun
        private void DoAttackRun(float elapsedTime)
        {

            float distanceToTarget = Owner.Center.Distance(Target.Center);
            float spacerdistance = Owner.Radius * 3 + Target.Radius;
            if (spacerdistance > Owner.maxWeaponsRange * .35f)
                spacerdistance = Owner.maxWeaponsRange * .35f;


            if (distanceToTarget > spacerdistance && distanceToTarget > Owner.maxWeaponsRange * .35f)
            {
                runTimer = 0f;
                AttackRunStarted = false;
                ThrustTowardsPosition(Target.Center, elapsedTime, Owner.speed);
                return;
            }


            if (distanceToTarget < Owner.maxWeaponsRange * .35f)
            {                
                runTimer += elapsedTime;
                if (runTimer > 7f) 
                {
                    DoNonFleetArtillery(elapsedTime);
                    return;
                }               
                aiNewDir += Owner.Center.FindVectorToTarget(Target.Center + Target.Velocity) * 0.35f;
                if (distanceToTarget < (Owner.Radius + Target.Radius) * 3f && !AttackRunStarted)
                {
                    AttackRunStarted = true;
                    int ran = UniverseRandom.IntBetween(1, 100);
                    ran = ran <= 50 ? 1 : -1;
                    AttackRunAngle = (float)ran * UniverseRandom.RandomBetween(75f, 100f) + MathHelper.ToDegrees(Owner.Rotation);
                    AttackVector = Owner.Center.PointFromAngle(AttackRunAngle, 1500f);
                }
                AttackVector = Owner.Center.PointFromAngle(AttackRunAngle, 1500f);
                MoveInDirection(AttackVector, elapsedTime);
                if (runTimer > 2)
                {
                    DoNonFleetArtillery(elapsedTime);
                    return;
                }

            }
        }
		private void DoBoardShip(float elapsedTime)
		{
			hasPriorityTarget = true;
			State = AIState.Boarding;
			if (EscortTarget == null || !EscortTarget.Active)
			{
				OrderQueue.Clear();
                State = AIState.AwaitingOrders;
				return;
			}
			if (EscortTarget.loyalty == Owner.loyalty)
			{
                OrderQueue.Clear();
                State = AIState.AwaitingOrders;
				return;
			}
			ThrustTowardsPosition(EscortTarget.Center, elapsedTime, Owner.speed);
			float Distance = Vector2.Distance(Owner.Center, EscortTarget.Center);
			//added by gremlin distance at which troops can board enemy ships
            if (Distance < EscortTarget.Radius + 300f)
			{
				if (Owner.TroopList.Count > 0)
				{
					EscortTarget.TroopList.Add(Owner.TroopList[0]);
					Owner.QueueTotalRemoval();
					return;
				}
			}
			else if (Distance > 10000f && Owner.Mothership != null && Owner.Mothership.GetAI().CombatState == CombatState.AssaultShip)
			{
				OrderReturnToHangar();
			}
		}

        private void DoCombat(float elapsedTime)
        {


            var ctarget = Target as Ship;
            if(Target == null || !Target.Active || ctarget != null && ctarget.engineState == Ship.MoveState.Warp)
            {
                Intercepting = false;
                Target = null;
                Target = PotentialTargets.Where(t => t.Active && t.engineState != Ship.MoveState.Warp && Vector2.Distance(t.Center, Owner.Center) <= Owner.SensorRange).FirstOrDefault() as GameplayObject;
                if (Target == null)
                {
                    if (OrderQueue.Count > 0) OrderQueue.RemoveFirst();
                    State = DefaultAIState;
                    return;
                }
            }
            awaitClosest = null;
            State = AIState.Combat;
            Owner.InCombat = true;
            Owner.InCombatTimer = 15f;
            if (Owner.Mothership != null && Owner.Mothership.Active)
                if (Owner.shipData.Role != ShipData.RoleName.troop
                    && (Owner.Health / Owner.HealthMax < DmgLevel[(int)Owner.shipData.ShipCategory] || Owner.shield_max > 0 && Owner.shield_percent <= 0)
                    || Owner.OrdinanceMax > 0 && Owner.Ordinance / Owner.OrdinanceMax <= .1f
                    || Owner.PowerCurrent <=1f && Owner.PowerDraw / Owner.PowerFlowMax <=.1f
                )
                {
                    OrderReturnToHangar();
                }

            if (State!= AIState.Resupply && Owner.OrdinanceMax > 0f && Owner.Ordinance / Owner.OrdinanceMax < 0.05f &&  !hasPriorityTarget)//this.Owner.loyalty != ArtificialIntelligence.universeScreen.player)
                if (FriendliesNearby.Where(supply => supply.HasSupplyBays && supply.Ordinance >= 100).Count() == 0)
                {
                    OrderResupplyNearest(false);
                    return;
                }
            if(State != AIState.Resupply && !Owner.loyalty.isFaction && State == AIState.AwaitingOrders && Owner.TroopCapacity >0 && Owner.TroopList.Count < Owner.GetHangars().Where(hangar=> hangar.IsTroopBay).Count() *.5f)
            {
                OrderResupplyNearest(false);
                return;
            }
            if (State != AIState.Resupply && Owner.Health >0 && Owner.Health / Owner.HealthMax < DmgLevel[(int)Owner.shipData.ShipCategory] 
                && Owner.shipData.Role >= ShipData.RoleName.supply)  //fbedard: repair level
                if (Owner.fleet == null ||  !Owner.fleet.HasRepair)
                {
                    OrderResupplyNearest(false);
                    return;
                }
            if (Vector2.Distance(Target.Center, Owner.Center) < 10000f)
            {
                if (Owner.engineState != Ship.MoveState.Warp && Owner.GetHangars().Count > 0 && !Owner.ManualHangarOverride)
                    if (!Owner.FightersOut) Owner.FightersOut = true;
                if (Owner.engineState == Ship.MoveState.Warp)
                    Owner.HyperspaceReturn();
            }
            else if (CombatState != CombatState.HoldPosition && CombatState != CombatState.Evade)
            {
                ThrustTowardsPosition(Target.Center, elapsedTime, Owner.speed);
                return;
            }
            if (!HasPriorityOrder && !hasPriorityTarget && Owner.Weapons.Count == 0 && Owner.GetHangars().Count == 0)
                CombatState = CombatState.Evade;
            if (!Owner.loyalty.isFaction && Owner.System!= null && TroopsOut == false && Owner.GetHangars().Any(troops => troops.IsTroopBay) || Owner.hasTransporter)
                if ( Owner.TroopList.All(troop => troop.GetOwner() == Owner.loyalty)) // this.Owner.TroopList.Where(troop => troop.GetOwner() == this.Owner.loyalty).Count() > 0 &&
                {
                    Planet invadeThis = null;
                    foreach (Planet invade in Owner.System.PlanetList.Where(owner => owner.Owner != null && owner.Owner != Owner.loyalty).OrderBy(troops => troops.TroopsHere.Count))
                        if (Owner.loyalty.GetRelations(invade.Owner).AtWar)
                        {
                            invadeThis = invade;
                            break;
                        }
                    if (!TroopsOut && !Owner.hasTransporter)
                        if (invadeThis != null)
                        {
                            TroopsOut = true;
                            foreach (Ship troop in Owner.GetHangars().Where(troop => troop.IsTroopBay && troop.GetHangarShip() != null && troop.GetHangarShip().Active).Select(ship => ship.GetHangarShip()))
                                troop.GetAI().OrderAssaultPlanet(invadeThis);
                        }
                        else if (Target is Ship && (Target as Ship).shipData.Role >= ShipData.RoleName.drone)
                        {
                            if (Owner.GetHangars().Count(troop => troop.IsTroopBay) * 60 >= (Target as Ship).MechanicalBoardingDefense)
                            {
                                TroopsOut = true;
                                foreach (ShipModule hangar in Owner.GetHangars())
                                {
                                    if (hangar.GetHangarShip() == null || Target == null || hangar.GetHangarShip().shipData.Role != ShipData.RoleName.troop || (Target as Ship).shipData.Role < ShipData.RoleName.drone)
                                        continue;
                                    hangar.GetHangarShip().GetAI().OrderTroopToBoardShip(Target as Ship);
                                }
                            }
                        }
                        else
                        {
                            TroopsOut = false;
                        }
                }

            { 
            if (Owner.fleet == null)
                switch (CombatState)
                {
                    case CombatState.Artillery:
                    {
                        DoNonFleetArtillery(elapsedTime);
                        break;
                    }
                    case CombatState.OrbitLeft:
                    {
                        OrbitShipLeft(Target as Ship, elapsedTime);
                        break;
                    }
                    case CombatState.BroadsideLeft:
                    {
                        DoNonFleetBroadsideLeft(elapsedTime);
                        break;
                    }
                    case CombatState.OrbitRight:
                    {
                        OrbitShip(Target as Ship, elapsedTime);
                        break;
                    }
                    case CombatState.BroadsideRight:
                    {
                        DoNonFleetBroadsideRight(elapsedTime);
                        break;
                    }
                    case CombatState.AttackRuns:
                    {
                        DoAttackRun(elapsedTime);
                        break;
                    }
                    case CombatState.HoldPosition:
                    {
                        DoHoldPositionCombat(elapsedTime);
                        break;
                    }
                    case CombatState.Evade:
                    {
                        DoEvadeCombat(elapsedTime);
                        break;
                    }
                    case CombatState.AssaultShip:
                    {
                        DoAssaultShipCombat(elapsedTime);
                        break;
                    }
                    case CombatState.ShortRange:
                    {
                        DoNonFleetArtillery(elapsedTime);
                        break;
                    }
                }
            else if (Owner.fleet != null)
                switch (CombatState)
                {
                    case CombatState.Artillery:
                    {
                        DoNonFleetArtillery(elapsedTime);
                        break;
                    }
                    case CombatState.OrbitLeft:
                    {
                        OrbitShipLeft(Target as Ship, elapsedTime);
                        break;
                    }
                    case CombatState.BroadsideLeft:
                    {
                        DoNonFleetBroadsideLeft(elapsedTime);
                        break;
                    }
                    case CombatState.OrbitRight:
                    {
                        OrbitShip(Target as Ship, elapsedTime);
                        break;
                    }
                    case CombatState.BroadsideRight:
                    {
                        DoNonFleetBroadsideRight(elapsedTime);
                        break;
                    }
                    case CombatState.AttackRuns:
                    {
                        DoAttackRun(elapsedTime);
                        break;
                    }
                    case CombatState.HoldPosition:
                    {
                        DoHoldPositionCombat(elapsedTime);
                        break;
                    }
                    case CombatState.Evade:
                    {
                        DoEvadeCombat(elapsedTime);
                        break;
                    }
                    case CombatState.AssaultShip:
                    {
                        DoAssaultShipCombat(elapsedTime);
                        break;
                    }
                    case CombatState.ShortRange:
                    {
                        DoNonFleetArtillery(elapsedTime);
                        break;
                    }
                }
                if (Target != null)
                    return;
                Owner.InCombat = false;
            }
        }

        //added by gremlin : troops out property        
        public bool TroopsOut
        {
            get
            {
                //this.troopsout = false;
                if (Owner.TroopsOut)
                {
                    troopsout = true;
                    return true;
                }

                if (Owner.TroopList.Count == 0)
                {
                    troopsout = true;
                    return true;
                }
                if (!Owner.GetHangars().Any(troopbay => troopbay.IsTroopBay))
                {
                    troopsout = true;
                    return true;
                }
                if (Owner.TroopList.Any(loyalty => loyalty.GetOwner() != Owner.loyalty))
                {
                    troopsout = true;
                    return true;
                }

                if (troopsout == true)
                    foreach (ShipModule hangar in Owner.GetHangars())
                        if (hangar.IsTroopBay && (hangar.GetHangarShip() == null || hangar.GetHangarShip() != null && !hangar.GetHangarShip().Active) && hangar.hangarTimer <= 0)
                        {
                            troopsout = false;
                            break;

                        }
                return troopsout;
            }
            set
            {
                troopsout = value;
                if (troopsout)
                {
                    Owner.ScrambleAssaultShips(0);
                    return;
                }
                Owner.RecoverAssaultShips();
            }
        }

        //added by gremlin : troop asssault planet
        public void OrderAssaultPlanet(Planet p)
        {
            State = AIState.AssaultPlanet;
            OrbitTarget = p;
            var shipGoal = new ShipGoal(Plan.LandTroop, Vector2.Zero, 0f)
            {
                TargetPlanet = OrbitTarget
            };
           
            OrderQueue.Clear();
            OrderQueue.AddLast(shipGoal);
            
        }
        public void OrderAssaultPlanetorig(Planet p)
        {
            State = AIState.AssaultPlanet;
            OrbitTarget = p;
        }

		private void DoDeploy(ShipGoal shipgoal)
		{
			if (shipgoal.goal == null)
			    return;
		    Planet target = shipgoal.TargetPlanet;
            if (shipgoal.goal.TetherTarget != Guid.Empty)
            {
                if (target == null)
                    universeScreen.PlanetsDict.TryGetValue(shipgoal.goal.TetherTarget, out target);
                shipgoal.goal.BuildPosition = target.Position + shipgoal.goal.TetherOffset;                
            }
            if (target !=null && Vector2.Distance(target.Position + shipgoal.goal.TetherOffset, Owner.Center) > 200f)
			{				
                shipgoal.goal.BuildPosition = target.Position + shipgoal.goal.TetherOffset;
				OrderDeepSpaceBuild(shipgoal.goal);
				return;
			}
			Ship platform = ResourceManager.CreateShipAtPoint(shipgoal.goal.ToBuildUID, Owner.loyalty, shipgoal.goal.BuildPosition);
			if (platform == null)
			    return;
		    string starDate = universeScreen.StarDate.ToString("#.0");
			foreach (SpaceRoad road in Owner.loyalty.SpaceRoadsList)
			foreach (RoadNode node in road.RoadNodesList)
			{
			    if (node.Position != shipgoal.goal.BuildPosition)
			        continue;
			    node.Platform = platform;
			    if (!StatTracker.SnapshotsDict.ContainsKey(starDate))
			        continue;
			    var nro = new NRO()
			    {
			        Node = node.Position,
			        Radius = 300000f,
			        StarDateMade = universeScreen.StarDate
			    };
			    StatTracker.SnapshotsDict[starDate][EmpireManager.Empires.IndexOf(Owner.loyalty)].EmpireNodes.Add(nro);
			}
		    if (shipgoal.goal.TetherTarget != Guid.Empty)
			{
				platform.TetherToPlanet(universeScreen.PlanetsDict[shipgoal.goal.TetherTarget]);
				platform.TetherOffset = shipgoal.goal.TetherOffset;
			}
			Owner.loyalty.GetGSAI().Goals.Remove(shipgoal.goal);
			Owner.QueueTotalRemoval();
		}

		private void DoEvadeCombat(float elapsedTime)
		{

            var AverageDirection = new Vector2();
            var count = 0;
            foreach (ShipWeight ship in NearbyShips)
            {
                if (ship.ship.loyalty == Owner.loyalty || !ship.ship.loyalty.isFaction && !Owner.loyalty.GetRelations(ship.ship.loyalty).AtWar)
                    continue;
                AverageDirection = AverageDirection + Owner.Center.FindVectorToTarget( ship.ship.Center);
                count++;
            }
            if (count != 0)
            {
                AverageDirection = AverageDirection / (float)count;
                AverageDirection = Vector2.Normalize(AverageDirection);
                AverageDirection = Vector2.Negate(AverageDirection);
                
                
                {
                    AverageDirection = AverageDirection * 7500f;
                    ThrustTowardsPosition(AverageDirection + Owner.Center, elapsedTime, Owner.speed);
                }
            }
		}

		public void DoExplore(float elapsedTime)
		{
			HasPriorityOrder = true;
			IgnoreCombat = true;
			if (ExplorationTarget == null)
			{
				ExplorationTarget = Owner.loyalty.GetGSAI().AssignExplorationTarget(Owner);
				if (ExplorationTarget == null)
				{
					OrderQueue.Clear();
					State = AIState.AwaitingOrders;
					return;
				}
			}
			else if (DoExploreSystem(elapsedTime))
			{
                if (Owner.loyalty == universeScreen.player)
                {
                    //added by gremlin  add shamatts notification here
                    SolarSystem system = ExplorationTarget;
                    var message = new StringBuilder(system.Name);
                    message.Append(" system explored.");

                    var planetsTypesNumber = new Map<string, int>();
                    if (system.PlanetList.Count > 0)
                    {
                        foreach (Planet planet in system.PlanetList)
                        {
                            // some planets don't have Type set and it is null
                            if (planet.Type == null)
                                planet.Type = "Other";

                            if (!planetsTypesNumber.ContainsKey(planet.Type))
                                planetsTypesNumber.Add(planet.Type, 1);
                            else
                                planetsTypesNumber[planet.Type] += 1;
                        }

                        foreach (var pair in planetsTypesNumber)
                            message.Append('\n').Append(pair.Value).Append(' ').Append(pair.Key);
                    }

                    foreach (Planet planet in system.PlanetList)
                    {
                        Building tile = planet.BuildingList.Find(t => t.IsCommodity);
                        if (tile != null)
                            message.Append('\n').Append(tile.Name).Append(" on ").Append(planet.Name);
                    }

                    if (system.combatTimer > 0)
                        message.Append("\nCombat in system!!!");

                    if (system.OwnerList.Count > 0 && !system.OwnerList.Contains(Owner.loyalty))
                        message.Append("\nContested system!!!");

                    Planet.universeScreen.NotificationManager.AddNotification(new Notification
                    {
                        Pause = false,
                        Message = message.ToString(),
                        ReferencedItem1 = system,
                        IconPath = "Suns/" + system.SunPath,
                        Action = "SnapToExpandSystem"
                    }, "sd_ui_notification_warning");
                }
                ExplorationTarget = null;
                            
			}
		}

		private bool DoExploreSystem(float elapsedTime)
		{
			SystemToPatrol = ExplorationTarget;
			if (PatrolRoute == null || PatrolRoute.Count == 0)
			{
				foreach (Planet p in SystemToPatrol.PlanetList)
				{
				    var patrolRoute = PatrolRoute;
				    patrolRoute?.Add(p);
				}
				if (SystemToPatrol.PlanetList.Count == 0)
				    return ExploreEmptySystem(elapsedTime, SystemToPatrol);
			}
			else
			{
				PatrolTarget = PatrolRoute[stopNumber];
				if (PatrolTarget.ExploredDict[Owner.loyalty])
				{
					ArtificialIntelligence artificialIntelligence = this;
					artificialIntelligence.stopNumber = artificialIntelligence.stopNumber + 1;
					if (stopNumber == PatrolRoute.Count)
					{
						stopNumber = 0;
						PatrolRoute.Clear();
                       
						return true;
					}
				}
				else
				{
					MovePosition = PatrolTarget.Position;
					float Distance = Vector2.Distance(Owner.Center, MovePosition);
					if (Distance < 75000f)
					    PatrolTarget.system.ExploredDict[Owner.loyalty] = true;
				    if (Distance > 15000f)
					{
                        if (Owner.velocityMaximum > Distance && Owner.speed >= Owner.velocityMaximum)
                            Owner.speed = Distance;
                        ThrustTowardsPosition(MovePosition, elapsedTime, Owner.speed);
					}
					else if (Distance >= 5500f)
					{
                        if (Owner.velocityMaximum > Distance && Owner.speed >= Owner.velocityMaximum)
                            Owner.speed = Distance;
                        ThrustTowardsPosition(MovePosition, elapsedTime, Owner.speed);
					}
					else
					{
						ThrustTowardsPosition(MovePosition, elapsedTime, Owner.speed);
						if (Distance < 500f)
						{
							PatrolTarget.ExploredDict[Owner.loyalty] = true;
							ArtificialIntelligence artificialIntelligence1 = this;
							artificialIntelligence1.stopNumber = artificialIntelligence1.stopNumber + 1;
							if (stopNumber == PatrolRoute.Count)
							{
								stopNumber = 0;
								PatrolRoute.Clear();
								return true;
							}
						}
					}
				}
			}
			return false;
		}

		private void DoHoldPositionCombat(float elapsedTime)
		{
			if (Owner.Velocity.Length() > 0f)
			{
                if (Owner.engineState == Ship.MoveState.Warp)
                    Owner.HyperspaceReturn();
                var forward = new Vector2((float)Math.Sin((double)Owner.Rotation), -(float)Math.Cos((double)Owner.Rotation));
				var right = new Vector2(-forward.Y, forward.X);
				var angleDiff = (float)Math.Acos((double)Vector2.Dot(Vector2.Normalize(Owner.Velocity), forward));
				float facing = Vector2.Dot(Vector2.Normalize(Owner.Velocity), right) > 0f ? 1f : -1f;
				if (angleDiff <= 0.2f)
				{
					Stop(elapsedTime);
					return;
				}
				RotateToFacing(elapsedTime, angleDiff, facing);
				return;
			}
			Owner.Center.FindVectorToTarget(Target.Center);
            //renamed forward, right and anglediff
			var forward2 = new Vector2((float)Math.Sin((double)Owner.Rotation), -(float)Math.Cos((double)Owner.Rotation));
			var right2 = new Vector2(-forward2.Y, forward2.X);
			Vector2 VectorToTarget = Owner.Center.FindVectorToTarget(Target.Center);
			var angleDiff2 = (float)Math.Acos((double)Vector2.Dot(VectorToTarget, forward2));
			if (angleDiff2 <= 0.02f)
			{
				DeRotate();
				return;
			}
			RotateToFacing(elapsedTime, angleDiff2, Vector2.Dot(VectorToTarget, right2) > 0f ? 1f : -1f);
		}

        
		private void DoLandTroop(float elapsedTime, ShipGoal goal)
		{
            if (Owner.shipData.Role != ShipData.RoleName.troop || Owner.TroopList.Count == 0)
                DoOrbit(goal.TargetPlanet, elapsedTime); //added by gremlin.

            float radius = goal.TargetPlanet.ObjectRadius + Owner.Radius * 2;
            float distCenter = Vector2.Distance(goal.TargetPlanet.Position, Owner.Center);

            if (Owner.shipData.Role == ShipData.RoleName.troop && Owner.TroopList.Count > 0)
			{
                if (Owner.engineState == Ship.MoveState.Warp && distCenter < 7500f)
                    Owner.HyperspaceReturn();
                if (distCenter < radius  )
                    ThrustTowardsPosition(goal.TargetPlanet.Position, elapsedTime, Owner.speed > 200 ? Owner.speed*.90f : Owner.velocityMaximum);
                else
                    ThrustTowardsPosition(goal.TargetPlanet.Position, elapsedTime, Owner.speed);
                if (distCenter < goal.TargetPlanet.ObjectRadius && goal.TargetPlanet.AssignTroopToTile(Owner.TroopList[0]))
                        Owner.QueueTotalRemoval();
                return;
			}
            else if (Owner.loyalty == goal.TargetPlanet.Owner || goal.TargetPlanet.GetGroundLandingSpots() == 0 || Owner.TroopList.Count <= 0 || Owner.shipData.Role != ShipData.RoleName.troop && Owner.GetHangars().Where(hangar => hangar.hangarTimer <= 0 && hangar.IsTroopBay).Count() == 0 && !Owner.hasTransporter)//|| goal.TargetPlanet.GetGroundStrength(this.Owner.loyalty)+3 > goal.TargetPlanet.GetGroundStrength(goal.TargetPlanet.Owner)*1.5)
			{                
				if (Owner.loyalty == EmpireManager.Player)
				    HadPO = true;
			    HasPriorityOrder = false;
                State = DefaultAIState;
				OrderQueue.Clear();
                Log.Info("Do Land Troop: Troop Assault Canceled");
			}
            else if (distCenter < radius)
			{
				var ToRemove = new Array<Troop>();
                //if (Vector2.Distance(goal.TargetPlanet.Position, this.Owner.Center) < 3500f)
				{
                    //Get limit of troops to land
                    int LandLimit = Owner.GetHangars().Where(hangar => hangar.hangarTimer <= 0 && hangar.IsTroopBay).Count();
                    foreach (ShipModule module in Owner.Transporters.Where(module => module.TransporterTimer <= 1f))
                        LandLimit += module.TransporterTroopLanding;
                    //Land troops
                    foreach (Troop troop in Owner.TroopList)
                    {
                        if (troop == null || troop.GetOwner() != Owner.loyalty)
                            continue;
                        if (goal.TargetPlanet.AssignTroopToTile(troop))
                        {
                            ToRemove.Add(troop);
                            LandLimit--;
                            if (LandLimit < 1)
                                break;
                        }
                        else
                        {
                            break;
                        }
                    }
                    //Clear out Troops
                    if (ToRemove.Count > 0)
                    {
                        bool flag; // = false;
                        foreach (Troop RemoveTroop in ToRemove)
                        {
                            flag = false;
                            foreach (ShipModule module in Owner.GetHangars())
                                if (module.hangarTimer < module.hangarTimerConstant)
                                {
                                    module.hangarTimer = module.hangarTimerConstant;
                                    flag = true;
                                    break;
                                }
                            if (flag)
                                continue;
                            foreach (ShipModule module in Owner.Transporters)
                                if (module.TransporterTimer < module.TransporterTimerConstant)
                                {
                                    module.TransporterTimer = module.TransporterTimerConstant;
                                    flag = true;
                                    break;
                                }
                        }
                                //module.TransporterTimer = module.TransporterTimerConstant;
                            foreach (Troop to in ToRemove)
                                Owner.TroopList.Remove(to);
                        
                    }
				}
			}
		}

        private void DoNonFleetArtillery(float elapsedTime)
        {
            //Heavily modified by Gretman
            var forward = new Vector2((float)Math.Sin((double)Owner.Rotation), -(float)Math.Cos((double)Owner.Rotation));
            var right = new Vector2(-forward.Y, forward.X);
            Vector2 VectorToTarget = Owner.Center.FindVectorToTarget(Target.Center);
            var angleDiff = (float)Math.Acos((double)Vector2.Dot(VectorToTarget, forward));
            float DistanceToTarget = Owner.Center.Distance(Target.Center) *.75f;

            float AdjustedRange = Owner.maxWeaponsRange - Owner.Radius;

            if (DistanceToTarget > AdjustedRange) 
            {
                ThrustTowardsPosition(Target.Center, elapsedTime, Owner.speed);
                return;
            }
            else if (DistanceToTarget < AdjustedRange //* 0.75f 
                && Vector2.Distance(Owner.Center + Owner.Velocity * elapsedTime, Target.Center) < DistanceToTarget 
                || DistanceToTarget < Owner.Radius) //Center + Radius = Dont touch me
            {
                Owner.Velocity = Owner.Velocity + Vector2.Normalize(-forward) * (elapsedTime * Owner.GetSTLSpeed());   
            }

            if (angleDiff <= 0.02f)
            {
                DeRotate();
                return;
            }
            RotateToFacing(elapsedTime, angleDiff, Vector2.Dot(VectorToTarget, right) > 0f ? 1f : -1f);
        }

        private void DoNonFleetBroadsideRight(float elapsedTime)
        {
            var forward = new Vector2((float)Math.Sin((double)Owner.Rotation), -(float)Math.Cos((double)Owner.Rotation));
            var right = new Vector2(-forward.Y, forward.X);
            Vector2 VectorToTarget = Owner.Center.FindVectorToTarget(Target.Center);
            var angleDiff = (float)Math.Acos((double)Vector2.Dot(VectorToTarget, right));
            float DistanceToTarget = Vector2.Distance(Owner.Center, Target.Center);
            if (DistanceToTarget > Owner.maxWeaponsRange)
            {
                ThrustTowardsPosition(Target.Center, elapsedTime, Owner.speed);
                return;
            }
            if (DistanceToTarget < Owner.maxWeaponsRange * 0.70f && Vector2.Distance(Owner.Center + Owner.Velocity * elapsedTime, Target.Center) < DistanceToTarget)
            {
                Ship owner = Owner;
                Owner.Velocity = Vector2.Zero;
            }
            if (angleDiff <= 0.02f)
            {
                DeRotate();
                return;
            }
            RotateToFacing(elapsedTime, angleDiff, Vector2.Dot(VectorToTarget, forward) > 0f ? -1f : 1f);
        }

        private void DoNonFleetBroadsideLeft(float elapsedTime)
        {
            var forward = new Vector2((float)Math.Sin((double)Owner.Rotation), -(float)Math.Cos((double)Owner.Rotation));
            var right = new Vector2(-forward.Y, forward.X);
            var left = new Vector2(forward.Y, -forward.X);
            Vector2 VectorToTarget = Owner.Center.FindVectorToTarget(Target.Center); 
            var angleDiff = (float)Math.Acos((double)Vector2.Dot(VectorToTarget, left));
            float DistanceToTarget = Owner.Center.Distance(Target.Center);
            if (DistanceToTarget > Owner.maxWeaponsRange)
            {
                ThrustTowardsPosition(Target.Center, elapsedTime, Owner.speed);
                return;
            }
            if (DistanceToTarget < Owner.maxWeaponsRange * 0.70f && Vector2.Distance(Owner.Center + Owner.Velocity * elapsedTime, Target.Center) < DistanceToTarget)
            {
                Ship owner = Owner;
                Owner.Velocity = Vector2.Zero;
            }
            if (angleDiff <= 0.02f)
            {
                DeRotate();
                return;
            }
            RotateToFacing(elapsedTime, angleDiff, Vector2.Dot(VectorToTarget, forward) > 0f ? 1f : -1f);
        }
        
        private void DoOrbit(Planet OrbitTarget, float elapsedTime)  //fbedard: my version of DoOrbit, fastest possible?
        {            
            if (Owner.velocityMaximum < 1)
                return;

            if (Owner.GetShipData().ShipCategory == ShipData.Category.Civilian && OrbitTarget.Position.InRadius(Owner.Center , Empire.ProjectorRadius * 2))
            {
                OrderMoveTowardsPosition(OrbitPos, 0, Vector2.Zero, false, this.OrbitTarget);                
                OrbitPos = OrbitTarget.Position;
                return;
            }

            if (OrbitTarget.Position.OutsideRadius ( Owner.Center, 15000f))
            {
                ThrustTowardsPosition(OrbitTarget.Position, elapsedTime, Owner.speed);
                OrbitPos = OrbitTarget.Position;
                return;
            }

            float radius = OrbitTarget.ObjectRadius + Owner.Radius +1200f;
            float distanceToOrbitSpot = Owner.Center.Distance(OrbitPos);
            
            if (findNewPosTimer <= 0f)
            {
                if (distanceToOrbitSpot <= radius || Owner.speed < 1f)
                {                    
                    OrbitalAngle += ((float)Math.Asin(Owner.yBankAmount * 10f)).ToDegrees();
                    if (OrbitalAngle >= 360f)
                        OrbitalAngle -= 360f;
                }
                findNewPosTimer =  elapsedTime * 10f;
                OrbitPos = OrbitTarget.Position.PointOnCircle(OrbitalAngle, radius);
            }
            else
            {
                findNewPosTimer -= elapsedTime;
            }

            if (distanceToOrbitSpot < 7500f)
            {
                if (Owner.engineState == Ship.MoveState.Warp)
                    Owner.HyperspaceReturn();
                if (State != AIState.Bombard)
                    HasPriorityOrder = false;
            }
            if (distanceToOrbitSpot < 500f)
                ThrustTowardsPosition(OrbitPos, elapsedTime, Owner.speed > 300f ? 300f : Owner.speed);
            else
                ThrustTowardsPosition(OrbitPos, elapsedTime, Owner.speed);
        }
        
        //do troop rebase
		private void DoRebase(ShipGoal Goal)
		{
			if (Owner.TroopList.Count == 0)
			{
				Owner.QueueTotalRemoval();
			}
			else if (Goal.TargetPlanet.AssignTroopToTile(Owner.TroopList[0]))
			{
				Owner.TroopList.Clear();
				Owner.QueueTotalRemoval();
				return;
			}
            else
            {
                OrderQueue.Clear();
                State = AIState.AwaitingOrders;
            }
		}

        //added by gremlin refit while in fleet
        //do refit 
        private void DoRefit(float elapsedTime, ShipGoal goal)
        {
            QueueItem qi = new BuildShip(goal);
            if (qi.sData == null)
            {
                OrderQueue.Clear();
                State = AIState.AwaitingOrders;
            }
            var cost = (int)(ResourceManager.ShipsDict[goal.VariableString].GetCost(Owner.loyalty) - Owner.GetCost(Owner.loyalty));
            if (cost < 0)
                cost = 0;
            cost = cost + 10 * (int)UniverseScreen.GamePaceStatic;
            if (Owner.loyalty.isFaction)
                qi.Cost = 0;
            else
                qi.Cost = (float)cost;
            qi.isRefit = true;
            //Added by McShooterz: refit keeps name and level
            if(Owner.VanityName != Owner.Name)
                qi.RefitName = Owner.VanityName;
            qi.sData.Level = (byte)Owner.Level;
            if (Owner.fleet != null)
            {
                Goal refitgoal = new FleetRequisition(goal,this);
                node.GoalGUID = refitgoal.guid;
                Owner.loyalty.GetGSAI().Goals.Add(refitgoal);
                qi.Goal = refitgoal;
            }
            OrbitTarget.ConstructionQueue.Add(qi);
            Owner.QueueTotalRemoval();
        }
        //do repair drone
		private void DoRepairDroneLogic(Weapon w)
		{
            // @todo Refactor this bloody mess
            //Turns out the ship was used to get a vector to the target ship and not actually used for any kind of targeting. 
            
		    Ship friendliesNearby =null;
		    using (FriendliesNearby.AcquireReadLock())
		    {
		        foreach (Ship ship in FriendliesNearby)
		        {
                    if (!ship.Active || ship.HealthMax * 0.95f < ship.Health 
                        || !Owner.Center.InRadius(ship.Center, 20000f))
                        continue;
                    friendliesNearby = ship;
		            break;
		        }
                if (friendliesNearby == null) return;
                Vector2 target = w.Center.FindVectorToTarget(friendliesNearby.Center);
                target.Y = target.Y * -1f;
                w.FireDrone(Vector2.Normalize(target));
            }            
		}
        //do repair beam
        private void DoRepairBeamLogic(Weapon w)
        {
            //foreach (Ship ship in w.GetOwner().loyalty.GetShips()
            foreach (Ship ship in FriendliesNearby
                .Where(ship => ship.Active && ship != w.GetOwner() 
                    && ship.Health / ship.HealthMax <.9f
                    && Vector2.Distance(Owner.Center, ship.Center) <= w.Range + 500f)
                    .OrderBy(ship => ship.Health))
                if (ship != null)
                {
                    w.FireTargetedBeam(ship);
                    return;
                }
        }
        //do ordinance transporter @TODO move to module and cleanup. this is a mod only method. Low priority
        private void DoOrdinanceTransporterLogic(ShipModule module)
        {
            foreach (Ship ship in module.GetParent().loyalty.GetShips()
                .Where(ship => Vector2.Distance(Owner.Center, ship.Center) <= module.TransporterRange + 500f 
                && ship.Ordinance < ship.OrdinanceMax && !ship.hasOrdnanceTransporter)
                .OrderBy(ship => ship.Ordinance).ToList())
                if (ship != null)
                {
                    module.TransporterTimer = module.TransporterTimerConstant;
                    var TransferAmount = 0f;
                    //check how much can be taken
                    if (module.TransporterOrdnance > module.GetParent().Ordinance)
                        TransferAmount = module.GetParent().Ordinance;
                    else
                        TransferAmount = module.TransporterOrdnance;
                    //check how much can be given
                    if (TransferAmount > ship.OrdinanceMax - ship.Ordinance)
                        TransferAmount = ship.OrdinanceMax - ship.Ordinance;
                    //Transfer
                    ship.Ordinance += TransferAmount;
                    module.GetParent().Ordinance -= TransferAmount;
                    module.GetParent().PowerCurrent -= module.TransporterPower * (TransferAmount / module.TransporterOrdnance);
                    if(Owner.InFrustum && ResourceManager.SoundEffectDict.ContainsKey("transporter"))
                    {
                        GameplayObject.audioListener.Position = ShipModule.universeScreen.camPos;
                        AudioManager.PlaySoundEffect(ResourceManager.SoundEffectDict["transporter"], GameplayObject.audioListener, module.GetParent().emitter, 0.5f);
                    }
                    return;
                }
        }
        //do transporter assault  @TODO move to module and cleanup. this is a mod only method. Low priority
        private void DoAssaultTransporterLogic(ShipModule module)
        {
            foreach (ShipWeight ship in NearbyShips.Where(Ship => Ship.ship.loyalty != null && Ship.ship.loyalty != Owner.loyalty && Ship.ship.shield_power <= 0 && Vector2.Distance(Owner.Center, Ship.ship.Center) <= module.TransporterRange + 500f).OrderBy(Ship => Vector2.Distance(Owner.Center, Ship.ship.Center)))
                if (ship != null)
                {
                    byte TroopCount = 0;
                    var Transported = false;
                    for (byte i = 0; i < Owner.TroopList.Count(); i++)
                    {
                        if (Owner.TroopList[i] == null)
                            continue;
                        if (Owner.TroopList[i].GetOwner() == Owner.loyalty)
                        {
                            ship.ship.TroopList.Add(Owner.TroopList[i]);
                            Owner.TroopList.Remove(Owner.TroopList[i]);
                            TroopCount++;
                            Transported = true;
                        }
                        if (TroopCount == module.TransporterTroopAssault)
                            break;
                    }
                    if (Transported)
                    {
                        module.TransporterTimer = module.TransporterTimerConstant;
                        if (Owner.InFrustum && ResourceManager.SoundEffectDict.ContainsKey("transporter"))
                        {
                            GameplayObject.audioListener.Position = ShipModule.universeScreen.camPos;
                            AudioManager.PlaySoundEffect(ResourceManager.SoundEffectDict["transporter"], GameplayObject.audioListener, module.GetParent().emitter, 0.5f);
                        }
                        return;
                    }
                }
        }

        //do hangar return
		private void DoReturnToHangar(float elapsedTime)
		{
			if (Owner.Mothership == null || !Owner.Mothership.Active)
			{
				OrderQueue.Clear();
				return;
			}
			ThrustTowardsPosition(Owner.Mothership.Center, elapsedTime, Owner.speed);			
            if (Owner.Center.InRadius(Owner.Mothership.Center, Owner.Mothership.Radius + 300f))
			{
				if (Owner.Mothership.TroopCapacity > Owner.Mothership.TroopList.Count && Owner.TroopList.Count == 1)
				    Owner.Mothership.TroopList.Add(Owner.TroopList[0]);
			    if (Owner.shipData.Role == ShipData.RoleName.supply)  //fbedard: Supply ship return with Ordinance
                    Owner.Mothership.Ordinance += Owner.Ordinance;
                Owner.Mothership.Ordinance += Owner.Mass / 5f;        //fbedard: New spawning cost
                if (Owner.Mothership.Ordinance > Owner.Mothership.OrdinanceMax)
                    Owner.Mothership.Ordinance = Owner.Mothership.OrdinanceMax;
                Owner.QueueTotalRemoval();
                foreach (ShipModule hangar in Owner.Mothership.GetHangars())
                {
                    if (hangar.GetHangarShip() != Owner)
                        continue;
                    //added by gremlin: prevent fighters from relaunching immediatly after landing.
                    float ammoReloadTime = Owner.OrdinanceMax * .1f;
                    float shieldrechargeTime = Owner.shield_max * .1f;
                    float powerRechargeTime = Owner.PowerStoreMax * .1f;
                    float rearmTime = Owner.Health;
                    rearmTime += Owner.Ordinance*.1f;
                    rearmTime += Owner.PowerCurrent * .1f;
                    rearmTime += Owner.shield_power * .1f;
                    rearmTime /= Owner.HealthMax + ammoReloadTime + shieldrechargeTime + powerRechargeTime;                    
                    rearmTime = (1.01f - rearmTime) * (hangar.hangarTimerConstant *(1.01f- (Owner.Level + hangar.GetParent().Level)/10 ));  // fbedard: rearm time from 50% to 150%
                    if (rearmTime < 0)
                        rearmTime = 1;
                    //CG: if the fighter is fully functional reduce rearm time to very little. The default 5 minute hangar timer is way too high. It cripples fighter usage.
                    //at 50% that is still 2.5 minutes if the fighter simply launches and returns. with lag that can easily be 10 or 20 minutes. 
                    //at 1.01 that should be 3 seconds for the default hangar.
                    hangar.SetHangarShip(null);
                    hangar.hangarTimer = rearmTime;
                    hangar.installedSlot.HangarshipGuid = Guid.Empty;                   
                }
			}
		}
        //do supply ship
		private void DoSupplyShip(float elapsedTime, ShipGoal goal)
		{
			if (EscortTarget == null || !EscortTarget.Active)
			{
				OrderQueue.Clear();
                OrderResupplyNearest(false);
				return;
			}
            if (EscortTarget.GetAI().State == AIState.Resupply || EscortTarget.GetAI().State == AIState.Scrap ||EscortTarget.GetAI().State == AIState.Refit)
            {
                OrderQueue.Clear();
                OrderResupplyNearest(false);
                return;
            }
			ThrustTowardsPosition(EscortTarget.Center, elapsedTime, Owner.speed);
			if (Owner.Center.InRadius(EscortTarget.Center, EscortTarget.Radius + 300f))                
			{
                float ord_amt = Owner.Ordinance;
				Ship escortTarget = EscortTarget;
                if (EscortTarget.Ordinance + ord_amt > EscortTarget.OrdinanceMax)
                    ord_amt = EscortTarget.OrdinanceMax - EscortTarget.Ordinance;
                EscortTarget.Ordinance += ord_amt;
                Owner.Ordinance -= ord_amt;
				OrderQueue.Clear();
                if (Owner.Ordinance > 0)
                    State = AIState.AwaitingOrders;
                else
                    Owner.ReturnToHangar();
			}
		}
        // do system defense
		private void DoSystemDefense(float elapsedTime)
		{
            SystemToDefend=SystemToDefend ?? Owner.System;                 
            if (SystemToDefend == null ||  awaitClosest?.Owner == Owner.loyalty)
                AwaitOrders(elapsedTime);
            else
                OrderSystemDefense(SystemToDefend);              
		}
        //do troop board ship
		private void DoTroopToShip(float elapsedTime)
		{
			if (EscortTarget == null || !EscortTarget.Active)
			{
				OrderQueue.Clear();
				return;
			}
			MoveTowardsPosition(EscortTarget.Center, elapsedTime);
            if (Owner.Center.InRadius(EscortTarget.Center, EscortTarget.Radius + 300f))
			{
				if (EscortTarget.TroopCapacity > EscortTarget.TroopList.Count)
				{
					EscortTarget.TroopList.Add(Owner.TroopList[0]);
					Owner.QueueTotalRemoval();
					return;
				}
				OrbitShip(EscortTarget, elapsedTime);
			}
		}
        //trade goods drop off
	    private void DropoffGoods()
	    {
	        if (end != null)
	        {
	            if (Owner.loyalty.data.Traits.Mercantile > 0f)
	                Owner.loyalty.AddTradeMoney(Owner.CargoSpace_Used * Owner.loyalty.data.Traits.Mercantile);

	            if (Owner.GetCargo()["Food"] > 0f)
	            {
	                int maxfood = (int) end.MAX_STORAGE - (int) end.FoodHere;
	                if (end.FoodHere + Owner.GetCargo()["Food"] <= end.MAX_STORAGE)
	                {
	                    Planet foodHere = end;
	                    foodHere.FoodHere = foodHere.FoodHere + (int) Owner.GetCargo()["Food"];
	                    Owner.GetCargo()["Food"] = 0f;
	                }
	                else
	                {
	                    Planet planet = end;
	                    planet.FoodHere = planet.FoodHere + maxfood;
	                    var cargo = Owner.GetCargo();
	                    var strs = cargo;
	                    cargo["Food"] = strs["Food"] - maxfood;
	                }
	            }
	            if (Owner.GetCargo()["Production"] > 0f)
	            {
	                int maxprod = (int) end.MAX_STORAGE - (int) end.ProductionHere;
	                if (end.ProductionHere + Owner.GetCargo()["Production"] <= end.MAX_STORAGE)
	                {
	                    Planet productionHere = end;
	                    productionHere.ProductionHere = productionHere.ProductionHere +
	                                                    (int) Owner.GetCargo()["Production"];
	                    Owner.GetCargo()["Production"] = 0f;
	                }
	                else
	                {
	                    Planet productionHere1 = end;
	                    productionHere1.ProductionHere = productionHere1.ProductionHere + maxprod;
	                    var item = Owner.GetCargo();
	                    var strs1 = item;
	                    item["Production"] = strs1["Production"] - maxprod;
	                }
	            }
	        }
	        start = null;
	        end = null;
	        OrderQueue.RemoveFirst();
	        OrderTrade(5f);
	    }
        //trade passengers drop off
	    private void DropoffPassengers()
	    {
	        if (end == null)
	        {
	            OrderQueue.RemoveFirst();
	            OrderTransportPassengers(0.1f);
	            return;
	        }

	        while (Owner.GetCargo()["Colonists_1000"] > 0f)
	        {
	            var cargo = Owner.GetCargo();
	            cargo["Colonists_1000"] = cargo["Colonists_1000"] - 1f;
	            Planet population = end;
	            population.Population = population.Population + Owner.loyalty.data.Traits.PassengerModifier;
	        }
	        if (end.Population > end.MaxPopulation + end.MaxPopBonus)
	            end.Population = end.MaxPopulation + end.MaxPopBonus;
	        OrderQueue.RemoveFirst();
	        start = null;
	        end = null;
	        OrderTransportPassengers(5f);
	    }
        //explore system
		private bool ExploreEmptySystem(float elapsedTime, SolarSystem system)
		{
			if (system.ExploredDict[Owner.loyalty])
			    return true;
		    MovePosition = system.Position;
			float Distance = Owner.Center.Distance(MovePosition);
			if (Distance < 75000f)
			{
				system.ExploredDict[Owner.loyalty] = true;
				return true;
			}
			if (Distance > 75000f)
			    ThrustTowardsPosition(MovePosition, elapsedTime, Owner.speed);
		    return false;
		}


        //fire on target
        public void FireOnTarget() //(float elapsedTime)
        {
            try
            {
                TargetShip = Target as Ship;
                //Relationship enemy =null;
                //base reasons not to fire. @TODO actions decided by loyalty like should be the same in all areas. 
                if (!Owner.hasCommand || Owner.engineState == Ship.MoveState.Warp || Owner.disabled ||
                    Owner.Weapons.Count == 0
                )
                    return;
                var hasPD = false;
                //Determine if there is something to shoot at
                if (BadGuysNear)
                {
                    //Target is dead or dying, will need a new one.
                    if (Target != null && (!Target.Active || TargetShip != null && TargetShip.dying))
                    {
                        foreach (Weapon purge in Owner.Weapons)
                        {
                            if (purge.Tag_PD || purge.TruePD)
                                hasPD = true;
                            if (purge.PrimaryTarget)
                            {
                                purge.PrimaryTarget = false;
                                purge.fireTarget = null;
                                purge.SalvoTarget = null;
                            }
                        }
                        Target = null;
                        TargetShip = null;
                    }
                    foreach (Weapon purge in Owner.Weapons)
                    {
                        if (purge.Tag_PD || purge.TruePD)
                            hasPD = true;
                        else continue;
                        break;
                    }
                    TrackProjectiles.Clear(); 
                    if (Owner.Mothership != null)
                        TrackProjectiles.AddRange(Owner.Mothership.GetAI().TrackProjectiles);
                    if (Owner.TrackingPower > 0 && hasPD) //update projectile list                         
                    {

                        if (Owner.System!= null)
                            foreach (GameplayObject missile in Owner.System.spatialManager.GetNearby(Owner))
                            {
                                var targettrack = missile as Projectile;
                                if (targettrack == null || targettrack.loyalty == Owner.loyalty || !targettrack.weapon.Tag_Intercept)
                                    continue;
                                TrackProjectiles.Add(targettrack);
                            }
                        else
                            foreach (GameplayObject missile in UniverseScreen.DeepSpaceManager.GetNearby(Owner))
                            {
                                var targettrack = missile as Projectile;
                                if (targettrack == null || targettrack.loyalty == Owner.loyalty || !targettrack.weapon.Tag_Intercept)
                                    continue;
                                TrackProjectiles.Add(targettrack);
                            }
                        TrackProjectiles = TrackProjectiles.OrderBy(prj =>  Vector2.Distance(Owner.Center, prj.Center)).ToArrayList();
                    }
       
                    float lag = Ship.universeScreen.Lag;
                    //Go through each weapon
                    float index = 0; //count up weapons.
                    //save target ship if it is a ship.
                    TargetShip = Target as Ship;
                    //group of weapons into chunks per thread available
                    var source = Enumerable.Range(0, Owner.Weapons.Count).ToArray();
                            var rangePartitioner = Partitioner.Create(0, source.Length);
                    //handle each weapon group in parallel
                            Parallel.ForEach(rangePartitioner, (range, loopState) =>
                                           {
                                               //standard for loop through each weapon group.
                                               for (int T = range.Item1; T < range.Item2; T++)
                                               {
                                                   Weapon weapon = Owner.Weapons[T];
                                                   weapon.TargetChangeTimer -= 0.0167f;
                                                   //Reasons for this weapon not to fire 
                                                   if ( !weapon.moduleAttachedTo.Active 
                                                       || weapon.timeToNextFire > 0f 
                                                       || !weapon.moduleAttachedTo.Powered || weapon.IsRepairDrone || weapon.isRepairBeam
                                                       || weapon.PowerRequiredToFire > Owner.PowerCurrent
                                                       || weapon.TargetChangeTimer >0
                                                       )
                                                       continue;
                                                   if ((!weapon.TruePD || !weapon.Tag_PD) && Owner.isPlayerShip())
                                                       continue;
                                                   var moduletarget = weapon.fireTarget as ShipModule;
                                                   //if firing at the primary target mark weapon as firing on primary.
                                                   if (!(weapon.fireTarget is Projectile) && weapon.fireTarget != null && (weapon.fireTarget == Target || moduletarget != null && moduletarget.GetParent() as GameplayObject == Target))
                                                       weapon.PrimaryTarget = true;                                                   
                                                    //check if weapon target as a gameplay object is still a valid target    
                                                   if (weapon.fireTarget !=null )
                                                       if (weapon.fireTarget !=null && !Owner.CheckIfInsideFireArc(weapon, weapon.fireTarget)                                                           
                                                           //check here if the weapon can fire on main target.                                                           
                                                           || Target != null && weapon.SalvoTimer <=0 && weapon.BeamDuration <=0 && !weapon.PrimaryTarget && !(weapon.fireTarget is Projectile) && Owner.CheckIfInsideFireArc(weapon, Target)                                                         
                                                       )
                                                       {
                                                           weapon.TargetChangeTimer = .1f * weapon.moduleAttachedTo.XSIZE * weapon.moduleAttachedTo.YSIZE;
                                                           weapon.fireTarget = null;
                                                           //if (weapon.isBeam || weapon.isMainGun)
                                                           //    weapon.TargetChangeTimer = .90f;
                                                           if (weapon.isTurret)
                                                               weapon.TargetChangeTimer *= .5f;
                                                           if(weapon.Tag_PD)
                                                               weapon.TargetChangeTimer *= .5f;
                                                           if (weapon.TruePD)
                                                               weapon.TargetChangeTimer *= .25f;
                                                       }
                                                   //if weapon target is null reset primary target and decrement target change timer.
                                                   if (weapon.fireTarget == null && !Owner.isPlayerShip())
                                                       weapon.PrimaryTarget = false;
                                                   //Reasons for this weapon not to fire                    
                                                   if (weapon.fireTarget == null && weapon.TargetChangeTimer >0 ) // ||!weapon.moduleAttachedTo.Active || weapon.timeToNextFire > 0f || !weapon.moduleAttachedTo.Powered || weapon.IsRepairDrone || weapon.isRepairBeam)
                                                       continue;
                                                   //main targeting loop. little check here to disable the whole thing for debugging.
                                                   if (true)
                                                   {
                                                       //Can this weapon fire on ships
                                                       if (BadGuysNear && !weapon.TruePD  )
                                                       {
                                                           //if there are projectile to hit and weapons that can shoot at them. do so. 
                                                           if(TrackProjectiles.Count >0 && weapon.Tag_PD )
                                                               for (var i = 0; i < TrackProjectiles.Count && i < Owner.TrackingPower + Owner.Level; i++)
                                                               {
                                                                   Projectile proj;
                                                                   {
                                                                       proj = TrackProjectiles[i];
                                                                   }

                                                                   if (proj == null || !proj.Active || proj.Health <= 0 || !proj.weapon.Tag_Intercept)
                                                                       continue;
                                                                   if (Owner.CheckIfInsideFireArc(weapon, proj as GameplayObject))
                                                                   {
                                                                       weapon.fireTarget = proj;
                                                                       //AddTargetsTracked++;
                                                                       break;
                                                                   }
                                                               }
                                                           //Is primary target valid
                                                           if (weapon.fireTarget == null)
                                                               if (Owner.CheckIfInsideFireArc(weapon, Target))
                                                               {
                                                                   weapon.fireTarget = Target;
                                                                   weapon.PrimaryTarget = true;
                                                               }

                                                           //Find alternate target to fire on
                                                           //this seems to be very expensive code. 
                                                           if (true)
                                                               if (weapon.fireTarget == null && Owner.TrackingPower > 0)
                                                               {
                                                                   //limit to one target per level.
                                                                   sbyte tracking = Owner.TrackingPower;
                                                                   for (var i = 0; i < PotentialTargets.Count && i < tracking + Owner.Level; i++) //
                                                                   {
                                                                       Ship potentialTarget = PotentialTargets[i];
                                                                       if (potentialTarget == TargetShip)
                                                                       {
                                                                           tracking++;
                                                                           continue;                                                                           
                                                                       }
                                                                       if (!Owner.CheckIfInsideFireArc(weapon, potentialTarget))
                                                                           continue;
                                                                       weapon.fireTarget = potentialTarget;
                                                                       //AddTargetsTracked++;
                                                                       break;

                                                                   }
                                                               }
                                                           //If a ship was found to fire on, change to target an internal module if target is visible  || weapon.Tag_Intercept
                                                           if (weapon.fireTarget is Ship && (GlobalStats.ForceFullSim || Owner.InFrustum || (weapon.fireTarget as Ship).InFrustum))// || (this.Owner.InFrustum || this.Target != null && TargetShip.InFrustum)))
                                                               weapon.fireTarget = (weapon.fireTarget as Ship).GetRandomInternalModule(weapon);
                                                       }
                                                       //No ship to target, check for projectiles
                                                       if (weapon.fireTarget == null && weapon.Tag_PD)
                                                           if (weapon.fireTarget == null)
                                                               for (var i = 0; i < TrackProjectiles.Count && i < Owner.TrackingPower + Owner.Level; i++)
                                                               {
                                                                   Projectile proj;
                                                                   {
                                                                       proj = TrackProjectiles[i];
                                                                   }

                                                                   if (proj == null || !proj.Active || proj.Health <= 0 || !proj.weapon.Tag_Intercept)
                                                                       continue;
                                                                   if (Owner.CheckIfInsideFireArc(weapon, proj as GameplayObject))
                                                                   {
                                                                       weapon.fireTarget = proj;
                                                                       //AddTargetsTracked++;
                                                                       break;
                                                                   }
                                                               }
                                                   }
                                               }
                                           });
                    //this section actually fires the weapons. This whole firing section can be moved to some other area of the code. This code is very expensive. 
                    if(true)
                    foreach (Weapon weapon in Owner.Weapons)
                        if (weapon.fireTarget != null && weapon.moduleAttachedTo.Active && weapon.timeToNextFire <= 0f && weapon.moduleAttachedTo.Powered)
                            if (!(weapon.fireTarget is Ship))
                            {
                                GameplayObject target = weapon.fireTarget;
                                if (weapon.isBeam)
                                {
                                    weapon.FireTargetedBeam(target);
                                }
                                else if (weapon.Tag_Guided)
                                {
                                    if (index > 10 && lag > .05 && !GlobalStats.ForceFullSim && !weapon.Tag_Intercept && weapon.fireTarget is ShipModule)
                                        FireOnTargetNonVisible(weapon, (weapon.fireTarget as ShipModule).GetParent());
                                    else
                                        weapon.Fire(new Vector2((float)Math.Sin((double)Owner.Rotation + weapon.moduleAttachedTo.facing.ToRadians()), -(float)Math.Cos((double)Owner.Rotation + weapon.moduleAttachedTo.facing.ToRadians())), target);
                                    index++;
                                }
                                else
                                {
                                    if (index > 10 && lag > .05 && !GlobalStats.ForceFullSim && weapon.fireTarget is ShipModule)
                                        FireOnTargetNonVisible(weapon, (weapon.fireTarget as ShipModule).GetParent());
                                    else
                                        CalculateAndFire(weapon, target, false);
                                    index++;
                                }
                            }
                            else
                            {
                                FireOnTargetNonVisible(weapon, weapon.fireTarget);
                            }
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "FireOnTarget() crashed");
            }
            TargetShip = null;
        }
        //fire on calculate and fire
        public void CalculateAndFire(Weapon weapon, GameplayObject target, bool SalvoFire)
        {
            var moduleTarget = target as ShipModule;
            var projectileTarget = target as Projectile;
            Vector2 dir = Vector2.Zero;
            Vector2 projectedPosition = Vector2.Zero;
            if (projectileTarget !=null)
            {
                projectedPosition = weapon.Center.FindPredictedVectorToTarget(weapon.ProjectileSpeed,
                    target.Center, projectileTarget.Velocity);

                //float distance = weapon.Center.Distance(projectileTarget.Center) + projectileTarget.Velocity.Length() == 0 ? 0 : 500;
                //dir = Vector2.Zero;
      
                //dir = weapon.Center.FindVectorToTarget(projectileTarget.Center) * (weapon.ProjectileSpeed + Owner.Velocity.Length());
                //float timeToTarget = distance / dir.Length();
                //projectedPosition = projectileTarget.Center + projectileTarget.Velocity * timeToTarget;
                //distance = weapon.Center.Distance( projectedPosition);
                //dir = weapon.Center.FindVectorToTarget(projectedPosition) * (weapon.ProjectileSpeed + Owner.Velocity.Length());
                //timeToTarget = distance / dir.Length();
                //projectedPosition = projectileTarget.Center + projectileTarget.Velocity * timeToTarget * 0.85f;
            }
            else if (moduleTarget !=null)
            {
                projectedPosition = weapon.Center.FindPredictedVectorToTarget(weapon.ProjectileSpeed,
                    target.Center, moduleTarget.GetParent().Velocity);


                //else
                //{
                //    Vector2 VectorToTarget = weapon.Center.FindVectorToTarget(target.Center);
                //    float distanceToTarget = (target.Center - weapon.Center).Length();

                //    float a = moduleTarget.GetParent().Velocity.LengthSquared() - (weapon.ProjectileSpeed * weapon.ProjectileSpeed);
                //    float b = 2 * (Vector2.Dot(moduleTarget.Center, VectorToTarget));
                //    float c = VectorToTarget.LengthSquared();

                //    //Then solve the quadratic equation for a, b, and c.That is, time = (-b + -sqrt(b * b - 4 * a * c)) / 2a.
                //    float time = (float) ((-b + -Math.Sqrt(b * b - 4 * a * c)) / (2 * a));
                //    //If(b * b - 4 * a * c) is negative, or a is 0, the equation has no solution, and you can't hit the target. 
                //    //Otherwise, you'll end up with 2 values(or 1, if b * b - 4 * a * c is zero).
                //    //Those values are the time values at which point you can hit the target.
                //    //If any of them are negative, discard them, because you can't send the target back in time to hit it.  
                //    //Take any of the remaining positive values (probably the smaller one).
                //    Vector2 predictedVector = target.Center + moduleTarget.GetParent().Velocity * time;
                //    VectorToTarget = weapon.Center.FindVectorToTarget(predictedVector);
                //    if (SalvoFire)
                //        weapon.FireSalvo(VectorToTarget, target);
                //    else
                //        weapon.Fire(VectorToTarget, target);
                //}

                


               
            }
            if (Owner.CheckIfInsideFireArc(weapon, projectedPosition))
            {
                projectedPosition = Vector2.Normalize(projectedPosition - weapon.Center);
                if (SalvoFire)
                    weapon.FireSalvo(projectedPosition, target);
                else
                    weapon.Fire(projectedPosition, target);
            }

            //dir = weapon.Center.FindVectorToTarget( projectedPosition);
            //dir.Y = dir.Y * -1f;
            //if (moduleTarget ==null  || moduleTarget.GetParent().Velocity.Length() >0)
            //    dir = Vector2.Normalize(dir);

            //if (SalvoFire)
            //    weapon.FireSalvo(dir, target);
            //else
            //    weapon.Fire(dir, target);
        }
        //fire on non visible
        private void FireOnTargetNonVisible(Weapon w, GameplayObject fireTarget)
        {
            if (Owner.Ordinance < w.OrdinanceRequiredToFire || Owner.PowerCurrent < w.PowerRequiredToFire)
                return;
            w.timeToNextFire = w.fireDelay;
            if (w.IsRepairDrone)
                return;
            if (TargetShip == null || !TargetShip.Active || TargetShip.dying || !w.TargetValid(TargetShip.shipData.Role)
                || TargetShip.engineState == Ship.MoveState.Warp || !Owner.CheckIfInsideFireArc(w, TargetShip))
                return;
            Ship owner = Owner;
            owner.Ordinance = owner.Ordinance - w.OrdinanceRequiredToFire;
            Ship powerCurrent = Owner;
            powerCurrent.PowerCurrent = powerCurrent.PowerCurrent - w.PowerRequiredToFire;
            powerCurrent.PowerCurrent -= w.BeamPowerCostPerSecond * w.BeamDuration;

            Owner.InCombatTimer = 15f;
            if (fireTarget is Projectile)
            {
                fireTarget.Damage(w.GetOwner(), w.DamageAmount);
                return;
            }
            if (!(fireTarget is Ship))
            {

                if (fireTarget is ShipModule)
                {
                    w.timeToNextFire = w.fireDelay;
                    var sortedList =
                        from slot in (fireTarget as ShipModule).GetParent().ExternalSlots
                        orderby Vector2.Distance(slot.module.Center, Owner.Center)
                        select slot;
                    float damage = w.DamageAmount;
                    if (w.isBeam)
                        damage = damage * 90f;
                    if (w.SalvoCount > 0)
                        damage = damage * (float) w.SalvoCount;
                    sortedList.First().module.Damage(Owner, damage);
                }
                return;
            }
            w.timeToNextFire = w.fireDelay;
             var firetarget = fireTarget as Ship;
            if (firetarget.ExternalSlots.Count == 0)
            {
                firetarget.Die(null, true);
                return;
            }
            if ((fireTarget as Ship).GetAI().CombatState == CombatState.Evade)   //fbedard: firing on evading ship can miss !
                if (RandomMath.RandomBetween(0f, 100f) < 5f + firetarget.experience)
                    return;

            var nearest = 0;
            ModuleSlot ClosestES = null;
            //bad fix for external module badness.
            //Ray ffer = new Ray();
            //BoundingBox target = new BoundingBox();
            //ffer.Position=new Vector3(this.Owner.Center,0f);

            try
            {
                foreach (ModuleSlot ES in firetarget.ExternalSlots)
                {
                    if (ES.module.ModuleType == ShipModuleType.Dummy || !ES.module.Active || ES.module.Health <= 0)
                        continue;
                    var temp = (int)ES.module.Center.Distance(Owner.Center);
                    if (nearest == 0 || temp < nearest)
                    {
                        nearest = temp;
                        ClosestES = ES;
                    } 
                }
            }
            catch { }
            if (ClosestES == null)
                return;           
            var externalSlots = firetarget.ExternalSlots.
                Where(close => close.module.Active && close.module.ModuleType != ShipModuleType.Dummy 
                && close.module.quadrant == ClosestES.module.quadrant && close.module.Health > 0).ToList();                         
            if (firetarget.shield_power > 0f)
            {
                for (var i = 0; i < firetarget.GetShields().Count; i++)
                    if (firetarget.GetShields()[i].Active && firetarget.GetShields()[i].shield_power > 0f)
                    {
                        float damage = w.DamageAmount;
                        if (w.isBeam)
                            damage = damage * 90f;
                        if (w.SalvoCount > 0)
                            damage = damage * (float)w.SalvoCount;
                        firetarget.GetShields()[i].Damage(Owner, damage);
                        return;
                    }
                return;
            }            
            if (externalSlots.ElementAt(0).module.shield_power > 0f)
            {
                for (var i = 0; i < externalSlots.Count(); i++)
                    if (externalSlots.ElementAt(i).module.Active && externalSlots.ElementAt(i).module.shield_power <= 0f)
                    {
                        float damage = w.DamageAmount;
                        if (w.isBeam)
                            damage = damage * 90f;
                        if (w.SalvoCount > 0)
                            damage = damage * (float)w.SalvoCount;
                        externalSlots.ElementAt(i).module.Damage(Owner, damage);
                        return;
                    }
                return;
            }

            for (var i = 0; i < externalSlots.Count(); i++)
                if (externalSlots.ElementAt(i).module.Active && externalSlots.ElementAt(i).module.shield_power <= 0f)
                {
                    float damage = w.DamageAmount;
                    if (w.isBeam)
                        damage = damage * 90f;
                    if (w.SalvoCount > 0)
                        damage = damage * (float)w.SalvoCount;
                    externalSlots.ElementAt(i).module.Damage(Owner, damage);
                    return;
                }
        }
        //go colonize
		public void GoColonize(Planet p)
		{
			State = AIState.Colonize;
			ColonizeTarget = p;
			GotoStep = 0;
		}
        //go colonize
		public void GoColonize(Planet p, Goal g)
		{
			State = AIState.Colonize;
			ColonizeTarget = p;
			ColonizeGoal = g;
			GotoStep = 0;
			OrderColonization(p);
		}
        //go rebase
		public void GoRebase(Planet p)
		{
			HasPriorityOrder = true;
			State = AIState.Rebase;
			OrbitTarget = p;
			findNewPosTimer = 0f;
            GotoStep = 0;
			HasPriorityOrder = true;
			MovePosition.X = p.Position.X;
			MovePosition.Y = p.Position.Y;
		}
        //movement goto
		public void GoTo(Vector2 movePos, Vector2 facing)
		{
			GotoStep = 0;
			if (Owner.loyalty == EmpireManager.Player)
			    HasPriorityOrder = true;
		    MovePosition.X = movePos.X;
			MovePosition.Y = movePos.Y;
			FinalFacingVector = facing;
			State = AIState.MoveTo;
		}
        //movement hold posistion
		public void HoldPosition()
		{
                if (Owner.isSpooling || Owner.engineState == Ship.MoveState.Warp)
                    Owner.HyperspaceReturn();
		    State = AIState.HoldPosition;
                Owner.isThrusting = false;
		}
        //movement final approach
		private void MakeFinalApproach(float elapsedTime, ShipGoal Goal)
		{
            if (Goal.TargetPlanet != null)
                lock (WayPointLocker)
                {
                    ActiveWayPoints.Last().Equals(Goal.TargetPlanet.Position);
                    Goal.MovePosition = Goal.TargetPlanet.Position;
                }
		    //if (this.RotateToFaceMovePosition(elapsedTime, Goal.MovePosition))
            //{
            //    Goal.SpeedLimit *= .9f;
            //}
            //else
            //{
            //    Goal.SpeedLimit *= 1.1f;
            //    if (this.Owner.engineState == Ship.MoveState.Sublight)
            //    {
            //        if (Goal.SpeedLimit > this.Owner.GetSTLSpeed())
            //            Goal.SpeedLimit = this.Owner.GetSTLSpeed();
            //    }
            //    else if (Goal.SpeedLimit > this.Owner.GetmaxFTLSpeed)
            //        Goal.SpeedLimit = this.Owner.GetmaxFTLSpeed;
            //}
            Owner.HyperspaceReturn();
			Vector2 velocity = Owner.Velocity;
            if (Goal.TargetPlanet != null)
                velocity += Goal.TargetPlanet.Position;
			float timetostop = velocity.Length() / Goal.SpeedLimit;
			float Distance = Vector2.Distance(Owner.Center, Goal.MovePosition);
			if (Distance / (Goal.SpeedLimit + 0.001f) <= timetostop)
			{
				OrderQueue.RemoveFirst();
			}
			else
			{
                if (DistanceLast == Distance)
                    Goal.SpeedLimit++;
                ThrustTowardsPosition(Goal.MovePosition, elapsedTime, Goal.SpeedLimit);
			}
			DistanceLast = Distance;
		}
        //added by gremlin Deveksmod MakeFinalApproach
        private void MakeFinalApproachDev(float elapsedTime, ShipGoal Goal)
        {
            float speedLimit = (int)Goal.SpeedLimit;

            Owner.HyperspaceReturn();
            Vector2 velocity = Owner.Velocity;
            float Distance = Vector2.Distance(Owner.Center, Goal.MovePosition);
            double timetostop;

            timetostop = (double)velocity.Length() / speedLimit;

            //if(this.RotateToFaceMovePosition(elapsedTime, Goal))
            //{
            //    speedLimit--;
            //}
            //else
            //{
            //    speedLimit++;
            //    if(speedLimit > this.Owner.GetSTLSpeed())
            //        speedLimit=this.Owner.GetSTLSpeed();
            //}
            

            
            //ShipGoal preserveGoal = this.OrderQueue.Last();

            //if ((preserveGoal.TargetPlanet != null && this.Owner.fleet == null && Vector2.Distance(preserveGoal.TargetPlanet.Position, this.Owner.Center) > 7500) || this.DistanceLast == Distance)
            //{

            //    this.OrderQueue.Clear();
            //    this.OrderQueue.AddFirst(preserveGoal);
            //    return;
            //}

            if ((double)Distance / velocity.Length() <= timetostop)  //+ .005f) //(Distance  / (velocity.Length() ) <= timetostop)//
            {
                OrderQueue.RemoveFirst();
            }
            else
            {
                Goal.SpeedLimit = speedLimit;

                ThrustTowardsPosition(Goal.MovePosition, elapsedTime, speedLimit);
            }
            DistanceLast = Distance;
        }
        //movement final approach
		private void MakeFinalApproachFleet(float elapsedTime, ShipGoal Goal)
		{
			float Distance = Vector2.Distance(Owner.Center, Goal.fleet.Position + Owner.FleetOffset);
			if (Distance < 100f || DistanceLast > Distance)
			    OrderQueue.RemoveFirst();
			else
			    MoveTowardsPosition(Goal.fleet.Position + Owner.FleetOffset, elapsedTime, Goal.fleet.speed);
		    DistanceLast = Distance;
		}
        //movement in direction
		private void MoveInDirection(Vector2 direction, float elapsedTime)
		{
			if (!Owner.EnginesKnockedOut)
			{
				Owner.isThrusting = true;
				Vector2 wantedForward = Vector2.Normalize(direction);
				var forward = new Vector2((float)Math.Sin((double)Owner.Rotation), -(float)Math.Cos((double)Owner.Rotation));
				var right = new Vector2(-forward.Y, forward.X);
				var angleDiff = (float)Math.Acos((double)Vector2.Dot(wantedForward, forward));
				float facing = Vector2.Dot(wantedForward, right) > 0f ? 1f : -1f;
				if (angleDiff > 0.22f)
				{
					Owner.isTurning = true;
					float RotAmount = Math.Min(angleDiff, facing * elapsedTime * Owner.rotationRadiansPerSecond);
					if (Math.Abs(RotAmount) > angleDiff)
					    RotAmount = RotAmount <= 0f ? -angleDiff : angleDiff;
				    if (RotAmount > 0f)
					{
						if (Owner.yRotation > -Owner.maxBank)
						{
							Ship owner = Owner;
							owner.yRotation = owner.yRotation - Owner.yBankAmount;
						}
					}
					else if (RotAmount < 0f && Owner.yRotation < Owner.maxBank)
					{
						Ship ship = Owner;
						ship.yRotation = ship.yRotation + Owner.yBankAmount;
					}
					Ship rotation = Owner;
					rotation.Rotation = rotation.Rotation + RotAmount;
				}
				else if (Owner.yRotation > 0f)
				{
					Ship owner1 = Owner;
					owner1.yRotation = owner1.yRotation - Owner.yBankAmount;
					if (Owner.yRotation < 0f)
					    Owner.yRotation = 0f;
				}
				else if (Owner.yRotation < 0f)
				{
					Ship ship1 = Owner;
					ship1.yRotation = ship1.yRotation + Owner.yBankAmount;
					if (Owner.yRotation > 0f)
					    Owner.yRotation = 0f;
				}
				Ship velocity = Owner;
				velocity.Velocity = velocity.Velocity + Vector2.Normalize(forward) * (elapsedTime * Owner.speed);
				if (Owner.Velocity.Length() > Owner.velocityMaximum)
				    Owner.Velocity = Vector2.Normalize(Owner.Velocity) * Owner.velocityMaximum;
			}
		}

		private void MoveInDirectionAtSpeed(Vector2 direction, float elapsedTime, float speed)
		{
			if (speed == 0f)
			{
				Owner.isThrusting = false;
				Owner.Velocity = Vector2.Zero;
				return;
			}
			if (!Owner.EnginesKnockedOut)
			{
				Owner.isThrusting = true;
				Vector2 wantedForward = Vector2.Normalize(direction);
				var forward = new Vector2((float)Math.Sin((double)Owner.Rotation), -(float)Math.Cos((double)Owner.Rotation));
				var right = new Vector2(-forward.Y, forward.X);
				var angleDiff = (float)Math.Acos((double)Vector2.Dot(wantedForward, forward));
				float facing = Vector2.Dot(wantedForward, right) > 0f ? 1f : -1f;
				if (angleDiff <= 0.02f)
				{
					DeRotate();
				}
				else
				{
					Owner.isTurning = true;
					Ship owner = Owner;
					owner.Rotation = owner.Rotation + Math.Min(angleDiff, facing * elapsedTime * Owner.rotationRadiansPerSecond);
				}
				Ship velocity = Owner;
				velocity.Velocity = velocity.Velocity + Vector2.Normalize(forward) * (elapsedTime * speed);
				if (Owner.Velocity.Length() > speed)
				    Owner.Velocity = Vector2.Normalize(Owner.Velocity) * speed;
			}
		}
        //movement to posisiton
		private void MoveTowardsPosition(Vector2 Position, float elapsedTime)
		{
			if (Vector2.Distance(Owner.Center, Position) < 50f)
			{
				Owner.Velocity = Vector2.Zero;
				return;
			}
			Position = Position - Owner.Velocity;
			if (!Owner.EnginesKnockedOut)
			{
				Owner.isThrusting = true;
				Vector2 wantedForward = Owner.Center.FindVectorToTarget(Position);
				var forward = new Vector2((float)Math.Sin((double)Owner.Rotation), -(float)Math.Cos((double)Owner.Rotation));
				var right = new Vector2(-forward.Y, forward.X);
				var angleDiff = (float)Math.Acos((double)Vector2.Dot(wantedForward, forward));
				float facing = Vector2.Dot(wantedForward, right) > 0f ? 1f : -1f;
				if (angleDiff > 0.02f)
				{
					float RotAmount = Math.Min(angleDiff, facing * elapsedTime * Owner.rotationRadiansPerSecond);
					if (RotAmount > 0f)
					{
						if (Owner.yRotation > -Owner.maxBank)
						{
							Ship owner = Owner;
							owner.yRotation = owner.yRotation - Owner.yBankAmount;
						}
					}
					else if (RotAmount < 0f && Owner.yRotation < Owner.maxBank)
					{
						Ship ship = Owner;
						ship.yRotation = ship.yRotation + Owner.yBankAmount;
					}
					Owner.isTurning = true;
					Ship rotation = Owner;
					rotation.Rotation = rotation.Rotation + RotAmount;
				}
				float speedLimit = Owner.speed;
				if (Owner.isSpooling)
				    speedLimit = speedLimit * Owner.loyalty.data.FTLModifier;
				else if (Vector2.Distance(Position, Owner.Center) < speedLimit)
				    speedLimit = Vector2.Distance(Position, Owner.Center) * 0.75f;
			    Ship velocity = Owner;
				velocity.Velocity = velocity.Velocity + Vector2.Normalize(forward) * (elapsedTime * speedLimit);
				if (Owner.Velocity.Length() > speedLimit)
				    Owner.Velocity = Vector2.Normalize(Owner.Velocity) * speedLimit;
			}
		}
        /// <summary>
        /// movement to posistion
        /// </summary>
        /// <param name="Position"></param>
        /// <param name="elapsedTime"></param>
        /// <param name="speedLimit"></param>
		private void MoveTowardsPosition(Vector2 Position, float elapsedTime, float speedLimit)
		{
			if (speedLimit < 1f)
			    speedLimit = 200f;
		    Position = Position - Owner.Velocity;
			if (!Owner.EnginesKnockedOut)
			{
				Owner.isThrusting = true;
				Vector2 wantedForward = Owner.Center.FindVectorToTarget(Position);
				var forward = new Vector2((float)Math.Sin((double)Owner.Rotation), -(float)Math.Cos((double)Owner.Rotation));
				var right = new Vector2(-forward.Y, forward.X);
				var angleDiff = (float)Math.Acos((double)Vector2.Dot(wantedForward, forward));
				float facing = Vector2.Dot(wantedForward, right) > 0f ? 1f : -1f;
				if (angleDiff > 0.02f)
				{
					float RotAmount = Math.Min(angleDiff, facing * elapsedTime * Owner.rotationRadiansPerSecond);
					if (RotAmount > 0f)
					{
						if (Owner.yRotation > -Owner.maxBank)
						{
							Ship owner = Owner;
							owner.yRotation = owner.yRotation - Owner.yBankAmount;
						}
					}
					else if (RotAmount < 0f && Owner.yRotation < Owner.maxBank)
					{
						Ship ship = Owner;
						ship.yRotation = ship.yRotation + Owner.yBankAmount;
					}
					Owner.isTurning = true;
					Ship rotation = Owner;
					rotation.Rotation = rotation.Rotation + RotAmount;
				}
				if (Owner.isSpooling)
				    speedLimit = speedLimit * Owner.loyalty.data.FTLModifier;
			    Ship velocity = Owner;
				velocity.Velocity = velocity.Velocity + Vector2.Normalize(forward) * (elapsedTime * speedLimit);
				if (Owner.Velocity.Length() > speedLimit)
				    Owner.Velocity = Vector2.Normalize(Owner.Velocity) * speedLimit;
			}
		}
        //order movement 1000
		private void MoveToWithin1000(float elapsedTime, ShipGoal goal)
        {

            var distWaypt = 15000f; //fbedard
            if (ActiveWayPoints.Count > 1)  
                distWaypt = Empire.ProjectorRadius / 2f;

            if (OrderQueue.Count > 1 && OrderQueue.Skip(1).First().Plan != Plan.MoveToWithin1000 && goal.TargetPlanet != null)
                lock (WayPointLocker)
                {
                    ActiveWayPoints.Last().Equals(goal.TargetPlanet.Position);
                    goal.MovePosition = goal.TargetPlanet.Position;
                }
            float speedLimit =  (int)Owner.speed  ;
            float single = Vector2.Distance(Owner.Center, goal.MovePosition);
            if (ActiveWayPoints.Count <= 1)
                if (single  < Owner.speed)
                    speedLimit = single;
            ThrustTowardsPosition(goal.MovePosition, elapsedTime, speedLimit);
            if (ActiveWayPoints.Count <= 1)
            {
                if (single <= 1500f)
                    lock (WayPointLocker)
                    {
                        if (ActiveWayPoints.Count > 1)
                            ActiveWayPoints.Dequeue();
                        if (OrderQueue.Count > 0)
                            OrderQueue.RemoveFirst();
                    }
                //else if(this.ColonizeTarget !=null)
                //{
                //    lock (this.WayPointLocker)
                //    {
                //        this.ActiveWayPoints.First().Equals(this.ColonizeTarget.Position);
                //        this.OrderQueue.First().MovePosition = this.ColonizeTarget.Position;
                //    }
                //}
                

            }
            else if (Owner.engineState == Ship.MoveState.Warp)
            {
                if (single <= distWaypt)
                    lock (WayPointLocker)
                    {
                        ActiveWayPoints.Dequeue();
                        if (OrderQueue.Count > 0)
                            OrderQueue.RemoveFirst();
                    }
                //if (this.ColonizeTarget != null )
                //{
                //    lock (this.WayPointLocker)
                //    {

                //        if (this.OrderQueue.Where(cgoal => cgoal.Plan == Plan.MoveToWithin1000).Count() == 1)
                //        {
                //            this.ActiveWayPoints.First().Equals(this.ColonizeTarget.Position);
                //            this.OrderQueue.First().MovePosition = this.ColonizeTarget.Position;
                //        }
                //    }
                //}
            }
            else if (single <= 1500f)
            {
                lock (WayPointLocker)
                {
                    ActiveWayPoints.Dequeue();
                    if (OrderQueue.Count > 0)
                        OrderQueue.RemoveFirst();
                }
            }
            //else if (this.ColonizeTarget != null)
            //{
            //    lock (this.WayPointLocker)
            //    {
            //        this.ActiveWayPoints.First().Equals(this.ColonizeTarget.Position);
            //    }
            //}
        }
        //order movement fleet 1000
		private void MoveToWithin1000Fleet(float elapsedTime, ShipGoal goal)
		{
			float Distance = Vector2.Distance(Owner.Center, goal.fleet.Position + Owner.FleetOffset);
            float speedLimit = goal.SpeedLimit;
            if (Owner.velocityMaximum >= Distance)
                speedLimit = Distance;

		    if (Distance > 10000f)
			{
				Owner.EngageStarDrive();
			}
			else if (Distance < 1000f)
			{
				Owner.HyperspaceReturn();
				OrderQueue.RemoveFirst();
				return;
			}
            MoveTowardsPosition(goal.fleet.Position + Owner.FleetOffset, elapsedTime, speedLimit);
		}
        //order orbit ship
        private void OrbitShip(Ship ship, float elapsedTime)
		{
			OrbitPos = ship.Center.PointOnCircle(OrbitalAngle, 1500f);
			if (Vector2.Distance(OrbitPos, Owner.Center) < 1500f)
			{
				ArtificialIntelligence orbitalAngle = this;
				orbitalAngle.OrbitalAngle = orbitalAngle.OrbitalAngle + 15f;
				if (OrbitalAngle >= 360f)
				{
					ArtificialIntelligence artificialIntelligence = this;
					artificialIntelligence.OrbitalAngle = artificialIntelligence.OrbitalAngle - 360f;
				}
				OrbitPos = ship.Position.PointOnCircle(OrbitalAngle, 2500f);
			}
			ThrustTowardsPosition(OrbitPos, elapsedTime, Owner.speed);
		}
        //order orbit ship
		private void OrbitShipLeft(Ship ship, float elapsedTime)
		{
			OrbitPos = ship.Center.PointOnCircle(OrbitalAngle, 1500f);
			if (Vector2.Distance(OrbitPos, Owner.Center) < 1500f)
			{
				ArtificialIntelligence orbitalAngle = this;
				orbitalAngle.OrbitalAngle = orbitalAngle.OrbitalAngle - 15f;
				if (OrbitalAngle >= 360f)
				{
					ArtificialIntelligence artificialIntelligence = this;
					artificialIntelligence.OrbitalAngle = artificialIntelligence.OrbitalAngle - 360f;
				}
				OrbitPos = ship.Position.PointOnCircle(OrbitalAngle, 2500f);
			}
			ThrustTowardsPosition(OrbitPos, elapsedTime, Owner.speed);
		}
        //order movement stop
		public void OrderAllStop()
		{
			OrderQueue.Clear();
			lock (WayPointLocker)
			{
				ActiveWayPoints.Clear();
			}
			State = AIState.HoldPosition;
            HasPriorityOrder = false;
			var stop = new ShipGoal(Plan.Stop, Vector2.Zero, 0f);            
			OrderQueue.AddLast(stop);
		}

	
        //order target ship
		public void OrderAttackSpecificTarget(Ship toAttack)
		{
			TargetQueue.Clear();
            
			if (toAttack == null)
			    return;

		    if (Owner.loyalty.TryGetRelations(toAttack.loyalty, out Relationship relations))
			{
				if (!relations.Treaty_Peace)
				{
					if (State == AIState.AttackTarget && Target == toAttack)
					    return;
				    if (State == AIState.SystemDefender && Target == toAttack)
				        return;
				    if (Owner.Weapons.Count == 0 || Owner.shipData.Role == ShipData.RoleName.troop)
					{
						OrderInterceptShip(toAttack);
						return;
					}
					Intercepting = true;
					lock (WayPointLocker)
					{
						ActiveWayPoints.Clear();
					}
					State = AIState.AttackTarget;
					Target = toAttack;
					Owner.InCombatTimer = 15f;
					OrderQueue.Clear();
					IgnoreCombat = false;
					TargetQueue.Add(toAttack);
					hasPriorityTarget = true;
					HasPriorityOrder = false;
					var combat = new ShipGoal(Plan.DoCombat, Vector2.Zero, 0f);
					OrderQueue.AddLast(combat);
					return;
				}
				OrderInterceptShip(toAttack);
			}
		}
        //order bomb planet
		public void OrderBombardPlanet(Planet toBombard)
		{
			lock (WayPointLocker)
			{
				ActiveWayPoints.Clear();
			}
			State = AIState. Bombard;
			Owner.InCombatTimer = 15f;
			OrderQueue.Clear();
			HasPriorityOrder = true;
			var combat = new ShipGoal(Plan.Bombard, Vector2.Zero, 0f)
			{
				TargetPlanet = toBombard
			};
			OrderQueue.AddLast(combat);
		}
        //order colinization
		public void OrderColonization(Planet toColonize)
		{
			if (toColonize == null)
			    return;
		    ColonizeTarget = toColonize;
			OrderMoveTowardsPosition(toColonize.Position, 0f, new Vector2(0f, -1f), true, toColonize);
            var colonize = new ShipGoal(Plan.Colonize, toColonize.Position, 0f)
			{
				TargetPlanet = ColonizeTarget
			};
			OrderQueue.AddLast(colonize);
			State = AIState.Colonize;
		}
        //order build platform no planet
		public void OrderDeepSpaceBuild(Goal goal)
		{
            OrderQueue.Clear();
            OrderMoveTowardsPosition(goal.BuildPosition, Owner.Center.RadiansToTarget(goal.BuildPosition), Owner.Center.FindVectorToTarget( goal.BuildPosition), true,null);
			var Deploy = new ShipGoal(Plan.DeployStructure, goal.BuildPosition, Owner.Center.RadiansToTarget(goal.BuildPosition))
			{
				goal = goal,
				VariableString = goal.ToBuildUID                
			};            
			OrderQueue.AddLast(Deploy);
          
		}
        //order explore
		public void OrderExplore()
		{
			if (State == AIState.Explore && ExplorationTarget != null)
			    return;
		    lock (WayPointLocker)
			{
				ActiveWayPoints.Clear();
			}
			OrderQueue.Clear();
			State = AIState.Explore;
			var Explore = new ShipGoal(Plan.Explore, Vector2.Zero, 0f);
			OrderQueue.AddLast(Explore);
		}
        //order remenant exterminate planet
		public void OrderExterminatePlanet(Planet toBombard)
		{
			lock (WayPointLocker)
			{
				ActiveWayPoints.Clear();
			}
			State = AIState.Exterminate;
			OrderQueue.Clear();
			var combat = new ShipGoal(Plan.Exterminate, Vector2.Zero, 0f)
			{
				TargetPlanet = toBombard
			};
			OrderQueue.AddLast(combat);
		}
        //order remnant exterminate target
		public void OrderFindExterminationTarget(bool ClearOrders)
		{
			if (ExterminationTarget == null || ExterminationTarget.Owner == null)
			{
				var plist = new Array<Planet>();
				foreach (var planetsDict in universeScreen.PlanetsDict)
				{
					if (planetsDict.Value.Owner == null)
					    continue;
				    plist.Add(planetsDict.Value);
				}
				var sortedList = 
					from planet in plist
					orderby Vector2.Distance(Owner.Center, planet.Position)
					select planet;
				if (sortedList.Any())
				{
					ExterminationTarget = sortedList.First<Planet>();
					OrderExterminatePlanet(ExterminationTarget);
					return;
				}
			}
			else if (ExterminationTarget != null && OrderQueue.Count == 0)
			{
				OrderExterminatePlanet(ExterminationTarget);
			}
		}
        //order movement fleet 
		public void OrderFormationWarp(Vector2 destination, float facing, Vector2 fvec)
		{
			lock (WayPointLocker)
			{
				ActiveWayPoints.Clear();
			}
			OrderQueue.Clear();
			OrderMoveDirectlyTowardsPosition(destination, facing, fvec, true, Owner.fleet.speed);
			State = AIState.FormationWarp;
		}
        //order movement fleet queued
		public void OrderFormationWarpQ(Vector2 destination, float facing, Vector2 fvec)
		{
			OrderMoveDirectlyTowardsPosition(destination, facing, fvec, false, Owner.fleet.speed);
			State = AIState.FormationWarp;
		}
        //order intercept
		public void OrderInterceptShip(Ship toIntercept)
		{
			Intercepting = true;
			lock (WayPointLocker)
			{
				ActiveWayPoints.Clear();
			}
			State = AIState.Intercept;
			Target = toIntercept;
			hasPriorityTarget = true;
			HasPriorityOrder = false;
			OrderQueue.Clear();
		}
        //order troops landall
		public void OrderLandAllTroops(Planet target)
		{
            if ((Owner.shipData.Role == ShipData.RoleName.troop || Owner.HasTroopBay || Owner.hasTransporter) && Owner.TroopList.Count > 0 && target.GetGroundLandingSpots() > 0)
            {
                HasPriorityOrder = true;
                State = AIState.AssaultPlanet;
                OrbitTarget = target;
                OrderQueue.Clear();
                lock (ActiveWayPoints)
                {
                    ActiveWayPoints.Clear();
                }
                var goal = new ShipGoal(Plan.LandTroop, Vector2.Zero, 0f)
                {
                    TargetPlanet = target
                };
                OrderQueue.AddLast(goal);
            }
            //else if (this.Owner.BombBays.Count > 0 && target.GetGroundStrength(this.Owner.loyalty) ==0)  //universeScreen.player == this.Owner.loyalty && 
            //{
            //    this.State = AIState.Bombard;
            //    this.OrderBombardTroops(target);
            //}
		}
        //order movement no pathing
        public void OrderMoveDirectlyTowardsPosition(Vector2 position, float desiredFacing, Vector2 fVec, bool ClearOrders)
		{
			Target = null;
			hasPriorityTarget = false;
			Vector2 wantedForward = Owner.Center.FindVectorToTarget(position);
			var forward = new Vector2((float)Math.Sin((double)Owner.Rotation), -(float)Math.Cos((double)Owner.Rotation));
			var right = new Vector2(-forward.Y, forward.X);
			var angleDiff = (float)Math.Acos((double)Vector2.Dot(wantedForward, forward));
			Vector2.Dot(wantedForward, right);
			if (angleDiff > 0.2f)
			    Owner.HyperspaceReturn();
		    OrderQueue.Clear();
			if (ClearOrders)
			    lock (WayPointLocker)
			    {
			        ActiveWayPoints.Clear();
			    }
		    if (Owner.loyalty == EmpireManager.Player)
		        HasPriorityOrder = true;
		    State = AIState.MoveTo;
			MovePosition = position;
			lock (WayPointLocker)
			{
				ActiveWayPoints.Enqueue(position);
			}
			FinalFacingVector = fVec;
			DesiredFacing = desiredFacing;
			lock (WayPointLocker)
			{
				for (var i = 0; i < ActiveWayPoints.Count; i++)
				{
					Vector2 waypoint = ActiveWayPoints.ToArray()[i];
					if (i != 0)
					{
						var to1k = new ShipGoal(Plan.MoveToWithin1000, waypoint, desiredFacing)
						{
							SpeedLimit = Owner.speed
						};
						OrderQueue.AddLast(to1k);
					}
					else
					{
						OrderQueue.AddLast(new ShipGoal(Plan.RotateToFaceMovePosition, waypoint, 0f));
						var to1k = new ShipGoal(Plan.MoveToWithin1000, waypoint, desiredFacing)
						{
							SpeedLimit = Owner.speed
						};
						OrderQueue.AddLast(to1k);
					}
					if (i == ActiveWayPoints.Count - 1)
					{
						var finalApproach = new ShipGoal(Plan.MakeFinalApproach, waypoint, desiredFacing)
						{
							SpeedLimit = Owner.speed
						};
						OrderQueue.AddLast(finalApproach);
						var slow = new ShipGoal(Plan.StopWithBackThrust, waypoint, 0f)
						{
							SpeedLimit = Owner.speed
						};
						OrderQueue.AddLast(slow);
						OrderQueue.AddLast(new ShipGoal(Plan.RotateToDesiredFacing, waypoint, desiredFacing));
					}
				}
			}
		}
        //order movement no pathing
		public void OrderMoveDirectlyTowardsPosition(Vector2 position, float desiredFacing, Vector2 fVec, bool ClearOrders, float speedLimit)
		{
			Target = null;
			hasPriorityTarget = false;
		    Vector2 wantedForward = Owner.Center.FindVectorToTarget(position);
			var forward = new Vector2((float)Math.Sin((double)Owner.Rotation), -(float)Math.Cos((double)Owner.Rotation));
			var right = new Vector2(-forward.Y, forward.X);
			var angleDiff = (float)Math.Acos((double)Vector2.Dot(wantedForward, forward));
			Vector2.Dot(wantedForward, right);
			if (angleDiff > 0.2f)
			    Owner.HyperspaceReturn();
		    OrderQueue.Clear();
			if (ClearOrders)
			    lock (WayPointLocker)
			    {
			        ActiveWayPoints.Clear();
			    }
		    if (Owner.loyalty == EmpireManager.Player)
		        HasPriorityOrder = true;
		    State = AIState.MoveTo;
			MovePosition = position;
			lock (WayPointLocker)
			{
				ActiveWayPoints.Enqueue(position);
			}
			FinalFacingVector = fVec;
			DesiredFacing = desiredFacing;
			lock (WayPointLocker)
			{
				for (var i = 0; i < ActiveWayPoints.Count; i++)
				{
					Vector2 waypoint = ActiveWayPoints.ToArray()[i];
					if (i != 0)
					{
						var to1k = new ShipGoal(Plan.MoveToWithin1000, waypoint, desiredFacing)
						{
							SpeedLimit = speedLimit
						};
						OrderQueue.AddLast(to1k);
					}
					else
					{
						OrderQueue.AddLast(new ShipGoal(Plan.RotateToFaceMovePosition, waypoint, 0f));
						var to1k = new ShipGoal(Plan.MoveToWithin1000, waypoint, desiredFacing)
						{
							SpeedLimit = speedLimit
						};
						OrderQueue.AddLast(to1k);
					}
					if (i == ActiveWayPoints.Count - 1)
					{
						var finalApproach = new ShipGoal(Plan.MakeFinalApproach, waypoint, desiredFacing)
						{
							SpeedLimit = speedLimit
						};
						OrderQueue.AddLast(finalApproach);
						var slow = new ShipGoal(Plan.StopWithBackThrust, waypoint, 0f)
						{
							SpeedLimit = speedLimit
						};
						OrderQueue.AddLast(slow);
						OrderQueue.AddLast(new ShipGoal(Plan.RotateToDesiredFacing, waypoint, desiredFacing));
					}
				}
			}
		}
        //order movement fleet to posistion
		public void OrderMoveToFleetPosition(Vector2 position, float desiredFacing, Vector2 fVec, bool ClearOrders, float SpeedLimit, Fleet fleet)
		{
			SpeedLimit = Owner.speed;
			if (ClearOrders)
			{
				OrderQueue.Clear();
				lock (WayPointLocker)
				{
					ActiveWayPoints.Clear();
				}
			}
			State = AIState.MoveTo;
			MovePosition = position;
			FinalFacingVector = fVec;
			DesiredFacing = desiredFacing;
			bool inCombat = Owner.InCombat;
			OrderQueue.AddLast(new ShipGoal(Plan.RotateToFaceMovePosition, MovePosition, 0f));
			var to1k = new ShipGoal(Plan.MoveToWithin1000Fleet, MovePosition, desiredFacing)
			{
				SpeedLimit = SpeedLimit,
				fleet = fleet
			};
			OrderQueue.AddLast(to1k);
			var finalApproach = new ShipGoal(Plan.MakeFinalApproachFleet, MovePosition, desiredFacing)
			{
				SpeedLimit = SpeedLimit,
				fleet = fleet
			};
			OrderQueue.AddLast(finalApproach);
			OrderQueue.AddLast(new ShipGoal(Plan.RotateInlineWithVelocity, Vector2.Zero, 0f));
			var slow = new ShipGoal(Plan.StopWithBackThrust, position, 0f)
			{
				SpeedLimit = Owner.speed
			};
			OrderQueue.AddLast(slow);
			OrderQueue.AddLast(new ShipGoal(Plan.RotateToDesiredFacing, MovePosition, desiredFacing));
		}
        // order movement to posiston
		public void OrderMoveTowardsPosition( Vector2  position , float desiredFacing, Vector2 fVec, bool ClearOrders, Planet TargetPlanet)
		{
            DistanceLast = 0f;
            Target = null;
			hasPriorityTarget = false;
		    Vector2 wantedForward = Owner.Center.FindVectorToTarget(position);

            var forward = new Vector2((float)Math.Sin((double)Owner.Rotation), -(float)Math.Cos((double)Owner.Rotation));
			var right = new Vector2(-forward.Y, forward.X);
			var angleDiff = (float)Math.Acos((double)Vector2.Dot(wantedForward, forward));
			Vector2.Dot(wantedForward, right);
			if (angleDiff > 0.2f)
			    Owner.HyperspaceReturn();
		    OrderQueue.Clear();
            if (ClearOrders)
                lock (WayPointLocker)
                {
                    ActiveWayPoints.Clear();
                }
		    if (universeScreen != null && Owner.loyalty == EmpireManager.Player)
		        HasPriorityOrder = true;
		    State = AIState.MoveTo;
			MovePosition = position;
           // try
            {
                PlotCourseToNew(position, ActiveWayPoints.Count > 0 ? ActiveWayPoints.Last<Vector2>() : Owner.Center);
            }
         //   catch
            //{
            //    lock (this.WayPointLocker)
            //    {
            //        this.ActiveWayPoints.Clear();
            //    }
            //}
            FinalFacingVector = fVec;
			DesiredFacing = desiredFacing;

			lock (WayPointLocker)
			{
                            Planet p;
            Vector2 waypoint;
                int AWPC = ActiveWayPoints.Count;
                for (var i = 0; i < AWPC; i++)
				{
					p =null;
                    waypoint = ActiveWayPoints.ToArray()[i];
					if (i != 0)
					{
                        if (AWPC - 1 == i)
                            p = TargetPlanet;

                        var to1k = new ShipGoal(Plan.MoveToWithin1000, waypoint, desiredFacing)
						{
							TargetPlanet=p,
                            SpeedLimit = Owner.speed
						};
						OrderQueue.AddLast(to1k);
					}
					else
					{
						if(AWPC -1 ==i)
                            p = TargetPlanet;
                        OrderQueue.AddLast(new ShipGoal(Plan.RotateToFaceMovePosition, waypoint, 0f));
						var to1k = new ShipGoal(Plan.MoveToWithin1000, waypoint, desiredFacing)
						{
                            TargetPlanet = p,
                            SpeedLimit = Owner.speed
						};
						OrderQueue.AddLast(to1k);
					}
                    if (i == AWPC - 1)
					{
						var finalApproach = new ShipGoal(Plan.MakeFinalApproach, waypoint, desiredFacing)
						{
							TargetPlanet=p,
                            SpeedLimit = Owner.speed
						};
						OrderQueue.AddLast(finalApproach);
						var slow = new ShipGoal(Plan.StopWithBackThrust, waypoint, 0f)
						{
                            TargetPlanet = TargetPlanet,
                            SpeedLimit = Owner.speed
						};
						OrderQueue.AddLast(slow);
						OrderQueue.AddLast(new ShipGoal(Plan.RotateToDesiredFacing, waypoint, desiredFacing));
					}
				}
			}
		}

        #region Unreferenced code
        //public void OrderMoveTowardsPosition(Vector2 position, float desiredFacing, Vector2 fVec, bool ClearOrders, float SpeedLimit)
        //{
        //    this.Target = null;
        //    Vector2 wantedForward = Vector2.Normalize(HelperFunctions.FindVectorToTarget(this.Owner.Center, position));
        //    Vector2 forward = new Vector2((float)Math.Sin((double)this.Owner.Rotation), -(float)Math.Cos((double)this.Owner.Rotation));
        //    Vector2 right = new Vector2(-forward.Y, forward.X);
        //    float angleDiff = (float)Math.Acos((double)Vector2.Dot(wantedForward, forward));
        //    Vector2.Dot(wantedForward, right);
        //    if (this.Owner.loyalty == EmpireManager.Player)
        //    {
        //        this.HasPriorityOrder = true;
        //    }
        //    if (angleDiff > 0.2f)
        //    {
        //        this.Owner.HyperspaceReturn();
        //    }
        //    this.hasPriorityTarget = false;
        //    if (ClearOrders)
        //    {
        //        this.OrderQueue.Clear();
        //    }
        //    this.State = AIState.MoveTo;
        //    this.MovePosition = position;
        //    this.PlotCourseToNew(position, this.Owner.Center);
        //    this.FinalFacingVector = fVec;
        //    this.DesiredFacing = desiredFacing;
        //    for (int i = 0; i < this.ActiveWayPoints.Count; i++)
        //    {
        //        Vector2 waypoint = this.ActiveWayPoints.ToArray()[i];
        //        if (i != 0)
        //        {
        //            ArtificialIntelligence.ShipGoal to1k = new ArtificialIntelligence.ShipGoal(ArtificialIntelligence.Plan.MoveToWithin1000, waypoint, desiredFacing)
        //            {
        //                SpeedLimit = SpeedLimit
        //            };
        //            this.OrderQueue.AddLast(to1k);
        //        }
        //        else
        //        {
        //            ArtificialIntelligence.ShipGoal to1k = new ArtificialIntelligence.ShipGoal(ArtificialIntelligence.Plan.MoveToWithin1000, waypoint, desiredFacing)
        //            {
        //                SpeedLimit = SpeedLimit
        //            };
        //            this.OrderQueue.AddLast(to1k);
        //        }
        //        if (i == this.ActiveWayPoints.Count - 1)
        //        {
        //            ArtificialIntelligence.ShipGoal finalApproach = new ArtificialIntelligence.ShipGoal(ArtificialIntelligence.Plan.MakeFinalApproach, waypoint, desiredFacing)
        //            {
        //                SpeedLimit = SpeedLimit
        //            };
        //            this.OrderQueue.AddLast(finalApproach);
        //            this.OrderQueue.AddLast(new ArtificialIntelligence.ShipGoal(ArtificialIntelligence.Plan.RotateInlineWithVelocity, Vector2.Zero, 0f));
        //            ArtificialIntelligence.ShipGoal slow = new ArtificialIntelligence.ShipGoal(ArtificialIntelligence.Plan.StopWithBackThrust, waypoint, 0f)
        //            {
        //                SpeedLimit = SpeedLimit
        //            };
        //            this.OrderQueue.AddLast(slow);
        //            this.OrderQueue.AddLast(new ArtificialIntelligence.ShipGoal(ArtificialIntelligence.Plan.RotateToDesiredFacing, waypoint, desiredFacing));
        //        }
        //    }
        //} 
        #endregion
        //order orbit nearest
		public void OrderOrbitNearest(bool ClearOrders)
		{
			lock (WayPointLocker)
			{
				ActiveWayPoints.Clear();
			}
			Target = null;
			Intercepting = false;
			Owner.HyperspaceReturn();
			if (ClearOrders)
			    OrderQueue.Clear();
		    var sortedList = 
				from toOrbit in Owner.loyalty.GetPlanets()
				orderby Vector2.Distance(Owner.Center, toOrbit.Position)
				select toOrbit;
			if (sortedList.Any())
			{
				var planet = sortedList.First<Planet>();
				OrbitTarget = planet;
				var orbit = new ShipGoal(Plan.Orbit, Vector2.Zero, 0f)
				{
					TargetPlanet = planet
				};
				resupplyTarget = planet;
				OrderQueue.AddLast(orbit);
				State = AIState.Orbit;
				return;
			}
			var systemList = 
				from solarsystem in Owner.loyalty.GetOwnedSystems()
				orderby Vector2.Distance(Owner.Center, solarsystem.Position)
				select solarsystem;
			if (systemList.Count<SolarSystem>() > 0)
			{
				Planet item = systemList.First<SolarSystem>().PlanetList[0];
				OrbitTarget = item;
				var orbit = new ShipGoal(Plan.Orbit, Vector2.Zero, 0f)
				{
					TargetPlanet = item
				};
				resupplyTarget = item;
				OrderQueue.AddLast(orbit);
				State = AIState.Orbit;
			}
		}
        //added by gremlin to run away
        //order flee
        public void OrderFlee(bool ClearOrders)
        {
            lock (WayPointLocker)
            {
                ActiveWayPoints.Clear();
            }
            Target = null;
            Intercepting = false;
            Owner.HyperspaceReturn();
            if (ClearOrders)
                OrderQueue.Clear();

            var systemList =
                from solarsystem in Owner.loyalty.GetOwnedSystems()
                where solarsystem.combatTimer <= 0f && Vector2.Distance(solarsystem.Position, Owner.Position) > 200000f
                orderby Vector2.Distance(Owner.Center, solarsystem.Position)
                select solarsystem;
            if (systemList.Any())
            {
                Planet item = systemList.First<SolarSystem>().PlanetList[0];
                OrbitTarget = item;
                var orbit = new ShipGoal(Plan.Orbit, Vector2.Zero, 0f)
                {
                    TargetPlanet = item
                };
                resupplyTarget = item;
                OrderQueue.AddLast(orbit);
                State = AIState.Flee;
            }
        }
        //order orbit planet
		public void OrderOrbitPlanet(Planet p)
		{
			lock (WayPointLocker)
			{
				ActiveWayPoints.Clear();
			}
			Target = null;
			Intercepting = false;
			Owner.HyperspaceReturn();
			OrbitTarget = p;
			OrderQueue.Clear();
			var orbit = new ShipGoal(Plan.Orbit, Vector2.Zero, 0f)
			{
				TargetPlanet = p
			};
			resupplyTarget = p;
			OrderQueue.AddLast(orbit);
			State = AIState.Orbit;
		}

		public void OrderQueueSpecificTarget(Ship toAttack)
		{
			if (TargetQueue.Count == 0 && Target != null && Target.Active && Target != toAttack)
			{
				OrderAttackSpecificTarget(Target as Ship);
				TargetQueue.Add(Target as Ship);
			}
			if (TargetQueue.Count == 0)
			{
				OrderAttackSpecificTarget(toAttack);
				return;
			}
			if (toAttack == null)
			    return;
		    //targetting relation
			if (Owner.loyalty.TryGetRelations(toAttack.loyalty, out Relationship relations))
			{
				if (!relations.Treaty_Peace)
				{
					if (State == AIState.AttackTarget && Target == toAttack)
					    return;
				    if (State == AIState.SystemDefender && Target == toAttack)
				        return;
				    if (Owner.Weapons.Count == 0 || Owner.shipData.Role == ShipData.RoleName.troop)
					{
						OrderInterceptShip(toAttack);
						return;
					}
					Intercepting = true;
					lock (WayPointLocker)
					{
						ActiveWayPoints.Clear();
					}
					State = AIState.AttackTarget;
					TargetQueue.Add(toAttack);
					hasPriorityTarget = true;
					HasPriorityOrder = false;
					return;
				}
				OrderInterceptShip(toAttack);
			}
		}
        //order rebase target
        public void OrderRebase(Planet p, bool ClearOrders)
        {

            lock (WayPointLocker)
            {
                ActiveWayPoints.Clear();
            }
            if (ClearOrders)
                OrderQueue.Clear();
            int troops = Owner.loyalty
                .GetShips().Where(troop => troop.TroopList.Count > 0)
                .Count(troopAi => troopAi.GetAI().OrderQueue.Any(goal => goal.TargetPlanet != null && goal.TargetPlanet == p));

            if (troops >= p.GetGroundLandingSpots())
            {
                OrderQueue.Clear();
                State = AIState.AwaitingOrders;
                return;
            }

            OrderMoveTowardsPosition(p.Position, 0f, new Vector2(0f, -1f), false,p);
            IgnoreCombat = true;
            var rebase = new ShipGoal(Plan.Rebase, Vector2.Zero, 0f)
            {
                TargetPlanet = p
            };
            OrderQueue.AddLast(rebase);
            State = AIState.Rebase;
            HasPriorityOrder = true;
        }
        //order rebase nearest
		public void OrderRebaseToNearest()
		{
            ////added by gremlin if rebasing dont rebase.
            //if (this.State == AIState.Rebase && this.OrbitTarget.Owner == this.Owner.loyalty)
            //    return;
            lock (WayPointLocker)
			{
				ActiveWayPoints.Clear();
			}
            
            var sortedList = 
				from planet in Owner.loyalty.GetPlanets()
                //added by gremlin if the planet is full of troops dont rebase there. RERC2 I dont think the about looking at incoming troops works.
                where Owner.loyalty
    .GetShips( )
    .Where(troop => troop.TroopList.Count > 0).Count(troopAi => troopAi.GetAI().OrderQueue.Any(goal => goal.TargetPlanet != null && goal.TargetPlanet == planet)) <= planet.GetGroundLandingSpots()


                /*where planet.TroopsHere.Count + this.Owner.loyalty.GetShips()
                .Where(troop => troop.Role == ShipData.RoleName.troop 
                    
                    && troop.GetAI().State == AIState.Rebase 
                    && troop.GetAI().OrbitTarget == planet).Count() < planet.TilesList.Sum(space => space.number_allowed_troops)*/
				orderby Vector2.Distance(Owner.Center, planet.Position)
				select planet;

           


			if (sortedList.Count<Planet>() <= 0)
			{
				State = AIState.AwaitingOrders;
				return;
			}
			var p = sortedList.First<Planet>();
			OrderMoveTowardsPosition(p.Position, 0f, new Vector2(0f, -1f), false,p);
			IgnoreCombat = true;
			var rebase = new ShipGoal(Plan.Rebase, Vector2.Zero, 0f)
			{
				TargetPlanet = p
			};

           
            OrderQueue.AddLast(rebase);
        
			State = AIState.Rebase;
			HasPriorityOrder = true;
		}
        //order refit
		public void OrderRefitTo(string toRefit)
		{
			lock (WayPointLocker)
			{
				ActiveWayPoints.Clear();
			}
			HasPriorityOrder = true;
			IgnoreCombat = true;
           
			OrderQueue.Clear();
          
			var sortedList = 
				from planet in Owner.loyalty.GetPlanets()
				orderby Vector2.Distance(Owner.Center, planet.Position)
				select planet;
			OrbitTarget = null;
			foreach (Planet Planet in sortedList)
			{
				if (!Planet.HasShipyard && !Owner.loyalty.isFaction)
				    continue;
			    OrbitTarget = Planet;
				break;
			}
			if (OrbitTarget == null)
			{
				State = AIState.AwaitingOrders;
				return;
			}
			OrderMoveTowardsPosition(OrbitTarget.Position, 0f, Vector2.One, true,OrbitTarget);
			var refit = new ShipGoal(Plan.Refit, Vector2.Zero, 0f)
			{
				TargetPlanet = OrbitTarget,
				VariableString = toRefit
			};
			OrderQueue.AddLast(refit);
			State = AIState.Refit;
		}
        //resupply order
		public void OrderResupply(Planet toOrbit, bool ClearOrders)
		{
          
            if (ClearOrders)
			{
				OrderQueue.Clear();
                HadPO = true;
			}
            else
            {
                HadPO = false;
            }
			lock (WayPointLocker)
			{
				ActiveWayPoints.Clear();
			}
			Target = null;
			OrbitTarget = toOrbit;
            awaitClosest = toOrbit;
            OrderMoveTowardsPosition(toOrbit.Position, 0f, Vector2.One, ClearOrders, toOrbit);
			State = AIState.Resupply;
			HasPriorityOrder = true;
		}

		//fbedard: Added dont retreat to a near planet in combat, and flee if nowhere to go
        //resupply order
        public void OrderResupplyNearest(bool ClearOrders)
		{
            if (Owner.Mothership != null && Owner.Mothership.Active && (Owner.shipData.Role != ShipData.RoleName.supply 
                || Owner.Ordinance > 0 || Owner.Health / Owner.HealthMax < DmgLevel[(int)Owner.shipData.ShipCategory]))
			{
				OrderReturnToHangar();
				return;
			}
			var shipyards = new Array<Planet>();
            if(Owner.loyalty.isFaction)
                return;
		    foreach (Planet planet in Owner.loyalty.GetPlanets())
			{
                if (!planet.HasShipyard || Owner.InCombat && Vector2.Distance(Owner.Center, planet.Position) < 15000f)
                    continue;
			    shipyards.Add(planet);
			}
            IOrderedEnumerable<Planet> sortedList = null;
            if(Owner.NeedResupplyTroops)
                sortedList =
                from p in shipyards
                orderby p.TroopsHere.Count > Owner.TroopCapacity,
                Vector2.Distance(Owner.Center, p.Position)                
                select p;
            else
			sortedList = 
				from p in shipyards
				orderby Vector2.Distance(Owner.Center, p.Position)
				select p;
            if (sortedList.Count<Planet>() > 0)
                OrderResupply(sortedList.First<Planet>(), ClearOrders);
            else
                OrderFlee(true);

		}
        //hangar order return
		public void OrderReturnToHangar()
		{
			var g = new ShipGoal(Plan.ReturnToHangar, Vector2.Zero, 0f);
            
            OrderQueue.Clear();
			OrderQueue.AddLast(g);
            
			HasPriorityOrder = true;
			State = AIState.ReturnToHangar;
		}
        //SCRAP Order
		public void OrderScrapShip()
		{
#if SHOWSCRUB
            //Log.Info(string.Concat(this.Owner.loyalty.PortraitName, " : ", this.Owner.Role)); 
#endif

            if (Owner.shipData.Role <= ShipData.RoleName.station && Owner.ScuttleTimer < 1)
            {
                Owner.ScuttleTimer = 1;
                State = AIState.Scuttle;
                HasPriorityOrder = true;
                Owner.QueueTotalRemoval();  //fbedard
                return;
            }
            lock (WayPointLocker)
			{
				ActiveWayPoints.Clear();
			}
            Owner.loyalty.ForcePoolRemove(Owner);

            if (Owner.fleet != null)
            {
                Owner.fleet.Ships.Remove(Owner);
                Owner.fleet = null;
            }
            HasPriorityOrder = true;
            IgnoreCombat = true;
			OrderQueue.Clear();
			var sortedList = 
				from planet in Owner.loyalty.GetPlanets()
				orderby Vector2.Distance(Owner.Center, planet.Position)
				select planet;
			OrbitTarget = null;
			foreach (Planet Planet in sortedList)
			{
				if (!Planet.HasShipyard)
				    continue;
			    OrbitTarget = Planet;
				break;
			}
			if (OrbitTarget == null)
			{
				State = AIState.AwaitingOrders;
			}
			else
			{
				OrderMoveTowardsPosition(OrbitTarget.Position, 0f, Vector2.One, true,OrbitTarget);
				var scrap = new ShipGoal(Plan.Scrap, Vector2.Zero, 0f)
				{
					TargetPlanet = OrbitTarget
				};
				OrderQueue.AddLast(scrap);
				State = AIState.Scrap;
			}
			State = AIState.Scrap;
		}

		private void OrderSupplyShip(Ship tosupply, float ord_amt)
		{
			var g = new ShipGoal(Plan.SupplyShip, Vector2.Zero, 0f);
			EscortTarget = tosupply;
			g.VariableNumber = ord_amt;
			IgnoreCombat = true;
			OrderQueue.Clear();
			OrderQueue.AddLast(g);
			State = AIState.Ferrying;
		}
        /// <summary>
        /// sysdefense order defend system
        /// </summary>
        /// <param name="system"></param>
		public void OrderSystemDefense(SolarSystem system)
		{
            //if (this.State == AIState.Intercept || this.Owner.InCombatTimer > 0)
            //    return;
            //bool inSystem = true;
            //if (this.Owner.BaseCanWarp && Vector2.Distance(system.Position, this.Owner.Position) / this.Owner.velocityMaximum > 11)
            //    inSystem = false;
            //else 
            //    inSystem = this.Owner.GetSystem() == this.SystemToDefend;
            //if (this.SystemToDefend == null)
            //{
            //    this.HasPriorityOrder = false;
            //    this.SystemToDefend = system;
            //    this.OrderQueue.Clear();
            //}
            //else

            ShipGoal goal = OrderQueue.LastOrDefault();

            if (SystemToDefend == null || SystemToDefend != system || awaitClosest == null || awaitClosest.Owner == null || awaitClosest.Owner != Owner.loyalty || Owner.System!= system && goal != null && OrderQueue.LastOrDefault().Plan != Plan.DefendSystem)
			{

#if SHOWSCRUB
                if (this.Target != null && (this.Target as Ship).Name == "Subspace Projector")
                    Log.Info(string.Concat("Scrubbed", (this.Target as Ship).Name)); 
#endif
                SystemToDefend = system;
                HasPriorityOrder = false;
				SystemToDefend = system;
				OrderQueue.Clear();
                OrbitTarget = (Planet)null;
				if (SystemToDefend.PlanetList.Count > 0)
				{
					var Potentials = new Array<Planet>();
					foreach (Planet p in SystemToDefend.PlanetList)
					{
						if (p.Owner == null || p.Owner != Owner.loyalty)
						    continue;
					    Potentials.Add(p);
					}
                    //if (Potentials.Count == 0)
                    //    foreach (Planet p in this.SystemToDefend.PlanetList)
                    //        if (p.Owner == null)
                    //            Potentials.Add(p);

                    if (Potentials.Count > 0)
                    {
                        awaitClosest = Potentials[UniverseRandom.InRange(Potentials.Count)];
                        OrderMoveTowardsPosition(awaitClosest.Position, 0f, Vector2.One, true, null);
                        OrderQueue.AddLast(new ShipGoal(Plan.DefendSystem, Vector2.Zero, 0f));
                        State = AIState.SystemDefender;                   
                    }
                    else
                    {
                        OrderResupplyNearest(true);
                    }
				}
                //this.OrderQueue.AddLast(new ArtificialIntelligence.ShipGoal(ArtificialIntelligence.Plan.DefendSystem, Vector2.Zero, 0f));
			}
        
            //this.State = AIState.SystemDefender;                   
		}
        //movement create goals from waypoints
		public void OrderThrustTowardsPosition(Vector2 position, float desiredFacing, Vector2 fVec, bool ClearOrders)
		{
			if (ClearOrders)
			{
				OrderQueue.Clear();
				lock (WayPointLocker)
				{
					ActiveWayPoints.Clear();
				}
			}
			FinalFacingVector = fVec;
			DesiredFacing = desiredFacing;
			lock (WayPointLocker)
			{
				for (var i = 0; i < ActiveWayPoints.Count; i++)
				{
					Vector2 waypoint = ActiveWayPoints.ToArray()[i];
					if (i == 0)
					{
						OrderQueue.AddLast(new ShipGoal(Plan.RotateInlineWithVelocity, Vector2.Zero, 0f));
						var stop = new ShipGoal(Plan.Stop, Vector2.Zero, 0f);
						OrderQueue.AddLast(stop);
						OrderQueue.AddLast(new ShipGoal(Plan.RotateToFaceMovePosition, waypoint, 0f));
						var to1k = new ShipGoal(Plan.MoveToWithin1000, waypoint, desiredFacing)
						{
							SpeedLimit = Owner.speed
						};
						OrderQueue.AddLast(to1k);
					}
				}
			}
		}

		public void OrderToOrbit(Planet toOrbit, bool ClearOrders)
		{
			if (ClearOrders)
			    OrderQueue.Clear();
		    HasPriorityOrder = true;
			lock (WayPointLocker)
			{
				ActiveWayPoints.Clear();
			}
			State = AIState.Orbit;
			OrbitTarget = toOrbit;
            if (Owner.shipData.ShipCategory == ShipData.Category.Civilian)  //fbedard: civilian ship will use projectors
                OrderMoveTowardsPosition(toOrbit.Position, 0f, new Vector2(0f, -1f), false, toOrbit);
			var orbit = new ShipGoal(Plan.Orbit, Vector2.Zero, 0f)
			{
				TargetPlanet = toOrbit
			};
            
			OrderQueue.AddLast(orbit);
            
		}
        public float TimeToTarget(Planet target)
        {
            float test = 0;
            test = Vector2.Distance(target.Position, Owner.Center) / Owner.GetmaxFTLSpeed;
            return test;
        }
        //added by: Gremalin. returns roughly the number of turns to a target planet restricting to targets that can use the freighter. 
        private float TradeSort(Ship ship, Planet PlanetCheck, string ResourceType, float cargoCount,bool Delivery)
        {
            /*here I am trying to predict the planets need versus the ships speed.
             * I am returning a weighted value that is based on this but primarily the returned value is the time it takes the freighter to get to the target in a straight line
             * 
             * 
             */
            //cargoCount = cargoCount > PlanetCheck.MAX_STORAGE ? PlanetCheck.MAX_STORAGE : cargoCount;
            float resourceRecharge =0;
            float resourceAmount =0;
            if (ResourceType == "Food")
            {
                resourceRecharge = PlanetCheck.NetFoodPerTurn;
                resourceAmount = PlanetCheck.FoodHere;
            }
            else if(ResourceType == "Production")
            {
                resourceRecharge =  PlanetCheck.NetProductionPerTurn;
                resourceAmount = PlanetCheck.ProductionHere;
            }
            float timeTotarget = ship.GetAI().TimeToTarget(PlanetCheck);
            float Effeciency =  resourceRecharge * timeTotarget;
            
            // return PlanetCheck.MAX_STORAGE / (PlanetCheck.MAX_STORAGE -(Effeciency + resourceAmount));

            if (Delivery)
            {
                // return Effeciency;// * ((PlanetCheck.MAX_STORAGE + cargoCount) / ((PlanetCheck.MAX_STORAGE - resourceAmount + 1)));
                // Effeciency =  (PlanetCheck.MAX_STORAGE - cargoCount) / (cargoCount + Effeciency + resourceAmount) ;
                //return timeTotarget * Effeciency;
                bool badCargo = Effeciency + resourceAmount > PlanetCheck.MAX_STORAGE ;
                //bool badCargo = (cargoCount + Effeciency + resourceAmount) > PlanetCheck.MAX_STORAGE - cargoCount * .5f; //cargoCount + Effeciency < 0 ||
                if (!badCargo)
                    return timeTotarget * (badCargo ? PlanetCheck.MAX_STORAGE / (Effeciency + resourceAmount) : 1);// (float)Math.Ceiling((double)timeTotarget);                
            }
            else
            {
                //return Effeciency * (PlanetCheck.MAX_STORAGE / ((PlanetCheck.MAX_STORAGE - resourceAmount + 1)));
                // Effeciency = (ship.CargoSpace_Max) / (PlanetCheck.MAX_STORAGE);
                //return timeTotarget * Effeciency;
                Effeciency = PlanetCheck.MAX_STORAGE * .5f < ship.CargoSpace_Max ? resourceAmount + Effeciency < ship.CargoSpace_Max * .5f ? ship.CargoSpace_Max*.5f / (resourceAmount + Effeciency) :1:1;
                //bool BadSupply = PlanetCheck.MAX_STORAGE * .5f < ship.CargoSpace_Max && PlanetCheck.FoodHere + Effeciency < ship.CargoSpace_Max * .5f;
                //if (!BadSupply)
                    return timeTotarget * Effeciency;// (float)Math.Ceiling((double)timeTotarget);
            }
            return timeTotarget + universeScreen.Size.X;
        }
        //trade pick trade targets
        public void OrderTrade(float elapsedTime)
        {            
            //trade timer is sent but uses arbitrary timer just to delay the routine.
            Owner.TradeTimer -= elapsedTime;
            if (Owner.TradeTimer > 0f)
                return;

            lock (WayPointLocker)
            {
                ActiveWayPoints.Clear();
            }

            OrderQueue.Clear();
            if (Owner.GetCargo()["Colonists_1000"] > 0.0f)
                return;

            if(start != null && end != null)  //resume trading
            {
                Owner.TradeTimer = 5f;
                if (Owner.GetCargo()["Food"] > 0f || Owner.GetCargo()["Production"] > 0f)
                {
                    OrderMoveTowardsPosition(end.Position, 0f, new Vector2(0f, -1f), true, end);
                    
                    OrderQueue.AddLast(new ShipGoal(Plan.DropOffGoods, Vector2.Zero, 0f));
                   
                    State = AIState.SystemTrader;
                    return;
                }
                else
                {
                    OrderMoveTowardsPosition(start.Position, 0f, new Vector2(0f, -1f), true, start);
                  
                    OrderQueue.AddLast(new ShipGoal(Plan.PickupGoods, Vector2.Zero, 0f));
                    
                    State = AIState.SystemTrader;
                    return;
                }
            }
            Planet potential = null;//<-unused
            var planets = new Array<Planet>();
            IOrderedEnumerable<Planet> sortPlanets;
            bool flag;
            var secondaryPlanets = new Array<Planet>();
            //added by gremlin if fleeing keep fleeing
            if (Math.Abs(Owner.CargoSpace_Max) < 1 || State == AIState.Flee || Owner.isConstructor || Owner.isColonyShip)
                return;

            //try
            {
                var allincombat = true;
                var noimport = true;
                foreach (Planet p in Owner.loyalty.GetPlanets())
                {
                    if (p.ParentSystem.combatTimer <= 0)
                        allincombat = false;
                    if (p.ps == Planet.GoodState.IMPORT || p.fs == Planet.GoodState.IMPORT)
                        noimport = false;
                }

                if (allincombat || noimport && Owner.CargoSpace_Used > 0)
                {
                    Owner.TradeTimer = 5f;
                    return;
                }
                if (Owner.loyalty.data.Traits.Cybernetic > 0)
                    Owner.TradingFood = false;

                //bool FoodFirst = true;
                //if ((Owner.GetCargo()["Production"] > 0f || !Owner.TradingFood || RandomMath.RandomBetween(0f, 1f) < 0.5f) && Owner.TradingProd && Owner.GetCargo()["Food"] == 0f)
                //    FoodFirst = false;
                

                if (end == null && Owner.CargoSpace_Used <1 ) //  && FoodFirst  && ( this.Owner.GetCargo()["Food"] > 0f))
                    foreach (Planet planet in Owner.loyalty.GetPlanets())
                    {
                        if (planet.ParentSystem.combatTimer > 0)
                            continue;
                        if (planet.fs == Planet.GoodState.IMPORT && InsideAreaOfOperation(planet))
                            planets.Add(planet);
                    }

                if (planets.Count > 0)
                {
                    // if (this.Owner.GetCargo()["Food"] > 0f)
                        //sortPlanets = planets.OrderBy(dest => Vector2.Distance(this.Owner.Position, dest.Position));
                        sortPlanets = planets.OrderBy(PlanetCheck =>
                        {
                            return TradeSort(Owner, PlanetCheck, "Food", Owner.CargoSpace_Used, true);
                        }
                    );
                    foreach (Planet p in sortPlanets)
                    {
                        flag = false;
                        float cargoSpaceMax = p.MAX_STORAGE - p.FoodHere;                            
                        var faster = true ;
                        float mySpeed = TradeSort(Owner, p, "Food", Owner.CargoSpace_Max, true); 
                        cargoSpaceMax += p.NetFoodPerTurn * mySpeed;
                        cargoSpaceMax = cargoSpaceMax > p.MAX_STORAGE ? p.MAX_STORAGE : cargoSpaceMax;
                        cargoSpaceMax = cargoSpaceMax < 0 ? 0 : cargoSpaceMax;
                        //Planet with negative food production need more food:
                        //cargoSpaceMax = (cargoSpaceMax - (p.NetFoodPerTurn * 5f)) / 2f;  //reduced cargoSpacemax on first try!

                        using (Owner.loyalty.GetShips().AcquireReadLock())
                        {
                            for (var k = 0; k < Owner.loyalty.GetShips().Count; k++)
                            {
                                Ship s = Owner.loyalty.GetShips()[k];
                                if (s != null && (s.shipData.Role == ShipData.RoleName.freighter || s.shipData.ShipCategory == ShipData.Category.Civilian) && s != Owner && !s.isConstructor)
                                {
                                    if (s.GetAI().State == AIState.SystemTrader && s.GetAI().end == p && s.GetAI().FoodOrProd == "Food" && s.CargoSpace_Used > 0)
                                    {

                                        float currenTrade = TradeSort(s, p, "Food", s.CargoSpace_Max, true);                                        
                                        if (currenTrade < mySpeed)
                                            faster = false;
                                        if (currenTrade !=0 )
                                        {
                                            flag = true;
                                            break;
                                        }
                                        float efficiency = currenTrade - mySpeed;
                                        if(mySpeed * p.NetFoodPerTurn < p.FoodHere && faster)
                                            continue;
                                        if(p.NetFoodPerTurn <=0)
                                            efficiency = s.CargoSpace_Max - efficiency * p.NetFoodPerTurn;                                        
                                        else
                                            efficiency = s.CargoSpace_Max - efficiency * p.NetFoodPerTurn;                                        
                                        if (efficiency > 0)
                                        {
                                            if (efficiency > s.CargoSpace_Max)
                                                efficiency = s.CargoSpace_Max;
                                            cargoSpaceMax = cargoSpaceMax - efficiency;
                                        }
                                        //ca

                                    }
                                    if (cargoSpaceMax <= 0f)
                                    {
                                        flag = true;
                                        break;
                                    }
                                }
                            }
                        }
                        if (!flag )
                        {
                            end = p;
                            break;
                        }
                        if (faster)
                            potential = p;
                    }
                    if (end != null)
                    {
                        FoodOrProd = "Food";
                        if (Owner.GetCargo()["Food"] > 0f)
                        {
                            OrderMoveTowardsPosition(end.Position, 0f, new Vector2(0f, -1f), true, end);
                            OrderQueue.AddLast(new ShipGoal(Plan.DropOffGoods, Vector2.Zero, 0f));
                            State = AIState.SystemTrader;
                            return;
                        }
                    }
                }

                #region deliver Production (return if already loaded)
                if (end == null && (Owner.TradingProd || Owner.GetCargo()["Production"] > 0f))
                {
                    planets.Clear();
                    secondaryPlanets.Clear();
                    foreach (Planet planet in Owner.loyalty.GetPlanets())
                    {
                        if (planet.ParentSystem.combatTimer > 0)
                            continue;

                        if (planet.ps == Planet.GoodState.IMPORT && InsideAreaOfOperation(planet))
                            planets.Add(planet);
                        else if (planet.MAX_STORAGE - planet.ProductionHere > 0)
                            secondaryPlanets.Add(planet);
                    }
              
                    if (Owner.CargoSpace_Used > 0.01f &&  planets.Count == 0 )
                        planets.AddRange(secondaryPlanets);

                    if (planets.Count > 0)
                    {
                        if (Owner.GetCargo()["Production"] > 0.01f)
                            //sortPlanets = planets.OrderBy(PlanetCheck=> (PlanetCheck.MAX_STORAGE - PlanetCheck.ProductionHere) >= this.Owner.CargoSpace_Max)
                            //    .ThenBy(dest => Vector2.Distance(this.Owner.Position, dest.Position));
                            sortPlanets = planets.OrderBy(PlanetCheck =>
                            {
                                return TradeSort(Owner, PlanetCheck, "Production", Owner.CargoSpace_Used, true);
                                
                            }
                   );//.ThenByDescending(f => f.ProductionHere / f.MAX_STORAGE);
                        else
                            //sortPlanets = planets.OrderBy(PlanetCheck=> (PlanetCheck.MAX_STORAGE - PlanetCheck.ProductionHere) >= this.Owner.CargoSpace_Max)
                            //    .ThenBy(dest => (dest.ProductionHere));
                            sortPlanets = planets.OrderBy(PlanetCheck =>
                            {
                                return TradeSort(Owner, PlanetCheck, "Production", Owner.CargoSpace_Max, true);
                            }
                   );//.ThenByDescending(f => f.ProductionHere / f.MAX_STORAGE);
                        foreach (Planet p in sortPlanets)
                        {
                            flag = false;
                            float cargoSpaceMax = p.MAX_STORAGE - p.ProductionHere;
                            var faster = true;
                            float thisTradeStr = TradeSort(Owner, p, "Production", Owner.CargoSpace_Max, true);
                            if (thisTradeStr >= universeScreen.Size.X && p.ProductionHere >= 0)
                                continue;

                            using (Owner.loyalty.GetShips().AcquireReadLock())
                            {
                                for (var k = 0; k < Owner.loyalty.GetShips().Count; k++)
                                {
                                    Ship s = Owner.loyalty.GetShips()[k];
                                    if (s != null && (s.shipData.Role == ShipData.RoleName.freighter || s.shipData.ShipCategory == ShipData.Category.Civilian) && s != Owner && !s.isConstructor)
                                    {
                                        if (s.GetAI().State == AIState.SystemTrader && s.GetAI().end == p && s.GetAI().FoodOrProd == "Prod")
                                        {

                                            float currenTrade = TradeSort(s, p, "Production", s.CargoSpace_Max, true);
                                            if (currenTrade < thisTradeStr)
                                                faster = false;
                                            if (currenTrade > UniverseData.UniverseWidth && !faster)
                                            {
                                                flag = true;
                                                break;
                                            }
                                            cargoSpaceMax = cargoSpaceMax - s.CargoSpace_Max;
                                        }

                                        if (cargoSpaceMax <= 0f)
                                        {
                                            flag = true;
                                            break;
                                        }
                                    }
                                }
                            }
                            if (!flag)
                            {
                                end = p;
                                break;
                            }
                            if (faster)
                                potential = p;
                        }
                        if (end != null)
                        {
                            FoodOrProd = "Prod";
                            if (Owner.GetCargo()["Production"] > 0f)
                            {
                                OrderMoveTowardsPosition(end.Position, 0f, new Vector2(0f, -1f), true, end);
                                OrderQueue.AddLast(new ShipGoal(Plan.DropOffGoods, Vector2.Zero, 0f));
                                State = AIState.SystemTrader;
                                return;
                            }
                        }
                    }
                }
                #endregion

                #region Deliver Food LAST (return if already loaded)
                if (end == null && (Owner.TradingFood || Owner.GetCargo()["Food"] > 0.01f) && Owner.GetCargo()["Production"] == 0.0f)
                {
                    planets.Clear();
                    foreach (Planet planet in Owner.loyalty.GetPlanets())
                    {
                        if (planet.ParentSystem.combatTimer > 0f)
                            continue;
                        if (planet.fs == Planet.GoodState.IMPORT && InsideAreaOfOperation(planet))
                            planets.Add(planet);
                    }

                    if (planets.Count > 0)
                    {
                        if (Owner.GetCargo()["Food"] > 0.01f)
                          //  sortPlanets = planets.OrderBy(PlanetCheck => (PlanetCheck.MAX_STORAGE - PlanetCheck.FoodHere) >= this.Owner.CargoSpace_Max)
                        sortPlanets = planets.OrderBy(PlanetCheck =>
                        {
                            return TradeSort(Owner, PlanetCheck, "Food", Owner.CargoSpace_Used, true);   
                        }
                            );//.ThenByDescending(f => f.FoodHere / f.MAX_STORAGE);
                        else
                            //sortPlanets = planets.OrderBy(PlanetCheck => (PlanetCheck.MAX_STORAGE - PlanetCheck.FoodHere) >= this.Owner.CargoSpace_Max)
                            //    .ThenBy(dest => (dest.FoodHere + (dest.NetFoodPerTurn - dest.consumption) * GoodMult));

                        sortPlanets = planets.OrderBy(PlanetCheck =>
                        {
                            return TradeSort(Owner, PlanetCheck, "Food", Owner.CargoSpace_Max, true);   
                        }
                            );//.ThenByDescending(f => f.FoodHere / f.MAX_STORAGE);
                        foreach (Planet p in sortPlanets)
                        {
                            flag = false;
                            float cargoSpaceMax = p.MAX_STORAGE - p.FoodHere;
                            var faster = true;
                            float mySpeed = TradeSort(Owner, p, "Food", Owner.CargoSpace_Max, true);
                            if (mySpeed >= universeScreen.Size.X)
                                continue;
                            cargoSpaceMax += p.NetFoodPerTurn * mySpeed;
                            cargoSpaceMax = cargoSpaceMax > p.MAX_STORAGE ? p.MAX_STORAGE : cargoSpaceMax;
                            cargoSpaceMax = cargoSpaceMax < 0.0f ? 0.0f : cargoSpaceMax;

                            using (Owner.loyalty.GetShips().AcquireReadLock())
                            {
                                for (var k = 0; k < Owner.loyalty.GetShips().Count; k++)
                                {
                                    Ship s = Owner.loyalty.GetShips()[k];
                                    if (s != null && (s.shipData.Role == ShipData.RoleName.freighter || s.shipData.ShipCategory == ShipData.Category.Civilian) && s != Owner && !s.isConstructor)
                                    {
                                        if (s.GetAI().State == AIState.SystemTrader && s.GetAI().end == p && s.GetAI().FoodOrProd == "Food")
                                        {

                                            float currenTrade = TradeSort(s, p, "Food", s.CargoSpace_Max, true);
                                            if (currenTrade < mySpeed)
                                                faster = false;
                                            if (currenTrade > UniverseData.UniverseWidth && !faster)
                                                continue;
                                            float efficiency = Math.Abs(currenTrade - mySpeed);
                                            if (mySpeed * p.NetFoodPerTurn < p.FoodHere && faster)
                                                continue;
                                            if (p.NetFoodPerTurn == 0.0f)
                                                efficiency = s.CargoSpace_Max + efficiency * p.NetFoodPerTurn;
                                            else
                                            if (p.NetFoodPerTurn < 0.0f)
                                                efficiency = s.CargoSpace_Max + efficiency * p.NetFoodPerTurn;
                                            else
                                                efficiency = s.CargoSpace_Max - efficiency * p.NetFoodPerTurn;
                                            if (efficiency > 0.0f)
                                            {
                                                if (efficiency > s.CargoSpace_Max)
                                                    efficiency = s.CargoSpace_Max;
                                                cargoSpaceMax = cargoSpaceMax - efficiency;
                                            }
                                            //ca

                                        }
                                        if (cargoSpaceMax <= 0.0f)
                                        {
                                            flag = true;
                                            break;
                                        }
                                    }
                                }
                            }
                            if (!flag)
                            {
                                end = p;
                                break;
                            }
                        }
                        if (end != null)
                        {
                            FoodOrProd = "Food";
                            if (Owner.GetCargo()["Food"] > 0f)
                            {
                                OrderMoveTowardsPosition(end.Position, 0f, new Vector2(0f, -1f), true, end);
                                OrderQueue.AddLast(new ShipGoal(Plan.DropOffGoods, Vector2.Zero, 0f));
                                State = AIState.SystemTrader;
                                return;
                            }
                        }
                    }
                }
                #endregion
                
                #region Get Food
                if (start == null && end != null && FoodOrProd == "Food"
                    && (Owner.CargoSpace_Used == 0 || Owner.CargoSpace_Used / Owner.CargoSpace_Max < .2f))
                {
                    planets.Clear();
                    foreach (Planet planet in Owner.loyalty.GetPlanets())
                    {
                        if (planet.ParentSystem.combatTimer > 0)
                            continue;

                        float distanceWeight = TradeSort(Owner, planet, "Food", Owner.CargoSpace_Max, false);
                        planet.ExportFSWeight = distanceWeight < planet.ExportFSWeight ? distanceWeight : planet.ExportFSWeight;
                        if (planet.fs == Planet.GoodState.EXPORT && InsideAreaOfOperation(planet))
                            planets.Add(planet);
                    }

                    if (planets.Count > 0)
                    {
                        sortPlanets = planets.OrderBy(PlanetCheck =>
                            {
                                return TradeSort(Owner, PlanetCheck, "Food", Owner.CargoSpace_Max, false);
                                    //+ this.TradeSort(this.Owner, this.end, "Food", this.Owner.CargoSpace_Max)
                                    ;
                                //weight += this.Owner.CargoSpace_Max / (PlanetCheck.FoodHere + 1);
                                //weight += Vector2.Distance(PlanetCheck.Position, this.Owner.Position) / this.Owner.GetmaxFTLSpeed;
                                //return weight;
                            });
                        foreach (Planet p in sortPlanets)
                        {
                            float cargoSpaceMax = p.FoodHere; 
                            flag = false;
                            float mySpeed = TradeSort(Owner, p, "Food", Owner.CargoSpace_Max, false);                            
                            //cargoSpaceMax = cargoSpaceMax + p.NetFoodPerTurn * mySpeed;
                            using (Owner.loyalty.GetShips().AcquireReadLock())
                            {
                                for (var k = 0; k < Owner.loyalty.GetShips().Count; k++)
                                {
                                    Ship s = Owner.loyalty.GetShips()[k];
                                    if (s != null && (s.shipData.Role == ShipData.RoleName.freighter || s.shipData.ShipCategory == ShipData.Category.Civilian) && s != Owner && !s.isConstructor)
                                    {
                                        ShipGoal plan =null;
                                        plan = s.GetAI().OrderQueue.LastOrDefault();

                                        if (plan != null && s.GetAI().State == AIState.SystemTrader && s.GetAI().start == p && plan.Plan == Plan.PickupGoods && s.GetAI().FoodOrProd == "Food")
                                        {

                                            float currenTrade = TradeSort(s, p, "Food", s.CargoSpace_Max, false);
                                            if (currenTrade > 1000)
                                                continue;

                                            float efficiency = Math.Abs(currenTrade - mySpeed);
                                            efficiency = s.CargoSpace_Max - efficiency * p.NetFoodPerTurn;
                                            if (efficiency > 0)
                                            {
                                                if (efficiency > s.CargoSpace_Max)
                                                    efficiency = s.CargoSpace_Max;
                                                cargoSpaceMax = cargoSpaceMax - efficiency;
                                            }
                                            //cargoSpaceMax = cargoSpaceMax - s.CargoSpace_Max;
                                        }
                                    
                                        if (cargoSpaceMax <=0+p.MAX_STORAGE*.1f)// < this.Owner.CargoSpace_Max)
                                        {
                                            flag = true;
                                            break;
                                        }
                                    }
                                }
                            }
                            if (!flag)
                            {
                                start = p;
                                //this.Owner.TradingFood = true;
                                //this.Owner.TradingProd = false;
                                break;
                            }
                        }
                    }
                }
                #endregion

                #region Get Production
                if (start == null && end != null && FoodOrProd == "Prod" 
                    && (Owner.CargoSpace_Used ==0 || Owner.CargoSpace_Used / Owner.CargoSpace_Max <.2f  ))
                {
                    planets.Clear();
                    foreach (Planet planet in Owner.loyalty.GetPlanets())
                        if (planet.ParentSystem.combatTimer <= 0)
                        {
                            float distanceWeight = TradeSort(Owner, planet, "Production", Owner.CargoSpace_Max, false);
                            planet.ExportPSWeight = distanceWeight < planet.ExportPSWeight ? distanceWeight : planet.ExportPSWeight;

                            if (planet.ps == Planet.GoodState.EXPORT && InsideAreaOfOperation(planet))
                                planets.Add(planet);
                        }
                    if (planets.Count > 0)
                    {
                        sortPlanets = planets.OrderBy(PlanetCheck => {//(PlanetCheck.ProductionHere > this.Owner.CargoSpace_Max))
                                //.ThenBy(dest => Vector2.Distance(this.Owner.Position, dest.Position));

                            return TradeSort(Owner, PlanetCheck, "Production", Owner.CargoSpace_Max, false);
                                  // + this.TradeSort(this.Owner, this.end, "Production", this.Owner.CargoSpace_Max);
                            
                        });
                        foreach (Planet p in sortPlanets)
                        {
                            flag = false;
                            float cargoSpaceMax = p.ProductionHere;
                            
                            float mySpeed = TradeSort(Owner, p, "Production", Owner.CargoSpace_Max, false);
                            //cargoSpaceMax = cargoSpaceMax + p.NetProductionPerTurn * mySpeed;

                            //+ this.TradeSort(this.Owner, this.end, "Production", this.Owner.CargoSpace_Max);
                            
                            ShipGoal plan;
                            using (Owner.loyalty.GetShips().AcquireReadLock())
                            {
                                for (var k = 0; k < Owner.loyalty.GetShips().Count; k++)
                                {
                                    Ship s = Owner.loyalty.GetShips()[k];
                                    if (s != null && (s.shipData.Role == ShipData.RoleName.freighter || s.shipData.ShipCategory == ShipData.Category.Civilian) && s != Owner && !s.isConstructor)
                                    {
                                        plan = null;
                                                                      
                                        try
                                        {
                                        
                                            plan = s.GetAI().OrderQueue.LastOrDefault();
                                        
                                        }
                                        catch (Exception ex)
                                        {
                                            Log.Error(ex, "Order Trade Orderqueue fail");
                                        }
                                        if (plan != null && s.GetAI().State == AIState.SystemTrader && s.GetAI().start == p && plan.Plan == Plan.PickupGoods && s.GetAI().FoodOrProd == "Prod")
                                        {

                                            float currenTrade = TradeSort(s, p, "Production", s.CargoSpace_Max, false);      
                                            if (currenTrade > 1000)
                                                continue;

                                            float efficiency = Math.Abs(currenTrade - mySpeed);
                                            efficiency = s.CargoSpace_Max - efficiency * p.NetProductionPerTurn;
                                            if(efficiency >0)
                                                cargoSpaceMax = cargoSpaceMax - efficiency;
                                        }
                                    
                                        if (cargoSpaceMax <= 0 + p.MAX_STORAGE * .1f) // this.Owner.CargoSpace_Max)
                                        {
                                            flag = true;
                                            break;
                                        }
                                    }
                                }
                            }
                            if (!flag)
                            {
                                start = p;
                                //this.Owner.TradingFood = false;
                                //this.Owner.TradingProd = true;
                                break;
                            }
                        }
                    }
                }
                #endregion

                if (start != null && end != null && start != end )
                {
                    //if (this.Owner.CargoSpace_Used == 00 && this.start.Population / this.start.MaxPopulation < 0.2 && this.end.Population > 2000f && Vector2.Distance(this.Owner.Center, this.end.Position) < 500f)  //fbedard: dont make empty run !
                    //    this.PickupAnyPassengers();
                    //if (this.Owner.CargoSpace_Used == 00 && Vector2.Distance(this.Owner.Center, this.end.Position) < 500f)  //fbedard: dont make empty run !
                    //    this.PickupAnyGoods();
                    OrderMoveTowardsPosition(start.Position + RandomMath.RandomDirection() * 500f, 0f, new Vector2(0f, -1f), true, start);
                    
                    OrderQueue.AddLast(new ShipGoal(Plan.PickupGoods, Vector2.Zero, 0f));
                   
                }
                else
                {                    
                    awaitClosest = start ?? end;
                    start = null;
                    end = null;
                    if(Owner.CargoSpace_Used >0)
                        Owner.CargoClear();
                }
                State = AIState.SystemTrader;
                Owner.TradeTimer = 5f;
                if (string.IsNullOrEmpty(FoodOrProd))
                    if (Owner.TradingFood)
                        FoodOrProd = "Food";
                    else
                        FoodOrProd = "Prod";
            }
            //catch { }
        }

	    private bool ShouldSuspendTradeDueToCombat()
	    {
	        return Owner.loyalty.GetOwnedSystems().All(combat => combat.combatTimer > 0);
	    }

		public void OrderTradeFromSave(bool hasCargo, Guid startGUID, Guid endGUID)
		{
            if (Owner.CargoSpace_Max == 0 || State == AIState.Flee || ShouldSuspendTradeDueToCombat())
                return;

            if (start == null && end == null)
                foreach (Planet p in Owner.loyalty.GetPlanets())
                {
                    if (p.guid == startGUID)
                        start = p;
                    if (p.guid != endGUID)
                        continue;
                    end = p;
                }
		    if (!hasCargo && start != null)
			{
				OrderMoveTowardsPosition(start.Position + RandomMath.RandomDirection() * 500f, 0f, new Vector2(0f, -1f), true, start);
                OrderQueue.AddLast(new ShipGoal(Plan.PickupGoods, Vector2.Zero, 0f));
				State = AIState.SystemTrader;
			}
			if (!hasCargo || end == null)
			{
				if (!hasCargo && (start == null || end == null))
				    OrderTrade(5f);
			    return;
			}
			OrderMoveTowardsPosition(end.Position + RandomMath.RandomDirection() * 500f, 0f, new Vector2(0f, -1f), true, end);
            OrderQueue.AddLast(new ShipGoal(Plan.DropOffGoods, Vector2.Zero, 0f));
			State = AIState.SystemTrader;
		}



	    private bool InsideAreaOfOperation(Planet planet)
	    {
	        if (Owner.AreaOfOperation.Count == 0)
	            return true;
            foreach (Rectangle AO in Owner.AreaOfOperation)
                if (HelperFunctions.CheckIntersection(AO, planet.Position))
                    return true;
	        return false;
	    }

	    private float RelativePlanetFertility(Planet p)
	    {
	        return p.Owner.data.Traits.Cybernetic > 0 ? p.MineralRichness : p.Fertility;
        }

	    private bool PassengerDropOffTarget(Planet p)
	    {
            return p != start && p.MaxPopulation > 2000f && p.Population / p.MaxPopulation < 0.5f
                && RelativePlanetFertility(p) >= 0.5f;
        }

	    private bool SelectPlanetByFilter(IList<Planet> safePlanets, out Planet targetPlanet, Func<Planet, bool> predicate)
	    {
            var closestD = 999999999f;
	        targetPlanet = null;

            foreach (Planet p in safePlanets)
            {
                if (!predicate(p) || !InsideAreaOfOperation(p))
                    continue;

                float distance = Vector2.Distance(Owner.Center, p.Position);
                if (distance >= closestD)
                    continue;

                closestD = distance;
                targetPlanet = p;
            }
	        return targetPlanet != null;
        }



        //trade pick passenger targets
        public void OrderTransportPassengers(float elapsedTime)
        {
            Owner.TradeTimer -= elapsedTime;
            if (Owner.TradeTimer > 0f || Owner.CargoSpace_Max == 0f || State == AIState.Flee || Owner.isConstructor)
                return;

            if (ShouldSuspendTradeDueToCombat())
            {
                Owner.TradeTimer = 5f;
                return;
            }

            var cargo = Owner.GetCargo();
            if (!cargo.ContainsKey("Colonists_1000"))
                cargo.Add("Colonists_1000", 0f);
            var safePlanets = Owner.loyalty.GetPlanets().Where(combat => combat.ParentSystem.combatTimer <= 0).ToList();
            OrderQueue.Clear();

            // RedFox: Where to drop nearest Population
            if (cargo["Colonists_1000"] > 0f)
            {
                if (SelectPlanetByFilter(safePlanets, out end, PassengerDropOffTarget))
                {
                    OrderMoveTowardsPosition(end.Position, 0f, new Vector2(0f, -1f), true, end);
                    State = AIState.PassengerTransport;
                    FoodOrProd = "Pass";
                    OrderQueue.AddLast(new ShipGoal(Plan.DropoffPassengers, Vector2.Zero, 0f));
                }
                return;
            }

            // RedFox: Where to load & drop nearest Population
            SelectPlanetByFilter(safePlanets, out start, p =>
            {
                return p.MaxPopulation > 1000 && p.Population > 1000;
            });
            SelectPlanetByFilter(safePlanets, out end, PassengerDropOffTarget);

            if (start != null && end != null)
            {
                OrderMoveTowardsPosition(start.Position + RandomMath.RandomDirection() * 500f, 0f, new Vector2(0f, -1f), true, start);
                OrderQueue.AddLast(new ShipGoal(Plan.PickupPassengers, Vector2.Zero, 0f));
            }
            else
            {
                awaitClosest = start ?? end;
                start = null;
                end = null;
            }
            Owner.TradeTimer = 5f;
            State = AIState.PassengerTransport;
            FoodOrProd = "Pass";
        }

		public void OrderTransportPassengersFromSave()
		{
		    OrderTransportPassengers(0.33f);
        }

		public void OrderTroopToBoardShip(Ship s)
		{
			HasPriorityOrder = true;
			EscortTarget = s;
            OrderQueue.Clear();
			OrderQueue.AddLast(new ShipGoal(Plan.BoardShip, Vector2.Zero, 0f));
        
		}

		public void OrderTroopToShip(Ship s)
		{
			EscortTarget = s;
			OrderQueue.Clear();
			OrderQueue.AddLast(new ShipGoal(Plan.TroopToShip, Vector2.Zero, 0f));
		}

		private void PickupGoods()
		{
            if (start == null)
            {
                OrderTrade(0.1f);
                return;
            }

            var cargo = Owner.GetCargo();
            if (FoodOrProd == "Food")
            {
                start.ProductionHere += cargo.ConsumeValue("Production");
                start.Population += cargo.ConsumeValue("Colonists_1000") * Owner.loyalty.data.Traits.PassengerModifier;

                float modifier = start.MAX_STORAGE * .10f;
				while (start.FoodHere > modifier && (int)Owner.CargoSpace_Max - (int)Owner.CargoSpace_Used > 0)
				{
					Owner.AddGood("Food", 1);
                    start.FoodHere -= 1f;
				}
                OrderQueue.RemoveFirst();
                OrderMoveTowardsPosition(end.Position + UniverseRandom.RandomDirection() * 500f, 0f, new Vector2(0f, -1f), true, end);
				OrderQueue.AddLast(new ShipGoal(Plan.DropOffGoods, Vector2.Zero, 0f));
				//this.State = AIState.SystemTrader;
			}
			else if (FoodOrProd != "Prod")
			{
				OrderTrade(0.1f);
			}
			else
            {
                start.FoodHere += cargo.ConsumeValue("Food");
                start.Population += cargo.ConsumeValue("Colonists_1000") * Owner.loyalty.data.Traits.PassengerModifier;
                float modifier = start.MAX_STORAGE *.10f;
                //if (this.start.ProductionHere < this.Owner.CargoSpace_Max)
                //{
                //    //this.OrderTrade(0.1f);
                //    modifier= this.start.ProductionHere * .5f;
                //}
                
                while (start.ProductionHere > modifier && (int)Owner.CargoSpace_Max - (int)Owner.CargoSpace_Used > 0)
				{
					Owner.AddGood("Production", 1);
					Planet productionHere1 = start;
					productionHere1.ProductionHere = productionHere1.ProductionHere - 1f;
				}
                OrderQueue.RemoveFirst();
                OrderMoveTowardsPosition(end.Position + UniverseRandom.RandomDirection() * 500f, 0f, new Vector2(0f, -1f), true, end);
				OrderQueue.AddLast(new ShipGoal(Plan.DropOffGoods, Vector2.Zero, 0f));
				//this.State = AIState.SystemTrader;
			}
			State = AIState.SystemTrader;
		}

        private void PickupAnyGoods()  //fbedard
        {
            if (end.FoodHere > Owner.CargoSpace_Max && end.fs == Planet.GoodState.EXPORT && start.MAX_STORAGE - start.FoodHere > Owner.CargoSpace_Max * 3f && start.fs == Planet.GoodState.IMPORT)
                while (end.FoodHere > 0f && (int)Owner.CargoSpace_Max - (int)Owner.CargoSpace_Used > 0)
                {
                Owner.AddGood("Food", 1);
                Planet foodHere = end;
                foodHere.FoodHere = foodHere.FoodHere - 1f;
                }

            if (end.ProductionHere > Owner.CargoSpace_Max && end.ps == Planet.GoodState.EXPORT && start.MAX_STORAGE - start.ProductionHere > Owner.CargoSpace_Max * 3f && start.ps == Planet.GoodState.IMPORT)
                while (end.ProductionHere > 0f && (int)Owner.CargoSpace_Max - (int)Owner.CargoSpace_Used > 0)
                {
                 Owner.AddGood("Production", 1);
                 Planet productionHere1 = end;
                 productionHere1.ProductionHere = productionHere1.ProductionHere - 1f;
                }
        }

        // trade pickup passengers
		private void PickupPassengers()
		{
		    var cargo = Owner.GetCargo();
            start.ProductionHere += cargo.ConsumeValue("Production");
		    start.FoodHere       += cargo.ConsumeValue("Food");
			while (Owner.CargoSpace_Used < Owner.CargoSpace_Max)
			{
				Owner.AddGood("Colonists_1000", 1);
                start.Population -= Owner.loyalty.data.Traits.PassengerModifier;
			}
			OrderQueue.RemoveFirst();
			OrderMoveTowardsPosition(end.Position, 0f, new Vector2(0f, -1f), true, end);
			State = AIState.PassengerTransport;
			OrderQueue.AddLast(new ShipGoal(Plan.DropoffPassengers, Vector2.Zero, 0f));
		}

        private void PickupAnyPassengers()  //fbedard
        {
            while (Owner.CargoSpace_Used < Owner.CargoSpace_Max)
            {
                Owner.AddGood("Colonists_1000", 1);
                end.Population -= Owner.loyalty.data.Traits.PassengerModifier;
            }
        }

        // movement cachelookup
        private bool PathCacheLookup(Point startp, Point endp, Vector2 startv, Vector2 endv)
        {            
            if (!Owner.loyalty.PathCache.TryGetValue(startp, out Map<Point, Empire.PatchCacheEntry> pathstart)
                || !pathstart.TryGetValue(endp, out Empire.PatchCacheEntry pathend))
                return false;

            lock (WayPointLocker)
            {
                if (pathend.Path.Count > 2)
                {
                    int n = pathend.Path.Count - 2;
                    for (var x = 1; x < n; ++x)
                    {
                        Vector2 point = pathend.Path[x];
                        if (point != Vector2.Zero)
                            ActiveWayPoints.Enqueue(point);
                    }
                }
                ActiveWayPoints.Enqueue(endv);
            }
            ++pathend.CacheHits;
            return true;
        }


        // movement pathing       
        private void PlotCourseToNew(Vector2 endPos, Vector2 startPos)
        {
            if (Owner.loyalty.grid != null && Vector2.Distance(startPos,endPos) > Empire.ProjectorRadius *2)
            {
                int reducer = Empire.Universe.reducer;//  (int)(Empire.ProjectorRadius );
                int granularity = Owner.loyalty.granularity; // (int)Empire.ProjectorRadius / 2;

                var startp = new Point((int)startPos.X, (int)startPos.Y);
                startp.X /= reducer;
                startp.Y /= reducer;
                startp.X += granularity;
                startp.Y += granularity;
                var endp = new Point((int)endPos.X, (int)endPos.Y);
                endp.X /= reducer;
                endp.Y /= reducer;
                endp.Y += granularity;
                endp.X += granularity;
                //@Bug Add sanity correct to prevent start and end from getting posistions off the map
                PathFinderFast path;
                Owner.loyalty.LockPatchCache.EnterReadLock();
                if (PathCacheLookup(startp, endp, startPos, endPos))
                {
                    Owner.loyalty.LockPatchCache.ExitReadLock();
                    return;
                }
                Owner.loyalty.LockPatchCache.ExitReadLock();

                path = new PathFinderFast(Owner.loyalty.grid)
                {
                    Diagonals = true,
                    HeavyDiagonals = false,
                    PunishChangeDirection = true,
                    Formula = HeuristicFormula.EuclideanNoSQR, // try with HeuristicFormula.MaxDXDY?
                    HeuristicEstimate = 1, // try with 2?
                    SearchLimit = 999999
                };

                var pathpoints = path.FindPath(startp, endp);
                lock (WayPointLocker)
                {
                    if (pathpoints != null)
                    {
                        var cacheAdd = new Array<Vector2>();
                        //byte lastValue =0;
                        int y = pathpoints.Count() - 1;                                                        
                        for (int x =y; x >= 0; x-=2)                            
                        {
                            PathFinderNode pnode = pathpoints[x];
                            //var value = this.Owner.loyalty.grid[pnode.X, pnode.Y];
                            //if (value != 1 && lastValue >1)
                            //{
                            //    lastValue--;
                            //    continue;
                            //}
                            //lastValue = value ==1 ?(byte)1 : (byte)2;
                            var translated = new Vector2((pnode.X - granularity) * reducer, (pnode.Y - granularity) * reducer);
                            if (translated == Vector2.Zero)
                                continue;
                            cacheAdd.Add(translated);
                                
                            if (Vector2.Distance(translated, endPos) > Empire.ProjectorRadius *2 
                                && Vector2.Distance(translated, startPos) > Empire.ProjectorRadius *2)
                                ActiveWayPoints.Enqueue(translated);
                        }

                        var cache = Owner.loyalty.PathCache;
                        if (!cache.ContainsKey(startp))
                        {
                            Owner.loyalty.LockPatchCache.EnterWriteLock();
                            var endValue = new Empire.PatchCacheEntry(cacheAdd);
                            var endkey   = new Map<Point, Empire.PatchCacheEntry>();

                            endkey.Add(endp, endValue);
                            cache.Add(startp, endkey);
                            Owner.loyalty.pathcacheMiss++;
                            Owner.loyalty.LockPatchCache.ExitWriteLock();

                        }
                        else if (!cache[startp].ContainsKey(endp))
                        {
                            Owner.loyalty.LockPatchCache.EnterWriteLock();
                                
                            var endValue = new Empire.PatchCacheEntry(cacheAdd);
                            cache[startp].Add(endp, endValue);
                            Owner.loyalty.pathcacheMiss++;
                            Owner.loyalty.LockPatchCache.ExitWriteLock();
                        }
                        else
                        {
                            Owner.loyalty.LockPatchCache.EnterReadLock();
                            PathCacheLookup(startp, endp, startPos, endPos);
                            Owner.loyalty.LockPatchCache.ExitReadLock();
                        }
                    }
                    ActiveWayPoints.Enqueue(endPos);
                    return;
                }
                   
                    
            }
            ActiveWayPoints.Enqueue(endPos);

            #if false
                Array<Vector2> goodpoints = new Array<Vector2>();
                //Grid path = new Grid(this.Owner.loyalty, 36, 10f);
                if (Empire.Universe != null && this.Owner.loyalty.SensorNodes.Count != 0)
                    goodpoints = this.Owner.loyalty.pathhMap.Pathfind(startPos, endPos, false);
                if (goodpoints != null && goodpoints.Count > 0)
                {
                    lock (this.WayPointLocker)
                    {
                        foreach (Vector2 wayp in goodpoints.Skip(1))
                        {

                            this.ActiveWayPoints.Enqueue(wayp);
                        }
                        //this.ActiveWayPoints.Enqueue(endPos);
                    }
                    //this.Owner.loyalty.lockPatchCache.EnterWriteLock();
                    //int cache;
                    //if (!this.Owner.loyalty.pathcache.TryGetValue(goodpoints, out cache))
                    //{

                    //    this.Owner.loyalty.pathcache.Add(goodpoints, 0);

                    //}
                    //cache++;
                    this.Owner.loyalty.lockPatchCache.ExitWriteLock();

                }
                else
                {
                    if (startPos != Vector2.Zero && endPos != Vector2.Zero)
                    {
                        // this.ActiveWayPoints.Enqueue(startPos);
                        this.ActiveWayPoints.Enqueue(endPos);
                    }
                    else
                        this.ActiveWayPoints.Clear();
                }
            #endif
        }

        private Array<Vector2> GoodRoad(Vector2 endPos, Vector2 startPos)
        {
            SpaceRoad targetRoad =null;
            var StartRoads = new Array<SpaceRoad>();
            var endRoads = new Array<SpaceRoad>();
            var nodePos = new Array<Vector2>();
            foreach (SpaceRoad road in Owner.loyalty.SpaceRoadsList)
            {
                Vector2 start = road.GetOrigin().Position;
                Vector2 end = road.GetDestination().Position;
                if (Vector2.Distance(start, startPos) < Empire.ProjectorRadius)
                    if (Vector2.Distance(end, endPos) < Empire.ProjectorRadius)
                        targetRoad = road;
                    else
                        StartRoads.Add(road);
                else if (Vector2.Distance(end, startPos) < Empire.ProjectorRadius)
                    if (Vector2.Distance(start, endPos) < Empire.ProjectorRadius)
                        targetRoad = road;
                    else
                        endRoads.Add(road);

                if (  targetRoad !=null)
                    break;
            }
            

            if(targetRoad != null)
            {
                foreach(RoadNode node in targetRoad.RoadNodesList)
                    nodePos.Add(node.Position);
                nodePos.Add(endPos);
                nodePos.Add(targetRoad.GetDestination().Position);
                nodePos.Add(targetRoad.GetOrigin().Position);
            }
            return nodePos;
            


        }
        
        private Array<Vector2> PlotCourseToNewViaRoad(Vector2 endPos, Vector2 startPos)
        {
            //return null;
            var goodPoints = new Array<Vector2>();
            var potentialEndRoads = new Array<SpaceRoad>();
            var potentialStartRoads = new Array<SpaceRoad>();
            RoadNode nearestNode = null;
            var distanceToNearestNode = 0f;
            foreach(SpaceRoad road in Owner.loyalty.SpaceRoadsList)
            {
                if (Vector2.Distance(road.GetOrigin().Position, endPos) < 300000f || Vector2.Distance(road.GetDestination().Position, endPos) < 300000f)
                    potentialEndRoads.Add(road);
                foreach(RoadNode projector in road.RoadNodesList)
                    if (nearestNode == null || Vector2.Distance(projector.Position, startPos) < distanceToNearestNode)
                    {
                        potentialStartRoads.Add(road);
                        nearestNode = projector;
                        distanceToNearestNode = Vector2.Distance(projector.Position, startPos);
                    }
            }

            var targetRoads = potentialStartRoads.Intersect(potentialEndRoads).ToList();
            if (targetRoads.Count == 1)
            {
                SpaceRoad targetRoad = targetRoads[0];
                bool startAtOrgin = Vector2.Distance(endPos, targetRoad.GetOrigin().Position) > Vector2.Distance(endPos, targetRoad.GetDestination().Position);
                var foundstart = false;
                if (startAtOrgin)
                    foreach (RoadNode node in targetRoad.RoadNodesList)
                    {
                        if (!foundstart && node != nearestNode)
                            continue;
                        else if (!foundstart)
                            foundstart = true;
                        goodPoints.Add(node.Position);
                        goodPoints.Add(targetRoad.GetDestination().Position);
                        goodPoints.Add(targetRoad.GetOrigin().Position);

                    }
                else
                    foreach (RoadNode node in targetRoad.RoadNodesList.Reverse<RoadNode>())
                    {
                        if (!foundstart && node != nearestNode)
                            continue;
                        else if (!foundstart)
                            foundstart = true;
                        goodPoints.Add(node.Position);
                        goodPoints.Add(targetRoad.GetDestination().Position);
                        goodPoints.Add(targetRoad.GetOrigin().Position);

                    }
          
            }
            else if (true)
            {
                while (potentialStartRoads.Intersect(potentialEndRoads).Count() == 0)
                {
                    var test = false;
                    foreach (SpaceRoad road in Owner.loyalty.SpaceRoadsList)
                    {
                        var flag = false;

                        if (!potentialStartRoads.Contains(road))
                            foreach (SpaceRoad proad in potentialStartRoads)
                                if (proad.GetDestination() == road.GetOrigin() || proad.GetOrigin() == road.GetDestination())
                                    flag = true;
                        if (flag)
                        {
                            potentialStartRoads.Add(road);
                            test = true;
                        }
                        
                    }
                    if(!test)
                    {
                        Log.Info("failed to find road path for {0}", Owner.loyalty.PortraitName);
                        return new Array<Vector2>();
                    }
                }
                while (!potentialEndRoads.Contains(potentialStartRoads[0]))
                {
                    var test = false;
                    foreach (SpaceRoad road in potentialStartRoads)
                    {
                        var flag = false;

                        if (!potentialEndRoads.Contains(road))
                            foreach (SpaceRoad proad in potentialEndRoads)
                                if (proad.GetDestination() == road.GetOrigin() || proad.GetOrigin() == road.GetDestination())
                                    flag = true;
                        if (flag)
                        {

                            test = true;
                            potentialEndRoads.Add(road);
                            
                        }
                        
                    }
                    if(!test)
                    {
                        Log.Info("failed to find road path for {0}", Owner.loyalty.PortraitName);
                        return new Array<Vector2>();
                    }


                }
                targetRoads = potentialStartRoads.Intersect(potentialEndRoads).ToList();
                if (targetRoads.Count >0)
                {
                    SpaceRoad targetRoad = null;
                    RoadNode targetnode = null;
                    float distance = -1f;
                    foreach (SpaceRoad road in targetRoads)
                    foreach (RoadNode node in road.RoadNodesList)
                        if (distance == -1f || Vector2.Distance(node.Position, startPos) < distance)
                        {
                            targetRoad = road;
                            targetnode = node;
                            distance = Vector2.Distance(node.Position, startPos);

                        }
                    var orgin = false;
                    var startnode = false;
                    foreach (SpaceRoad road in targetRoads)
                        if (road.GetDestination() == targetRoad.GetDestination() || road.GetDestination() == targetRoad.GetOrigin())
                            orgin = true;
                    if (orgin)
                        foreach (RoadNode node in targetRoad.RoadNodesList)
                            if (!startnode || node != targetnode)
                            {
                                continue;
                            }
                            else
                            {
                                startnode = true;
                                goodPoints.Add(node.Position);
                                goodPoints.Add(targetRoad.GetDestination().Position);
                                goodPoints.Add(targetRoad.GetOrigin().Position);
                            }
                    else
                        foreach (RoadNode node in targetRoad.RoadNodesList.Reverse<RoadNode>())
                            if (!startnode || node != targetnode)
                            {
                                continue;
                            }
                            else
                            {
                                startnode = true;
                                goodPoints.Add(node.Position);
                                goodPoints.Add(targetRoad.GetDestination().Position);
                                goodPoints.Add(targetRoad.GetOrigin().Position);
                            }
                    while (Vector2.Distance(targetRoad.GetOrigin().Position,endPos)>300000 
                        &&  Vector2.Distance(targetRoad.GetDestination().Position,endPos)>300000)
                    {
                        targetRoads.Remove(targetRoad);
                        if(orgin)
                        {
                            var test = false;
                            foreach(SpaceRoad road in targetRoads)
                                if(road.GetOrigin()==targetRoad.GetDestination())
                                {
                                    foreach(RoadNode node in road.RoadNodesList)
                                    {
                                        goodPoints.Add(node.Position);
                                        goodPoints.Add(targetRoad.GetDestination().Position);
                                        goodPoints.Add(targetRoad.GetOrigin().Position);
                                    }
                                    targetRoad = road;
                                    test = true;
                                    break;
                                }
                                else if(road.GetDestination() == targetRoad.GetDestination())
                                {
                                    orgin = false;
                                    if (road.GetOrigin() == targetRoad.GetDestination())
                                        foreach (RoadNode node in road.RoadNodesList.Reverse<RoadNode>())
                                        {
                                            goodPoints.Add(node.Position);
                                            goodPoints.Add(targetRoad.GetDestination().Position);
                                            goodPoints.Add(targetRoad.GetOrigin().Position);
                                        }
                                    test = true;
                                    targetRoad = road;
                                    break;
                                }
                            if (!test)
                                orgin = false;
                        }
                        else
                        {
                            var test = false;
                            foreach (SpaceRoad road in targetRoads)
                                if (road.GetOrigin() == targetRoad.GetOrigin())
                                {
                                    foreach (RoadNode node in road.RoadNodesList)
                                    {
                                        goodPoints.Add(node.Position);
                                        goodPoints.Add(targetRoad.GetDestination().Position);
                                        goodPoints.Add(targetRoad.GetOrigin().Position);
                                    }
                                    targetRoad = road;
                                    test = true;
                                    break;
                                }
                                else if (road.GetDestination() == targetRoad.GetOrigin())
                                {
                                    orgin = true;
                                    if (road.GetOrigin() == targetRoad.GetDestination())
                                        foreach (RoadNode node in road.RoadNodesList.Reverse<RoadNode>())
                                        {
                                            goodPoints.Add(node.Position);
                                            goodPoints.Add(targetRoad.GetDestination().Position);
                                            goodPoints.Add(targetRoad.GetOrigin().Position);
                                        }
                                    targetRoad = road;
                                    test = true;
                                    break;
                                }
                            if (!test)
                                break;
                        }

                    }
                }
            }
            return goodPoints;
        }

		private void RotateInLineWithVelocity(float elapsedTime, ShipGoal Goal)
		{
			if (Owner.Velocity == Vector2.Zero)
			{
				OrderQueue.RemoveFirst();
				return;
			}
			var forward = new Vector2((float)Math.Sin((double)Owner.Rotation), -(float)Math.Cos((double)Owner.Rotation));
			var right = new Vector2(-forward.Y, forward.X);
			var angleDiff = (float)Math.Acos((double)Vector2.Dot(Vector2.Normalize(Owner.Velocity), forward));
			float facing = Vector2.Dot(Vector2.Normalize(Owner.Velocity), right) > 0f ? 1f : -1f;
			if (angleDiff <= 0.2f)
			{
				OrderQueue.RemoveFirst();
				return;
			}
			RotateToFacing(elapsedTime, angleDiff, facing);
		}

		private void RotateToDesiredFacing(float elapsedTime, ShipGoal goal)
		{
			Vector2 p = MathExt.PointFromRadians(Vector2.Zero, goal.DesiredFacing, 1f);
			Vector2 fvec = Vector2.Zero.FindVectorToTarget(p);
			Vector2 wantedForward = Vector2.Normalize(fvec);
			var forward = new Vector2((float)Math.Sin((double)Owner.Rotation), -(float)Math.Cos((double)Owner.Rotation));
			var right = new Vector2(-forward.Y, forward.X);
			var angleDiff = (float)Math.Acos((double)Vector2.Dot(wantedForward, forward));
			float facing = Vector2.Dot(wantedForward, right) > 0f ? 1f : -1f;
			if (angleDiff <= 0.02f)
			{
				OrderQueue.RemoveFirst();
				return;
			}
			RotateToFacing(elapsedTime, angleDiff, facing);
		}

		private bool RotateToFaceMovePosition(float elapsedTime, ShipGoal goal)
		{
            var turned = false;
            var forward = new Vector2((float)Math.Sin((double)Owner.Rotation), -(float)Math.Cos((double)Owner.Rotation));
			var right = new Vector2(-forward.Y, forward.X);
			Vector2 VectorToTarget = Owner.Center.FindVectorToTarget(goal.MovePosition);
			var angleDiff = (float)Math.Acos((double)Vector2.Dot(VectorToTarget, forward));
			if (angleDiff > 0.2f)
			{
				Owner.HyperspaceReturn();
				RotateToFacing(elapsedTime, angleDiff, Vector2.Dot(VectorToTarget, right) > 0f ? 1f : -1f);
                turned = true;
			}
			else if (OrderQueue.Count > 0)
			{
				OrderQueue.RemoveFirst();
				
			}
            return turned;
		}
        private bool RotateToFaceMovePosition(float elapsedTime, Vector2 MovePosition)
        {
            var turned = false;
            var forward = new Vector2((float)Math.Sin((double)Owner.Rotation), -(float)Math.Cos((double)Owner.Rotation));
            var right = new Vector2(-forward.Y, forward.X);
            Vector2 VectorToTarget = Owner.Center.FindVectorToTarget( MovePosition);
            var angleDiff = (float)Math.Acos((double)Vector2.Dot(VectorToTarget, forward));
            if (angleDiff > Owner.rotationRadiansPerSecond*elapsedTime )
            {
                Owner.HyperspaceReturn();
                RotateToFacing(elapsedTime, angleDiff, Vector2.Dot(VectorToTarget, right) > 0f ? 1f : -1f);
                turned = true;
            }
 
            return turned;
        }
        //movement rotate
		private void RotateToFacing(float elapsedTime, float angleDiff, float facing)
		{
			Owner.isTurning = true;
			float RotAmount = Math.Min(angleDiff, facing * elapsedTime * Owner.rotationRadiansPerSecond);
			if (Math.Abs(RotAmount) > angleDiff)
			    RotAmount = RotAmount <= 0f ? -angleDiff : angleDiff;
		    if (RotAmount > 0f)
			{
				if (Owner.yRotation > -Owner.maxBank)
				{
					Ship owner = Owner;
					owner.yRotation = owner.yRotation - Owner.yBankAmount;
				}
			}
			else if (RotAmount < 0f && Owner.yRotation < Owner.maxBank)
			{
				Ship ship = Owner;
				ship.yRotation = ship.yRotation + Owner.yBankAmount;
			}
			if (!float.IsNaN(RotAmount))
			{
				Ship rotation = Owner;
				rotation.Rotation = rotation.Rotation + RotAmount;
			}
		}

        //targetting get targets
        public GameplayObject ScanForCombatTargets(Vector2 Position, float Radius)
        {

            BadGuysNear = false;
            FriendliesNearby.Clear();
            PotentialTargets.Clear();
            NearbyShips.Clear();
            //this.TrackProjectiles.Clear();

            if (hasPriorityTarget && Target == null)
            {
                hasPriorityTarget = false;
                if (TargetQueue.Count > 0)
                {
                    hasPriorityTarget = true;
                    Target = TargetQueue.First<Ship>();
                }
            }
            if (Target != null)
                if ((Target as Ship).loyalty == Owner.loyalty)
                {
                    Target = null;
                    hasPriorityTarget = false;
                }


                else if (
                    !Intercepting && (Target as Ship).engineState == Ship.MoveState.Warp) //||((double)Vector2.Distance(Position, this.Target.Center) > (double)Radius ||
                {
                    Target = (GameplayObject)null;
                    if (!HasPriorityOrder && Owner.loyalty != universeScreen.player)
                        State = AIState.AwaitingOrders;
                    return (GameplayObject)null;
                }
            //Doctor: Increased this from 0.66f as seemed slightly on the low side. 
            CombatAI.PreferredEngagementDistance = Owner.maxWeaponsRange * 0.75f;
            SolarSystem thisSystem = Owner.System;
            if(thisSystem != null)
                foreach (Planet p in thisSystem.PlanetList)
                {
                    Empire emp = p.Owner;
                    if (emp !=null && emp != Owner.loyalty)
                    {
                        Relationship test = null;
                        Owner.loyalty.TryGetRelations(emp, out test);
                        if (!test.Treaty_OpenBorders || !test.Treaty_NAPact || Vector2.Distance(Owner.Center, p.Position) >Radius)
                            BadGuysNear = true;
                        break;
                    }


                }
            {
                if (EscortTarget != null && EscortTarget.Active && EscortTarget.GetAI().Target != null)
                {
                    var sw = new ShipWeight();
                    sw.ship = EscortTarget.GetAI().Target as Ship;
                    sw.weight = 2f;
                    NearbyShips.Add(sw);
                }
                var nearby = UniverseScreen.ShipSpatialManager.GetNearby(Owner);
                for (var i = 0; i < nearby.Count; i++)
                {
                    var item1 = nearby[i] as Ship;
                    float distance = Vector2.Distance(Owner.Center, item1.Center);
                    if (item1 != null && item1.Active && !item1.dying && distance <= Radius + (Radius == 0 ? 10000 : 0))
                    {
                        
                        Empire empire = item1.loyalty;
                        var shipTarget = item1.GetAI().Target as Ship;
                        if (empire == Owner.loyalty)
                        {
                            FriendliesNearby.Add(item1);
                        }
                        else if (empire != Owner.loyalty && Radius > 0
                            && shipTarget != null
                            && shipTarget == EscortTarget && item1.engineState != Ship.MoveState.Warp)
                        {

                            var sw = new ShipWeight();
                            sw.ship = item1;
                            sw.weight = 3f;

                            NearbyShips.Add(sw);
                            BadGuysNear = true;
                            //this.PotentialTargets.Add(item1);
                        }
                        else if (Radius > 0 && (item1.loyalty != Owner.loyalty 
                            && Owner.loyalty.GetRelations(item1.loyalty).AtWar
                            || Owner.loyalty.isFaction || item1.loyalty.isFaction))//&& Vector2.Distance(this.Owner.Center, item.Center) < 15000f)
                        {
                            var sw = new ShipWeight();
                            sw.ship = item1;
                            sw.weight = 1f;
                            NearbyShips.Add(sw);
                            //this.PotentialTargets.Add(item1);
                            BadGuysNear = Vector2.Distance(Position, item1.Position) <= Radius;
                        }
                        else if (Radius == 0 &&
                            (item1.loyalty != Owner.loyalty
                            && Owner.loyalty.GetRelations(item1.loyalty).AtWar
                            || Owner.loyalty.isFaction || item1.loyalty.isFaction)
                            )
                        {
                            BadGuysNear = true;
                        }
                    }
                }
            }


            #region supply ship logic   //fbedard: for launch only
            if (Owner.GetHangars().Where(hangar => hangar.IsSupplyBay).Count() > 0 && Owner.engineState != Ship.MoveState.Warp)  // && !this.Owner.isSpooling
            {
                IOrderedEnumerable<Ship> sortedList = null;
                {
                    sortedList = FriendliesNearby.Where(ship => ship != Owner 
                        && ship.engineState != Ship.MoveState.Warp
                        && ship.GetAI().State != AIState.Scrap
                        && ship.GetAI().State != AIState.Resupply
                        && ship.GetAI().State != AIState.Refit
                        && ship.Mothership == null 
                        && ship.OrdinanceMax > 0 
                        && ship.Ordinance / ship.OrdinanceMax < 0.5f
                        && !ship.IsTethered())
                        .OrderBy(ship => Math.Truncate(Vector2.Distance(Owner.Center, ship.Center) + 4999) / 5000).ThenByDescending(ship => ship.OrdinanceMax - ship.Ordinance);
//                      .OrderBy(ship => ship.HasSupplyBays).ThenBy(ship => ship.OrdAddedPerSecond).ThenBy(ship => Math.Truncate((Vector2.Distance(this.Owner.Center, ship.Center) + 4999)) / 5000).ThenBy(ship => ship.OrdinanceMax - ship.Ordinance);
                }

                    if (sortedList.Count() > 0)
                    {
                        var skip = 0;
                        var inboundOrdinance = 0f;
                    if(Owner.HasSupplyBays)
                        foreach (ShipModule hangar in Owner.GetHangars().Where(hangar => hangar.IsSupplyBay))
                        {
                            if (hangar.GetHangarShip() != null && hangar.GetHangarShip().Active)
                            {
                                if (hangar.GetHangarShip().GetAI().State != AIState.Ferrying && hangar.GetHangarShip().GetAI().State != AIState.ReturnToHangar && hangar.GetHangarShip().GetAI().State != AIState.Resupply && hangar.GetHangarShip().GetAI().State != AIState.Scrap)
                                {
                                    if (sortedList.Skip(skip).Count() > 0)
                                    {
                                        var g1 = new ShipGoal(Plan.SupplyShip, Vector2.Zero, 0f);
                                        hangar.GetHangarShip().GetAI().EscortTarget = sortedList.Skip(skip).First();

                                        hangar.GetHangarShip().GetAI().IgnoreCombat = true;
                                        hangar.GetHangarShip().GetAI().OrderQueue.Clear();
                                        hangar.GetHangarShip().GetAI().OrderQueue.AddLast(g1);
                                        hangar.GetHangarShip().GetAI().State = AIState.Ferrying;
                                        continue;
                                    }
                                    else
                                    {
                                        //hangar.GetHangarShip().QueueTotalRemoval();
                                        hangar.GetHangarShip().GetAI().State = AIState.ReturnToHangar;  //shuttle with no target
                                        continue;
                                    }
                                }
                                else if (sortedList.Skip(skip).Count() > 0 && hangar.GetHangarShip().GetAI().EscortTarget == sortedList.Skip(skip).First() && hangar.GetHangarShip().GetAI().State == AIState.Ferrying)
                                {
                                    inboundOrdinance = inboundOrdinance + 100f;
                                    if ((inboundOrdinance + sortedList.Skip(skip).First().Ordinance) / sortedList.First().OrdinanceMax > 0.5f)
                                    {
                                        skip++;
                                        inboundOrdinance = 0;
                                        continue;
                                    }
                                }
                                continue;
                            }
                            if (!hangar.Active || hangar.hangarTimer > 0f || Owner.Ordinance >= 100f && sortedList.Skip(skip).Count() <= 0)
                                continue;                            
                            if (ResourceManager.ShipsDict["Supply_Shuttle"].Mass / 5f > Owner.Ordinance)  //fbedard: New spawning cost
                                continue;
                            Ship shuttle = ResourceManager.CreateShipFromHangar("Supply_Shuttle", Owner.loyalty, Owner.Center, Owner);
                            shuttle.VanityName = "Supply Shuttle";
                            //shuttle.shipData.Role = ShipData.RoleName.supply;
                            //shuttle.GetAI().DefaultAIState = AIState.Flee;
                            shuttle.Velocity = UniverseRandom.RandomDirection() * shuttle.speed + Owner.Velocity;
                            if (shuttle.Velocity.Length() > shuttle.velocityMaximum)
                                shuttle.Velocity = Vector2.Normalize(shuttle.Velocity) * shuttle.speed;
                            Owner.Ordinance -= shuttle.Mass / 5f;

                            if (Owner.Ordinance >= 100f)
                            {
                                inboundOrdinance = inboundOrdinance + 100f;
                                Owner.Ordinance = Owner.Ordinance - 100f;
                                hangar.SetHangarShip(shuttle);
                                var g = new ShipGoal(Plan.SupplyShip, Vector2.Zero, 0f);
                                shuttle.GetAI().EscortTarget = sortedList.Skip(skip).First();
                                shuttle.GetAI().IgnoreCombat = true;
                                shuttle.GetAI().OrderQueue.Clear();
                                shuttle.GetAI().OrderQueue.AddLast(g);
                                shuttle.GetAI().State = AIState.Ferrying;
                            }
                            else  //fbedard: Go fetch ordinance when mothership is low on ordinance
                            {
                                shuttle.Ordinance = 0f;
                                hangar.SetHangarShip(shuttle);
                                shuttle.GetAI().IgnoreCombat = true;
                                shuttle.GetAI().State = AIState.Resupply;
                                shuttle.GetAI().OrderResupplyNearest(true);
                            }
                            break;
                        }
                    }
    
            } 
            if (Owner.shipData.Role == ShipData.RoleName.supply && Owner.Mothership == null)
                OrderScrapShip();   //Destroy shuttle without mothership

            #endregion

            //}           
            foreach (ShipWeight nearbyShip in NearbyShips )
                // Doctor: I put modifiers for the ship roles Fighter and Bomber in here, so that when searching for targets they prioritise their targets based on their selected ship role.
                // I'll additionally put a ScanForCombatTargets into the carrier fighter code such that they use this code to select their own weighted targets.
            //Parallel.ForEach(this.NearbyShips, nearbyShip =>
                if (nearbyShip.ship.loyalty != Owner.loyalty)
                {
                    if (Target as Ship == nearbyShip.ship)
                        nearbyShip.weight += 3;
                    if (nearbyShip.ship.Weapons.Count ==0)
                    {
                        ShipWeight vultureWeight = nearbyShip;
                        vultureWeight.weight = vultureWeight.weight + CombatAI.PirateWeight;
                    }
                    
                    if (nearbyShip.ship.Health / nearbyShip.ship.HealthMax < 0.5f)
                    {
                        ShipWeight vultureWeight = nearbyShip;
                        vultureWeight.weight = vultureWeight.weight + CombatAI.VultureWeight;
                    }
                    if (nearbyShip.ship.Size < 30)
                    {
                        ShipWeight smallAttackWeight = nearbyShip;
                        smallAttackWeight.weight = smallAttackWeight.weight + CombatAI.SmallAttackWeight;
                        if (Owner.shipData.ShipCategory == ShipData.Category.Fighter)
                            smallAttackWeight.weight *= 2f;
                        if (Owner.shipData.ShipCategory == ShipData.Category.Bomber)
                            smallAttackWeight.weight /= 2f;
                    }
                    if (nearbyShip.ship.Size > 30 && nearbyShip.ship.Size < 100)
                    {
                        ShipWeight mediumAttackWeight = nearbyShip;
                        mediumAttackWeight.weight = mediumAttackWeight.weight + CombatAI.MediumAttackWeight;
                        if (Owner.shipData.ShipCategory == ShipData.Category.Bomber)
                            mediumAttackWeight.weight *= 1.5f;
                    }
                    if (nearbyShip.ship.Size > 100)
                    {
                        ShipWeight largeAttackWeight = nearbyShip;
                        largeAttackWeight.weight = largeAttackWeight.weight + CombatAI.LargeAttackWeight;
                        if (Owner.shipData.ShipCategory == ShipData.Category.Fighter)
                            largeAttackWeight.weight /= 2f;
                        if (Owner.shipData.ShipCategory == ShipData.Category.Bomber)
                            largeAttackWeight.weight *= 2f;
                    }
                    float rangeToTarget = Vector2.Distance(nearbyShip.ship.Center, Owner.Center);
                    if (rangeToTarget <= CombatAI.PreferredEngagementDistance) 
                        // && Vector2.Distance(nearbyShip.ship.Center, this.Owner.Center) >= this.Owner.maxWeaponsRange)
                    {
                        ShipWeight shipWeight = nearbyShip;
                        shipWeight.weight = (int)Math.Ceiling(shipWeight.weight + 5 *
                                                              ((CombatAI.PreferredEngagementDistance -Vector2.Distance(Owner.Center,nearbyShip.ship.Center))
                                                               / CombatAI.PreferredEngagementDistance  ))
                            
                            ;
                    }
                    else if (rangeToTarget > CombatAI.PreferredEngagementDistance + Owner.velocityMaximum * 5)
                    {
                        ShipWeight shipWeight1 = nearbyShip;
                        shipWeight1.weight = shipWeight1.weight - 2.5f * (rangeToTarget / (CombatAI.PreferredEngagementDistance + Owner.velocityMaximum * 5));
                    }
                    if(Owner.Mothership !=null)
                    {
                        rangeToTarget = Vector2.Distance(nearbyShip.ship.Center, Owner.Mothership.Center);
                        if (rangeToTarget < CombatAI.PreferredEngagementDistance)
                            nearbyShip.weight += 1;

                    }
                    if (EscortTarget != null)
                    {
                        rangeToTarget = Vector2.Distance(nearbyShip.ship.Center, EscortTarget.Center);
                        if( rangeToTarget <5000) // / (this.CombatAI.PreferredEngagementDistance +this.Owner.velocityMaximum ))
                            nearbyShip.weight += 1;
                        else
                            nearbyShip.weight -= 2;
                        if (nearbyShip.ship.GetAI().Target == EscortTarget)
                            nearbyShip.weight += 1;

                    }
                    if(nearbyShip.ship.Weapons.Count <1)
                        nearbyShip.weight -= 3;

                    foreach (ShipWeight otherShip in NearbyShips)
                        if (otherShip.ship.loyalty != Owner.loyalty)
                        {
                            if (otherShip.ship.GetAI().Target != Owner)
                                continue;
                            ShipWeight selfDefenseWeight = nearbyShip;
                            selfDefenseWeight.weight = selfDefenseWeight.weight + 0.2f * CombatAI.SelfDefenseWeight;
                        }
                        else if (otherShip.ship.GetAI().Target != nearbyShip.ship)
                        {
                            continue;
                        }
                }
                else
                {
                    NearbyShips.QueuePendingRemoval(nearbyShip);
                }
            //this.PotentialTargets = this.NearbyShips.Where(loyalty=> loyalty.ship.loyalty != this.Owner.loyalty) .OrderBy(weight => weight.weight).Select(ship => ship.ship).ToList();
            //if (this.Owner.Role == ShipData.RoleName.platform)
            //{
            //    this.NearbyShips.ApplyPendingRemovals();
            //    IEnumerable<ArtificialIntelligence.ShipWeight> sortedList =
            //        from potentialTarget in this.NearbyShips
            //        orderby Vector2.Distance(this.Owner.Center, potentialTarget.ship.Center)
            //        select potentialTarget;
            //    if (sortedList.Count<ArtificialIntelligence.ShipWeight>() > 0)
            //    {
            //        this.Target = sortedList.ElementAt<ArtificialIntelligence.ShipWeight>(0).ship;
            //    }
            //    return this.Target;
            //}
            NearbyShips.ApplyPendingRemovals();
            IEnumerable<ShipWeight> sortedList2 =
                from potentialTarget in NearbyShips
                orderby potentialTarget.weight descending //, Vector2.Distance(potentialTarget.ship.Center,this.Owner.Center) 
                select potentialTarget;
            
            {
                //this.PotentialTargets.ClearAdd() ;//.ToList() as BatchRemovalCollection<Ship>;

                //trackprojectiles in scan for targets.

                PotentialTargets.ClearAdd(sortedList2.Select(ship => ship.ship));
                   // .Where(potentialTarget => Vector2.Distance(potentialTarget.Center, this.Owner.Center) < this.CombatAI.PreferredEngagementDistance));
                    
            }
            if (Target != null && !Target.Active)
            {
                Target = null;
                hasPriorityTarget = false;
            }
            else if (Target != null && Target.Active && hasPriorityTarget)
            {
                var ship = Target as Ship;
                if (Owner.loyalty.GetRelations(ship.loyalty).AtWar || Owner.loyalty.isFaction || ship.loyalty.isFaction)
                    BadGuysNear = true;
                return Target;
            }
            if (sortedList2.Count<ShipWeight>() > 0)
                Target = sortedList2.ElementAt<ShipWeight>(0).ship;

            if (Owner.Weapons.Count > 0 || Owner.GetHangars().Count > 0)
                return Target;          
            return null;
        }
        //Targeting SetCombatStatus
        private void SetCombatStatus(float elapsedTime)
        {
            //if(this.State==AIState.Scrap)
            //{
            //    this.Target = null;
            //    this.Owner.InCombatTimer = 0f;
            //    this.Owner.InCombat = false;
            //    this.TargetQueue.Clear();
            //    return;
                
            //}
            var radius = 30000f;
            Vector2 senseCenter = Owner.Center;
            if (UseSensorsForTargets)
                if (Owner.Mothership != null)
                {
                    if (Vector2.Distance(Owner.Center, Owner.Mothership.Center) <= Owner.Mothership.SensorRange - Owner.SensorRange)
                    {
                        senseCenter = Owner.Mothership.Center;
                        radius = Owner.Mothership.SensorRange;
                    }
                }
                else
                {
                    radius = Owner.SensorRange;
                    if (Owner.inborders) radius += 10000;
                }
            else if (Owner.Mothership != null )
                senseCenter = Owner.Mothership.Center;


            if (Owner.fleet != null)
            {
                if (!hasPriorityTarget)
                    Target = ScanForCombatTargets(senseCenter, radius);
                else
                    ScanForCombatTargets(senseCenter, radius);
            }
            else if (!hasPriorityTarget)
            {
                //#if DEBUG
                //                if (this.State == AIState.Intercept && this.Target != null)
                //                    Log.Info(this.Target); 
                //#endif
                if (Owner.Mothership != null)
                {
                    Target = ScanForCombatTargets(senseCenter, radius);

                    if (Target == null)
                        Target = Owner.Mothership.GetAI().Target;
                }
                else
                {
                    Target = ScanForCombatTargets(senseCenter, radius);
                }
            }
            else
            {

                if (Owner.Mothership != null)
                    Target = ScanForCombatTargets(senseCenter, radius) ?? Owner.Mothership.GetAI().Target;
                else
                    ScanForCombatTargets(senseCenter, radius);
            }
            if (State == AIState.Resupply)
                return;
            if ((Owner.shipData.Role == ShipData.RoleName.freighter || Owner.shipData.ShipCategory == ShipData.Category.Civilian) && Owner.CargoSpace_Max > 0 || Owner.shipData.Role == ShipData.RoleName.scout || Owner.isConstructor || Owner.shipData.Role == ShipData.RoleName.troop || IgnoreCombat || State == AIState.Resupply || State == AIState.ReturnToHangar || State == AIState.Colonize || Owner.shipData.Role == ShipData.RoleName.supply)
                return;
            if (Owner.fleet != null && State == AIState.FormationWarp)
            {
                bool doreturn = !(Owner.fleet != null && State == AIState.FormationWarp && Vector2.Distance(Owner.Center, Owner.fleet.Position + Owner.FleetOffset) < 15000f);
                if (doreturn)
                    return;
            }
            if (Owner.fleet != null)
                foreach (FleetDataNode datanode in Owner.fleet.DataNodes)
                {
                    if (datanode.Ship!= Owner)
                        continue;
                    node = datanode;
                    break;
                }
            if (Target != null && !Owner.InCombat)
            {
                Owner.InCombatTimer = 15f;
                if (!HasPriorityOrder && OrderQueue.Count > 0 && OrderQueue.ElementAt<ShipGoal>(0).Plan != Plan.DoCombat)
                {
                    var combat = new ShipGoal(Plan.DoCombat, Vector2.Zero, 0f);
                    State = AIState.Combat;
                    OrderQueue.AddFirst(combat);
                    return;
                }
                else if (!HasPriorityOrder)
                {
                    var combat = new ShipGoal(Plan.DoCombat, Vector2.Zero, 0f);
                    State = AIState.Combat;
                    OrderQueue.AddFirst(combat);
                    return;
                }
                else 
                {
                    if (!HasPriorityOrder || CombatState == CombatState.HoldPosition || OrderQueue.Count != 0)
                        return;
                    var combat = new ShipGoal(Plan.DoCombat, Vector2.Zero, 0f);
                    State = AIState.Combat;
                    OrderQueue.AddFirst(combat);
                    
                }
            }
        }

		private void ScrapShip(float elapsedTime, ShipGoal goal)
		{
            if (Vector2.Distance(goal.TargetPlanet.Position, Owner.Center) >= goal.TargetPlanet.ObjectRadius + Owner.Radius)   //2500f)   //OrbitTarget.ObjectRadius *15)
			{
                //goal.MovePosition = goal.TargetPlanet.Position;
                //this.MoveToWithin1000(elapsedTime, goal);
                //goal.SpeedLimit = this.Owner.GetSTLSpeed();
                DoOrbit(goal.TargetPlanet, elapsedTime);
				return;
			}
			OrderQueue.Clear();
			Planet targetPlanet = goal.TargetPlanet;
			targetPlanet.ProductionHere = targetPlanet.ProductionHere + Owner.GetCost(Owner.loyalty) / 2f;
			Owner.QueueTotalRemoval();
            Owner.loyalty.GetGSAI().recyclepool++;
		}

		private void SetCombatStatusorig(float elapsedTime)
		{
			if (Owner.fleet != null)
			    if (!hasPriorityTarget)
			        Target = ScanForCombatTargets(Owner.Center, 30000f);
			    else
			        ScanForCombatTargets(Owner.Center, 30000f);
			else if (!hasPriorityTarget)
			    Target = ScanForCombatTargets(Owner.Center, 30000f);
			else
			    ScanForCombatTargets(Owner.Center, 30000f);
		    if (State == AIState.Resupply)
		        return;
		    if ((Owner.shipData.Role == ShipData.RoleName.freighter || Owner.shipData.ShipCategory == ShipData.Category.Civilian || Owner.shipData.Role == ShipData.RoleName.scout || Owner.isConstructor || Owner.shipData.Role == ShipData.RoleName.troop || IgnoreCombat || State == AIState.Resupply || State == AIState.ReturnToHangar) && !Owner.IsSupplyShip)
		        return;
		    if (Owner.fleet != null && State == AIState.FormationWarp)
			{
				var doreturn = true;
				if (Owner.fleet != null && State == AIState.FormationWarp && Vector2.Distance(Owner.Center, Owner.fleet.Position + Owner.FleetOffset) < 15000f)
				    doreturn = false;
			    if (doreturn)
			        return;
			}
			if (Owner.fleet != null)
			    foreach (FleetDataNode datanode in Owner.fleet.DataNodes)
			    {
			        if (datanode.Ship!= Owner)
			            continue;
			        node = datanode;
			        break;
			    }
		    if (Target != null && !Owner.InCombat)
			{
				Owner.InCombat = true;
				Owner.InCombatTimer = 15f;
				if (!HasPriorityOrder && OrderQueue.Count > 0 && OrderQueue.ElementAt<ShipGoal>(0).Plan != Plan.DoCombat)
				{
					var combat = new ShipGoal(Plan.DoCombat, Vector2.Zero, 0f);
					State = AIState.Combat;
					OrderQueue.AddFirst(combat);
					return;
				}
				if (!HasPriorityOrder)
				{
					var combat = new ShipGoal(Plan.DoCombat, Vector2.Zero, 0f);
					State = AIState.Combat;
					OrderQueue.AddFirst(combat);
					return;
				}
				if (HasPriorityOrder && CombatState != CombatState.HoldPosition && OrderQueue.Count == 0)
				{
					var combat = new ShipGoal(Plan.DoCombat, Vector2.Zero, 0f);
					State = AIState.Combat;
					OrderQueue.AddFirst(combat);
					return;
				}
			}
			else if (Target == null)
			{
				Owner.InCombat = false;
			}
		}

		public void SetPriorityOrder()
		{
			OrderQueue.Clear();
			HasPriorityOrder = true;
			Intercepting = false;
			hasPriorityTarget = false;
		}

		private void Stop(float elapsedTime)
		{
			Owner.HyperspaceReturn();
			if (Owner.Velocity == Vector2.Zero || Owner.Velocity.Length() > Owner.VelocityLast.Length())
			{
				Owner.Velocity = Vector2.Zero;
				return;
			}
			var forward = new Vector2((float)Math.Sin((double)Owner.Rotation), -(float)Math.Cos((double)Owner.Rotation));
			if (Owner.Velocity.Length() / Owner.velocityMaximum <= elapsedTime || (forward.X <= 0f || Owner.Velocity.X <= 0f) && (forward.X >= 0f || Owner.Velocity.X >= 0f))
			{
				Owner.Velocity = Vector2.Zero;
				return;
			}
			Ship owner = Owner;
			owner.Velocity = owner.Velocity + Vector2.Normalize(-forward) * (elapsedTime * Owner.velocityMaximum);
		}

		private void Stop(float elapsedTime, ShipGoal Goal)
		{
			Owner.HyperspaceReturn();
			if (Owner.Velocity == Vector2.Zero || Owner.Velocity.Length() > Owner.VelocityLast.Length())
			{
				Owner.Velocity = Vector2.Zero;
				OrderQueue.RemoveFirst();
				return;
			}
			var forward = new Vector2((float)Math.Sin((double)Owner.Rotation), -(float)Math.Cos((double)Owner.Rotation));
			if (Owner.Velocity.Length() / Owner.velocityMaximum <= elapsedTime || (forward.X <= 0f || Owner.Velocity.X <= 0f) && (forward.X >= 0f || Owner.Velocity.X >= 0f))
			{
				Owner.Velocity = Vector2.Zero;
				return;
			}
			Ship owner = Owner;
			owner.Velocity = owner.Velocity + Vector2.Normalize(-forward) * (elapsedTime * Owner.velocityMaximum);
		}
        //movement StopWithBackwardsThrust
        private void StopWithBackwardsThrust(float elapsedTime, ShipGoal Goal)
		{
			if(Goal.TargetPlanet !=null)
			    lock (WayPointLocker)
			    {
			        ActiveWayPoints.Last().Equals(Goal.TargetPlanet.Position);
			        Goal.MovePosition = Goal.TargetPlanet.Position;
			    }
		    if (Owner.loyalty == EmpireManager.Player)
		        HadPO = true;
		    HasPriorityOrder = false;
			float Distance = Vector2.Distance(Owner.Center, Goal.MovePosition);
			//if (Distance < 100f && Distance < 25f)
            if (Distance < 200f)  //fbedard
			{
				OrderQueue.RemoveFirst();
				lock (WayPointLocker)
				{
					ActiveWayPoints.Clear();
				}
				Owner.Velocity = Vector2.Zero;
				if (Owner.loyalty == EmpireManager.Player)
				    HadPO = true;
			    HasPriorityOrder = false;
			}
			Owner.HyperspaceReturn();
            //Vector2 forward2 = Quaternion
            //Quaternion.AngleAxis(_angle, Vector3.forward) * normalizedDirection1
			var forward = new Vector2((float)Math.Sin((double)Owner.Rotation), -(float)Math.Cos((double)Owner.Rotation));
			if (Owner.Velocity == Vector2.Zero || Vector2.Distance(Owner.Center + Owner.Velocity * elapsedTime, Goal.MovePosition) > Vector2.Distance(Owner.Center, Goal.MovePosition))
			{
				Owner.Velocity = Vector2.Zero;
				OrderQueue.RemoveFirst();
				if (ActiveWayPoints.Count > 0)
				    lock (WayPointLocker)
				    {
				        ActiveWayPoints.Dequeue();
				    }
			    return;
			}
			Vector2 velocity = Owner.Velocity;
			float timetostop = velocity.Length() / Goal.SpeedLimit;
            //added by gremlin devekmod timetostopfix
            if (Vector2.Distance(Owner.Center, Goal.MovePosition) / Goal.SpeedLimit <= timetostop + .005) 
            //if (Vector2.Distance(this.Owner.Center, Goal.MovePosition) / (this.Owner.Velocity.Length() + 0.001f) <= timetostop)
			{
				Ship owner = Owner;
				owner.Velocity = owner.Velocity + Vector2.Normalize(forward) * (elapsedTime * Goal.SpeedLimit);
				if (Owner.Velocity.Length() > Goal.SpeedLimit)
				    Owner.Velocity = Vector2.Normalize(Owner.Velocity) * Goal.SpeedLimit;
			}
			else
			{
				Ship ship = Owner;
				ship.Velocity = ship.Velocity + Vector2.Normalize(forward) * (elapsedTime * Goal.SpeedLimit);
				if (Owner.Velocity.Length() > Goal.SpeedLimit)
				{
					Owner.Velocity = Vector2.Normalize(Owner.Velocity) * Goal.SpeedLimit;
					return;
				}
			}
		}
        private void StopWithBackwardsThrustbroke(float elapsedTime, ShipGoal Goal)
        {
            
            if (Owner.loyalty == EmpireManager.Player)
                HadPO = true;
            HasPriorityOrder = false;
            float Distance = Vector2.Distance(Owner.Center, Goal.MovePosition);
            if (Distance < 200 )//&& Distance > 25f)
            {
                OrderQueue.RemoveFirst();
                lock (WayPointLocker)
                {
                    ActiveWayPoints.Clear();
                }
                Owner.Velocity = Vector2.Zero;
                if (Owner.loyalty == EmpireManager.Player)
                    HadPO = true;
                HasPriorityOrder = false;
            }
            Owner.HyperspaceReturn();
            var forward = new Vector2((float)Math.Sin((double)Owner.Rotation), -(float)Math.Cos((double)Owner.Rotation));
            if (Owner.Velocity == Vector2.Zero || Vector2.Distance(Owner.Center + Owner.Velocity * elapsedTime, Goal.MovePosition) > Vector2.Distance(Owner.Center, Goal.MovePosition))
            {
                Owner.Velocity = Vector2.Zero;
                OrderQueue.RemoveFirst();
                if (ActiveWayPoints.Count > 0)
                    lock (WayPointLocker)
                    {
                        ActiveWayPoints.Dequeue();
                    }
                return;
            }
            Vector2 velocity = Owner.Velocity;
            float timetostop = (int)velocity.Length() / Goal.SpeedLimit;
            if (Vector2.Distance(Owner.Center, Goal.MovePosition) / Goal.SpeedLimit <= timetostop + .005) //(this.Owner.Velocity.Length() + 1)
                if (Math.Abs((int)(DistanceLast - Distance)) < 10)
                {

                    var to1k = new ShipGoal(Plan.MakeFinalApproach, Goal.MovePosition, 0f)
                                    {
                                        SpeedLimit = Owner.speed > Distance ? Distance : Owner.GetSTLSpeed()
                                    };
                    lock (WayPointLocker)
                    {
                        OrderQueue.AddFirst(to1k);
                    }
                    DistanceLast = Distance;
                    return;
                }
            if (Vector2.Distance(Owner.Center, Goal.MovePosition) / (Owner.Velocity.Length() + 0.001f) <= timetostop)
            {
                Ship owner = Owner;
                owner.Velocity = owner.Velocity + Vector2.Normalize(-forward) * (elapsedTime * Goal.SpeedLimit);
                if (Owner.Velocity.Length() > Goal.SpeedLimit)
                    Owner.Velocity = Vector2.Normalize(Owner.Velocity) * Goal.SpeedLimit;
            }
            else
            {
                Ship ship = Owner;
                ship.Velocity = ship.Velocity + Vector2.Normalize(forward) * (elapsedTime * Goal.SpeedLimit);
                if (Owner.Velocity.Length() > Goal.SpeedLimit)
                {
                    Owner.Velocity = Vector2.Normalize(Owner.Velocity) * Goal.SpeedLimit;
                    return;
                }
            }

            DistanceLast = Distance;
        }
        // bookmark : Main Movement Code
                
        private void ThrustTowardsPosition(Vector2 Position, float elapsedTime, float speedLimit)        //Gretman's Version
        {
            if (speedLimit == 0f) speedLimit = Owner.speed;
            float Distance = Vector2.Distance(Position, Owner.Center);
            if (Owner.engineState != Ship.MoveState.Warp) Position = Position - Owner.Velocity;
            if (Owner.EnginesKnockedOut) return;

            Owner.isThrusting = true;
            Vector2 wantedForward = Owner.Center.FindVectorToTarget(Position);
            var forward = new Vector2((float)Math.Sin((double)Owner.Rotation), -(float)Math.Cos((double)Owner.Rotation));
            var right = new Vector2(-forward.Y, forward.X);
            var angleDiff = (float)Math.Acos((double)Vector2.Dot(wantedForward, forward));
            float facing = Vector2.Dot(wantedForward, right) > 0f ? 1f : -1f;

            float TurnRate = Owner.TurnThrust / Owner.Mass / 700f;

            #region Warp

            if (angleDiff * 1.25f > TurnRate && Distance > 2500f && Owner.engineState == Ship.MoveState.Warp)      //Might be a turning issue
            {
                if (angleDiff > 1.0f)
                {
                    Owner.HyperspaceReturn();      //Too sharp of a turn. Drop out of warp
                }
                else {
                    float WarpSpeed = (Owner.WarpThrust / Owner.Mass + 0.1f) * Owner.loyalty.data.FTLModifier;
                    if (Owner.inborders && Owner.loyalty.data.Traits.InBordersSpeedBonus > 0) WarpSpeed *= 1 + Owner.loyalty.data.Traits.InBordersSpeedBonus;

                    if (Owner.VanityName == "MerCraft") Log.Info("AngleDiff: " + angleDiff + "     TurnRate = " + TurnRate + "     WarpSpeed = " + WarpSpeed + "     Distance = " + Distance);
                    //AngleDiff: 1.500662     TurnRate = 0.2491764     WarpSpeed = 26286.67     Distance = 138328.4

                    if (ActiveWayPoints.Count >= 2 && Distance > Empire.ProjectorRadius / 2 && Vector2.Distance(Owner.Center, ActiveWayPoints.ElementAt(1)) < Empire.ProjectorRadius * 5)
                    {
                        Vector2 wantedForwardNext = Owner.Center.FindVectorToTarget(ActiveWayPoints.ElementAt(1));
                        var angleDiffNext = (float)Math.Acos((double)Vector2.Dot(wantedForwardNext, forward));
                        if (angleDiff > angleDiffNext || angleDiffNext < TurnRate * 0.5) //Angle to next waypoint is better than angle to this one, just cut the corner.
                        {
                            lock (WayPointLocker)
                            {
                                ActiveWayPoints.Dequeue();
                            }
                            if (OrderQueue.Count > 0)      OrderQueue.RemoveFirst();
                            return;
                        }
                    }
                    //                          Turn per tick         ticks left          Speed per tic
                    else if (angleDiff > TurnRate / elapsedTime * (Distance / (WarpSpeed / elapsedTime) ) )       //Can we make the turn in the distance we have remaining?
                    {
                        Owner.WarpThrust -= Owner.NormalWarpThrust * 0.02f;   //Reduce warpthrust by 2 percent every frame until this is an acheivable turn
                    }
                    else if (Owner.WarpThrust < Owner.NormalWarpThrust)
                    {
                        Owner.WarpThrust += Owner.NormalWarpThrust * 0.01f;   //Increase warpthrust back to normal 1 percent at a time
                        if (Owner.WarpThrust > Owner.NormalWarpThrust) Owner.WarpThrust = Owner.NormalWarpThrust;    //Make sure we dont accidentally go over
                    }
                }
            }
            else if (Owner.WarpThrust < Owner.NormalWarpThrust && angleDiff < TurnRate) //Intentional allowance of the 25% added to angle diff in main if, so it wont accelerate too soon
            {
                Owner.WarpThrust += Owner.NormalWarpThrust * 0.01f;   //Increase warpthrust back to normal 1 percent at a time
                if (Owner.WarpThrust > Owner.NormalWarpThrust) Owner.WarpThrust = Owner.NormalWarpThrust;    //Make sure we dont accidentally go over
            }

            #endregion

            if (hasPriorityTarget && Distance < Owner.maxWeaponsRange * 0.85f)        //If chasing something, and within weapons range
            {
                if (Owner.engineState == Ship.MoveState.Warp) Owner.HyperspaceReturn();
            }
            else if (!HasPriorityOrder && !hasPriorityTarget && Distance < 1000f && ActiveWayPoints.Count <= 1 && Owner.engineState == Ship.MoveState.Warp)
            {
                Owner.HyperspaceReturn();
            }

            if (angleDiff > 0.025f)     //Stuff for the ship visually banking on the Y axis when turning
            {
                float RotAmount = Math.Min(angleDiff, facing * elapsedTime * Owner.rotationRadiansPerSecond);
                if (RotAmount > 0f && Owner.yRotation > -Owner.maxBank) Owner.yRotation = Owner.yRotation - Owner.yBankAmount;
                else if (RotAmount < 0f && Owner.yRotation < Owner.maxBank) Owner.yRotation = Owner.yRotation + Owner.yBankAmount;
                Owner.isTurning = true;
                Owner.Rotation = Owner.Rotation + (RotAmount > angleDiff ? angleDiff : RotAmount);
                return;       //I'm not sure about the return statement here. -Gretman
            }

            if (State != AIState.FormationWarp || Owner.fleet == null)        //not in a fleet
            {
                if (Distance > 7500f && !Owner.InCombat && angleDiff < 0.25f) Owner.EngageStarDrive();
                else if (Distance > 15000f && Owner.InCombat && angleDiff < 0.25f) Owner.EngageStarDrive();
            }
            else        //In a fleet
            {
                if (Distance > 7500f)   //Not near destination
                {
                    var fleetReady = true;
                    
                    using (Owner.fleet.Ships.AcquireReadLock())
                    {
                        foreach (Ship ship in Owner.fleet.Ships)
                        {
                            if (ship.GetAI().State != AIState.FormationWarp) continue;
                            if (ship.GetAI().ReadyToWarp && (ship.PowerCurrent / (ship.PowerStoreMax + 0.01f) >= 0.2f || ship.isSpooling))
                            {
                                if (Owner.FightersOut) Owner.RecoverFighters();       //Recall Fighters
                                continue;
                            }
                            fleetReady = false;
                            break;
                        }
                    }

                    float distanceFleetCenterToDistance = Owner.fleet.StoredFleetDistancetoMove;
                    speedLimit = Owner.fleet.speed;

                #region FleetGrouping
                #if true
                    if (Distance <= distanceFleetCenterToDistance)
                    {
                        float speedreduction = distanceFleetCenterToDistance - Distance;
                        speedLimit = Owner.fleet.speed - speedreduction;

                        if (speedLimit > Owner.fleet.speed) speedLimit = Owner.fleet.speed;
                    }
                    else if (Distance > distanceFleetCenterToDistance && Distance > Owner.speed)
                    {
                        float speedIncrease = Distance - distanceFleetCenterToDistance;
                        speedLimit = Owner.fleet.speed + speedIncrease;
                    }
                #endif
                #endregion

                    if (fleetReady) Owner.EngageStarDrive();   //Fleet is ready to Go into warp
                    else if (Owner.engineState == Ship.MoveState.Warp) Owner.HyperspaceReturn(); //Fleet is not ready for warp
                }
                else if (Owner.engineState == Ship.MoveState.Warp)
                {
                    Owner.HyperspaceReturn(); //Near Destination
                }
            }

            if (speedLimit > Owner.velocityMaximum) speedLimit = Owner.velocityMaximum;
            else if (speedLimit < 0) speedLimit = 0;

            Owner.Velocity = Owner.Velocity + Vector2.Normalize(forward) * (elapsedTime * speedLimit);
            if (Owner.Velocity.Length() > speedLimit) Owner.Velocity = Vector2.Normalize(Owner.Velocity) * speedLimit;
        }

        private void ThrustTowardsPositionOld(Vector2 Position, float elapsedTime, float speedLimit)
        {
            if (speedLimit == 0f)
                speedLimit = Owner.speed;
            float Ownerspeed = Owner.speed;
            if (Ownerspeed > speedLimit)
                Ownerspeed = speedLimit;
            float Distance = Position.Distance(Owner.Center);
 
            if (Owner.engineState != Ship.MoveState.Warp )
                Position = Position - Owner.Velocity;
            if (!Owner.EnginesKnockedOut)
            {
                Owner.isThrusting = true;

                Vector2 wantedForward = Vector2.Normalize(Owner.Center.FindVectorToTarget(Position));
                var forward = new Vector2((float)Math.Sin((double)Owner.Rotation), -(float)Math.Cos((double)Owner.Rotation));
                var right = new Vector2(-forward.Y, forward.X);
                double angleDiff = Math.Acos(Vector2.Dot(wantedForward, forward));
                double facing = Vector2.Dot(wantedForward, right)> 0f ? 1f : -1f;
#region warp
                if (angleDiff > 0.25f && Owner.engineState == Ship.MoveState.Warp)
                {
                    if (Owner.VanityName == "MerCraftA") Log.Info("angleDiff: " + angleDiff);
                    if (ActiveWayPoints.Count > 1)
                    {
                        if (angleDiff > 1.0f)
                        {
                            Owner.HyperspaceReturn();
                            if (Owner.VanityName == "MerCraft") Log.Info("Dropped out of warp:  Master Angle too large for warp." 
                                + "   angleDiff: " + angleDiff);
                        }
                        if (Distance <= Empire.ProjectorRadius / 2f)
                            if (angleDiff > 0.25f) //Gretman tinkering with fbedard's 2nd attempt to smooth movement around waypoints
                            {
                                if (Owner.VanityName == "MerCraft") Log.Info("Pre Dequeue Queue size:  " + ActiveWayPoints.Count);
                                lock (WayPointLocker)
                                {
                                    ActiveWayPoints.Dequeue();
                                }
                                if (Owner.VanityName == "MerCraft") Log.Info("Post Dequeue Pre Remove 1st Queue size:  " + ActiveWayPoints.Count);
                                if (OrderQueue.Count > 0)
                                    OrderQueue.RemoveFirst();
                                if (Owner.VanityName == "MerCraft") Log.Info("Post Remove 1st Queue size:  " + ActiveWayPoints.Count);
                                Position = ActiveWayPoints.First();
                                Distance = Vector2.Distance(Position, Owner.Center);
                                wantedForward = Owner.Center.FindVectorToTarget(Position);
                                forward = new Vector2((float)Math.Sin((double)Owner.Rotation), -(float)Math.Cos((double)Owner.Rotation));
                                angleDiff = Math.Acos((double)Vector2.Dot(wantedForward, forward));

                                speedLimit = speedLimit * 0.75f;
                                if (Owner.VanityName == "MerCraft") Log.Info("Rounded Corner:  Slowed down.   angleDiff: {0}", angleDiff);
                            }
                            else
                            {
                                if (Owner.VanityName == "MerCraft") Log.Info("Pre Dequeue Queue size:  " + ActiveWayPoints.Count);
                                lock (WayPointLocker)
                                {
                                    ActiveWayPoints.Dequeue();
                                }
                                if (Owner.VanityName == "MerCraft") Log.Info("Post Dequeue Pre Remove 1st Queue size:  " + ActiveWayPoints.Count);
                                if (OrderQueue.Count > 0)
                                    OrderQueue.RemoveFirst();
                                if (Owner.VanityName == "MerCraft") Log.Info("Post Remove 1st Queue size:  " + ActiveWayPoints.Count);
                                Position = ActiveWayPoints.First();
                                Distance = Vector2.Distance(Position, Owner.Center);
                                wantedForward = Owner.Center.FindVectorToTarget(Position);
                                forward = new Vector2((float)Math.Sin(Owner.Rotation), -(float)Math.Cos(Owner.Rotation));
                                angleDiff = Math.Acos(Vector2.Dot(wantedForward, forward));
                                if (Owner.VanityName == "MerCraft") Log.Info("Rounded Corner:  Did not slow down." + "   angleDiff: " + angleDiff);
                            }
                    }
                    else if (Target != null)
                    {
                        float d = Vector2.Distance(Target.Center, Owner.Center);
                        if (angleDiff > 0.400000005960464f)
                            Owner.HyperspaceReturn();
                        else if (d > 25000f)
                            Owner.HyperspaceReturn();
                    }
                    else if (State != AIState.Bombard && State != AIState.AssaultPlanet && State != AIState.BombardTroops && !IgnoreCombat || OrderQueue.Count <= 0)
                    {
                        Owner.HyperspaceReturn();
                    }
                    else if (OrderQueue.Last().TargetPlanet != null)
                    {
                        float d = OrderQueue.Last().TargetPlanet.Position.Distance(Owner.Center);
                        wantedForward = Owner.Center.FindVectorToTarget(OrderQueue.Last().TargetPlanet.Position);
                        angleDiff = (float)Math.Acos((double)Vector2.Dot(wantedForward, forward));                        
                        if (angleDiff > 0.400000005960464f)
                            Owner.HyperspaceReturn();
                        else if (d > 25000f)
                            Owner.HyperspaceReturn();
                    }
                    else if (angleDiff > .25)
                    {
                        Owner.HyperspaceReturn();
                    }
                }
#endregion

                if (hasPriorityTarget && Distance < Owner.maxWeaponsRange)
                {
                    if (Owner.engineState == Ship.MoveState.Warp)
                        Owner.HyperspaceReturn();
                }
                else if (!HasPriorityOrder && !hasPriorityTarget && Distance < 1000f && ActiveWayPoints.Count <= 1 && Owner.engineState == Ship.MoveState.Warp)
                {
                    Owner.HyperspaceReturn();
                }
                float TurnSpeed = 1;
                if (angleDiff > Owner.yBankAmount*.1) 
                {
                    double RotAmount = Math.Min(angleDiff, facing *  Owner.yBankAmount); 
                    if (RotAmount > 0f)
                    {                        
                        if (Owner.yRotation > -Owner.maxBank)
                        {                            
                            Ship owner = Owner;
                            owner.yRotation = owner.yRotation - Owner.yBankAmount;
                        }
                    }
                    else if (RotAmount < 0f && Owner.yRotation < Owner.maxBank)
                    {                        
                        Ship owner1 = Owner;
                        owner1.yRotation = owner1.yRotation + Owner.yBankAmount;                        
                    }                
                    Owner.isTurning = true;
                    Ship rotation = Owner;
                    rotation.Rotation = rotation.Rotation + (RotAmount > angleDiff ? (float)angleDiff: (float)RotAmount);
                    {
                        float nimble = Owner.rotationRadiansPerSecond;
                        if (angleDiff < nimble)
                            TurnSpeed = (float)((nimble * 1.5 - angleDiff) / (nimble * 1.5));

                    }                   
                }
                if (State != AIState.FormationWarp || Owner.fleet == null)
                {
                    if (Distance > 7500f && !Owner.InCombat && angleDiff < 0.25f)
                        Owner.EngageStarDrive();
                    else if (Distance > 15000f && Owner.InCombat && angleDiff < 0.25f)
                        Owner.EngageStarDrive();
                    if (Owner.engineState == Ship.MoveState.Warp)
                        if (angleDiff > .1f)
                            speedLimit = Ownerspeed; 
                        else
                            speedLimit = (int)Owner.velocityMaximum;
                    else if (Distance > Ownerspeed * 10f)
                        speedLimit = Ownerspeed;
                    speedLimit *= TurnSpeed;
                    Ship velocity = Owner;
                    velocity.Velocity = velocity.Velocity +   Vector2.Normalize(forward) * (elapsedTime * speedLimit);
                    if (Owner.Velocity.Length() > speedLimit)
                        Owner.Velocity = Vector2.Normalize(Owner.Velocity) * speedLimit; 
                }
                else
                {
                    if (Distance > 7500f)                    
                    {
                        var fleetReady = true;
                        using (Owner.fleet.Ships.AcquireReadLock())
                        {
                            foreach (Ship ship in Owner.fleet.Ships)
                            {
                                if(ship.GetAI().State != AIState.FormationWarp)
                                    continue;
                                if (ship.GetAI().ReadyToWarp
                                
                                    && (ship.PowerCurrent / (ship.PowerStoreMax + 0.01f) >= 0.2f || ship.isSpooling ) 
                                )
                                {
                                    if (Owner.FightersOut)
                                        Owner.RecoverFighters();                                
                                    continue;
                                }
                                fleetReady = false;
                                break;
                            }
                        }

                        float distanceFleetCenterToDistance = Owner.fleet.StoredFleetDistancetoMove; //
                            speedLimit = Owner.fleet.speed;
#region FleetGrouping
                            float fleetPosistionDistance = Distance;
                            if (fleetPosistionDistance <= distanceFleetCenterToDistance )
                            {
                                float speedreduction = distanceFleetCenterToDistance - Distance;
                                speedLimit = (int)( Owner.fleet.speed - speedreduction);
                                if (speedLimit < 0)
                                    speedLimit = 0;
                                else if (speedLimit > Owner.fleet.speed)
                                    speedLimit = (int)Owner.fleet.speed;
                            }
                            else if (fleetPosistionDistance > distanceFleetCenterToDistance && Distance > Ownerspeed)
                            {
                                float speedIncrease = Distance - distanceFleetCenterToDistance ;                             
                                speedLimit = (int)(Owner.fleet.speed + speedIncrease);
  
                            }
#endregion
                            if (fleetReady)
                                Owner.EngageStarDrive();
                            else if (Owner.engineState == Ship.MoveState.Warp)
                                Owner.HyperspaceReturn();
                    }
                    else if (Owner.engineState == Ship.MoveState.Warp)
                    {
                        Owner.HyperspaceReturn();
                    }

                    if (speedLimit > Owner.velocityMaximum)
                        speedLimit = Owner.velocityMaximum;
                    else if (speedLimit < 0)
                        speedLimit = 0;
                    Ship velocity1 = Owner;
                    velocity1.Velocity = velocity1.Velocity + Vector2.Normalize(forward) * (elapsedTime * speedLimit);
                    if (Owner.Velocity.Length() > speedLimit)
                    {
                        Owner.Velocity = Vector2.Normalize(Owner.Velocity) * speedLimit;
                        return;
                    }
                }
            }
        }



        //added by gremlin Devekmod AuUpdate(fixed)
	    public void Update(float elapsedTime)
	    {
	        if (BadGuysNear)
	            CombatAI.UpdateCombatAI(Owner);
	        ShipGoal toEvaluate;
	        if (State == AIState.AwaitingOrders && DefaultAIState == AIState.Exterminate)
	            State = AIState.Exterminate;
	        if (ClearOrdersNext)
	        {
	            OrderQueue.Clear();
	            ClearOrdersNext = false;
	            awaitClosest = null;
	            State = AIState.AwaitingOrders;
	        }
	        var ToRemove = new Array<Ship>();
	        foreach (Ship target in TargetQueue)
	        {
	            if (target.Active)
	                continue;
	            ToRemove.Add(target);
	        }
	        foreach (Ship ship in ToRemove)
	            TargetQueue.Remove(ship);
	        if (!hasPriorityTarget)
	            TargetQueue.Clear();
	        if (Owner.loyalty == universeScreen.player &&
	            (State == AIState.MoveTo && Vector2.Distance(Owner.Center, MovePosition) > 100f || State == AIState.Orbit ||
	             State == AIState.Bombard || State == AIState.AssaultPlanet || State == AIState.BombardTroops ||
	             State == AIState.Rebase || State == AIState.Scrap || State == AIState.Resupply || State == AIState.Refit ||
	             State == AIState.FormationWarp))
	        {
	            HasPriorityOrder = true;
	            HadPO = false;
	            EscortTarget = null;

	        }
	        if (HadPO && State != AIState.AwaitingOrders)
	            HadPO = false;
	        if (State == AIState.Resupply)
	        {
	            HasPriorityOrder = true;
	            if (Owner.Ordinance >= Owner.OrdinanceMax && Owner.Health >= Owner.HealthMax)
	                //fbedard: consider health also
	            {
	                HasPriorityOrder = false;
	                State = AIState.AwaitingOrders;
	            }
	        }
	      
	        if (State == AIState.Flee && !BadGuysNear && State != AIState.Resupply && !HasPriorityOrder)
	        {
	            if (OrderQueue.Count > 0)
	                OrderQueue.Remove(OrderQueue.Last);
	            if (FoodOrProd == "Pass")
	                State = AIState.PassengerTransport;
	            else if (FoodOrProd == "Food" || FoodOrProd == "Prod")
	                State = AIState.SystemTrader;
	            else
	                State = DefaultAIState;
	        }
	        ScanForThreatTimer -= elapsedTime;
	        if (ScanForThreatTimer < 0f)
	        {
	            SetCombatStatus(elapsedTime);
	            ScanForThreatTimer = 2f;
	            if (Owner.loyalty.data.Traits.Pack)
	            {
	                Owner.DamageModifier = -0.25f;
	                Ship owner = Owner;
	                owner.DamageModifier = owner.DamageModifier + 0.05f * (float) FriendliesNearby.Count;
	                if (Owner.DamageModifier > 0.5f)
	                    Owner.DamageModifier = 0.5f;
	            }
	        }
	        UtilityModuleCheckTimer -= elapsedTime;
	        if (Owner.engineState != Ship.MoveState.Warp && UtilityModuleCheckTimer <= 0f)
	        {
	            UtilityModuleCheckTimer = 1f;
	            //Added by McShooterz: logic for transporter modules
	            if (Owner.hasTransporter)
	                foreach (ShipModule module in Owner.Transporters)
	                    if (module.TransporterTimer <= 0f && module.Active && module.Powered &&
	                        module.TransporterPower < Owner.PowerCurrent)
	                    {
	                        if (FriendliesNearby.Count > 0 && module.TransporterOrdnance > 0 && Owner.Ordinance > 0)
	                            DoOrdinanceTransporterLogic(module);
	                        if (module.TransporterTroopAssault > 0 && Owner.TroopList.Any())
	                            DoAssaultTransporterLogic(module);
	                    }
	            //Do repair check if friendly ships around and no combat
	            if (!Owner.InCombat && FriendliesNearby.Count > 0)
	            {
	                //Added by McShooterz: logic for repair beams
	                if (Owner.hasRepairBeam)
	                    foreach (ShipModule module in Owner.RepairBeams)
	                        if (module.InstalledWeapon.timeToNextFire <= 0f &&
	                            module.InstalledWeapon.moduleAttachedTo.Powered &&
	                            Owner.Ordinance >= module.InstalledWeapon.OrdinanceRequiredToFire &&
	                            Owner.PowerCurrent >= module.InstalledWeapon.PowerRequiredToFire)
	                            DoRepairBeamLogic(module.InstalledWeapon);
	                if (Owner.HasRepairModule)
	                    foreach (Weapon weapon in Owner.Weapons)
	                    {
	                        if (weapon.timeToNextFire > 0f || !weapon.moduleAttachedTo.Powered ||
	                            Owner.Ordinance < weapon.OrdinanceRequiredToFire ||
	                            Owner.PowerCurrent < weapon.PowerRequiredToFire || !weapon.IsRepairDrone)
	                        {
	                            //Gretman -- Added this so repair drones would cooldown outside combat (+15s)
	                            if (weapon.timeToNextFire > 0f)
	                                weapon.timeToNextFire = MathHelper.Max(weapon.timeToNextFire - 1, 0f);
	                            continue;
	                        }
	                        DoRepairDroneLogic(weapon);
	                    }
	            }
	        }
	        if (State == AIState.ManualControl)
	            return;
	        ReadyToWarp = true;
	        Owner.isThrusting = false;
	        Owner.isTurning = false;

	        if (State == AIState.SystemTrader && start != null && end != null &&
	            (start.Owner != Owner.loyalty || end.Owner != Owner.loyalty))
	        {
	            start = null;
	            end = null;
	            OrderTrade(5f);
	            return;
	        }
	        if (State == AIState.PassengerTransport && start != null && end != null &&
	            (start.Owner != Owner.loyalty || end.Owner != Owner.loyalty))
	        {
	            start = null;
	            end = null;
	            OrderTransportPassengers(5f);
	            return;
	        }
           
            

	        if (OrderQueue.Count == 0)
	        {
	            if (Owner.fleet == null)
	            {
	                lock (WayPointLocker)
	                {
	                    ActiveWayPoints.Clear();
	                }
	                AIState state = State;
	                if (state <= AIState.MoveTo)
	                {
	                    if (state <= AIState.SystemTrader)
	                    {
	                        if (state == AIState.DoNothing)
	                            AwaitOrders(elapsedTime);
	                        else
	                            switch (state)
	                            {
	                                case AIState.AwaitingOrders:
	                                {
	                                    if (Owner.loyalty != universeScreen.player)
	                                        AwaitOrders(elapsedTime);
	                                    else
	                                        AwaitOrdersPlayer(elapsedTime);
	                                    if (Owner.loyalty.isFaction)
	                                        break;

	                                    if (Owner.OrdinanceMax < 1 || Owner.Ordinance / Owner.OrdinanceMax >= 0.2f)

	                                        break;
	                                    if (
	                                        FriendliesNearby.Where(
	                                            supply => supply.HasSupplyBays && supply.Ordinance >= 100).Count() > 0)
	                                        break;
	                                    var shipyards = new Array<Planet>();
	                                    for (var i = 0; i < Owner.loyalty.GetPlanets().Count; i++)
	                                    {
	                                        Planet item = Owner.loyalty.GetPlanets()[i];
	                                        if (item.HasShipyard)
	                                            shipyards.Add(item);
	                                    }
	                                    var sortedList =
	                                        from p in shipyards
	                                        orderby Vector2.Distance(Owner.Center, p.Position)
	                                        select p;
	                                    if (sortedList.Count<Planet>() <= 0)
	                                        break;
	                                    OrderResupply(sortedList.First<Planet>(), true);
	                                    break;
	                                }
	                                case AIState.Escort:
	                                {
	                                    if (EscortTarget == null || !EscortTarget.Active)
	                                    {
	                                        EscortTarget = null;
	                                        OrderQueue.Clear();
	                                        ClearOrdersNext = false;
	                                        if (Owner.Mothership != null && Owner.Mothership.Active)
	                                        {
	                                            OrderReturnToHangar();
	                                            break;
	                                        }
	                                        State = AIState.AwaitingOrders; //fbedard
	                                        break;
	                                    }
	                                    if (Owner.BaseStrength == 0 ||
	                                        Owner.Mothership == null &&
	                                        EscortTarget.Center.InRadius(Owner.Center, Owner.SensorRange) ||
	                                        Owner.Mothership == null || !Owner.Mothership.GetAI().BadGuysNear ||
	                                        EscortTarget != Owner.Mothership)
	                                    {
	                                        OrbitShip(EscortTarget, elapsedTime);
	                                        break;
	                                    }
	                                    // Doctor: This should make carrier-launched fighters scan for their own combat targets, except using the mothership's position
	                                    // and a standard 30k around it instead of their own. This hopefully will prevent them flying off too much, as well as keeping them
	                                    // in a carrier-based role while allowing them to pick appropriate target types depending on the fighter type.
	                                    //gremlin Moved to setcombat status as target scan is expensive and did some of this already. this also shortcuts the UseSensorforTargets switch. Im not sure abuot the using the mothership target. 
	                                    // i thought i had added that in somewhere but i cant remember where. I think i made it so that in the scan it takes the motherships target list and adds it to its own. 
	                                    else
	                                    {
	                                        DoCombat(elapsedTime);
	                                        break;
	                                    }
	                                }
	                                case AIState.SystemTrader:
	                                {
	                                    OrderTrade(elapsedTime);
	                                    if (start == null || end == null)
	                                        AwaitOrders(elapsedTime);
	                                    break;
	                                }
	                            }
	                    }
	                    else if (state == AIState.PassengerTransport)
	                    {
	                        OrderTransportPassengers(elapsedTime);
	                        if (start == null || end == null)
	                            AwaitOrders(elapsedTime);
	                    }
	                }
	                else if (state <= AIState.ReturnToHangar)
	                {
	                    switch (state)
	                    {
	                        case AIState.SystemDefender:
	                        {
	                            AwaitOrders(elapsedTime);
	                            break;
	                        }
	                        case AIState.AwaitingOffenseOrders:
	                        {
	                            break;
	                        }
	                        case AIState.Resupply:
	                        {
	                            AwaitOrders(elapsedTime);
	                            break;
	                        }
	                        default:
	                        {
	                            if (state == AIState.ReturnToHangar)
	                            {
	                                DoReturnToHangar(elapsedTime);
	                                break;
	                            }
	                            else
	                            {
	                                break;
	                            }
	                        }
	                    }
	                }
	                else if (state != AIState.Intercept)
	                {
	                    if (state == AIState.Exterminate)
	                        OrderFindExterminationTarget(true);
	                }
	                else if (Target != null)
	                {
	                    OrbitShip(Target as Ship, elapsedTime);
	                }
	            }
	            else
	            {
	                float DistanceToFleetOffset = Vector2.Distance(Owner.Center, Owner.fleet.Position + Owner.FleetOffset);
	                if (DistanceToFleetOffset <= 75f)
	                {
	                    Owner.Velocity = Vector2.Zero;
	                    Vector2 vector2 = MathExt.PointFromRadians(Vector2.Zero, Owner.fleet.facing, 1f);
	                    Vector2 fvec = Vector2.Zero.FindVectorToTarget(vector2);
	                    Vector2 wantedForward = Vector2.Normalize(fvec);
	                    var forward = new Vector2((float) Math.Sin((double) Owner.Rotation),
	                        -(float) Math.Cos((double) Owner.Rotation));
	                    var right = new Vector2(-forward.Y, forward.X);
	                    var angleDiff = (float) Math.Acos((double) Vector2.Dot(wantedForward, forward));
	                    float facing = Vector2.Dot(wantedForward, right) > 0f ? 1f : -1f;
	                    if (angleDiff > 0.02f)
	                        RotateToFacing(elapsedTime, angleDiff, facing);
	                    if (DistanceToFleetOffset <= 75f) //fbedard: dont override high priority resupply
	                    {
	                        State = AIState.AwaitingOrders;
	                        HasPriorityOrder = false;
	                    }
	                    //add fun idle fleet ship stuff here

	                }
	                else if (State != AIState.HoldPosition && DistanceToFleetOffset > 75f)
	                {
	                    ThrustTowardsPosition(Owner.fleet.Position + Owner.FleetOffset, elapsedTime, Owner.fleet.speed);
	                    lock (WayPointLocker)
	                    {
	                        ActiveWayPoints.Clear();
	                        ActiveWayPoints.Enqueue(Owner.fleet.Position + Owner.FleetOffset);
	                        if (State != AIState.AwaitingOrders) //fbedard: set new order for ship returning to fleet
	                            State = AIState.AwaitingOrders;
	                        if (Owner.fleet.GetStack().Count > 0)
	                            ActiveWayPoints.Enqueue(Owner.fleet.GetStack().Peek().MovePosition + Owner.FleetOffset);
	                    }
	                }


	            }
	        }
	        else if (OrderQueue.Count > 0)
	        {
	            toEvaluate = OrderQueue.First();
	            Planet target = toEvaluate.TargetPlanet;
	            switch (toEvaluate.Plan)
	            {
	                case Plan.HoldPosition:          HoldPosition(); break;                            
	                case Plan.Stop:	                 Stop(elapsedTime, toEvaluate); break;
	                case Plan.Scrap:
	                {
	                    ScrapShip(elapsedTime, toEvaluate);
	                    break;
	                }
	                case Plan.Bombard: //Modified by Gretman
	                    target = toEvaluate.TargetPlanet; //Stop Bombing if:
	                    if (Owner.Ordinance < 0.05 * Owner.OrdinanceMax //'Aint Got no bombs!
	                        || target.TroopsHere.Count == 0 && target.Population <= 0f //Everyone is dead
	                        || (target.GetGroundStrengthOther(Owner.loyalty) + 1) * 1.5
	                        <= target.GetGroundStrength(Owner.loyalty))
	                        //This will tilt the scale just enough so that if there are 0 troops, a planet can still be bombed.

	                    {
	                        //As far as I can tell, if there were 0 troops on the planet, then GetGroundStrengthOther and GetGroundStrength would both return 0,
	                        //meaning that the planet could not be bombed since that part of the if statement would always be true (0 * 1.5 <= 0)
	                        //Adding +1 to the result of GetGroundStrengthOther tilts the scale just enough so a planet with no troops at all can still be bombed
	                        //but having even 1 allied troop will cause the bombine action to abort.

	                        OrderQueue.Clear();
	                        State = AIState.AwaitingOrders;
	                        var orbit = new ShipGoal(Plan.Orbit, Vector2.Zero, 0f)
	                        {
	                            TargetPlanet = toEvaluate.TargetPlanet
	                        };

	                        OrderQueue.AddLast(orbit); //Stay in Orbit

	                        HasPriorityOrder = false;
	                        //Log.Info("Bombardment info! " + target.GetGroundStrengthOther(this.Owner.loyalty) + " : " + target.GetGroundStrength(this.Owner.loyalty));

	                    } //Done -Gretman

	                    DoOrbit(toEvaluate.TargetPlanet, elapsedTime);
	                    float radius = toEvaluate.TargetPlanet.ObjectRadius + Owner.Radius + 1500;
	                    if (toEvaluate.TargetPlanet.Owner == Owner.loyalty)
	                    {
	                        OrderQueue.Clear();
	                        return;
	                    }
	                    else if (Vector2.Distance(Owner.Center, toEvaluate.TargetPlanet.Position) < radius)
	                    {
	                        using (Array<ShipModule>.Enumerator enumerator = Owner.BombBays.GetEnumerator())
	                        {
	                            while (enumerator.MoveNext())
	                            {
	                                ShipModule current = enumerator.Current;
	                                if (current.BombTimer <= 0f)
	                                {
	                                    var bomb = new Bomb(new Vector3(Owner.Center, 0.0f), Owner.loyalty);
	                                    bomb.WeaponName = current.BombType;
	                                    if (Owner.Ordinance >
	                                        ResourceManager.WeaponsDict[current.BombType].OrdinanceRequiredToFire)
	                                    {
	                                        Owner.Ordinance -=
	                                            ResourceManager.WeaponsDict[current.BombType].OrdinanceRequiredToFire;
	                                        bomb.SetTarget(toEvaluate.TargetPlanet);
	                                        universeScreen.BombList.Add(bomb);
	                                        current.BombTimer = ResourceManager.WeaponsDict[current.BombType].fireDelay;
	                                    }
	                                }
	                            }
	                            break;
	                        }
	                    }
	                    else
	                    {
	                        break;
	                    }
	                case Plan.Exterminate:
	                {
	                    DoOrbit(toEvaluate.TargetPlanet, elapsedTime);
	                    radius = toEvaluate.TargetPlanet.ObjectRadius + Owner.Radius + 1500;
	                    if (toEvaluate.TargetPlanet.Owner == Owner.loyalty || toEvaluate.TargetPlanet.Owner == null)
	                    {
	                        OrderQueue.Clear();
	                        OrderFindExterminationTarget(true);
	                        return;
	                    }
	                    else
	                    {
	                        if (Vector2.Distance(Owner.Center, toEvaluate.TargetPlanet.Position) >= radius)
	                            break;
	                        Array<ShipModule>.Enumerator enumerator1 = Owner.BombBays.GetEnumerator();
	                        try
	                        {
	                            while (enumerator1.MoveNext())
	                            {
	                                ShipModule mod = enumerator1.Current;
	                                if (mod.BombTimer > 0f)
	                                    continue;
	                                var b = new Bomb(new Vector3(Owner.Center, 0f), Owner.loyalty)
	                                {
	                                    WeaponName = mod.BombType
	                                };
	                                if (Owner.Ordinance <= ResourceManager.WeaponsDict[mod.BombType].OrdinanceRequiredToFire)
	                                    continue;
	                                Ship owner1 = Owner;
	                                owner1.Ordinance = owner1.Ordinance - ResourceManager.WeaponsDict[mod.BombType].OrdinanceRequiredToFire;
	                                b.SetTarget(toEvaluate.TargetPlanet);
	                                universeScreen.BombList.Add(b);	                                
	                                mod.BombTimer = ResourceManager.WeaponsDict[mod.BombType].fireDelay;
	                            }
	                            break;
	                        }
	                        finally
	                        {
	                            ((IDisposable) enumerator1).Dispose();
	                        }
	                    }
	                }
	                case Plan.RotateToFaceMovePosition:    RotateToFaceMovePosition(elapsedTime, toEvaluate);break;                                                
	                case Plan.RotateToDesiredFacing:	   RotateToDesiredFacing(elapsedTime, toEvaluate); break;
                    case Plan.MoveToWithin1000:            MoveToWithin1000(elapsedTime, toEvaluate);break;	                
	                    
	                case Plan.MakeFinalApproachFleet:	                
	                    if (Owner.fleet != null)
	                    {
	                        MakeFinalApproachFleet(elapsedTime, toEvaluate);
	                        break;
	                    }
	                    State = AIState.AwaitingOrders;
	                    break;
	                
	                case Plan.MoveToWithin1000Fleet:	                
	                    if (Owner.fleet != null)
	                    {
	                        MoveToWithin1000Fleet(elapsedTime, toEvaluate);
	                        break;
	                    }	                    
	                        State = AIState.AwaitingOrders;
	                        break;	                    	                
	                case Plan.MakeFinalApproach:	        MakeFinalApproach(elapsedTime, toEvaluate); break;
                    case Plan.RotateInlineWithVelocity:	    RotateInLineWithVelocity(elapsedTime, toEvaluate); break;
                    case Plan.StopWithBackThrust:	        StopWithBackwardsThrust(elapsedTime, toEvaluate); break;
                    case Plan.Orbit:	               	    DoOrbit(toEvaluate.TargetPlanet, elapsedTime); break;
                    case Plan.Colonize:                     Colonize(toEvaluate.TargetPlanet); break;
                    case Plan.Explore: 	                    DoExplore(elapsedTime); break;
	                case Plan.Rebase:                       DoRebase(toEvaluate); break;
	                case Plan.DefendSystem:                 DoSystemDefense(elapsedTime); break;                            
	                case Plan.DoCombat:                     DoCombat(elapsedTime); break;                            
	                case Plan.MoveTowards:	                MoveTowardsPosition(MovePosition, elapsedTime); break;
                    case Plan.PickupPassengers:
	                {
	                    if (start != null)
	                        PickupPassengers();
	                    else
	                        State = AIState.AwaitingOrders;
	                    break;
	                }
	                case Plan.DropoffPassengers:            DropoffPassengers(); break;                     
	                case Plan.DeployStructure:              DoDeploy(toEvaluate); break;
	                case Plan.PickupGoods:                  PickupGoods();break;	                                               
	                case Plan.DropOffGoods:	                DropoffGoods(); break;                            
	                case Plan.ReturnToHangar:	            DoReturnToHangar(elapsedTime); break;
	                case Plan.TroopToShip:                  DoTroopToShip(elapsedTime); break;                     
	                case Plan.BoardShip:	                DoBoardShip(elapsedTime); break;
	                case Plan.SupplyShip:                   DoSupplyShip(elapsedTime, toEvaluate); break;
	                case Plan.Refit:	                    DoRefit(elapsedTime, toEvaluate); break;                            
	                case Plan.LandTroop:                    DoLandTroop(elapsedTime, toEvaluate); break;
                    default:
	                    break;
	            }
	        }
	        if (State == AIState.Rebase)
	            foreach (ShipGoal goal in OrderQueue)
	            {
	                if (goal.Plan != Plan.Rebase || goal.TargetPlanet == null || goal.TargetPlanet.Owner == Owner.loyalty)
	                    continue;
	                OrderQueue.Clear();
	                State = AIState.AwaitingOrders;
	                break;
	            }	        
	        TriggerDelay -= elapsedTime;
	        if (BadGuysNear)
	        {
	            OrderQueue.thisLock.EnterWriteLock();
	            var docombat = false;
	            var tempShipGoal = OrderQueue.First;
	            ShipGoal firstgoal = tempShipGoal != null ? tempShipGoal.Value : null;
	            if (Owner.Weapons.Count > 0 || Owner.GetHangars().Count > 0 || Owner.Transporters.Count > 0)
	            {

	                if (Target != null) 
	                    docombat = !HasPriorityOrder && !IgnoreCombat && State != AIState.Resupply &&
	                               (OrderQueue.Count == 0 ||
	                                firstgoal != null && firstgoal.Plan != Plan.DoCombat && firstgoal.Plan != Plan.Bombard &&
	                                firstgoal.Plan != Plan.BoardShip);

	                if (docombat) 
	                    OrderQueue.AddFirst(new ShipGoal(Plan.DoCombat, Vector2.Zero, 0f));
	                if (TriggerDelay < 0)
	                {
	                    TriggerDelay = elapsedTime * 2;
	                    FireOnTarget();
	                }

	            }
	            OrderQueue.thisLock.ExitWriteLock();
	        }
	        else
	        {
	            foreach (Weapon purge in Owner.Weapons)
	            {
	                if (purge.fireTarget != null)
	                {
	                    purge.PrimaryTarget = false;
	                    purge.fireTarget = null;
	                    purge.SalvoTarget = null;
	                }
	                if (purge.AttackerTargetting != null)
	                    purge.AttackerTargetting.Clear();
	            }
	            if (Owner.GetHangars().Count > 0 && Owner.loyalty != universeScreen.player)
	                foreach (ShipModule hangar in Owner.GetHangars())
	                {
	                    if (hangar.IsTroopBay || hangar.IsSupplyBay || hangar.GetHangarShip() == null
	                        || hangar.GetHangarShip().GetAI().State == AIState.ReturnToHangar)
	                        continue;
	                    hangar.GetHangarShip().GetAI().OrderReturnToHangar();
	                }
	            else if (Owner.GetHangars().Count > 0)
	                foreach (ShipModule hangar in Owner.GetHangars())
	                {
	                    if (hangar.IsTroopBay
	                        || hangar.IsSupplyBay
	                        || hangar.GetHangarShip() == null
	                        || hangar.GetHangarShip().GetAI().State == AIState.ReturnToHangar
	                        || hangar.GetHangarShip().GetAI().hasPriorityTarget
	                        || hangar.GetHangarShip().GetAI().HasPriorityOrder

	                    )
	                        continue;
	                    hangar.GetHangarShip().DoEscort(Owner);
	                }
	        }
	        if (Owner.shipData.ShipCategory == ShipData.Category.Civilian && BadGuysNear) //fbedard: civilian will evade
	            CombatState = CombatState.Evade;

	        if (State != AIState.Resupply && !HasPriorityOrder &&
	            Owner.Health / Owner.HealthMax < DmgLevel[(int) Owner.shipData.ShipCategory] &&
	            Owner.shipData.Role >= ShipData.RoleName.supply) //fbedard: ships will go for repair
	            if (Owner.fleet == null || Owner.fleet != null && !Owner.fleet.HasRepair)
	                OrderResupplyNearest(false);
	        if (State == AIState.AwaitingOrders && Owner.NeedResupplyTroops)
	            OrderResupplyNearest(false);
	        if (State == AIState.AwaitingOrders && Owner.needResupplyOrdnance)
	            OrderResupplyNearest(false);
	        if (State == AIState.Resupply && !HasPriorityOrder)
	            HasPriorityOrder = true;
	        if (!Owner.isTurning)
	        {
	            DeRotate();
	            return;
	        }
	        else
	        {
	            return;
	        }
	    }

	    public enum Plan
		{
			Stop,
			Scrap,
			HoldPosition,
			Bombard,
			Exterminate,
			RotateToFaceMovePosition,
			RotateToDesiredFacing,
			MoveToWithin1000,
			MakeFinalApproachFleet,
			MoveToWithin1000Fleet,
			MakeFinalApproach,
			RotateInlineWithVelocity,
			StopWithBackThrust,
			Orbit,
			Colonize,
			Explore,
			Rebase,
			DoCombat,
			MoveTowards,
			Trade,
			DefendSystem,
            TransportPassengers,
			PickupPassengers,
			DropoffPassengers,
			DeployStructure,
			PickupGoods,
			DropOffGoods,
			ReturnToHangar,
			TroopToShip,
			BoardShip,
			SupplyShip,
			Refit,
			LandTroop,
			MoveToWithin7500,
            BombTroops
		}

		public class ShipGoal
		{
			public Plan Plan;

			public Goal goal;

			public float VariableNumber;

			public string VariableString;

			public Fleet fleet;

			public Vector2 MovePosition;

			public float DesiredFacing;

			public float FacingVector;

			public Planet TargetPlanet;

			public float SpeedLimit = 1f;

			public ShipGoal(Plan p, Vector2 pos, float facing)
			{
				Plan = p;
				MovePosition = pos;
				DesiredFacing = facing;
			}
		}

	    public class ShipWeight
	    {
	        public Ship ship;

	        public float weight;
	        public bool defendEscort;

	        public ShipWeight() {}
	    }

	    public class WayPoints
        {
            public Planet planet { get; set; }
            public Ship ship { get; set; }            
            public Vector2 location { get ; set; }
        }
		private enum transportState
		{
			ChoosePickup,
			GoToPickup,
			ChooseDropDestination,
			GotoDrop,
			DoDrop
		}

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~ArtificialIntelligence() { Dispose(false); }

        private void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    NearbyShips?.Dispose();
                    FriendliesNearby?.Dispose();
                }
                NearbyShips = null;
                FriendliesNearby = null;
                disposed = true;
            }
        }        
    }
}