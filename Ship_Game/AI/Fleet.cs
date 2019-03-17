using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Ship_Game.AI.Tasks;
using Ship_Game.Debug;
using Ship_Game.Ships;
using System;
using System.Linq;

namespace Ship_Game.AI
{
    public sealed partial class Fleet : ShipGroup
    {
        public BatchRemovalCollection<FleetDataNode> DataNodes = new BatchRemovalCollection<FleetDataNode>();
        public Guid Guid = Guid.NewGuid();
        public string Name = "";

        Array<Ship> CenterShips = new Array<Ship>();
        Array<Ship> LeftShips = new Array<Ship>();
        Array<Ship> RightShips = new Array<Ship>();
        Array<Ship> RearShips = new Array<Ship>();
        Array<Ship> ScreenShips = new Array<Ship>();
        public Array<Squad> CenterFlank = new Array<Squad>();
        public Array<Squad> LeftFlank = new Array<Squad>();
        public Array<Squad> RightFlank = new Array<Squad>();
        public Array<Squad> ScreenFlank = new Array<Squad>();
        public Array<Squad> RearFlank = new Array<Squad>();
        public Array<Array<Squad>> AllFlanks = new Array<Array<Squad>>();

        Map<Ship, Array<Ship>> InterceptorDict = new Map<Ship, Array<Ship>>();
        int DefenseTurns = 50;
        public MilitaryTask FleetTask;
        MilitaryTask CoreFleetSubTask;
        public FleetCombatStatus Fcs;


        public int FleetIconIndex;
        public int TaskStep;
        public bool IsCoreFleet;

        Array<Ship> AllButRearShips => Ships.Except(RearShips).ToArrayList();
        public bool HasRepair { get; private set; }  //fbedard: ships in fleet with repair capability will not return for repair.
        public bool HasOrdnanceSupplyShuttles { get; private set; } // FB: fleets with supply bays will be able to resupply ships
        public bool ReadyForWarp { get; private set; }
        public override string ToString() => $"Fleet {Name} size={Ships.Count} pos={Position} guid={Guid}";

        public Fleet()
        {
            FleetIconIndex = RandomMath.IntBetween(1, 10);
        }

        public void SetNameByFleetIndex(int index)
        {
            string suffix = "th";
            switch (index % 10) {
                case 1: suffix = "st"; break;
                case 2: suffix = "nd"; break;
                case 3: suffix = "rd"; break;
            }
            Name = index + suffix + " fleet";
        }

        public override void AddShip(Ship ship) => AddShip(ship, false);

        public void AddShip(Ship newShip, bool updateOnly)
        {
            if (newShip == null) // Added ship should never be null
            {
                Log.Error($"Ship Was Null for {Name}");
                return;
            }

            using (Ships.AcquireWriteLock())
            {
                if (newShip.IsPlatformOrStation)
                    return;

                FleetShipAddsRepair(newShip);
                FleetShipAddsOrdnanceSupplyShuttles(newShip);

                if (updateOnly && Ships.Contains(newShip))
                    return;

                // This is finding a logic bug: Ship is already in a fleet or this fleet already contains the ship.
                // This should likely be two different checks. There is also the possibilty that the ship is in another
                // Fleet ship list.
                if (newShip.fleet != null || Ships.Contains(newShip))
                {
                    Log.Warning($"{newShip}: already in a fleet");
                    return; // recover
                }

                AddShipToNodes(newShip);
                AssignPositions(Direction);
            }

        }

        void FleetShipAddsRepair(Ship ship)
        {
            HasRepair = HasRepair || ship.hasRepairBeam || (ship.HasRepairModule && ship.Ordinance > 0);
        }

        void FleetShipAddsOrdnanceSupplyShuttles( Ship ship)
        {
            HasOrdnanceSupplyShuttles = HasOrdnanceSupplyShuttles || (ship.Carrier.HasSupplyBays && ship.Ordinance >= 100);
        }

        public void AddExistingShip(Ship ship) => AddShipToNodes(ship);

        void AddShipToNodes(Ship shiptoadd)
        {
            base.AddShip(shiptoadd);
            shiptoadd.fleet = this;
            SetSpeed();
            AddShipToDataNode(shiptoadd);
        }


        public int CountCombatSquads => CenterFlank.Count + LeftFlank.Count + RightFlank.Count + ScreenFlank.Count;

        void ClearFlankList()
        {
            CenterShips.Clear();
            LeftShips.Clear();
            RightShips.Clear();
            ScreenShips.Clear();
            RearShips.Clear();
            CenterFlank.Clear();
            LeftFlank.Clear();
            RightFlank.Clear();
            ScreenFlank.Clear();
            RearFlank.Clear();
        }

        void ResetFlankLists()
        {
            ClearFlankList();

            var mainShipList = new Array<Ship>(Ships);
            var largestShip = mainShipList.FindMax(ship => (int)(ship.DesignRole));
            ShipData.RoleName largestCombat = largestShip.DesignRole;

            for (int i = mainShipList.Count - 1; i >= 0; i--)
            {
                Ship ship = mainShipList[i];

                if (ship.DesignRole >= ShipData.RoleName.fighter && ship.DesignRole == largestCombat)
                {
                    ScreenShips.Add(ship);
                    mainShipList.RemoveAtSwapLast(i);
                }
                else if (ship.DesignRole            == ShipData.RoleName.troop ||
                         ship.DesignRole            == ShipData.RoleName.freighter ||
                         ship.shipData.ShipCategory == ShipData.Category.Civilian ||
                         ship.DesignRole            == ShipData.RoleName.troopShip
                )
                {
                    RearShips.Add(ship);
                    mainShipList.RemoveAtSwapLast(i);
                }
                else if (ship.DesignRole < ShipData.RoleName.fighter)
                {
                    CenterShips.Add(ship);
                    mainShipList.RemoveAtSwapLast(i);
                }
                else
                {
                    int leftOver = mainShipList.Count;
                    if (leftOver % 2 == 0)
                        RightShips.Add(ship);
                    else
                        LeftShips.Add(ship);
                    mainShipList.RemoveAtSwapLast(i);
                }
            }

            int totalShips = CenterShips.Count;
            foreach (Ship ship in mainShipList.OrderByDescending(ship => ship.GetStrength() + ship.SurfaceArea))
            {
                if (totalShips < 4) CenterShips.Add(ship);
                else if (totalShips < 8) LeftShips.Add(ship);
                else if (totalShips < 12) RightShips.Add(ship);
                else if (totalShips < 16) ScreenShips.Add(ship);
                else if (totalShips < 20 && RearShips.Count == 0) RearShips.Add(ship);

                ++totalShips;
                if (totalShips != 16) continue;
                //so far as i can tell this has zero effect.
                ship.FleetCombatStatus = FleetCombatStatus.Maintain;
                totalShips = 0;
            }

        }

