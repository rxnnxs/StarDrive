// ReSharper disable once CheckNamespace

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Ship_Game.Gameplay;

namespace Ship_Game.AI {
    public sealed partial class EmpireAI
    {        
        private float FindTaxRateToReturnAmount(float amount)
        {
            for (int i = 0; i < 100; i++)
            {
                if (OwnerEmpire.EstimateIncomeAtTaxRate(i / 100f) >= amount)
                {
                    return i / 100f;
                }
            }
            return 1;
        }

        private void RunEconomicPlanner()
        {
            float risk = GetRisk();
            float money = OwnerEmpire.Money;
            money = money < 1 ? 1 : money;
            //gremlin: Use self adjusting tax rate based on wanted treasury of 10(1 full year) of total income.
            //BuildCapacity = OwnerEmpire.GetTotalShipMaintenance();
            float treasuryGoal = OwnerEmpire.GrossTaxes + OwnerEmpire.OtherIncome +
                                 OwnerEmpire.TradeMoneyAddedThisTurn +
                                 OwnerEmpire.data.FlatMoneyBonus; //mmore savings than GDP 
            treasuryGoal *= (OwnerEmpire.data.treasuryGoal * 100);
            treasuryGoal  =Math.Max(1,treasuryGoal);
            float goalClamped = 1f.Clamp(0,money / treasuryGoal);
            float treasuryGoalRatio =  1 - goalClamped;
            treasuryGoal *= treasuryGoalRatio;
            float tempTax = FindTaxRateToReturnAmount(treasuryGoal);
            if (tempTax - OwnerEmpire.data.TaxRate > .02f)
                OwnerEmpire.data.TaxRate += .02f;
            else
                OwnerEmpire.data.TaxRate = tempTax;
            float militaryRatio = OwnerEmpire.getResStrat().MilitaryRatio;
            var resStrat = OwnerEmpire.getResStrat();
            SetBudgetForeArea(goalClamped * .1f, ref OwnerEmpire.data.DefenseBudget, Math.Max(risk, militaryRatio));            
            SetBudgetForeArea(goalClamped * .1f, ref OwnerEmpire.data.SSPBudget, resStrat.IndustryRatio + resStrat.ExpansionRatio);
            SetBudgetForeArea(goalClamped * .1f, ref BuildCapacity, Math.Max(risk, militaryRatio));           
            SetBudgetForeArea(goalClamped *.25f, ref OwnerEmpire.data.SpyBudget, Math.Max(risk, militaryRatio));
        }
        private float SetBudgetForeArea(float percentOfIncome, ref float area, float risk)
        {
            float budget = OwnerEmpire.Money * percentOfIncome * risk;
            
            budget = Math.Max(0, budget);
            area = budget;
            return budget;
        }
        public float GetRisk(float riskLimit =.75f)
        {
            float risk = 0;
            foreach (var kv in OwnerEmpire.AllRelations)
            {
                risk += kv.Value.Risk.MaxRisk >  riskLimit ? 0 :kv.Value.Risk.MaxRisk;
            }
            return risk;
        }
    }
}