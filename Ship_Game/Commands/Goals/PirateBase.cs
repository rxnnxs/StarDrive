﻿using System;
using SDGraphics;
using Ship_Game.AI;
using Ship_Game.Data.Serialization;
using Ship_Game.Ships;

namespace Ship_Game.Commands.Goals
{
    [StarDataType]
    public class PirateBase : Goal
    {
        [StarData] public sealed override Ship TargetShip { get; set; }

        Pirates Pirates => Owner.Pirates;
        Ship Base => TargetShip;
        
        [StarDataConstructor]
        public PirateBase(Empire owner) : base(GoalType.PirateBase, owner)
        {
            Steps = new Func<GoalStep>[]
            {
               SalvageShips
            };
        }

        public PirateBase(Empire owner, Ship ship, string systemName) : this(owner)
        {
            TargetShip = ship;
            if (Pirates.Verbose)
                Log.Info(ConsoleColor.Green, $"---- Pirates: New {Owner.Name} Base in {systemName} ----");
        }

        GoalStep SalvageShips()
        {
            if (Base == null || !Base.Active || Base.Loyalty != Owner)
            {
                Pirates.LevelDown();
                return GoalStep.GoalFailed; // Base is destroyed or captured, behead the Director
            }

            if (Base.InCombat) // No base operations while in combat
            {
                CallForHelp();
                return GoalStep.TryAgain;
            }

            Base.ChangeOrdnance(Base.OrdinanceMax / 10); // Slowly replenish the base's ordnance stores

            Ship[] friendlies = Base.AI.FriendliesNearby;
            for (int i = 0; i < friendlies.Length; i++)
            {
                Ship ship = friendlies[i];
                if (ship.IsPlatformOrStation || ship.IsHangarShip)
                    continue; // Do not mess with our own structures

                if (ship.InRadius(Base.Position, Base.Radius + 3000))
                {
                    ship.ChangeOrdnance(ship.OrdinanceMax / 10);
                    Pirates.ProcessShip(ship, Base);
                }
            }

            return GoalStep.TryAgain;
        }

        void CallForHelp()
        {
            if (Pirates.Owner.AI.HasGoal(g => g.Type == GoalType.PirateDefendBase && g.TargetShip == Base))
                return; // Help is coming

            Pirates.AddGoalDefendBase(Pirates.Owner, Base);
        }
    }
}