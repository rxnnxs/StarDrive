﻿using Ship_Game.AI;
using Ship_Game.Commands.Goals;
using System.Linq;
using Ship_Game.AI.Tasks;
using Ship_Game.Gameplay;

namespace Ship_Game
{
    public partial class Empire
    {
        bool HasWarMissionTargeting(Planet planet)
        {
            return EmpireAI.Goals.Any(g => g.IsWarMission && g.TargetPlanet == planet);
        }

        public bool GetPotentialTargetPlanets(Empire enemy, WarType warType, out Planet[] targetPlanets)
        {
            targetPlanets = null;
            switch (warType)
            {
                case WarType.GenocidalWar:
                case WarType.ImperialistWar: targetPlanets = enemy.GetPlanets().Filter(p => !HasWarMissionTargeting(p)); break;
                case WarType.BorderConflict: targetPlanets = PotentialPlanetTargetsBorderWar(enemy);                     break;
                case WarType.DefensiveWar:   targetPlanets = PotentialPlanetTargetsDefensiveWar(enemy);                  break;
            }

            return targetPlanets?.Length > 0;
        }

        Planet[] PotentialPlanetTargetsBorderWar(Empire enemy)
        {
            var potentialPlanets = enemy.GetPlanets().Filter(p => p.ParentSystem.HasPlanetsOwnedBy(this)
                                                                  && !HasWarMissionTargeting(p));

            return potentialPlanets;
        }

        Planet[] PotentialPlanetTargetsDefensiveWar(Empire enemy)
        {
            Array<SolarSystem> potentialSystems = new Array<SolarSystem>();
            var theirSystems = enemy.GetOwnedSystems();
            foreach (SolarSystem system in OwnedSolarSystems)
            {
                if (system.FiveClosestSystems.Any(s => theirSystems.Contains(s)))
                    potentialSystems.AddUnique(system);
            }

            Array<Planet> targetPlanets = new Array<Planet>();
            foreach (SolarSystem system in potentialSystems)
            {
                var potentialPlanets = system.PlanetList.Filter(p => p.Owner == enemy && !HasWarMissionTargeting(p));
                targetPlanets.AddRange(potentialPlanets);
            }

            return targetPlanets.ToArray();
        }

        public Planet[] SortPlanetTargets(Planet[] targets, WarType warType, Empire enemy)
        {
            switch (warType)
            {
                default:
                case WarType.BorderConflict: return targets.SortedDescending(p => p.ColonyPotentialValue(this));
                case WarType.DefensiveWar:   return targets.Sorted(p => p.Center.SqDist(WeightedCenter));
                case WarType.ImperialistWar: return targets.SortedDescending(p => p.Center.SqDist(WeightedCenter) * p.ColonyPotentialValue(this));
                case WarType.GenocidalWar:   return targets.SortedDescending(p => p.Center.SqDist(WeightedCenter) * p.ColonyPotentialValue(enemy));
            }
        }

        public void CreateStageFleetTask(Planet targetPlanet, Empire enemy)
        {
            MilitaryTask task = new MilitaryTask(targetPlanet, this)
            {
                Priority = 5,
                type     = MilitaryTask.TaskType.StageFleet,
            };

            EmpireAI.AddPendingTask(task);
        }

        public void CreateWarTask(Planet targetPlanet, Empire enemy, Goal goal)
        {
            // todo advanced mission types per personality or prepare for war strategy
            MilitaryTask.TaskType taskType = MilitaryTask.TaskType.StrikeForce;
            if (IsAlreadyStriking())
            {
                if (canBuildBombers
                     && !IsAlreadyGlassingPlanet(targetPlanet)
                     && (targetPlanet.Population < 1
                         || targetPlanet.ColonyPotentialValue(enemy) / targetPlanet.ColonyPotentialValue(this) > PersonalityModifiers.DoomFleetThreshold))
                {
                    taskType = MilitaryTask.TaskType.GlassPlanet;
                }
                else
                {
                    taskType = MilitaryTask.TaskType.AssaultPlanet;
                }
            }

            MilitaryTask task = new MilitaryTask(targetPlanet, this)
            {
                Priority = 5,
                type     = taskType,
                GoalGuid = goal.guid,
                Goal     = goal
            };

            EmpireAI.AddPendingTask(task);
        }

