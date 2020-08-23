﻿using System;
using Ship_Game.AI;
using Ship_Game.AI.Tasks;
using Ship_Game.Ships;

namespace Ship_Game.Commands.Goals
{
    public class RemnantBalancersEngage : Goal
    {
        public const string ID = "RemnantBalancersEngage";
        public override string UID => ID;
        public Planet TargetPlanet;
        private Remnants Remnants;
        private Ship Portal;

        public RemnantBalancersEngage() : base(GoalType.RemnantBalancersEngage)
        {
            Steps = new Func<GoalStep>[]
            {
                SelectFirstTargetPlanet,
                SelectPortalToSpawnFrom,
                GatherFleet,
                WaitForCompletion
            };
        }

        public RemnantBalancersEngage(Empire owner, Empire target) : this()
        {
            empire       = owner;
            TargetEmpire = target;
            PostInit();
            Log.Info(ConsoleColor.Green, $"---- Remnants: New {empire.Name} Engagement: Ancient Balancers for {TargetEmpire.Name} ----");
        }

        public sealed override void PostInit()
        {
            Remnants     = empire.Remnants;
            TargetPlanet = ColonizationTarget;
            Portal       = TargetShip;

        }

        public override bool IsRaid => true;

        bool StillStrongest => EmpireManager.MajorEmpires.FindMax(e => e.CurrentMilitaryStrength) == TargetEmpire;
        
        bool SelectTargetPlanet()
        {
            int desiredPlanetLevel = (RandomMath.RollDie(5) - 5 + Remnants.Level).LowerBound(1);
            var potentialPlanets   = TargetEmpire.GetPlanets().Filter(p => p.Level == desiredPlanetLevel);
            if (potentialPlanets.Length == 0) // Try lower level planets if not found exact level
                potentialPlanets = TargetEmpire.GetPlanets().Filter(p => p.Level != desiredPlanetLevel);

            if (potentialPlanets.Length == 0)
                return false; // Could not find a target planet

            ColonizationTarget = potentialPlanets.RandItem();
            TargetPlanet       = ColonizationTarget; // We will use TargetPlanet for better readability
            return true;
        }

        GoalStep SelectFirstTargetPlanet()
        {
            return SelectTargetPlanet() ? GoalStep.GoToNextStep : GoalStep.GoalComplete;
        }

        GoalStep SelectPortalToSpawnFrom()
        {
            if (!Remnants.GetPortals(out Ship[] portals))
                return GoalStep.GoalFailed;

            Portal     = portals.FindMin(s => s.Center.Distance(TargetPlanet.Center));
            TargetShip = Portal; // Save compatibility
            return GoalStep.GoToNextStep;
        }

        GoalStep GatherFleet()
        {
            if (!StillStrongest)
            {
                Fleet.OrderEscort(Portal);
                Fleet.FleetTask.DisbandFleet(Fleet);
                Fleet.FleetTask.EndTask();
                return GoalStep.GoalComplete;
            }

            if (!Remnants.CreateShip(Portal, out Ship ship))
                return GoalStep.TryAgain;

            if (Fleet == null)
            {
                var task = MilitaryTask.CreateRemnantEngagement(TargetPlanet, empire);
                empire.GetEmpireAI().AddPendingTask(task);
                task.CreateRemnantFleet(empire, ship, "Ancient Balancers", out Fleet);
            }
            else
            {
                Fleet.AddShip(ship);
            }

            ship.AI.AddEscortGoal(Portal);
            if (Fleet.GetStrength() < TargetEmpire.CurrentMilitaryStrength / 2)
                return GoalStep.TryAgain;

            Fleet.AutoArrange();
            Fleet.TaskStep = 1;
            return GoalStep.GoToNextStep;
        }

        GoalStep WaitForCompletion()
        {
            if (!StillStrongest)
            {
                Fleet.OrderEscort(Portal);
                Fleet.FleetTask.DisbandFleet(Fleet);
                Fleet.FleetTask.EndTask();
                return GoalStep.GoalComplete;
            }

            if (Fleet.TaskStep == 6 || TargetPlanet.Owner != TargetEmpire)
            {
                // Select a new planet
                if (!SelectTargetPlanet())
                    return GoalStep.GoalComplete;

                Fleet.TaskStep = 1;
            }

            return GoalStep.TryAgain;
        }
    }
}