        enum FlankType
        {
            Left,
            Right
        }

        void FlankToCenterOffset(Array<Squad> flank, FlankType flankType)
        {
            if (flank.IsEmpty) return;
            int centerSquadCount = Math.Max(1, CenterFlank.Count);
            for (int x = 0; x < flank.Count; x++)
            {
                Squad squad = flank[x];
                var offset = centerSquadCount * 1400 + x * 1400;
                if (flankType == FlankType.Left)
                    offset *= -1;
                squad.Offset = new Vector2(offset, 0f);
            }
        }

        void LeftFlankToCenterOffset() => FlankToCenterOffset(LeftFlank, FlankType.Left);
        void RightFlankToCenterOffset() => FlankToCenterOffset(RightFlank, FlankType.Right);

        public void AutoArrange()
        {
            ResetFlankLists(); // set up center, left, right, screen, rear...

            SetSpeed();

            CenterFlank = SortSquadBySize(CenterShips);
            LeftFlank   = SortSquadBySpeed(LeftShips);
            RightFlank  = SortSquadBySpeed(RightShips);
            ScreenFlank = SortSquadBySpeed(ScreenShips);
            RearFlank   = SortSquadBySpeed(RearShips);
            AllFlanks.Add(CenterFlank);
            AllFlanks.Add(LeftFlank);
            AllFlanks.Add(RightFlank);
            AllFlanks.Add(ScreenFlank);
            AllFlanks.Add(RearFlank);
            Position = AveragePosition();

            ArrangeSquad(CenterFlank, Vector2.Zero);
            ArrangeSquad(ScreenFlank, new Vector2(0.0f, -2500f));
            ArrangeSquad(RearFlank  , new Vector2(0.0f, 2500f));

            LeftFlankToCenterOffset();
            RightFlankToCenterOffset();

            for (int x = 0; x < Ships.Count; x++)
            {
                Ship s = Ships[x];
                AddShipToDataNode(s);
            }
            AutoAssembleFleet(0.0f);

            for (int i = 0; i < Ships.Count; i++)
            {
                Ship s = Ships[i];
                if (s.InCombat)
                    continue;
                s.AI.OrderAllStop();
                s.AI.OrderThrustTowardsPosition(Position + s.FleetOffset, Direction, false);
            }
        }

        void AddShipToDataNode(Ship ship)
        {
            FleetDataNode node = DataNodes.Find(s => s.Ship == ship) ??
                                 DataNodes.Find(s => s.ShipName == ship.Name);
            if (node == null)
            {
                node = new FleetDataNode
                {
                    FleetOffset  = ship.RelativeFleetOffset,
                    OrdersOffset = ship.RelativeFleetOffset
                };
                DataNodes.Add(node);
            }
            ship.RelativeFleetOffset = node.FleetOffset;

            node.Ship         = ship;
            node.ShipName     = ship.Name;
            node.OrdersRadius = node.OrdersRadius < 2 ? ship.AI.GetSensorRadius() : node.OrdersRadius;
            ship.AI.FleetNode = node;
        }

        enum SquadSortType
        {
            Size,
            Speed
        }

        Array<Squad> SortSquadBySpeed(Array<Ship> allShips) => SortSquad(allShips, SquadSortType.Speed);
        Array<Squad> SortSquadBySize(Array<Ship> allShips) => SortSquad(allShips, SquadSortType.Size);

        Array<Squad> SortSquad(Array<Ship> allShips, SquadSortType sort)
        {
            var destSquad = new Array<Squad>();
            if (allShips.IsEmpty)
                return destSquad;

            int SortValue(Ship ship)
            {
                switch (sort)
                {
                    case SquadSortType.Size:  return ship.SurfaceArea;
                    case SquadSortType.Speed: return (int)ship.GetSTLSpeed();
                    default:                  return 0;
                }
            }

            allShips.Sort((a, b) =>
            {
                int aValue = SortValue(a);
                int bValue = SortValue(b);

                int order = bValue - aValue;
                if (order != 0) return order;
                return b.guid.CompareTo(a.guid);
            });

            var squad = new Squad { Fleet = this };
            destSquad.Add(squad);
            for (int x = 0; x < allShips.Count; ++x)
            {
                if (squad.Ships.Count < 4)
                    squad.Ships.Add(allShips[x]);
                if (squad.Ships.Count != 4 && x != allShips.Count - 1) continue;

                squad = new Squad { Fleet = this };
                destSquad.Add(squad);
            }
            return destSquad;
        }

        static void ArrangeSquad(Array<Squad> squad, Vector2 squadOffset)
        {
            int leftSide  = 0;
            int rightSide = 0;

            for (int index = 0; index < squad.Count; ++index)
            {
                if (index == 0)
                    squad[index].Offset = squadOffset;
                else if (index % 2 == 1)
                {
                    ++leftSide;
                    squad[index].Offset = new Vector2(leftSide * (-1400 + squadOffset.X), squadOffset.Y);
                }
                else
                {
                    ++rightSide;
                    squad[index].Offset = new Vector2(rightSide * (1400 + squadOffset.X), squadOffset.Y);
                }
            }
        }

        void AutoAssembleFleet(float facing)
        {
            for (int flank = 0; flank < AllFlanks.Count; flank++)
            {
                Array<Squad> squads = AllFlanks[flank];
                foreach (Squad squad in squads)
                {
                    for (int index = 0; index < squad.Ships.Count; ++index)
                    {
                        Ship ship = squad.Ships[index];
                        float radiansAngle;
                        switch (index)
                        {
                            default:
                            case 0: radiansAngle = Vectors.Up.ToRadians();    break;
                            case 1: radiansAngle = Vectors.Left.ToRadians();  break;
                            case 2: radiansAngle = Vectors.Right.ToRadians(); break;
                            case 3: radiansAngle = Vectors.Down.ToRadians();  break;
                        }

                        Vector2 offset = Vector2.Zero.PointFromRadians((squad.Offset.ToRadians() + facing), squad.Offset.Length());
                        ship.FleetOffset = offset + Vector2.Zero.PointFromRadians(radiansAngle + facing, 500f);
                        ship.RelativeFleetOffset = squad.Offset + Vector2.Zero.PointFromRadians(radiansAngle, 500f);
                    }
                }
            }
        }

