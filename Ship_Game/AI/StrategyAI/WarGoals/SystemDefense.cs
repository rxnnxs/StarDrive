﻿using System;
using Microsoft.Xna.Framework;
using Ship_Game.Empires.DataPackets;
using static Ship_Game.AI.ThreatMatrix;

namespace Ship_Game.AI.StrategyAI.WarGoals
{
    public sealed class SystemDefense : AttackShips
    {
        /// <summary>
        /// Initializes from save a new instance of the <see cref="Defense"/> class.
        /// </summary>
        public SystemDefense(Campaign campaign, Theater war) : base(campaign, war) => CreateSteps();

        public SystemDefense(CampaignType campaignType, Theater war) : base(campaignType, war) => CreateSteps();

        protected override void CreateSteps()
        {
            Steps = new Func<GoalStep>[]
            {
               SetupShipTargets,
               AssesCampaign
            };
        }

        protected override GoalStep SetupShipTargets()
        {
            int basePriority     = OwnerTheater.Priority;
            int important        = basePriority - 1;
            int normal           = basePriority;
            int casual           = basePriority + 1;
            int unImportant      = basePriority + 2;
            var systems = new Array<IncomingThreat>();
            var ownedSystems = Owner.GetOwnedSystems();
            if (OwnerWar.WarType == WarType.EmpireDefense)
            {
                foreach (IncomingThreat threatenedSystem in Owner.SystemWithThreat)
                {
                    if (threatenedSystem.ThreatTimedOut) continue;
                    systems.Add(threatenedSystem);
                }
            }

            var highValueSystems = systems.Filter(s => s.TargetSystem.PlanetList.Any(p => p.Owner == Owner && p.Level > 3));

            highValueSystems.Sort(ts => ts.TargetSystem.WarValueTo(Owner));

            for (int i = 0; i < highValueSystems.Length; i++)
            {
                var threatenedSystem = highValueSystems[i];
                var priority = casual - threatenedSystem.TargetSystem.PlanetList
                    .FindMax(p => p.Owner == Owner ? p.Level : 0)?.Level ?? 0;
                Tasks.StandardSystemDefense(threatenedSystem.TargetSystem, priority, threatenedSystem.Strength, 1);
            }

            foreach (var system in Owner.GetOwnedSystems())
            {
                float str = Owner.KnownEnemyStrengthIn(system);
                if (str > 100)
                {
                    var priority = casual - system.PlanetList
                    .FindMax(p => p.Owner == Owner ? p.Level : 0)?.Level ?? 0;
                    Tasks.StandardSystemDefense(system, priority, str, 1);
                }
            }

            return GoalStep.GoToNextStep;
        }
    }
}
