using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Ship_Game.AI;
using Ship_Game.AI.Tasks;
using Ship_Game.Commands.Goals;
using Ship_Game.Debug.Page;
using Ship_Game.Gameplay;
using Ship_Game.GameScreens.Sandbox;
using Ship_Game.Ships;
using System;
using System.Collections.Generic;
using System.Linq;
using SDGraphics;
using SDUtils;
using Ship_Game.Ships.AI;
using Ship_Game.Fleets;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;
using Ship_Game.Universe;

namespace Ship_Game.Debug
{
    public enum DebugModes
    {
        Normal,
        Targeting,
        PathFinder,
        DefenseCo,
        Trade,
        Planets,
        AO,
        ThreatMatrix,
        SpatialManager,
        input,
        Tech,
        Solar, // Sun timers, black hole data, pulsar radiation radius...
        War,
        Pirates,
        Remnants,
        Agents,
        Relationship,
        FleetMulti,
        StoryAndEvents,
        Tasks,
        Particles,
        Last // dummy value
    }


    public sealed partial class DebugInfoScreen : GameScreen
    {
        public bool IsOpen = true;
        readonly UniverseScreen Screen;
        readonly UniverseState Universe;
        Rectangle Win = new(30, 100, 1200, 700);

        // TODO: Use these stats in some DebugPage
        public static int ShipsDied;
        public static int ProjDied;
        public static int ProjCreated;
        public static int ModulesDied;

        public DebugModes Mode => Screen.DebugMode;
        readonly Array<DebugPrimitive> Primitives = new();
        DebugPage Page;

        public DebugInfoScreen(UniverseScreen screen) : base(screen, toPause: null)
        {
            Screen = screen;
            Universe = screen.UState;
        }

        readonly Dictionary<string, Array<string>> ResearchText = new();

        public void ResearchLog(string text, Empire empire)
        {
            if (!DebugLogText(text, DebugModes.Tech))
                return;
            if (ResearchText.TryGetValue(empire.Name, out Array<string> empireTechs))
            {
                empireTechs.Add(text);
            }
            else
            {
                ResearchText.Add(empire.Name, new Array<string> {text});
            }
        }

        public void ClearResearchLog(Empire empire)
        {
            if (ResearchText.TryGetValue(empire.Name, out Array<string> empireTechs))
                empireTechs.Clear();
        }

        public override void PerformLayout()
        {
        }

        public override bool HandleInput(InputState input)
        {
            if (input.KeyPressed(Keys.Left) || input.KeyPressed(Keys.Right))
            {
                ResearchText.Clear();
                HideAllDebugGameInfo();

                DebugModes mode = Mode;
                mode += input.KeyPressed(Keys.Left) ? -1 : +1;
                Screen.UState.SetDebugMode(Mode switch
                {
                    >= DebugModes.Last => DebugModes.Normal,
                    < DebugModes.Normal => DebugModes.Last - 1,
                    _ => mode
                });
                return true;
            }
            return base.HandleInput(input);
        }

        public override void Update(float fixedDeltaTime)
        {
            if (Page != null && Page.Mode != Mode) // destroy page if it's no longer needed
            {
                Page.RemoveFromParent();
                Page = null;
            }

            if (Page == null) // create page if needed
            {
                switch (Mode)
                {
                    case DebugModes.PathFinder: Page = Add(new PathFinderDebug(Screen, this)); break;
                    case DebugModes.Trade:      Page = Add(new TradeDebug(Screen, this)); break;
                    case DebugModes.Planets:    Page = Add(new PlanetDebug(Screen,this)); break;
                    case DebugModes.Solar:      Page = Add(new SolarDebug(Screen, this)); break;
                    case DebugModes.War:            Page = Add(new DebugWar(Screen, this)); break;
                    case DebugModes.AO:             Page = Add(new DebugAO(Screen, this)); break;
                    case DebugModes.SpatialManager: Page = Add(new SpatialDebug(Screen, this)); break;
                    case DebugModes.StoryAndEvents: Page = Add(new StoryAndEventsDebug(Screen, this)); break;
                    case DebugModes.Particles:      Page = Add(new ParticleDebug(Screen, this)); break;
                }
            }

            UpdateDebugShips();
            base.Update(fixedDeltaTime);
        }

        void UpdateDebugShips()
        {
            //if (DebugPlatformSpeed == null) // platform is only enabled in sandbox universe
            //    return;
            //float platformSpeed = DebugPlatformSpeed.AbsoluteValue;
            //float speedLimiter = SpeedLimitSlider.RelativeValue;

            //if (Screen.SelectedShip != null)
            //{
            //    Ship ship = Screen.SelectedShip;
            //    ship.SetSpeedLimit(speedLimiter * ship.VelocityMaximum);
            //}

            //foreach (PredictionDebugPlatform platform in GetPredictionDebugPlatforms())
            //{
            //    platform.CanFire = CanDebugPlatformFire;
            //    if (platformSpeed.NotZero())
            //    {
            //        platform.Velocity.X = platformSpeed;
            //    }
            //}
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            try
            {
                TextFont = Fonts.Arial20Bold;
                SetTextCursor(50f, 50f, Color.Red);

                DrawString(Color.Yellow, Mode.ToString());

                TextCursor.Y -= (float)(Fonts.Arial20Bold.LineSpacing + 2) * 4;
                TextCursor.X += Fonts.Arial20Bold.TextWidth("XXXXXXXXXXXXXXXXXXXX");

                DrawDebugPrimitives(elapsed.RealTime.Seconds);
                TextFont = Fonts.Arial12Bold;
                switch (Mode)
                {
                    case DebugModes.Normal:       EmpireInfo();       break;
                    case DebugModes.DefenseCo:    DefcoInfo();        break;
                    case DebugModes.ThreatMatrix: ThreatMatrixInfo(); break;
                    case DebugModes.Targeting:    Targeting();        break;
                    case DebugModes.input:        InputDebug();       break;
                    case DebugModes.Tech:         Tech();             break;
                    case DebugModes.Pirates:      Pirates();          break;
                    case DebugModes.Remnants:     RemnantInfo();      break;
                    case DebugModes.Agents:       AgentsInfo();       break;
                    case DebugModes.Relationship: Relationships();    break;
                    case DebugModes.FleetMulti:   FleetMultipliers(); break;
                    case DebugModes.Tasks:        Tasks();            break;
                }

                base.Draw(batch, elapsed);
                ShipInfo();
            }
            catch { }
        }