        public void AssembleFleet2(Vector2 facingVec) => AssembleFleet(facingVec, IsCoreFleet);

        public void Reset()
        {
            while (Ships.Count > 0) {
                var ship = Ships.PopLast();
                ship.ClearFleet();
            }
            TaskStep  = 0;
            FleetTask = null;
            GoalStack.Clear();
        }

        void EvaluateTask(float elapsedTime)

        {
            if (Ships.Count == 0)
                FleetTask.EndTask();
            if (FleetTask == null)
                return;
            if (Empire.Universe.SelectedFleet == this)
                Empire.Universe.DebugWin?.DrawCircle(DebugModes.AO, Position, FleetTask.AORadius, Color.AntiqueWhite);
            switch (FleetTask.type)
            {
                case MilitaryTask.TaskType.ClearAreaOfEnemies:         DoClearAreaOfEnemies(FleetTask); break;
                case MilitaryTask.TaskType.AssaultPlanet:              DoAssaultPlanet(FleetTask); break;
                case MilitaryTask.TaskType.CorsairRaid:                DoCorsairRaid(elapsedTime); break;
                case MilitaryTask.TaskType.CohesiveClearAreaOfEnemies: DoCohesiveClearAreaOfEnemies(FleetTask); break;
                case MilitaryTask.TaskType.Exploration:                DoExplorePlanet(FleetTask); break;
                case MilitaryTask.TaskType.DefendSystem:               DoDefendSystem(FleetTask); break;
                case MilitaryTask.TaskType.DefendClaim:                DoClaimDefense(FleetTask); break;
                case MilitaryTask.TaskType.DefendPostInvasion:         DoPostInvasionDefense(FleetTask); break;
                case MilitaryTask.TaskType.GlassPlanet:                DoGlassPlanet(FleetTask); break;
            }
            Owner.GetEmpireAI().TaskList.ApplyPendingRemovals();
        }

        void DoExplorePlanet(MilitaryTask task) //Mer Gretman Left off here
        {
            bool eventBuildingFound = task.TargetPlanet.EventsOnBuildings();

            if (EndInvalidTask(!StillInvasionEffective(task)) || !StillCombatEffective(task))
            {
                task.IsCoreFleetTask = false;
                FleetTask = null;
                TaskStep = 0;
                return;
            }

             if (EndInvalidTask(eventBuildingFound || task.TargetPlanet.Owner != null
                                                   && task.TargetPlanet.Owner != Owner))
                return;

            switch (TaskStep)
            {
                case 0:
                    FleetTaskGatherAtRally(task);
                    TaskStep = 1;
                    break;
                case 1:
                    if (!HasArrivedAtRallySafely(5000)) break;
                    GatherAtAO(task, distanceFromAO: 50000f);
                    TaskStep = 2;
                    break;
                case 2:
                    if (ArrivedAtOffsetRally(task))
                        TaskStep = 3;
                    break;
                case 3:
                    EscortingToPlanet(task);
                    TaskStep = 4;
                    break;

                case 4:
                    WaitingForPlanetAssault(task);
                    if (EndInvalidTask(!IsFleetSupplied())) break;
                    if (ShipsOffMission(task))
                        TaskStep = 3;
                    break;
                case 5:
                    for (int i = 0; i < Ships.Count; i++)
                    {
                        Ships[i].AI.OrderLandAllTroops(task.TargetPlanet);
                    }
                    Position = task.TargetPlanet.Center;
                    AssembleFleet2(AveragePosition().DirectionToTarget(Position));
                    break;
            }
        }

        void DoPostInvasionDefense(MilitaryTask task)
        {
            if (EndInvalidTask(--DefenseTurns <= 0))
                return;

            switch (TaskStep)
            {
                case 0:
                    if (EndInvalidTask(FleetTask.TargetPlanet == null))
                        break;
                    SetPostInvasionFleetCombat();
                    TaskStep = 1;
                    break;
                case 1:
                    if (EndInvalidTask(!IsFleetSupplied()))
                        break;
                    PostInvasionStayInAO();
                    TaskStep = 2;
                    break;
                case 2:
                    if (PostInvasionAnyShipsOutOfAO(task))
                    {
                        TaskStep = 1;
                        break;
                    }
                    if (Ships.Any(ship => ship.InCombat))
                        break;
                    AssembleFleet2(Vector2.Zero);
                    break;
            }
        }

        void DoAssaultPlanet(MilitaryTask task)
        {
            if (!Owner.IsEmpireAttackable(task.TargetPlanet.Owner))
            {
                if (task.TargetPlanet.Owner == Owner || task.TargetPlanet.AnyOfOurTroops(Owner))
                {
                    var militaryTask = MilitaryTask.CreatePostInvasion(task.TargetPlanet.Center, task.WhichFleet, Owner);
                    Owner.GetEmpireAI().RemoveFromTaskList(task);
                    FleetTask = militaryTask;
                    Owner.GetEmpireAI().AddToTaskList(militaryTask);
                    for (int x =0; x < Ships.Count; ++x)
                    {
                        var ship = Ships[x];
                        if (ship.Carrier.AnyPlanetAssaultAvailable)
                            RemoveShip(ship);
                    }
                }
                else
                {
                    Log.Info($"Invasion ({Owner.Name}) planet ({task.TargetPlanet}) Not attackable");
                    task.EndTask();
                }
                return;
            }

            if (EndInvalidTask(!StillInvasionEffective(task)) | !StillCombatEffective(task))
            {
                task.IsCoreFleetTask = false;
                FleetTask = null;
                TaskStep = 0;
                return;
            }

            switch (TaskStep)
            {
                case 0:
                    FleetTaskGatherAtRally(task);
                    SetRestrictedCombatWeights(5000);
                    TaskStep = 1;
                    break;
                case 1:
                    if (!HasArrivedAtRallySafely(5000))
                        break;

                    GatherAtAO(task, distanceFromAO: Owner.ProjectorRadius * 1.1f);
                    TaskStep = 2;
                    break;
                case 2:
                    if (!ArrivedAtOffsetRally(task)) break;
                    TaskStep = 3;

                    Position = task.TargetPlanet.Center;
                    AssembleFleet2(AveragePosition().DirectionToTarget(Position));
                    break;
                case 3:
                    EscortingToPlanet(task);
                    TaskStep = 4;
                    break;
                case 4:
                    WaitingForPlanetAssault(task);
                    if (ShipsOffMission(task))
                        TaskStep = 3;
                    if (!IsFleetSupplied())
                        TaskStep = 5;
                    break;

                case 5:
                    SendFleetToResupply();
                    TaskStep = 3;
                    break;
            }
        }

