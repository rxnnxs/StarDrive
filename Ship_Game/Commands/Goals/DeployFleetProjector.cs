﻿using Ship_Game.AI;
using Ship_Game.Fleets;
using Ship_Game.Ships;
using System;
using System.Linq;
using SDUtils;
using Ship_Game.Data.Serialization;
using Ship_Game.Universe;
using Vector2 = SDGraphics.Vector2;

namespace Ship_Game.Commands.Goals
{
    [StarDataType]
    public class DeployFleetProjector : Goal
    {
        public const string ID = "DeployFleetProjector";
        public override string UID => ID;

        public DeployFleetProjector(int id, UniverseState us)
            : base(GoalType.DeployFleetProjector, id, us)
        {
            Steps = new Func<GoalStep>[]
            {
                BuildProjector,
                WaitAndPrioritizeProjector,
                RemoveProjectorWhenCompleted
            };
        }

        public DeployFleetProjector(Fleet fleet, Planet claim, Empire e)
            : this(e.Universum.CreateId(), e.Universum)
        {
            empire             = e;
            ColonizationTarget = claim;
            Fleet              = fleet;

            Evaluate();
        }

        GoalStep BuildProjector()
        {
            if (Fleet == null || ColonizationTarget.ParentSystem.HasPlanetsOwnedBy(empire))
                return GoalStep.GoalComplete;

            float distanceToDeploy = empire.GetProjectorRadius() * 0.8f;
            Vector2 direction      = Fleet.FleetTask.TargetPlanet.Position.DirectionToTarget(Fleet.AveragePosition());
            BuildPosition          = ColonizationTarget.Position + direction.Normalized() * distanceToDeploy;
            Goal goal              = new BuildConstructionShip(BuildPosition, "Subspace Projector", empire);
            goal.Fleet             = Fleet;
            empire.GetEmpireAI().AddGoal(goal);
            return GoalStep.GoToNextStep;
        }

        GoalStep WaitAndPrioritizeProjector()
        {
            var goals = empire.GetEmpireAI().SearchForGoals(GoalType.DeepSpaceConstruction).Filter(g => g.Fleet == Fleet);
            if (goals.Length > 0)
            {
                Goal constructionGoal = goals.First();
                if (constructionGoal.FinishedShip == null)
                {
                    if (Fleet == null)
                    {
                        constructionGoal.PlanetBuildingAt?.Construction.Cancel(constructionGoal);
                        return GoalStep.GoalFailed;
                    }

                    constructionGoal.PlanetBuildingAt?.Construction.PrioritizeProjector(BuildPosition);
                    return GoalStep.TryAgain;
                }

                FinishedShip = constructionGoal.FinishedShip; // We have a construction ship on the way
                return GoalStep.GoToNextStep;
            }

            return GoalStep.GoalFailed;
        }

        GoalStep RemoveProjectorWhenCompleted()
        {
            if (Fleet?.FleetTask == null)
            {
                FinishedShip?.AI.OrderScrapShip();
                var projectors = empire.GetProjectors();
                for (int i = 0; i < projectors.Count; i++)
                {
                    Ship ship = projectors[i];
                    if (ship.Position.InRadius(BuildPosition, 1000))
                        ship.ScuttleTimer = 120;
                }

                return GoalStep.GoalComplete;
            }

            return GoalStep.TryAgain;
        }
    }
}