        void Tech()
        {
            TextCursor.Y -= (float)(Fonts.Arial20Bold.LineSpacing + 2) * 4;
            int column = 0;
            foreach (Empire e in Universe.Empires)
            {
                if (e.IsFaction || e.data.Defeated)
                    continue;

                SetTextCursor(Win.X + 10 + 255 * column, Win.Y + 10, e.EmpireColor);
                DrawString(e.data.Traits.Name);

                if (e.data.DiplomaticPersonality != null)
                {
                    DrawString(e.data.DiplomaticPersonality.Name);
                    DrawString(e.data.EconomicPersonality.Name);
                }

                DrawString($"Corvettes: {e.canBuildCorvettes}");
                DrawString($"Frigates: {e.canBuildFrigates}");
                DrawString($"Cruisers: {e.canBuildCruisers}");
                DrawString($"Battleships: {e.CanBuildBattleships}");
                DrawString($"Capitals: {e.canBuildCapitals}");
                DrawString($"Bombers: {e.canBuildBombers}");
                DrawString($"Carriers: {e.canBuildCarriers}");
                DrawString($"Troopships: {e.canBuildTroopShips}");
                NewLine();
                if (e.Research.HasTopic)
                {
                    DrawString($"Research: {e.Research.Current.Progress:0}/{e.Research.Current.TechCost:0} ({e.Research.NetResearch.String()} / {e.Research.MaxResearchPotential.String()})");
                    DrawString("   --" + e.Research.Topic);
                    Ship bestShip = e.AI.TechChooser.LineFocus.BestCombatShip;
                    if (bestShip != null)
                    {
                        var neededTechs = bestShip.ShipData.TechsNeeded.Except(e.ShipTechs);
                        float techCost = 0;
                        foreach(var tech in neededTechs)
                            techCost += e.TechCost(tech);

                        DrawString($"Ship : {bestShip.Name}");
                        DrawString($"Hull : {bestShip.BaseHull.Role}");
                        DrawString($"Role : {bestShip.DesignRole}");
                        DrawString($"Str : {(int)bestShip.BaseStrength} - Tech : {techCost}");
                    }
                }
                DrawString("");
                if (ResearchText.TryGetValue(e.Name, out var empireLog))
                    for (int x = 0; x < empireLog.Count - 1; x++)
                    {
                        var text = empireLog[x];
                        DrawString(text ?? "Error");
                    }
                ++column;
            }
        }

        void Targeting()
        {
            IReadOnlyList<Ship> masterShipList = Screen.UState.Ships;
            for (int i = 0; i < masterShipList.Count; ++i)
            {
                Ship ship = masterShipList[i];
                if (ship == null || !ship.InFrustum || ship.AI.Target == null)
                    continue;

                foreach (Weapon weapon in ship.Weapons)
                {
                    var module = weapon.FireTarget as ShipModule;
                    if (module == null || module.GetParent() != ship.AI.Target || weapon.Tag_Beam || weapon.Tag_Guided)
                        continue;

                    Screen.DrawCircleProjected(module.Position, 8f, 6, Color.MediumVioletRed);
                    if (weapon.DebugLastImpactPredict.NotZero())
                    {
                        Vector2 impactNoError = weapon.ProjectedImpactPointNoError(module);
                        Screen.DrawLineProjected(weapon.Origin, weapon.DebugLastImpactPredict, Color.Yellow);

                        Screen.DrawCircleProjected(impactNoError, 22f, 10, Color.BlueViolet, 2f);
                        Screen.DrawStringProjected(impactNoError, 28f, Color.BlueViolet, "pip");
                        Screen.DrawLineProjected(impactNoError, weapon.DebugLastImpactPredict, Color.DarkKhaki, 2f);
                    }

                    // TODO: re-implement this
                    //Projectile projectile = ship.CopyProjectiles.FirstOrDefault(p => p.Weapon == weapon);
                    //if (projectile != null)
                    //{
                    //    Screen.DrawLineProjected(projectile.Center, projectile.Center + projectile.Velocity, Color.Red);
                    //}
                    break;
                }
            }
        }

        void DrawWeaponArcs(Ship ship)
        {
            foreach (Weapon w in ship.Weapons)
            {
                ShipModule m = w.Module;
                float facing = ship.Rotation + m.TurretAngleRads;
                float range = w.GetActualRange(ship.Loyalty);

                Vector2 moduleCenter = m.Position + m.WorldSize*0.5f;
                ShipDesignScreen.DrawWeaponArcs(ScreenManager.SpriteBatch, Screen, w, m, moduleCenter, 
                                                range * 0.25f, ship.Rotation, m.TurretAngle);

                DrawCircleImm(w.Origin, m.Radius/(float)Math.Sqrt(2), Color.Crimson);

                Ship targetShip = ship.AI.Target;
                GameObject target = targetShip;
                if (w.FireTarget is ShipModule sm)
                {
                    targetShip = sm.GetParent();
                    target = sm;
                }

                if (targetShip != null)
                {
                    bool inRange = ship.CheckRangeToTarget(w, target);
                    float bigArc = m.FieldOfFire*1.2f;
                    bool inBigArc = RadMath.IsTargetInsideArc(m.Position, target.Position,
                                                    ship.Rotation + m.TurretAngleRads, bigArc);
                    if (inRange && inBigArc) // show arc lines if we are close to arc edges
                    {
                        bool inArc = ship.IsInsideFiringArc(w, target.Position);

                        Color inArcColor = inArc ? Color.LawnGreen : Color.Orange;
                        DrawLineImm(m.Position, target.Position, inArcColor, 3f);

                        DrawLineImm(m.Position, m.Position + facing.RadiansToDirection() * range, Color.Crimson);
                        Vector2 left  = (facing - m.FieldOfFire * 0.5f).RadiansToDirection();
                        Vector2 right = (facing + m.FieldOfFire * 0.5f).RadiansToDirection();
                        DrawLineImm(m.Position, m.Position + left * range, Color.Crimson);
                        DrawLineImm(m.Position, m.Position + right * range, Color.Crimson);

                        string text = $"Target: {targetShip.Name}\nInArc: {inArc}";
                        DrawShadowStringProjected(m.Position, 0f, 1f, inArcColor, text);
                    }
                }
            }
        }
        
        void DrawSensorInfo(Ship ship)
        {
            foreach (Projectile p in ship.AI.TrackProjectiles)
            {
                float r = Math.Max(p.Radius, 32f);
                DrawCircleImm(p.Position, r, Color.Yellow, 1f);
            }
            foreach (Ship s in ship.AI.FriendliesNearby)
            {
                DrawCircleImm(s.Position, s.Radius, Color.Green, 1f);
            }
            foreach (Ship s in ship.AI.PotentialTargets)
            {
                DrawCircleImm(s.Position, s.Radius, Color.Red, 1f);
            }
        }