        void DoCorsairRaid(float elapsedTime)
        {
            if (TaskStep != 0)
                return;

            FleetTask.TaskTimer -= elapsedTime;
            Ship station = Owner.GetShips().Find(ship => ship.Name == "Corsair Asteroid Base");
            if (FleetTask.TaskTimer > 0.0)
            {
                EndInvalidTask(Ships.Count == 0);
                return;
            }
            if (EndInvalidTask(station == null)) return;

            AssembleFleet2(Vector2.One);
            // ReSharper disable once PossibleNullReferenceException station should never be null here
            FormationWarpTo(station.Position, Vector2.One);
            FleetTask.EndTaskWithMove();
        }

        void DoDefendSystem(MilitaryTask task)
        {
            // this is currently unused. the system needs to be created with a defensive fleet.
            // no defensive fleets are created during the game. yet...
            switch (TaskStep)
            {
                case -1:
                    FleetTaskGatherAtRally(task);
                    SetAllShipsPriorityOrder();
                    break;
                case 0:
                    FleetTaskGatherAtRally(task);
                    TaskStep = 1;
                    break;
                case 1:
                    if (!ArrivedAtOffsetRally(task))
                        break;
                    GatherAtAO(task, distanceFromAO: 125000f);
                    TaskStep = 2;
                    SetAllShipsPriorityOrder();
                    break;
                case 2:
                    if (ArrivedAtOffsetRally(task))
                        TaskStep = 3;
                    break;
                case 3:
                    if (!AttackEnemyStrengthClumpsInAO(task))
                        DoOrbitTaskArea(task);
                    TaskStep = 4;
                    break;
                case 4:
                    if (EndInvalidTask(task.TargetPlanet.Owner != null))
                        break;
                    if (!IsFleetSupplied())
                        TaskStep = 5;

                    if (ShipsOffMission(task))
                        TaskStep = 3;
                    break;
                case 5:
                    SendFleetToResupply();
                    TaskStep = 6;
                    break;
                case 6:
                    if (!IsFleetSupplied())
                        break;
                    TaskStep = 3;
                    break;
            }
        }

        void DoClaimDefense(MilitaryTask task)
        {
            if (EndInvalidTask(task.TargetPlanet.Owner != null))
                return;
            task.AO = task.TargetPlanet.Center;
            switch (TaskStep)
            {
                case 0:
                    SetLooseCombatWeights();
                    FleetTaskGatherAtRally(task);
                    TaskStep = 1;
                    break;
                case 1:
                    if (!HasArrivedAtRallySafely(5000)) break;
                    GatherAtAO(task, 10000);
                    TaskStep = 2;
                    break;
                case 2:
                    if (!ArrivedAtCombatRally(task))
                        break;
                    TaskStep = 3;
                    CancelFleetMoveInArea(task.AO, task.AORadius * 2);
                    break;
                case 3:
                    if (!DoOrbitTaskArea(task))
                        AttackEnemyStrengthClumpsInAO(task);
                    TaskStep = 4;
                    break;
                case 4:
                    if (!IsFleetSupplied())
                        TaskStep = 5;
                    if (ShipsOffMission(task))
                        TaskStep = 3;
                    break;
                case 5:
                    SendFleetToResupply();
                    TaskStep = 6;
                    break;
                case 6:
                    if (!IsFleetSupplied())
                        break;
                    TaskStep = 3;
                    break;
            }
        }

        void DoCohesiveClearAreaOfEnemies(MilitaryTask task)
        {
            if (CoreFleetSubTask == null) TaskStep = 1;

            switch (TaskStep)
            {
                case 1:
                    if (EndInvalidTask(!MoveFleetToNearestCluster(task)))
                    {
                        CoreFleetSubTask = null;
                        break;
                    }
                    SetRestrictedCombatWeights(task.AORadius);
                    TaskStep = 2;
                    break;
                case 2:
                    if (!ArrivedAtCombatRally(CoreFleetSubTask)) break;
                    TaskStep = 3;
                    break;
                case 3:
                    if (!AttackEnemyStrengthClumpsInAO(CoreFleetSubTask))
                    {
                        TaskStep = 1;
                        CoreFleetSubTask = null;
                        break;
                    }
                    CancelFleetMoveInArea(task.AO, task.AORadius * 2);
                    TaskStep = 4;
                    break;
                case 4:
                    if (!IsFleetSupplied())
                    {
                        TaskStep = 5;
                        break;
                    }
                    if (ShipsOffMission(CoreFleetSubTask))
                        TaskStep = 3;
                    for (int i = 0; i < Ships.Count; i++)
                    {
                        Ship ship = Ships[i];
                        if (ship.AI.BadGuysNear)
                            ship.AI.ClearPriorityOrder();
                    }
                    break;

                case 5:
                    SendFleetToResupply();
                    TaskStep = 4;
                    break;
                case 6:
                    IsFleetSupplied(wantedSupplyRatio: .9f);
                    break;
            }
        }