        public bool TryGetPrepareForWarType(Empire enemy, out WarType warType)
        {
            warType = WarType.SkirmishWar;
            Relationship rel = GetRelations(enemy);
            if (!rel.AtWar && rel.PreparingForWar)
                warType = rel.PreparingForWarType;
            else
                return false;

            return true;
        }

        public bool ShouldGoToWar(Relationship rel, Empire them)
        {
            if (them.data.Defeated || !rel.PreparingForWar || rel.AtWar)
                return false;

            var currentWarInformation = AllActiveWars.FilterSelect(w => !w.Them.isFaction,
                                          w => GetRelations(w.Them).KnownInformation);

            float currentEnemyStr    = currentWarInformation.Sum(i => i.OffensiveStrength);
            float currentEnemyBuild  = currentWarInformation.Sum(i => i.EconomicStrength);
            float ourCurrentStrength = AIManagedShips.EmpireReadyFleets.AccumulatedStrength;
            float theirKnownStrength = rel.KnownInformation.AllianceTotalStrength.LowerBound(15000) + currentEnemyStr;
            float theirBuildCapacity = rel.KnownInformation.AllianceEconomicStrength.LowerBound(10) + currentEnemyBuild;
            float ourBuildCapacity   = GetEmpireAI().BuildCapacity;

            var array = EmpireManager.GetAllies(this);
            for (int i = 0; i < array.Count; i++)
            {
                var ally = array[i];
                ourBuildCapacity   += ally.GetEmpireAI().BuildCapacity;
                ourCurrentStrength += ally.OffensiveStrength;
            }

            bool weAreStronger = ourCurrentStrength > theirKnownStrength * PersonalityModifiers.GoToWarTolerance
                                 && ourBuildCapacity > theirBuildCapacity * PersonalityModifiers.GoToWarTolerance;

            return weAreStronger;
        }

        bool IsAlreadyStriking()
        {
            return EmpireAI.GetTasks().Any(t => t.type == MilitaryTask.TaskType.StrikeForce);
        }

        bool IsAlreadyGlassingPlanet(Planet planet)
        {
            return EmpireAI.GetTasks().Any(t => t.type == MilitaryTask.TaskType.GlassPlanet && t.TargetPlanet == planet);
        }

        public bool CanAddAnotherWarGoal(Empire enemy)
        {
            return EmpireAI.Goals
                .Filter(g => g.IsWarMission && g.TargetEmpire == enemy).Length <= DifficultyModifiers.NumWarTasksPerWar;
        }

        public bool TryGetMissionsVsEmpire(Empire enemy, out Goal[] goals)
        {
            goals = EmpireAI.Goals.Filter(g => g.IsWarMission && g.TargetEmpire == enemy);
            return goals.Length > 0;
        }

        public Goal[] GetDefendSystemsGoal()
        {
            return EmpireAI.Goals.Filter(g => g.type == GoalType.DefendSystem);
        }

        public bool NoEmpireDefenseGoal()
        {
            return !EmpireAI.Goals.Any(g => g.type == GoalType.EmpireDefense);
        }

        public void AddDefenseSystemGoal(SolarSystem system, int priority, float strengthWanted, int fleetCount)
        {
            EmpireAI.Goals.Add(new DefendSystem(this, system, priority, strengthWanted, fleetCount));
        }

        public bool IsAlreadyDefendingSystem(SolarSystem system)
        {
            return EmpireAI.Goals.Any(g => g.type == GoalType.DefendSystem);
        }
    }

    public enum WarMissionType
    {
        Standard // todo advanced types
    }
}