        void ShipInfo()
        {
            float y = (ScreenHeight - 700f).Clamped(100, 450);
            SetTextCursor(Win.X + 10, y, Color.White);

            // never show ship info in particle debug
            if (Mode == DebugModes.Particles)
                return;

            if (Screen.SelectedFleet != null)
            {
                Fleet fleet = Screen.SelectedFleet;
                DrawArrowImm(fleet.FinalPosition, fleet.FinalPosition+fleet.FinalDirection*200f, Color.OrangeRed);
                DrawCircleImm(fleet.FinalPosition, fleet.GetRelativeSize().Length(), Color.Red);
                DrawCircleImm(fleet.FinalPosition, ShipEngines.AtFinalFleetPos, Color.MediumVioletRed);

                foreach (Ship ship in fleet.Ships)
                    VisualizeShipGoal(ship, false);

                DrawString($"Fleet: {fleet.Name}  IsCoreFleet:{fleet.IsCoreFleet}");
                DrawString($"Ships:{fleet.Ships.Count} STR:{fleet.GetStrength()} Vmax:{fleet.SpeedLimit}");
                DrawString($"Distance: {fleet.AveragePosition().Distance(fleet.FinalPosition)}");
                DrawString($"FormationMove:{fleet.InFormationMove}  ReadyForWarp:{fleet.ReadyForWarp}");

                if (fleet.FleetTask != null)
                {
                    DrawString(fleet.FleetTask.Type.ToString());
                    if (fleet.FleetTask.TargetPlanet != null)
                        DrawString(fleet.FleetTask.TargetPlanet.Name);
                    DrawString($"Step: {fleet.TaskStep}");
                }
                else
                {
                    DrawCircleImm(fleet.AveragePosition(), 30, Color.Magenta);
                    DrawCircleImm(fleet.AveragePosition(), 60, Color.DarkMagenta);
                }

                if (fleet.Ships.NotEmpty)
                {
                    DrawString("");
                    DrawString("-- First Ship AIState:");
                    DrawShipOrderQueueInfo(fleet.Ships.First);
                    DrawWayPointsInfo(fleet.Ships.First);
                }
            }
            // only show CurrentGroup if we selected more than one ship
            else if (Screen.CurrentGroup != null && Screen.SelectedShipList.Count > 1)
            {
                ShipGroup group = Screen.CurrentGroup;
                DrawArrowImm(group.FinalPosition, group.FinalPosition+group.FinalDirection*200f, Color.OrangeRed);
                foreach (Ship ship in group.Ships)
                    VisualizeShipGoal(ship, false);

                DrawString($"ShipGroup ({group.CountShips})  x {(int)group.FinalPosition.X} y {(int)group.FinalPosition.Y}");
                DrawString("");
                DrawString("-- First Ship AIState:");
                DrawShipOrderQueueInfo(Screen.SelectedShipList.First);
            }
            else if (Screen.SelectedShip != null)
            {
                Ship ship = Screen.SelectedShip;

                DrawString($"Ship {ship.ShipName}  x {ship.Position.X:0} y {ship.Position.Y:0}");
                DrawString($"VEL: {ship.Velocity.Length():0}  "
                          +$"LIMIT: {ship.SpeedLimit:0}  "
                          +$"Vmax: {ship.VelocityMax:0}  "
                          +$"FTLMax: {ship.MaxFTLSpeed:0}  "
                          +$"{ship.WarpState}  {ship.ThrustThisFrame}  {ship.DebugThrustStatus}");

                DrawString($"ENG:{ship.ShipEngines.EngineStatus} FTL:{ship.ShipEngines.ReadyForWarp} FLEET:{ship.ShipEngines.ReadyForFormationWarp}");

                VisualizeShipOrderQueue(ship);
                if (Screen.UState.IsSystemViewOrCloser && Mode == DebugModes.Normal && !Screen.ShowShipNames)
                    DrawWeaponArcs(ship);
                DrawSensorInfo(ship);

                DrawString($"On Defense: {ship.Loyalty.AI.DefensiveCoordinator.Contains(ship)}");
                if (ship.Fleet != null)
                {
                    DrawString($"Fleet: {ship.Fleet.Name}  {(int)ship.Fleet.FinalPosition.X}x{(int)ship.Fleet.FinalPosition.Y}  Vmax:{ship.Fleet.SpeedLimit}");
                }

                DrawString(ship.Pool != null ? "In Force Pool" : "NOT In Force Pool");

                if (ship.AI.State == AIState.SystemDefender)
                {
                    SolarSystem systemToDefend = ship.AI.SystemToDefend;
                    DrawString($"Defending {systemToDefend?.Name ?? "Awaiting Order"}");
                }

                DrawString(ship.System == null ? "Deep Space" : $"System: {ship.System.Name}");
                var influence = ship.GetProjectorInfluenceEmpires().Select(e => e.Name);
                DrawString("Influence: " + (ship.IsInFriendlyProjectorRange ? "Friendly"
                                         :  ship.IsInHostileProjectorRange  ? "Hostile" : "Neutral")
                                         + " | " + string.Join(",", influence));

                string gravityWell = ship.Universe.GravityWells ? ship?.System?.IdentifyGravityWell(ship)?.Name : "disabled";
                DrawString($"GravityWell: {gravityWell}   Inhibited:{ship.IsInhibitedByUnfriendlyGravityWell}");

                var combatColor = ship.InCombat ? Color.Green : Color.LightPink;
                var inCombat = ship.InCombat ? ship.AI.BadGuysNear ? "InCombat" : "ERROR" : "NotInCombat";
                DrawString(combatColor, $"{inCombat} PriTarget:{ship.AI.HasPriorityTarget} PriOrder:{ship.AI.HasPriorityOrder}");
                if (ship.AI.IgnoreCombat)
                    DrawString(Color.Pink, "Ignoring Combat!");
                if (ship.IsFreighter)
                {
                    DrawString($"Trade Timer:{ship.TradeTimer}");
                    ShipAI.ShipGoal g = ship.AI.OrderQueue.PeekLast;
                    if (g?.Trade != null && g.Trade.BlockadeTimer < 120)
                        DrawString($"Blockade Timer:{g.Trade.BlockadeTimer}");
                }

                if (ship.AI.Target is Ship shipTarget)
                {
                    SetTextCursor(Win.X + 200, 620f, Color.White);
                    DrawString("Target: "+ shipTarget.Name);
                    DrawString(shipTarget.Active ? "Active" : "Error - Active");
                }
                float currentStr = ship.GetStrength(), baseStr = ship.BaseStrength;
                DrawString($"Strength: {currentStr.String(0)} / {baseStr.String(0)}  ({(currentStr/baseStr).PercentString()})");
                DrawString($"HP: {ship.Health.String(0)} / {ship.HealthMax.String(0)}  ({ship.HealthPercent.PercentString()})");
                DrawString($"Mass: {ship.Mass.String(0)}");
                DrawString($"EMP Damage: {ship.EMPDamage} / {ship.EmpTolerance} :Recovery: {ship.EmpRecovery}");
                DrawString($"IntSlots: {ship.ActiveInternalModuleSlots}/{ship.NumInternalSlots}  ({ship.InternalSlotsHealthPercent.PercentString()})");
                DrawString($"DPS: {ship.TotalDps}");
                SetTextCursor(Win.X + 250, 600f, Color.White);
                foreach (KeyValuePair<SolarSystem, SystemCommander> entry in ship.Loyalty.AI.DefensiveCoordinator.DefenseDict)
                    foreach (var defender in entry.Value.OurShips) {
                        if (defender.Key == ship.Id)
                            DrawString(entry.Value.System.Name);
                    }
            }
            else if (Screen.SelectedShipList.NotEmpty)
            {
                IReadOnlyList<Ship> ships = Screen.SelectedShipList;
                foreach (Ship ship in ships)
                    VisualizeShipGoal(ship, false);

                DrawString($"SelectedShips: {ships.Count} ");
                DrawString($"Total Str: {ships.Sum(s => s.BaseStrength).String(1)} ");
            }
            VisualizePredictionDebugger();
        }

