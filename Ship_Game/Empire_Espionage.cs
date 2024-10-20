﻿using SDGraphics;
using SDUtils;
using Ship_Game.AI;
using Ship_Game.AI.StrategyAI.WarGoals;
using Ship_Game.Data.Serialization;
using Ship_Game.Gameplay;
using Ship_Game.Utils;
using System;

namespace Ship_Game
{
    public partial class Empire
    {
        [StarData] public bool CanBeScannedByPlayer { get; private set; } = true;
        [StarData] public int EspionageDefenseWeight { get; private set; } = 50;
        [StarData] public float EspionageDefenseRatio { get; private set; } = 1;
        [StarData] public float TotalMoneyLeechedLastTurn { get; private set; }
        [StarData] public float EspionageCostLastTurn { get; private set; }
        [StarData] public float EspionageBudgetMultiplier { get; private set; } = 1; // 1-5
        public const int MaxEspionageDefenseWeight = 50;

        public bool LegacyEspionageEnabled => Universe.P.UseLegacyEspionage;
        public bool NewEspionageEnabled => !Universe.P.UseLegacyEspionage;
        public float EspionagePointsPerTurn => TotalPopBillion * EspionageBudgetMultiplier;

        public void SetCanBeScannedByPlayer(bool value)
        {
            CanBeScannedByPlayer = value;
        }

        public void UpdateMoneyLeechedLastTurn()
        {
            if (LegacyEspionageEnabled || IsFaction || data.IsRebelFaction)
                return;

            TotalMoneyLeechedLastTurn = 0;
            foreach (Empire e in Universe.ActiveMajorEmpires.Filter(e => e != this))
                TotalMoneyLeechedLastTurn += GetEspionage(e).ExtractMoneyLeechedThisTurn();
        }

        public float GetEspionageCost()
        {
            if (CalcTotalEspionageWeight() == EspionageDefenseWeight)
                return 0;

            else 
                return TotalPopBillion * (EspionageBudgetMultiplier - 1);
        }

        public void SetAiEspionageBudgetMultiplier(float budget)
        {
            float totalPopBillion = TotalPopBillion;
            if (totalPopBillion < 10 || EspionageDefenseWeight == CalcTotalEspionageWeight())
                EspionageBudgetMultiplier = 1;
            else
                EspionageBudgetMultiplier = (budget / totalPopBillion) + 1;
        }

        public int CalcTotalEspionageWeight(bool grossWeight = false)
        {
            return !grossWeight ? Universe.ActiveMajorEmpires.Filter(e => e != this)
                                    .Sum(e => GetRelations(e).Espionage.ActualWeight) + EspionageDefenseWeight
                                : Universe.ActiveMajorEmpires.Filter(e => e != this)
                                    .Sum(e => GetRelations(e).Espionage.GrossWeight) + EspionageDefenseWeight;
        }

        public void SetEspionageDefenseWeight(int value)
        {
            EspionageDefenseWeight = value.Clamped(0, MaxEspionageDefenseWeight); 
        }

        public void SetEspionageBudgetMultiplier(float value)
        {
            EspionageBudgetMultiplier = value;
        }

        public void UpdateEspionage()
        {
            if (LegacyEspionageEnabled)
                return;

            int totalWeight = CalcTotalEspionageWeight();
            foreach (Empire empire in Universe.ActiveMajorEmpires.Filter(e => e != this))
                GetEspionage(empire).Update(totalWeight);

            UpdateEspionageDefenseRatio(totalWeight);
        }

        public void UpdateEspionageDefenseRatio(int totalWeight)
        {
            EspionageDefenseRatio = ((float)EspionageDefenseWeight / totalWeight.LowerBound(1) * EspionageBudgetMultiplier).UpperBound(1);
        }

        public void UpdateEspionageDefenseRatio()
        {
            UpdateEspionageDefenseRatio(CalcTotalEspionageWeight());
        }

        public Espionage GetEspionage(Empire targetEmpire) => GetRelations(targetEmpire).Espionage;

        public void AddRebellion(Planet targetPlanet, int numTroops)
        {
            Empire rebels = null;
            if (!data.RebellionLaunched)
                rebels = Universe.CreateRebelsFromEmpireData(data, this);

            if (rebels == null)
                rebels = Universe.GetEmpireByName(data.RebelName);

            for (int i = 0; i < numTroops; i++)
            {
                foreach (string troopType in ResourceManager.TroopTypes)
                {
                    if (WeCanBuildTroop(troopType) && ResourceManager.TryCreateTroop(troopType, rebels, out Troop t))
                    {
                        t.Name = rebels.data.TroopName.Text;
                        t.Description = rebels.data.TroopDescription.Text;
                        if (!t.TryLandTroop(targetPlanet))
                            t.Launch(targetPlanet); // launch the rebels

                        break;
                    }
                }
            }
        }

        public bool TryGetRebels(out Empire rebels)
        {
            rebels = Universe.GetEmpireByName(data.RebelName);
            return rebels != null;
        }

        public int GetNumOfTheirMoles(Empire them)
        {
            return NewEspionageEnabled ? GetEspionage(them).NumPlantedMoles 
                                       : them.data.MoleList.Count(m =>  Universe.GetPlanet(m.PlanetId).Owner == this);
        }

        public float GetEspionageDefenseStrVsPiratesOrRemnants(int factionMaxLevel)
        {
            return LegacyEspionageEnabled ? GetSpyDefense() : EspionageDefenseRatio * factionMaxLevel;
        }

        public bool IsSafeToActivateOpsOnAllies(Empire ally) => !IsHonorable  && (ally.isPlayer || ally.GetRelations(this).TimesSpiedOnAlly <= 1);
    }
}