        void DoGlassPlanet(MilitaryTask task)
        {
            if (task.TargetPlanet.Owner == Owner || task.TargetPlanet.Owner == null)
                task.EndTask();
            else if (task.TargetPlanet.Owner != null & task.TargetPlanet.Owner != Owner && !task.TargetPlanet.Owner.GetRelations(Owner).AtWar)
            {
                task.EndTask();
            }
            else
            {
                switch (TaskStep)
                {
                    case 0:
                    {
                        if (Owner.FindClosestSpacePort(task.AO, out Planet closestPlanet))
                        {
                            Vector2 dir = closestPlanet.Center.DirectionToTarget(task.AO);
                            MoveToNow(closestPlanet.Center, dir);
                            TaskStep = 1;
                        }
                        break;
                    }
                    case 1:
                        int step = MoveToPositionIfAssembled(task, task.AO, 15000f, 150000f);
                        if (step == -1)
                            task.EndTask();
                        TaskStep += step;
                        break;
                    case 2:
                        if (task.WaitForCommand && Owner.GetEmpireAI().ThreatMatrix
                                .PingRadarStr(task.TargetPlanet.Center, 30000f, Owner) > 250.0)
                            break;
                        foreach (Ship ship in Ships)
                            ship.AI.OrderBombardPlanet(task.TargetPlanet);
                        TaskStep = 4;
                        break;
                    case 4:
                        if (!IsFleetSupplied())
                        {
                            TaskStep = 5;
                            break;
                        }
                        else
                        {
                            TaskStep = 2;
                            break;
                        }
                    case 5:
                    {
                        if (Owner.FindClosestSpacePort(Position, out Planet closestPlanet))
                        {
                            Position = closestPlanet.Center;
                            foreach (Ship ship in Ships)
                                ship.AI.OrderResupply(closestPlanet, true);
                            TaskStep = 6;
                        }
                        break;
                    }
                    case 6:
                        float fleetOrdinance = 0.0f;
                        float fleetOrdinanceMax = 0.0f;
                        foreach (Ship ship in Ships)
                        {
                            if (ship.AI.State != AIState.Resupply)
                            {
                                TaskStep = 5;
                                return;
                            }
                            ship.AI.SetPriorityOrder(clearOrders: false);
                            fleetOrdinance    += ship.Ordinance;
                            fleetOrdinanceMax += ship.OrdinanceMax;
                        }
                        if ((int)fleetOrdinance != (int)fleetOrdinanceMax)
                            break;
                        TaskStep = 0;
                        break;
                }
            }
        }

        void DoClearAreaOfEnemies(MilitaryTask task)
        {
            if (EndInvalidTask(!StillCombatEffective(task)))
            {
                FleetTask = null;
                TaskStep = 0;
                return;
            }
            switch (TaskStep)
            {
                case 0:
                    FleetTaskGatherAtRally(task);
                    SetRestrictedCombatWeights(task.AORadius);
                    TaskStep = 1;
                    break;
                case 1:
                    if (!HasArrivedAtRallySafely(5000))
                        break;
                    GatherAtAO(task, distanceFromAO: 10000f);
                    TaskStep = 2;
                    break;
                case 2:
                    if (!ArrivedAtCombatRally(task))
                        break;
                    TaskStep = 3;
                    Position = task.AO;
                    AssembleFleet2(AveragePosition().DirectionToTarget(Position));
                    break;
                case 3:
                    if (!AttackEnemyStrengthClumpsInAO(task))
                        DoOrbitTaskArea(task);
                    else
                        CancelFleetMoveInArea(task.AO, task.AORadius * 2);
                    TaskStep = 4;
                    break;
                case 4:
                    if (EndInvalidTask(!Owner.IsEmpireAttackable(task.TargetPlanet.Owner)))
                        break;
                    if (ShipsOffMission(task))
                        TaskStep = 3;
                    if (!IsFleetSupplied())
                        TaskStep = 5;
                    break;
                case 5:
                    SendFleetToResupply();
                    TaskStep = 6;
                    break;
                case 6:
                    if (!IsFleetSupplied(wantedSupplyRatio: .9f))
                        break;
                    TaskStep = 4;
                    break;
            }
        }

        bool EndInvalidTask(bool condition)
        {
            if (!condition) return false;
            FleetTask.EndTask();
            return true;
        }

        /// @return true if order successful. Fails when enemies near.
        bool DoOrbitTaskArea(MilitaryTask task)
        {
            CombatStatus status = FleetInAreaInCombat(task.AO, task.AORadius);

            if (status < CombatStatus.ClearSpace)
                return false;

            DoOrbitAreaRestricted(task.TargetPlanet, task.AO, task.AORadius);
            return true;
        }

        void CancelFleetMoveInArea(Vector2 pos, float radius )
        {
            foreach (Ship ship in Ships)
            {
                if (!ship.Center.OutsideRadius(pos, radius) &&
                    ship.AI.State == AIState.FormationWarp)
                {
                    ship.AI.State = AIState.AwaitingOrders;
                    ship.AI.ClearPriorityOrder();
                }
            }
        }

        void SetPriorityOrderTo(Array<Ship> ships)
        {
            for (int i = 0; i < ships.Count; ++i)
            {
                Ship ship = Ships[i];
                ship.AI.SetPriorityOrder(true);
            }
        }

        void SetAllShipsPriorityOrder() => SetPriorityOrderTo(Ships);

        void FleetTaskGatherAtRally(MilitaryTask task)
        {
            Planet planet       = Owner.FindNearestRallyPoint(task.AO);
            Vector2 movePoint   = planet.Center;
            Vector2 finalFacing = movePoint.DirectionToTarget(task.AO);

            SetAllShipsPriorityOrder();
            MoveToNow(movePoint, finalFacing);
        }

        bool HasArrivedAtRallySafely(float distanceFromPosition)
        {
            MoveStatus status = IsFleetAssembled(distanceFromPosition);
            if (status == MoveStatus.Dispersed)
                return false;
            bool invalid = status == MoveStatus.InCombat;
            if (invalid)
            {
                DebugInfo(FleetTask, $"Fleet in Combat");
            }
            return !EndInvalidTask(invalid);
        }

        void GatherAtAO(MilitaryTask task, float distanceFromAO)
        {
            Position = task.AO.OffsetTowards(AveragePosition(), distanceFromAO);
            FormationWarpTo(Position, AveragePosition().DirectionToTarget(task.AO));
        }

        void HoldFleetPosition()
        {
            for (int index = 0; index < Ships.Count; index++)
            {
                Ship ship = Ships[index];
                ship.AI.State = AIState.HoldPosition;
                if (ship.shipData.Role == ShipData.RoleName.troop)
                    ship.AI.HoldPosition();
            }
        }

        bool ArrivedAtOffsetRally(MilitaryTask task)
        {
            if (IsFleetAssembled(5000f) != MoveStatus.Assembled)
                return false;

            HoldFleetPosition();

            InterceptorDict.Clear();
            return true;
        }