        IEnumerable<PredictionDebugPlatform> GetPredictionDebugPlatforms()
        {
            IReadOnlyList<Ship> ships = Screen.UState.Ships;
            for (int i = 0; i < ships.Count; ++i)
                if (ships[i] is PredictionDebugPlatform platform)
                    yield return platform;
        }

        void VisualizePredictionDebugger()
        {
            foreach (PredictionDebugPlatform platform in GetPredictionDebugPlatforms())
            {
                //DrawString($"Platform Accuracy: {(int)(platform.AccuracyPercent*100)}%");
                foreach (PredictedLine line in platform.Predictions)
                {
                    DrawLineImm(line.Start, line.End, Color.YellowGreen);
                    //DrawCircleImm(line.End, 75f, Color.Red);
                }
            }
        }

        void VisualizeShipGoal(Ship ship, bool detailed = true)
        {
            if (ship?.AI.OrderQueue.NotEmpty == true)
            {
                ShipAI.ShipGoal goal = ship.AI.OrderQueue.PeekFirst;
                Vector2 pos = ship.AI.GoalTarget;

                DrawLineImm(ship.Position, pos, Color.YellowGreen);
                //if (detailed) DrawCircleImm(pos, 1000f, Color.Yellow);
                //DrawCircleImm(pos, 75f, Color.Maroon);

                Vector2 thrustTgt = ship.AI.ThrustTarget;
                if (detailed && thrustTgt.NotZero())
                {
                    DrawLineImm(pos, thrustTgt, Color.Orange);
                    DrawLineImm(ship.Position, thrustTgt, Color.Orange);
                    DrawCircleImm(thrustTgt, 40f, Color.MediumVioletRed, 2f);
                }

                // goal direction arrow
                DrawArrowImm(pos, pos + goal.Direction * 50f, Color.Wheat);

                // velocity arrow
                if (detailed)
                    DrawArrowImm(ship.Position, ship.Position+ship.Velocity, Color.OrangeRed);

                // ship direction arrow
                DrawArrowImm(ship.Position, ship.Position+ship.Direction*200f, Color.GhostWhite);
            }
            if (ship?.AI.HasWayPoints == true)
            {
                WayPoint[] wayPoints = ship.AI.CopyWayPoints();
                if (wayPoints.Length > 0)
                {
                    DrawLineImm(ship.Position, wayPoints[0].Position, Color.ForestGreen);
                    for (int i = 1; i < wayPoints.Length; ++i) // draw WayPoints chain
                        DrawLineImm(wayPoints[i-1].Position, wayPoints[i].Position, Color.ForestGreen);
                }
            }
            if (ship?.Fleet != null)
            {
                Vector2 formationPos = ship.Fleet.GetFormationPos(ship);
                Vector2 finalPos = ship.Fleet.GetFinalPos(ship);
                Color color = Color.Magenta.Alpha(0.5f);
                DrawCircleImm(finalPos, ship.Radius*0.5f, Color.Blue.Alpha(0.5f), 0.8f);
                DrawCircleImm(formationPos, ship.Radius-10, color, 0.8f);
                DrawLineImm(ship.Position, formationPos, color, 0.8f);
            }
        }

        void VisualizeShipOrderQueue(Ship ship)
        {
            VisualizeShipGoal(ship);
            DrawShipOrderQueueInfo(ship);
            DrawWayPointsInfo(ship);
        }

        void DrawShipOrderQueueInfo(Ship ship)
        {
            if (ship.AI.OrderQueue.NotEmpty)
            {
                ShipAI.ShipGoal[] goals = ship.AI.OrderQueue.ToArray();
                Vector2 pos = ship.AI.GoalTarget;
                DrawString($"AIState: {ship.AI.State}  CombatState: {ship.AI.CombatState}  FromTarget: {pos.Distance(ship.Position).String(0)}");
                DrawString($"OrderQueue ({goals.Length}):");
                for (int i = 0; i < goals.Length; ++i)
                {
                    ShipAI.ShipGoal g = goals[i];
                    DrawString($"  {i+1}:  {g.Plan}  {g.MoveOrder}");
                }
            }
            else
            {
                DrawString($"AIState: {ship.AI.State}  CombatState: {ship.AI.CombatState}");
                DrawString("OrderQueue is EMPTY");
            }
        }

        void DrawWayPointsInfo(Ship ship)
        {
            if (ship.AI.HasWayPoints)
            {
                WayPoint[] wayPoints = ship.AI.CopyWayPoints();
                DrawString($"WayPoints ({wayPoints.Length}):");
                for (int i = 0; i < wayPoints.Length; ++i)
                    DrawString($"  {i+1}:  {wayPoints[i].Position}");
            }
        }

