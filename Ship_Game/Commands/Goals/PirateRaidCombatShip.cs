﻿using System;
using SDGraphics;
using Ship_Game.AI;
using Ship_Game.Data.Serialization;
using Ship_Game.Ships;
using Ship_Game.Universe;

namespace Ship_Game.Commands.Goals
{
    [StarDataType]
    public class PirateRaidCombatShip : Goal
    {
        public const string ID = "PirateRaidCombatShip";
        public override string UID => ID;
        [StarData] Pirates Pirates;

        [StarDataConstructor]
        public PirateRaidCombatShip(int id, UniverseState us)
            : base(GoalType.PirateRaidCombatShip, id, us)
        {
            Steps = new Func<GoalStep>[]
            {
               DetectAndSpawnRaidForce,
               CheckIfHijacked
            };
        }

        public PirateRaidCombatShip(Empire owner, Empire targetEmpire)
            : this(owner.Universum.CreateId(), owner.Universum)
        {
            empire        = owner;
            TargetEmpire  = targetEmpire;
            StarDateAdded = empire.Universum.StarDate;

            PostInit();
            Log.Info(ConsoleColor.Green, $"---- Pirates: New {empire.Name} Combat Ship Raid vs. {targetEmpire.Name} ----");
        }

        public sealed override void PostInit()
        {
            Pirates = empire.Pirates;
        }

        public override bool IsRaid => true;

        GoalStep DetectAndSpawnRaidForce()
        {
            if (Pirates.PaidBy(TargetEmpire) || Pirates.VictimIsDefeated(TargetEmpire))
                return GoalStep.GoalFailed; // They paid or dead

            if (Pirates.GetTarget(TargetEmpire, Pirates.TargetType.CombatShipAtWarp, out Ship combatShip))
            {
                combatShip.HyperspaceReturn();
                TargetShip = combatShip;
                if (Pirates.Level > TargetShip.TroopCount * 5 / ((int)UState.Difficulty).LowerBound(1) + TargetShip.Level)
                {
                    TargetShip.Loyalty.AddMutinyNotification(TargetShip, GameText.MutinySucceeded, Pirates.Owner);
                    TargetShip.LoyaltyChangeFromBoarding(Pirates.Owner, false);
                    Pirates.ExecuteProtectionContracts(TargetEmpire, TargetShip);
                }
                else
                {
                    TargetShip.Loyalty.AddMutinyNotification(TargetShip, GameText.MutinyAverted, Pirates.Owner);
                }

                Pirates.ExecuteVictimRetaliation(TargetEmpire);
                KillMutinyDefenseTroops(Pirates.Level / 2 - TargetShip.Level);
                return TargetShip.Loyalty == Pirates.Owner ? GoalStep.GoToNextStep : GoalStep.GoalFailed;
            }

            // Try locating viable freighters for 1 year (10 turns), else just give up
            return empire.Universum.StarDate < StarDateAdded + 1 ? GoalStep.TryAgain : GoalStep.GoalFailed;
        }

        GoalStep CheckIfHijacked()
        {
            if (TargetShip == null
                || !TargetShip.Active
                || TargetShip.Loyalty != Pirates.Owner)
            {
                return GoalStep.GoalFailed; // Target destroyed or escaped
            }

            if (TargetShip.Loyalty == Pirates.Owner)
            {
                TargetShip.AI.OrderPirateFleeHome(signalRetreat: true);
                return GoalStep.GoalComplete;
            }

            return GoalStep.TryAgain;
        }

        void KillMutinyDefenseTroops(int numToKill)
        {
            for (int i = 0; i < numToKill; i++)
                TargetShip.KillOneOfOurTroops();
        }
    }
}