        bool ArrivedAtCombatRally(MilitaryTask task)
        {
            return IsFleetAssembled(5000f, task.AO) != MoveStatus.Dispersed;
        }

        Ship[] AvailableShips => AllButRearShips.Filter(ship => !ship.AI.HasPriorityOrder);

        bool AttackEnemyStrengthClumpsInAO(MilitaryTask task)
        {
            Map<Vector2, float> enemyClumpsDict = Owner.GetEmpireAI().ThreatMatrix
                .PingRadarStrengthClusters(task.AO, task.AORadius, 2500, Owner);

            if (enemyClumpsDict.Count == 0)
                return false;

            var availableShips = new Array<Ship>(AvailableShips);

            foreach (var kv in enemyClumpsDict.OrderBy(dis => dis.Key.SqDist(task.AO)))
            {
                if (availableShips.Count == 0)
                    break;

                float attackStr = 0.0f;
                for (int i = availableShips.Count - 1; i >= 0; --i)
                {
                    if (attackStr > kv.Value * 3)
                        break;

                    Ship ship = availableShips[i];
                    if (ship.AI.HasPriorityOrder || ship.InCombat)
                    {
                        availableShips.RemoveAtSwapLast(i);
                        continue;
                    }
                    Vector2 vFacing = ship.Center.DirectionToTarget(kv.Key);
                    ship.AI.OrderMoveTowardsPosition(kv.Key, vFacing, true, null);
                    ship.ForceCombatTimer();

                    availableShips.RemoveAtSwapLast(i);
                    attackStr += ship.GetStrength();
                }
            }

            foreach (Ship needEscort in RearShips)
            {
                if (availableShips.IsEmpty) break;
                Ship ship = availableShips.PopLast();
                ship.DoEscort(needEscort);
            }

            foreach (Ship ship in availableShips)
                ship.AI.OrderMoveDirectlyTowardsPosition(task.AO, Vectors.Up, true);

            return true;
        }

        bool MoveFleetToNearestCluster(MilitaryTask task)
        {
            var strengthCluster = new ThreatMatrix.StrengthCluster
            {
                Empire = Owner,
                Granularity = 5000f,
                Postition = task.AO,
                Radius = task.AORadius
            };

            strengthCluster = Owner.GetEmpireAI().ThreatMatrix.FindLargestStengthClusterLimited(strengthCluster, GetStrength(), AveragePosition());
            if (strengthCluster.Strength <= 0) return false;
            CoreFleetSubTask = new MilitaryTask
            {
                AO = strengthCluster.Postition,
                AORadius = strengthCluster.Granularity
            };
            GatherAtAO(CoreFleetSubTask, 7500);
            return true;
        }

        bool ShipsOffMission(MilitaryTask task)
        {
            return AllButRearShips.Any(ship => !ship.AI.HasPriorityOrder
                                     && (!ship.InCombat
                                         && ship.Center.OutsideRadius(task.AO, task.AORadius * 1.5f)));
        }

        void SetRestrictedCombatWeights(float ordersRadius)
        {
            for (int i = 0; i < Ships.Count; i++)
            {
                Ship ship = Ships[i];
                ship.AI.FleetNode.AssistWeight   = 1f;
                ship.AI.FleetNode.DefenderWeight = 1f;
                ship.AI.FleetNode.OrdersRadius   = ordersRadius;
            }
        }

        void SetLooseCombatWeights()
        {
            for (int i = 0; i < Ships.Count; i++)
            {
                Ship ship = Ships[i];
                ship.AI.FleetNode.AssistWeight = 1f;
                ship.AI.FleetNode.DefenderWeight = 1f;
                ship.AI.FleetNode.OrdersRadius = ship.SensorRange;
            }
        }


        void WaitingForPlanetAssault(MilitaryTask task)
        {
            float theirGroundStrength = GetGroundStrOfPlanet(task.TargetPlanet);
            float ourGroundStrength   = FleetTask.TargetPlanet.GetGroundStrength(Owner);
            bool invading = IsInvading(theirGroundStrength, ourGroundStrength, task);
            bool bombing  = BombPlanet(ourGroundStrength, task);
            if (!bombing && !invading)
                EndInvalidTask(true);
        }

        void SendFleetToResupply()
        {
            Planet rallyPoint = Owner.RallyShipYardNearestTo(AveragePosition());
            if (rallyPoint == null) return;
            for (int i = 0; i < Ships.Count; i++)
            {
                Ship ship = Ships[i];
                if (ship.AI.HasPriorityOrder) continue;
                ship.AI.OrderResupply(rallyPoint, true);
            }
        }

        void DebugInfo(MilitaryTask task, string text)
            => Empire.Universe?.DebugWin?.DebugLogText($"{task.type}: ({Owner.Name}) Planet: {task.TargetPlanet?.Name ?? "None"} {text}", DebugModes.Normal);

        bool StillCombatEffective(MilitaryTask task)
        {
            float targetStrength =
                Owner.GetEmpireAI().ThreatMatrix.PingRadarStr(task.AO, task.AORadius, Owner);
            float fleetStrengthThreshold = GetStrength() * 2;
            if (!(targetStrength >= fleetStrengthThreshold))
                return true;
            DebugInfo(task, $"Enemy Strength too high. Them: {targetStrength} Us: {fleetStrengthThreshold}");
            return false;
        }

        bool StillInvasionEffective(MilitaryTask task)
        {
            bool troopsOnPlanet        = task.TargetPlanet.AnyOfOurTroops(Owner);
            bool invasionTroops               = Ships.Any(troops => troops.Carrier.AnyPlanetAssaultAvailable);
            bool stillMissionEffective = troopsOnPlanet || invasionTroops;
            if (!stillMissionEffective)
                DebugInfo(task, $" No Troops on Planet and No Ships.");
            return stillMissionEffective;
        }