        void Pirates()
        {
            int column = 0;
            foreach (Empire e in Universe.PirateFactions)
            {
                if (e.data.Defeated)
                    continue;

                IReadOnlyList<Goal> goals = e.Pirates.Owner.AI.Goals;
                SetTextCursor(Win.X + 10 + 255 * column, Win.Y + 95, e.EmpireColor);
                DrawString("------------------------");
                DrawString(e.Name);
                DrawString("------------------------");
                DrawString($"Level: {e.Pirates.Level}");
                DrawString($"Pirate Bases Goals: {goals.Count(g => g.Type == GoalType.PirateBase)}");
                DrawString($"Spawned Ships: {e.Pirates.SpawnedShips.Count}");
                NewLine();
                DrawString($"Payment Management Goals ({goals.Count(g => g.Type == GoalType.PirateDirectorPayment)})");
                DrawString("---------------------------------------------"); foreach (Goal g in goals)
                {
                    if (g.Type == GoalType.PirateDirectorPayment)
                    {
                        Empire target     = g.TargetEmpire;
                        string targetName = target.Name;
                        int threatLevel   = e.Pirates.ThreatLevelFor(g.TargetEmpire);
                        DrawString(target.EmpireColor, $"Payment Director For: {targetName}, Threat Level: {threatLevel}, Timer: {e.Pirates.PaymentTimerFor(target)}");
                    }
                }

                NewLine();
                DrawString($"Raid Management Goals ({goals.Count(g => g.Type == GoalType.PirateDirectorRaid)})");
                DrawString("---------------------------------------------");
                foreach (Goal g in goals)
                {
                    if (g.Type == GoalType.PirateDirectorRaid)
                    {
                        Empire target = g.TargetEmpire;
                        string targetName = target.Name;
                        int threatLevel = e.Pirates.ThreatLevelFor(g.TargetEmpire);
                        DrawString(target.EmpireColor, $"Raid Director For: {targetName}, Threat Level: {threatLevel}");
                    }
                }

                NewLine();
                DrawString($"Ongoing Raids ({goals.Count(g => g.IsRaid)}/{e.Pirates.Level})");
                DrawString("---------------------------------------------");
                foreach (Goal g in goals)
                {
                    if (g.IsRaid)
                    {
                        Empire target = g.TargetEmpire;
                        string targetName = target.Name;
                        Ship targetShip = g.TargetShip;
                        string shipName = targetShip?.Name ?? "None";
                        DrawString(target.EmpireColor, $"{g.Type} vs. {targetName}, Target Ship: {shipName} in {targetShip?.SystemName ?? "None"}");
                    }
                }

                NewLine();

                DrawString($"Base Defense Goals ({goals.Count(g => g.Type == GoalType.PirateDefendBase)})");
                DrawString("---------------------------------------------");
                foreach (Goal g in goals)
                {
                    if (g.Type == GoalType.PirateDefendBase)
                    {
                        Ship targetShip = g.TargetShip;
                        string shipName = targetShip?.Name ?? "None";
                        DrawString($"Defending {shipName} in {targetShip?.SystemName ?? "None"}");
                    }
                }

                NewLine();

                DrawString($"Fighter Designs We Can Launch ({e.Pirates.ShipsWeCanBuild.Count})");
                DrawString("---------------------------------------------");
                foreach (string shipName in e.Pirates.ShipsWeCanBuild)
                    DrawString(shipName);

                NewLine();

                DrawString($"Ship Designs We Can Spawn ({e.Pirates.ShipsWeCanSpawn.Count})");
                DrawString("---------------------------------------------");
                foreach (string shipName in e.Pirates.ShipsWeCanSpawn)
                    DrawString(shipName);

                column += 3;
            }
        }

        void AgentsInfo()
        {
            int column = 0;
            foreach (Empire e in Universe.MajorEmpires)
            {
                if (e.data.Defeated)
                    continue;

                SetTextCursor(Win.X + 10 + 255 * column, Win.Y + 95, e.EmpireColor);
                DrawString("------------------------");
                DrawString(e.Name);
                DrawString("------------------------");

                NewLine();
                DrawString($"Agent list ({e.data.AgentList.Count}):");
                DrawString("------------------------");
                foreach (Agent agent in e.data.AgentList.Sorted(a => a.Level))
                {
                    Empire target = Universe.GetEmpireByName(agent.TargetEmpire);
                    Color color   = target?.EmpireColor ?? e.EmpireColor;
                    DrawString(color, $"Level: {agent.Level}, Mission: {agent.Mission}, Turns: {agent.TurnsRemaining}");
                }

                column += 1;
            }
        }

        void Tasks()
        {
            int column = 0;
            foreach (Empire e in Universe.NonPlayerMajorEmpires)
            {
                if (e.data.Defeated)
                    continue;

                SetTextCursor(Win.X + 10 + 300 * column, Win.Y + 200, e.EmpireColor);
                DrawString("--------------------------");
                DrawString(e.Name);
                DrawString($"{e.Personality}");
                DrawString($"Average War Grade: {e.GetAverageWarGrade()}");
                DrawString("----------------------------");
                int taskEvalLimit   = e.IsAtWarWithMajorEmpire ? (int)e.GetAverageWarGrade().LowerBound(3) : 10;
                int taskEvalCounter = 0;
                var tasks = e.AI.GetTasks().Filter(t => !t.QueuedForRemoval).OrderByDescending(t => t.Priority)
                    .ThenByDescending(t => t.MinimumTaskForceStrength).ToArr();

                var tasksWithFleets = tasks.Filter(t => t.Fleet != null);
                if (tasksWithFleets.Length > 0)
                {
                    DrawString(Color.Gray, "-----Tasks with Fleets------");
                    for (int i = tasksWithFleets.Length - 1; i >= 0; i--)
                    {
                        MilitaryTask task = tasksWithFleets[i];
                        DrawTask(task, e);
                    }
                }

                var tasksForEval = tasks.Filter(t => t.NeedEvaluation);
                NewLine();
                DrawString(Color.Gray, "--Tasks Being Evaluated ---");
                for (int i = tasksForEval.Length - 1; i >= 0; i--)
                {
                    if (taskEvalCounter == taskEvalLimit)
                    {
                        NewLine();
                        DrawString(Color.Gray, "--------Queued Tasks--------");
                    }

                    MilitaryTask task = tasksForEval[i];
                    DrawTask(task, e);
                    if (task.NeedEvaluation)
                        taskEvalCounter += 1;
                }

                column += 1;
            }

            // Local Method
            void DrawTask(MilitaryTask t, Empire e)
            {
                Color color   = t.TargetEmpire?.EmpireColor ?? e.EmpireColor;
                string target = t.TargetPlanet?.Name ?? t.TargetSystem?.Name ?? "";
                string fleet  = t.Fleet != null ? $"Fleet Step: {t.Fleet.TaskStep}" : "";
                float str     = t.Fleet?.GetStrength() ?? t.MinimumTaskForceStrength;
                DrawString(color, $"({t.Priority}) {t.Type}, {target}, str: {(int)str}, {fleet}");
            }
        }

