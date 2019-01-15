﻿using System;

namespace Ship_Game.Universe.SolarBodies
{
    public abstract class ColonyResource
    {
        protected readonly Planet Planet;
        public float Percent; // Percentage workers allocated [0.0-1.0]
        public bool PercentLock; // Percentage slider locked by user

        // Per Turn: Raw value produced before we apply any taxes or consume stuff
        public float GrossIncome { get; protected set; }

        // Per Turn: NetIncome = GrossIncome - (taxes + consumption)
        public float NetIncome { get; protected set; }

        // Per Turn: GrossIncome assuming we have Percent=1
        public float GrossMaxPotential { get; protected set; }

        // Per Turn: NetMaxPotential = GrossMaxPotential - (taxes + consumption)
        public float NetMaxPotential { get; protected set; }

        // Per Turn: Flat income added; no taxes applied
        public float FlatBonus { get; protected set; }

        // Per Turn: NetFlatBonus = FlatBonus - tax
        public float NetFlatBonus { get; protected set; }

        // Per Turn: Resources generated by colonists
        public float YieldPerColonist { get; protected set; }

        // Per Turn: NetYieldPerColonist = YieldPerColonist - taxes
        public float NetYieldPerColonist { get; protected set; }

        protected float Tax; // ex: 0.25 for 25% tax rate
        public float AfterTax(float grossValue) => grossValue - grossValue*Tax;

        protected ColonyResource(Planet planet) { Planet = planet; }

        protected abstract void RecalculateModifiers();
        
        // Purely used for estimation
        protected virtual float AvgResourceConsumption() => 0.0f;

        public virtual void Update(float consumption)
        {
            FlatBonus = 0f;
            RecalculateModifiers();
            
            GrossMaxPotential = YieldPerColonist * Planet.PopulationBillion;
            GrossIncome = FlatBonus + Percent * GrossMaxPotential;

            // taxes get applied before consumption
            // because government gets to eat their pie first :)))
            NetIncome           = AfterTax(GrossIncome) - consumption;
            NetMaxPotential     = AfterTax(GrossMaxPotential) - consumption;
            NetFlatBonus        = AfterTax(NetFlatBonus);
            NetYieldPerColonist = AfterTax(YieldPerColonist);
        }

        public float ColonistIncome(float yieldPerColonist)
        {
            return Percent * yieldPerColonist * Planet.PopulationBillion;
        }

        // Nominal workers needed to neither gain nor lose storage
        // @param flat Extra flat bonus to use in calculation
        // @param perCol Extra per colonist bonus to use in calculation
        public float WorkersNeededForEquilibrium(float flat = 0.0f, float perCol = 0.0f)
        {
            if (Planet.Population <= 0)
                return 0;

            float grossColo = (YieldPerColonist + perCol) * Planet.PopulationBillion;
            float grossFlat = (FlatBonus + flat);
            
            float netColo = AfterTax(grossColo);
            float netFlat = AfterTax(grossFlat);

            float needed = AvgResourceConsumption() - netFlat;
            float minWorkers = needed / netColo;
            return minWorkers.Clamped(0.0f, 0.9f);
        }

        public float EstPercentForNetIncome(float targetNetIncome)
        {
            // give negative flat bonus to shift the equilibrium point
            // towards targetNetIncome
            float flat = (-targetNetIncome) / (1f - Tax);
            return WorkersNeededForEquilibrium(flat);
        }


        public void AutoBalanceWorkers(float otherWorkers)
        {
            Percent = Math.Max(1f - otherWorkers, 0f);
        }

        public void AutoBalanceWorkers()
        {
            ColonyResource a, b;
            if      (this == Planet.Food) { a = Planet.Prod; b = Planet.Res;  }
            else if (this == Planet.Prod) { a = Planet.Food; b = Planet.Res;  }
            else if (this == Planet.Res)  { a = Planet.Food; b = Planet.Prod; }
            else return; // we're not Food,Prod,Res, so bail out
            AutoBalanceWorkers(a.Percent + b.Percent);
        }
    }


    public class ColonyFood : ColonyResource
    {
        public ColonyFood(Planet planet) : base(planet)
        {
        }

        protected override void RecalculateModifiers()
        {
            float plusPerColonist = 0f;
            foreach (Building b in Planet.BuildingList)
            {
                plusPerColonist += b.PlusFoodPerColonist;
                FlatBonus       += b.PlusFlatFoodAmount;
            }


            YieldPerColonist = Planet.Fertility + plusPerColonist;
            Tax = 0f;
            // If we use tax effects with Food resource,
            // we need a base yield offset for balance
            //YieldPerColonist += 0.25f;
        }

        protected override float AvgResourceConsumption()
        {
            return Planet.NonCybernetic ? Planet.Consumption : 0f;
        }

        public override void Update(float consumption)
        {
            base.Update(Planet.NonCybernetic ? consumption : 0f);
        }
    }

    public class ColonyProduction : ColonyResource
    {
        public ColonyProduction(Planet planet) : base(planet)
        {
        }

        protected override void RecalculateModifiers()
        {
            float richness = Planet.MineralRichness;
            float plusPerColonist = 0f;
            foreach (Building b in Planet.BuildingList)
            {
                plusPerColonist += b.PlusProdPerColonist;
                FlatBonus += b.PlusProdPerRichness * richness;
                FlatBonus += b.PlusFlatProductionAmount;
            }
            float productMod = Planet.Owner.data.Traits.ProductionMod;
            YieldPerColonist = (richness + plusPerColonist) * (1 + productMod);
            Tax = Planet.Owner.data.TaxRate;
        }
        
        protected override float AvgResourceConsumption()
        {
            return Planet.IsCybernetic ? Planet.Consumption : 0f;
        }

        public override void Update(float consumption)
        {
            base.Update(Planet.IsCybernetic ? consumption : 0f);
        }
    }

    public class ColonyResearch : ColonyResource
    {
        public ColonyResearch(Planet planet) : base(planet)
        {
        }

        protected override void RecalculateModifiers()
        {
            float plusPerColonist = 0f;
            foreach (Building b in Planet.BuildingList)
            {
                plusPerColonist += b.PlusResearchPerColonist;
                FlatBonus       += b.PlusFlatResearchAmount;
            }
            float researchMod = Planet.Owner.data.Traits.ResearchMod;
            // @note Research only comes from buildings
            // Outposts and Capital Cities always grant a small bonus
            YieldPerColonist = plusPerColonist * (1 + researchMod);
            Tax = Planet.Owner.data.TaxRate;
        }

        // @todo Estimate how much research we need
        protected override float AvgResourceConsumption() => 4.0f; // This is a good MINIMUM research value for estimation
    }

    public class ColonyMoney : ColonyResource
    {
        public ColonyMoney(Planet planet) : base(planet)
        {
        }

        protected override void RecalculateModifiers()
        {
            float incomePerColonist = 1f;
            float taxPerColonist = Planet.Owner.data.Traits.TaxMod;
            foreach (Building b in Planet.BuildingList)
            {
                incomePerColonist += b.CreditsPerColonist;
                taxPerColonist    += b.PlusTaxPercentage;
            }

            // the yield we get from this colony is the tax rate
            YieldPerColonist = incomePerColonist * taxPerColonist;
            Percent = 1f; // everyone pays taxes! no exemptions! no corruption! :D

            // And finally the actual tax rate comes from current empire tax %
            Tax = Planet.Owner.data.TaxRate;
        }
    }
}