        void InvadeTactics(Array<Ship> flankShips, InvasionTactics type, Vector2 moveTo)
        {
            foreach (Ship ship in flankShips)
            {
                ShipAI ai = ship.AI;
                ai.CombatState = ship.shipData.CombatState;
                if (!ship.Center.OutsideRadius(FleetTask.TargetPlanet.Center, FleetTask.AORadius))
                    continue;

                ai.CancelIntercept();
                ai.FleetNode.AssistWeight = 1f;
                ai.FleetNode.DefenderWeight = 1f;
                ai.FleetNode.OrdersRadius = ship.maxWeaponsRange;
                switch (type) {
                    case InvasionTactics.Screen:
                        if (!ship.InCombat)
                            ai.OrderMoveDirectlyTowardsPosition(moveTo + ship.FleetOffset, Direction, false);
                        break;
                    case InvasionTactics.Rear:
                        if (!ai.HasPriorityOrder)
                            ai.OrderMoveDirectlyTowardsPosition(moveTo + ship.FleetOffset, Direction, false, Speed * 0.75f);
                        break;
                    case InvasionTactics.Center:
                        if (!ship.InCombat || (ai.State != AIState.Bombard && ship.DesignRole != ShipData.RoleName.bomber))
                            ai.OrderMoveDirectlyTowardsPosition(moveTo + ship.FleetOffset, Direction, false);
                        break;
                    case InvasionTactics.Side:
                        if (!ship.InCombat)
                            ai.OrderMoveDirectlyTowardsPosition(moveTo + ship.FleetOffset, Direction, false);
                        break;
                }
            }
        }

        private enum InvasionTactics
        {
            Screen,
            Center,
            Side,
            Rear
        }

        bool EscortingToPlanet(MilitaryTask task)
        {
            Vector2 targetPos = task.TargetPlanet.Center;
            AttackEnemyStrengthClumpsInAO(task);
            InvadeTactics(ScreenShips, InvasionTactics.Screen, targetPos);
            InvadeTactics(CenterShips, InvasionTactics.Center, targetPos);
            InvadeTactics(RearShips, InvasionTactics.Rear, targetPos);
            InvadeTactics(RightShips, InvasionTactics.Rear, targetPos);
            InvadeTactics(LeftShips, InvasionTactics.Side, targetPos);

            return !task.TargetPlanet.AnyOfOurTroops(Owner) || Ships.Any(bombers => bombers.AI.State == AIState.Bombard);
        }

        void StopBombPlanet()
        {
            foreach (Ship ship in Ships.Filter(ship => ship.BombBays.Count > 0))
            {
                if (ship.AI.State == AIState.Bombard)
                    ship.AI.ClearOrders();
            }
        }

        bool StartBombing(MilitaryTask task)
        {
            bool anyShipsBombing = false;
            foreach (Ship ship in Ships.Filter(ship => ship.BombBays.Count > 0))
            {
                if (!ship.AI.HasPriorityOrder && ship.AI.State != AIState.Bombard)
                    ship.AI.OrderBombardPlanet(task.TargetPlanet);
                anyShipsBombing |= ship.AI.State == AIState.Bombard;
            }
            return anyShipsBombing;
        }

        // @return TRUE if any ships are bombing planet
        // Bombing is done if we have no ground strength or if
        // there are more than provided free spaces (???)
        bool BombPlanet(float ourGroundStrength, MilitaryTask task , int freeSpacesNeeded = 5)
        {
            bool doBombs = ourGroundStrength <= 0 || task.TargetPlanet.GetGroundLandingSpots() >= freeSpacesNeeded;
            if (doBombs)
                return StartBombing(task);
            StopBombPlanet();
            return false;
        }

        bool IsInvading(float theirGroundStrength, float ourGroundStrength, MilitaryTask task, int LandingspotsNeeded =5)
        {
            int freeLandingSpots = task.TargetPlanet.GetGroundLandingSpots();
            if (freeLandingSpots < 1)
                return false;
            float planetAssaultStrength = 0.0f;
            foreach (Ship ship in RearShips)
                planetAssaultStrength += ship.Carrier.PlanetAssaultStrength;

            planetAssaultStrength += ourGroundStrength;
            if (planetAssaultStrength < theirGroundStrength * 0.75f)
            {
                DebugInfo(task, $"Fail insufficient forces. us: {planetAssaultStrength} them:{theirGroundStrength}");
                return false;
            }
            if (freeLandingSpots < LandingspotsNeeded)
            {
                DebugInfo(task,$"Fail insufficient landing space. planetHas: {freeLandingSpots} Needed: {LandingspotsNeeded}");
                return false;
            }

            if (ourGroundStrength < 1)
                StopBombPlanet();

            if (Ships.Any(ship => ship.Center.InRadius(task.AO, task.AORadius)))
                OrderShipsToInvade(RearShips, task);
            return true;
        }

        void OrderShipsToInvade(Array<Ship> ships, MilitaryTask task)
        {
            foreach (Ship ship in ships)
            {
                ship.AI.OrderLandAllTroops(task.TargetPlanet);
                ship.AI.SetPriorityOrder(false);
            }
        }

        float GetGroundStrOfPlanet(Planet p) => p.GetGroundStrengthOther(Owner);

        void SetPostInvasionFleetCombat()
        {
            foreach (FleetDataNode node in DataNodes)
            {
                node.OrdersRadius = FleetTask.AORadius;
                node.AssistWeight = 1;
                node.DPSWeight = -1;
            }
        }

        void PostInvasionStayInAO()
        {
            foreach (Ship ship in Ships)
            {
                if (ship.Center.SqDist(FleetTask.AO) > ship.AI.FleetNode.OrdersRadius)
                    ship.AI.OrderThrustTowardsPosition(FleetTask.AO + ship.FleetOffset, 60f.AngleToDirection(), true);
            }
        }

        bool PostInvasionAnyShipsOutOfAO(MilitaryTask task) =>
            Ships.Any(ship => task.AO.OutsideRadius(ship.Center, ship.AI.FleetNode.OrdersRadius));

        int MoveToPositionIfAssembled(MilitaryTask task, Vector2 position, float assemblyRadius = 5000f, float moveToWithin = 7500f )
        {
            MoveStatus nearFleet = IsFleetAssembled(assemblyRadius, task.AO);

            if (nearFleet == MoveStatus.InCombat)
                return -1;

            if (nearFleet == MoveStatus.Assembled)
            {
                Vector2 dir = AveragePosition().DirectionToTarget(position);
                Position = position + dir * moveToWithin;
                FormationWarpTo(Position, dir);
                return 1;
            }
            return 0;
        }