        void Relationships()
        {
            int column = 0;
            foreach (Empire e in Universe.NonPlayerMajorEmpires)
            {
                if (e.data.Defeated)
                    continue;

                SetTextCursor(Win.X + 10 + 255 * column, Win.Y + 95, e.EmpireColor);
                DrawString("--------------------------");
                DrawString(e.Name);
                DrawString($"{e.Personality}");
                DrawString($"Average War Grade: {e.GetAverageWarGrade()}");
                DrawString("----------------------------");
                foreach (Relationship rel in e.AllRelations)
                {
                    if (rel.Them.IsFaction || GlobalStats.RestrictAIPlayerInteraction && rel.Them.isPlayer || rel.Them.data.Defeated)
                        continue;

                    DrawString(rel.Them.EmpireColor, $"{rel.Them.Name}");
                    DrawString(rel.Them.EmpireColor, $"Posture: {rel.Posture}");
                    DrawString(rel.Them.EmpireColor, $"Trust (A/U/T)   : {rel.AvailableTrust.String(2)}/{rel.TrustUsed.String(2)}/{rel.Trust.String(2)}");
                    DrawString(rel.Them.EmpireColor, $"Anger Diplomatic: {rel.Anger_DiplomaticConflict.String(2)}");
                    DrawString(rel.Them.EmpireColor, $"Anger Border    : {rel.Anger_FromShipsInOurBorders.String(2)}");
                    DrawString(rel.Them.EmpireColor, $"Anger Military  : {rel.Anger_MilitaryConflict.String(2)}");
                    DrawString(rel.Them.EmpireColor, $"Anger Territory : {rel.Anger_TerritorialConflict.String(2)}");
                    string nap   = rel.Treaty_NAPact      ? "NAP "      : "";
                    string trade = rel.Treaty_Trade       ? ",Trade "   : "";
                    string open  = rel.Treaty_OpenBorders ? ",Borders " : "";
                    string ally  = rel.Treaty_Alliance    ? ",Allied "  : "";
                    string peace = rel.Treaty_Peace       ? "Peace"     : "";
                    DrawString(rel.Them.EmpireColor, $"Treaties: {nap}{trade}{open}{ally}{peace}");
                    if (rel.NumTechsWeGave > 0)
                        DrawString(rel.Them.EmpireColor, $"Techs We Gave Them: {rel.NumTechsWeGave}");

                    if (rel.ActiveWar != null)
                        DrawString(rel.Them.EmpireColor, "*** At War! ***");

                    if (rel.PreparingForWar)
                        DrawString(rel.Them.EmpireColor, "*** Preparing for War! ***");
                    if (rel.PreparingForWar)
                        DrawString(rel.Them.EmpireColor, $"*** {rel.PreparingForWarType} ***");

                    DrawString(e.EmpireColor, "----------------------------");
                }

                column += 1;
            }
        }

        void FleetMultipliers()
        {
            int column = 0;
            foreach (Empire e in Universe.ActiveNonPlayerMajorEmpires)
            {
                if (e.data.Defeated)
                    continue;

                SetTextCursor(Win.X + 10 + 255 * column, Win.Y + 95, e.EmpireColor);
                DrawString("--------------------------");
                DrawString(e.Name);
                DrawString($"{e.Personality}");
                DrawString("----------------------------");
                NewLine(2);
                DrawString("Remnants Strength Multipliers");
                DrawString("---------------------------");
                Empire remnants = Universe.Remnants;
                DrawString(remnants.EmpireColor, $"{remnants.Name}: {e.GetFleetStrEmpireMultiplier(remnants).String(2)}");
                NewLine(2);
                DrawString("Empire Strength Multipliers");
                DrawString("---------------------------");
                foreach (Empire empire in Universe.ActiveMajorEmpires.Filter(empire => empire != e))
                    DrawString($"{empire.Name}: {e.GetFleetStrEmpireMultiplier(empire).String(2)}");

                NewLine(2);
                DrawString("Pirates Strength Multipliers");
                DrawString("---------------------------");
                foreach (Empire empire in Universe.PirateFactions.Filter(faction => faction != Universe.Unknown))
                    DrawString(empire.EmpireColor, $"{empire.Name}: {e.GetFleetStrEmpireMultiplier(empire).String(2)}");

                column += 1;
            }
        }

        void RemnantInfo()
        {
            Empire e = Universe.Remnants;
            SetTextCursor(Win.X + 10 + 255, Win.Y + 250, e.EmpireColor);
            DrawString($"Remnant Story: {e.Remnants.Story}");
            DrawString(!e.Remnants.Activated
                ? $"Trigger Progress: {e.Remnants.StoryTriggerKillsXp}/{e.Remnants.ActivationXpNeeded.String()}"
                : $"Level Up Stardate: {e.Remnants.NextLevelUpDate}");

            DrawString(!e.Remnants.Hibernating
                ? $"Next Hibernation in: {e.Remnants.NextLevelUpDate - e.Remnants.NeededHibernationTurns / 10f}"
                : $"Hibernating for: {e.Remnants.HibernationTurns} turns");

            string activatedString = e.Remnants.Activated ? "Yes" : "No";
            activatedString        = e.data.Defeated ? "Defeated" : activatedString;
            DrawString($"Activated: {activatedString}");
            DrawString($"Level: {e.Remnants.Level}");
            DrawString($"Resources: {e.Remnants.Production.String()}");
            NewLine();
            DrawString("Empires Population and Strength:");
            for (int i = 0; i < Universe.MajorEmpires.Length; i++)
            {
                Empire empire = Universe.MajorEmpires[i];
                if (!empire.data.Defeated)
                    DrawString(empire.EmpireColor, $"{empire.data.Name} - Pop: {empire.TotalPopBillion.String()}, Strength: {empire.CurrentMilitaryStrength.String(0)}");
            }

            var empiresList = GlobalStats.RestrictAIPlayerInteraction ? Universe.NonPlayerMajorEmpires.Filter(emp => !emp.data.Defeated)
                                                                      : Universe.MajorEmpires.Filter(emp => !emp.data.Defeated);

            NewLine();
            float averagePop = empiresList.Average(empire => empire.TotalPopBillion);
            float averageStr = empiresList.Average(empire => empire.CurrentMilitaryStrength);
            DrawString($"AI Empire Average Pop:         {averagePop.String(1)}");
            DrawString($"AI Empire Average Strength: {averageStr.String(0)}");

            NewLine();
            Empire bestPop  = empiresList.FindMax(empire => empire.TotalPopBillion);
            Empire bestStr  = empiresList.FindMax(empire => empire.CurrentMilitaryStrength);
            Empire worstStr = empiresList.FindMin(empire => empire.CurrentMilitaryStrength);

            float diffFromAverageScore    = bestPop.TotalPopBillion / averagePop.LowerBound(1) * 100;
            float diffFromAverageStrBest  = bestStr.CurrentMilitaryStrength / averageStr.LowerBound(1) * 100;
            float diffFromAverageStrWorst = worstStr.CurrentMilitaryStrength / averageStr.LowerBound(1) * 100;

            DrawString(bestPop.EmpireColor, $"Highest Pop Empire: {bestPop.data.Name} ({(diffFromAverageScore - 100).String(1)}% above average)");
            DrawString(bestStr.EmpireColor, $"Strongest Empire:   {bestStr.data.Name} ({(diffFromAverageStrBest - 100).String(1)}% above average)");
            DrawString(worstStr.EmpireColor, $"Weakest Empire:     {worstStr.data.Name} ({(diffFromAverageStrWorst - 100).String(1)}% below average)");

            NewLine();
            DrawString("Goals:");
            foreach (Goal goal in e.AI.Goals)
            {
                if (goal.TargetPlanet != null)
                {
                    Color color = goal.TargetPlanet.Owner?.EmpireColor ?? e.EmpireColor;
                    DrawString(color, $"{goal.Type}, Target Planet: {goal.TargetPlanet.Name}");
                }
                else
                {
                    DrawString($"{goal.Type}");
                }
            }

            NewLine();
            DrawString("Fleets:");
            foreach (Fleet fleet in e.GetFleetsDict().Values)
            {
                if (fleet.FleetTask == null)
                    continue;

                Color color = fleet.FleetTask.TargetPlanet?.Owner?.EmpireColor ?? e.EmpireColor;
                DrawString(color,$"Target Planet: {fleet.FleetTask.TargetPlanet?.Name ?? ""}, Ships: {fleet.Ships.Count}" +
                                  $", str: {fleet.GetStrength().String()}, Task Step: {fleet.TaskStep}");
            }
        }

