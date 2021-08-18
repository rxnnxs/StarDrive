﻿using System;
using Microsoft.Xna.Framework;
using Ship_Game.AI;
using Ship_Game.Ships;

namespace Ship_Game.Commands.Goals
{
    public class BuildConstructionShip : Goal
    {
        public const string ID = "BuildConstructionShip";
        public override string UID => ID;

        public BuildConstructionShip() : base(GoalType.DeepSpaceConstruction)
        {
            Steps = new Func<GoalStep>[]
            {
                FindPlanetToBuildAt,
                WaitForShipBuilt,
                OrderDeepSpaceBuild,
                WaitForDeployment
            };
        }

        public BuildConstructionShip(Vector2 buildPosition, string platformUid, Empire owner) : this()
        {
            BuildPosition = buildPosition;
            ToBuildUID = platformUid;
            empire = owner;
            Evaluate();
        }

        GoalStep FindPlanetToBuildAt()
        {
            if (!ResourceManager.GetShipTemplate(ToBuildUID, out Ship toBuild))
            {
                Log.Error($"BuildConstructionShip: no ship to build with uid={ToBuildUID ?? "null"}");
                return GoalStep.GoalFailed;
            }

            // ShipToBuild will be the constructor ship -- usually a freighter
            // once the freighter is deployed, it will mutate into ToBuildUID

            ShipToBuild = ShipBuilder.PickConstructor(empire)?.shipData;
            if (ShipToBuild == null)
                throw new Exception($"PickConstructor failed for {empire.Name}."+
                                    "This is a FATAL bug in data files, where Empire is not able to do space construction!");

            if (!empire.FindPlanetToBuildShipAt(empire.SafeSpacePorts, toBuild.shipData, out Planet planet, priority: 0.25f))
                return GoalStep.TryAgain;

            // toBuild is only used for cost calculation
            planet.Construction.Enqueue(toBuild, ShipToBuild, this);
            if (toBuild.IsSubspaceProjector && Fleet != null) // SSP Needed for Offensive fleets, rush it
                planet.Construction.MoveToAndContinuousRushFirstItem();

            return GoalStep.GoToNextStep;
        }

        GoalStep OrderDeepSpaceBuild()
        {
            if (FinishedShip == null) 
                return GoalStep.GoalFailed;

            FinishedShip.AI.OrderDeepSpaceBuild(this);
            return GoalStep.GoToNextStep;
        }

        GoalStep WaitForDeployment()
        {
            // FB - must keep this goal until the ship deployed it's structure. 
            // If the goal is not kept, load game construction ships loses the empire goal and get stuck
            return FinishedShip == null ? GoalStep.GoalComplete : GoalStep.TryAgain;
        }

    }
}