        public void UpdateAI(float elapsedTime, int which)
        {
            if (FleetTask != null)
            {
                EvaluateTask(elapsedTime);
            }
            else
            {
                if (EmpireManager.Player == Owner || IsCoreFleet )
                {
                    if (!IsCoreFleet) return;
                    foreach (Ship ship in Ships)
                    {
                        ship.AI.CancelIntercept();
                    }
                    return;
                }
                Owner.GetEmpireAI().UsedFleets.Remove(which);
                for (int i = 0; i < Ships.Count; ++i)
                {
                    Ship s = Ships[i];
                    RemoveShipAt(s, i--);

                    s.AI.ClearOrders();
                    s.HyperspaceReturn();
                    s.isSpooling = false;
                    if (s.shipData.Role == ShipData.RoleName.troop)
                        s.AI.OrderRebaseToNearest();
                    else
                        Owner.ForcePoolAdd(s);
                }
                Reset();
            }
        }

        void RemoveFromAllSquads(Ship ship)
        {
            if (DataNodes != null)
            {
                using (DataNodes.AcquireWriteLock())
                    foreach (FleetDataNode fleetDataNode in DataNodes)
                        if (fleetDataNode.Ship == ship)
                            fleetDataNode.Ship = null;
            }
            if (AllFlanks == null)
                return;
            foreach (Array<Squad> flank in AllFlanks)
            {
                foreach (Squad squad in flank)
                {
                    if (squad.Ships.Contains(ship))
                        squad.Ships.QueuePendingRemoval(ship);
                    if (squad.DataNodes == null) continue;
                    using (squad.DataNodes.AcquireWriteLock())
                        foreach (FleetDataNode fleetDataNode in squad.DataNodes)
                        {
                            if (fleetDataNode.Ship == ship)
                                fleetDataNode.Ship = null;
                        }
                }
            }
        }

        void RemoveShipAt(Ship ship, int index)
        {
            ship.fleet = null;
            RemoveFromAllSquads(ship);
            Ships.RemoveAtSwapLast(index);
        }

        public bool RemoveShip(Ship ship)
        {
            if (ship == null) return false;
            if (ship.Active && ship.fleet != this)
                Log.Warning($"{ship.fleet?.Name ?? "No Fleet"} : not equal {Name}");
            if (ship.AI.State != AIState.AwaitingOrders && ship.Active)
                Empire.Universe.DebugWin?.DebugLogText($"Fleet RemoveShip: Ship not awaiting orders and removed from fleet State: {ship.AI.State}", DebugModes.Normal);
            ship.fleet = null;
            RemoveFromAllSquads(ship);
            if (Ships.Remove(ship) || !ship.Active) return true;
            Empire.Universe.DebugWin?.DebugLogText("Fleet RemoveShip: Ship is not in this fleet", DebugModes.Normal);
            return false;
        }

        public float SpeedLimiter(Ship ship)
        {
            float distance = ship.Center.Distance(Position);

            float distanceFleetCenterToDistance = StoredFleetDistanceToMove
                                                  - Position.Distance(Position + ship.FleetOffset);
            float shipSpeedLimit = Speed;
            if (distance <= distanceFleetCenterToDistance)
            {
                float reduction = distanceFleetCenterToDistance - distance;
                shipSpeedLimit = Math.Max(1, Speed - reduction);
                if (shipSpeedLimit > Speed)
                    shipSpeedLimit = Speed;
            }
            else if (distance > distanceFleetCenterToDistance)
            {
                float speedIncrease = distance - distanceFleetCenterToDistance;
                shipSpeedLimit = Speed + speedIncrease;
            }
            return shipSpeedLimit;
        }
        public void Update(float elapsedTime)
        {
            HasRepair = false;
            ReadyForWarp = true;
            for (int i = Ships.Count - 1; i >= 0; --i)
            {
                Ship ship = Ships[i];
                if (!ship.Active)
                {
                    RemoveShip(ship);
                    continue;
                }
                if (ship.AI.State == AIState.FormationWarp)
                {
                    SetCombatMoveAtPosition(ship, Position, 7500);
                    Empire.Universe.DebugWin?.DrawCircle(DebugModes.Pathing, Position, 100000, Color.Yellow);
                }
                AddShip(ship, true);
                ReadyForWarp = ReadyForWarp && ship.ShipReadyForFormationWarp() > ShipStatus.Poor;
            }
            Ships.ApplyPendingRemovals();

            if (Ships.Count <= 0 || GoalStack.Count <= 0)
                return;
            GoalStack.Peek().Evaluate(elapsedTime);
        }

        public enum FleetCombatStatus
        {
            Maintain,
            Loose,
            Free
        }

        public sealed class Squad : IDisposable
        {
            public FleetDataNode MasterDataNode = new FleetDataNode();
            public BatchRemovalCollection<FleetDataNode> DataNodes = new BatchRemovalCollection<FleetDataNode>();
            public BatchRemovalCollection<Ship> Ships = new BatchRemovalCollection<Ship>();
            public Fleet Fleet;
            public Vector2 Offset;

            public void Dispose()
            {
                Destroy();
                GC.SuppressFinalize(this);
            }
            ~Squad() { Destroy(); }

            void Destroy()
            {
                DataNodes?.Dispose(ref DataNodes);
                Ships?.Dispose(ref Ships);
            }
        }

        public enum FleetGoalType
        {
            AttackMoveTo,
            MoveTo
        }

        protected override void Destroy()
        {
            if (Ships != null)
                for (int x = Ships.Count - 1; x >= 0; x--)
                {
                    var ship = Ships[x];
                    RemoveShip(ship);
                }

            DataNodes?.Dispose(ref DataNodes);
            CenterShips     = null;
            LeftShips       = null;
            RightShips      = null;
            RearShips       = null;
            ScreenShips     = null;
            CenterFlank     = null;
            LeftFlank       = null;
            RightFlank      = null;
            ScreenFlank     = null;
            RearFlank       = null;
            AllFlanks       = null;
            InterceptorDict = null;
            FleetTask       = null;
            base.Destroy();
        }

        public static string GetDefaultFleetNames(int index)
        {
            switch (index)
            {
                case 1: return "First";
                case 2: return "Second";
                case 3: return "Third";
                case 4: return "Fourth";
                case 5: return "Fifth";
                case 6: return "Sixth";
                case 7: return "Seventh";
                case 8: return "Eight";
                case 9: return "Ninth";
            }
            return "";
        }
    }
}