        void EmpireInfo()
        {
            int column = 0;
            foreach (Empire e in Universe.MajorEmpires)
            {
                if (e.data.Defeated)
                    continue;
                EmpireAI eAI = e.AI;
                SetTextCursor(Win.X + 10 + 255 * column, Win.Y + 95, e.EmpireColor);
                DrawString(e.data.Traits.Name);

                if (e.data.DiplomaticPersonality != null)
                {
                    DrawString(e.data.DiplomaticPersonality.Name);
                    DrawString(e.data.EconomicPersonality.Name);
                }
                DrawString($"Money: {e.Money.String()} A:({e.GetActualNetLastTurn().String()}) T:({e.GrossIncome.String()})");
                float normalizedBudget = e.NormalizedMoney;
                float treasuryGoal = e.AI.TreasuryGoal(normalizedBudget);
               
                DrawString($"Treasury Goal: {(int)eAI.ProjectedMoney} {(int)( e.AI.CreditRating * 100)}%");
                float taxRate = e.data.TaxRate * 100f;
                
                var ships = e.OwnedShips;
                DrawString($"Threat : av:{eAI.ThreatLevel:#0.00} $:{eAI.EconomicThreat:#0.00} " +
                           $"b:{eAI.BorderThreat:#0.00} e:{eAI.EnemyThreat:#0.00}");
                DrawString("Tax Rate:     "+taxRate.ToString("#.0")+"%");
                DrawString($"War Maint:  ({(int)e.AI.BuildCapacity}) Shp:{(int)e.TotalWarShipMaintenance} " +
                           $"Trp:{(int)(e.TotalTroopShipMaintenance + e.TroopCostOnPlanets)}");
                var warShips = ships.Filter(s => s.DesignRoleType == RoleType.Warship ||
                                                 s.DesignRoleType == RoleType.WarSupport ||
                                                 s.DesignRoleType == RoleType.Troop);
                DrawString($"   #:({warShips.Length})" +
                           $" f{warShips.Count(warship => warship?.DesignRole == RoleName.fighter || warship?.DesignRole == RoleName.corvette)}" +
                           $" g{warShips.Count(warship => warship?.DesignRole == RoleName.frigate || warship.DesignRole == RoleName.prototype)}" +
                           $" c{warShips.Count(warship => warship?.DesignRole == RoleName.cruiser)}" +
                           $" b{warShips.Count(warship => warship?.DesignRole == RoleName.battleship)}" +
                           $" c{warShips.Count(warship => warship?.DesignRole == RoleName.capital)}" +
                           $" v{warShips.Count(warship => warship?.DesignRole == RoleName.carrier)}" +
                           $" m{warShips.Count(warship => warship?.DesignRole == RoleName.bomber)}"
                           );
                DrawString($"Civ Maint:  " +
                           $"({(int)e.AI.CivShipBudget}) {(int)e.TotalCivShipMaintenance} " +
                           $"#{ships.Count(freighter => freighter?.DesignRoleType == RoleType.Civilian)} " +
                           $"Inc({e.AverageTradeIncome})");
                DrawString($"Other Ship Maint:  Orb:{(int)e.TotalOrbitalMaintenance} - Sup:{(int)e.TotalEmpireSupportMaintenance}" +
                           $" #{ships.Count(warship => warship?.DesignRole == RoleName.platform || warship?.DesignRole == RoleName.station)}");
                DrawString($"Scrap:  {(int)e.TotalMaintenanceInScrap}");

                DrawString($"Build Maint:   ({(int)e.data.ColonyBudget}) {(int)e.TotalBuildingMaintenance}");
                DrawString($"Spy Count:     ({(int)e.data.SpyBudget}) {e.data.AgentList.Count}");
                DrawString("Spy Defenders: "+e.data.AgentList.Count(defenders => defenders.Mission == AgentMission.Defending));
                DrawString("Planet Count:  "+e.GetPlanets().Count);
                if (e.Research.HasTopic)
                {
                    DrawString($"Research: {e.Research.Current.Progress:0}/{e.Research.Current.TechCost:0}({e.Research.NetResearch.String()})");
                    DrawString("   --" + e.Research.Topic);
                }
                else
                {
                    NewLine(2);
                }

                NewLine(3);
                DrawString("Total Pop: "+ e.TotalPopBillion.String(1) 
                                        + "/" + e.MaxPopBillion.String(1) 
                                        + "/" + e.GetTotalPopPotential().String(1));

                DrawString("Gross Food: "+ e.GetGrossFoodPerTurn().String());
                DrawString("Military Str: "+ (int)e.CurrentMilitaryStrength);
                DrawString("Offensive Str: " + (int)e.OffensiveStrength);
                DrawString($"Fleets: Str: {(int)e.AIManagedShips.InitialStrength} Avail: {e.AIManagedShips.InitialReadyFleets}");
                for (int x = 0; x < e.AI.Goals.Count; x++)
                {
                    Goal g = e.AI.Goals[x];
                    if (g is MarkForColonization)
                    {
                        NewLine();
                        DrawString($"{g.TypeName} {g.TargetPlanet.Name}" +
                                   $" (x{e.GetFleetStrEmpireMultiplier(g.TargetEmpire).String(1)})");

                        DrawString(15f, $"Step: {g.StepName}");
                        if (g.FinishedShip != null && g.FinishedShip.Active)
                            DrawString(15f, "Has ship");
                    }
                }

                MilitaryTask[] tasks = e.AI.GetTasks().ToArr();
                for (int j = 0; j < tasks.Length; j++)
                {
                    MilitaryTask task = tasks[j];
                    string sysName = "Deep Space";
                    for (int i = 0; i < e.Universum.Systems.Count; i++)
                    {
                        SolarSystem sys = e.Universum.Systems[i];
                        if (task.AO.InRadius(sys.Position, sys.Radius))
                            sysName = sys.Name;
                    }

                    NewLine();
                    var planet =task.TargetPlanet?.Name ?? "";
                    DrawString($"FleetTask: {task.Type} {sysName} {planet}");
                    DrawString(15f, $"Priority:{task.Priority}");
                    float ourStrength = task.Fleet?.GetStrength() ?? task.MinimumTaskForceStrength;
                    string strMultiplier = $" (x{e.GetFleetStrEmpireMultiplier(task.TargetEmpire).String(1)})";
                    
                    DrawString(15f, $"Strength: Them: {(int)task.EnemyStrength} Us: {(int)ourStrength} {strMultiplier}");
                    if (task.WhichFleet != -1)
                    {
                        DrawString(15f, "Fleet: " + task.Fleet?.Name);
                        DrawString(15f, $" Ships: {task.Fleet?.Ships.Count} CanWin: {task.Fleet?.CanTakeThisFight(task.EnemyStrength, task,true)}");
                    }
                }

                NewLine();
                foreach (Relationship rel in e.AllRelations)
                {
                    string plural = rel.Them.data.Traits.Plural;
                    TextColor = rel.Them.EmpireColor;
                    if (rel.Treaty_NAPact)
                        DrawString(15f, "NA Pact with " + plural);

                    if (rel.Treaty_Trade)
                        DrawString(15f, "Trade Pact with " + plural);

                    if (rel.Treaty_OpenBorders)
                        DrawString(15f, "Open Borders with " + plural);

                    if (rel.AtWar)
                        DrawString(15f, $"War with {plural} ({rel.ActiveWar?.WarType})");
                }
                ++column;
                if (Screen.SelectedSystem != null)
                {
                    SetTextCursor(Win.X + 10, 600f, Color.White);
                    foreach (Ship ship in Screen.SelectedSystem.ShipList)
                    {
                        DrawString(ship?.Active == true ? ship.Name : ship?.Name + " (inactive)");
                    }

                    SetTextCursor(Win.X + 300, 600f, Color.White);
                }
            }
        }

        void DefcoInfo()
        {
            foreach (Empire e in Universe.Empires)
            {
                DefensiveCoordinator defco = e.AI.DefensiveCoordinator;
                foreach (var kv in defco.DefenseDict)
                {
                    Screen.DrawCircleProjectedZ(kv.Value.System.Position, kv.Value.RankImportance * 100, e.EmpireColor, 6);
                    Screen.DrawCircleProjectedZ(kv.Value.System.Position, kv.Value.IdealShipStrength * 10, e.EmpireColor, 3);
                    Screen.DrawCircleProjectedZ(kv.Value.System.Position, kv.Value.TroopsWanted * 100, e.EmpireColor, 4);
                }
                foreach(Ship ship in defco.DefensiveForcePool)
                    Screen.DrawCircleProjectedZ(ship.Position, 50f, e.EmpireColor, 6);

                foreach(AO ao in e.AI.AreasOfOperations)
                    Screen.DrawCircleProjectedZ(ao.Center, ao.Radius, e.EmpireColor, 16);
            }
        }

        void ThreatMatrixInfo()
        {
            foreach (Empire e in Universe.Empires)
            {
                var pins = e.AI.ThreatMatrix.GetPins();
                for (int i = 0; i < pins.Length; i++)
                {
                    ThreatMatrix.Pin pin = pins[i];
                    if (pin?.Ship == null || pin.Position == Vector2.Zero)
                        continue;
                    float increaser = (int) e.Universum.Screen.viewState / 100f;
                    Screen.DrawCircleProjected(pin.Position,
                        increaser + pin.Ship.Radius, 6, e.EmpireColor);

                    if (!pin.InBorders) continue;
                    Screen.DrawCircleProjected(pin.Position,
                        increaser + pin.Ship.Radius, 3, e.EmpireColor);
                }
            }
        }

        void InputDebug()
        {
            DrawString($"Mouse Moved {Screen.Input.MouseMoved}");

            DrawString($"RightHold Held  {Screen.Input.RightHold.IsHolding}");
            DrawString($"RightHold Time  {Screen.Input.RightHold.Time}");
            DrawString($"RightHold Start {Screen.Input.RightHold.StartPos}");
            DrawString($"RightHold End   {Screen.Input.RightHold.EndPos}");

            DrawString($"LeftHold Held   {Screen.Input.LeftHold.IsHolding}");
            DrawString($"LeftHold Time   {Screen.Input.LeftHold.Time}");
            DrawString($"LeftHold Start  {Screen.Input.LeftHold.StartPos}");
            DrawString($"LeftHold End    {Screen.Input.LeftHold.EndPos}");
        }



        public void DefenseCoLogsNull(bool found, Ship ship, SolarSystem systemToDefend)
        {
            if (Mode != DebugModes.DefenseCo)
                return;
            if (!found && ship.Active)
            {
                Log.Info(ConsoleColor.Yellow, systemToDefend == null
                                    ? "SystemCommander: Remove : SystemToDefend Was Null"
                                    : "SystemCommander: Remove : Ship Not Found in Any");
            }
        }

        public void DefenseCoLogsMultipleSystems(Ship ship)
        {
            if (Mode != DebugModes.DefenseCo) return;
            Log.Info(color: ConsoleColor.Yellow, text: $"SystemCommander: Remove : Ship Was in Multiple SystemCommanders: {ship}");
        }
        public void DefenseCoLogsNotInSystem()
        {
            if (Mode != DebugModes.DefenseCo) return;
            Log.Info(color: ConsoleColor.Yellow, text: "SystemCommander: Remove : Not in SystemCommander");
        }

        public void DefenseCoLogsNotInPool()
        {
            if (Mode != DebugModes.DefenseCo) return;
            Log.Info(color: ConsoleColor.Yellow, text: "DefensiveCoordinator: Remove : Not in DefensePool");
        }
        public void DefenseCoLogsSystemNull()
        {
            if (Mode != DebugModes.DefenseCo) return;
            Log.Info(color: ConsoleColor.Yellow, text: "DefensiveCoordinator: Remove : SystemToDefend Was Null");
        }
    }
}