﻿using System;
using System.Collections.Generic;
using Ship_Game.AI;

namespace Ship_Game.Ships
{
    public partial class Ship
    {
        public float TradeTimer = 300;
        public bool TransportingColonists  { get; set; }
        public bool TransportingFood       { get; set; }
        public bool TransportingProduction { get; set; }
        public bool AllowInterEmpireTrade  { get; set; }
        public Array<Guid> TradeRoutes     { get; private set; } = new Array<Guid>();

        public bool IsCandidateForTradingBuild => IsFreighter && !IsConstructor;

        public bool IsFreighter => DesignRole == ShipData.RoleName.freighter
                                   && shipData.ShipCategory == ShipData.Category.Civilian;

        public bool IsIdleFreighter => IsFreighter
                                       && AI != null
                                       && (!AI.HasPriorityOrder || !AI.HasWayPoints)
                                       && AI.State != AIState.SystemTrader
                                       && AI.State != AIState.Flee
                                       && AI.State != AIState.Refit
                                       && AI.State != AIState.Scrap
                                       && AI.State != AIState.Scuttle
                                       && AI.State != AIState.Resupply;

        public bool AddTradeRoute(Planet planet)
        {
            if (planet.Owner == null)
                return false;

            if (planet.Owner == loyalty || loyalty.IsTradeTreaty(planet.Owner))
            {
                TradeRoutes.AddUnique(planet.guid);
                return true;
            }

            return false;
        }

        public bool CanTransportGoodsType(Goods goods)
        {
            if (!loyalty.ManualTrade)
                return true;

            switch (goods)
            {
                default:
                case Goods.Food:       return TransportingFood;
                case Goods.Production: return TransportingProduction;
                case Goods.Colonists:  return TransportingColonists;
            }
        }

        public bool TryGetBestTradeRoute(Goods goods, Planet[] exportPlanets, Planet importPlanet, out ExportPlanetAndEta exportAndEta)
        {
            exportAndEta = default;
            if (!CanTransportGoodsType(goods) || !InTradingZones(importPlanet))
                return false;

            var potentialRoutes = new Map<int, Planet>();
            if (GetCargo(goods) >= CargoSpaceMax * 0.25f)
            {
                int eta = (int)GetAstrogateTimeTo(importPlanet);
                if (TradeDistanceOk(importPlanet, eta))
                    potentialRoutes.Add(eta, importPlanet); // import planet since there is not export planet.
            }

            for (int i = 0; i < exportPlanets.Length; i++)
            {
                Planet exportPlanet = exportPlanets[i];
                if (InTradingZones(exportPlanet))
                {
                    int eta = (int)(GetAstrogateTimeTo(exportPlanet) + GetAstrogateTimeBetween(exportPlanet, importPlanet));
                    if (!potentialRoutes.ContainsKey(eta) && TradeDistanceOk(importPlanet, eta))
                        potentialRoutes.Add(eta, exportPlanet);
                }
            }

            if (potentialRoutes.Keys.Count == 0)
                return false;

            int fastest       = potentialRoutes.FindMinKey(d => d);
            Planet bestExport = potentialRoutes[fastest];
            exportAndEta      = new ExportPlanetAndEta(bestExport, fastest);
            return true;
        }

        public struct ExportPlanetAndEta
        {
            public readonly Planet Planet;
            public readonly int Eta;

            public ExportPlanetAndEta(Planet exportPlanet, int eta)
            {
                Planet = exportPlanet;
                Eta    = eta; ;
            }
        }

        // limit eta for Inter Empire trade
        bool TradeDistanceOk(Planet importPlanet, int eta) =>  importPlanet.Owner == loyalty || eta <= 30;

        public void RemoveTradeRoute(Planet planet)
        {
            TradeRoutes.Remove(planet.guid);
        }

        public void RefreshTradeRoutes()
        {
            if (!loyalty.isPlayer)
                return; // Trade routes are available only for players

            foreach (Guid planetGuid in TradeRoutes)
            {
                Planet planet = Empire.Universe.GetPlanet(planetGuid);
                if (planet.Owner == loyalty)
                    continue;

                if (planet.Owner == null || !loyalty.IsTradeTreaty(planet.Owner))
                    RemoveTradeRoute(planet);
            }
        }

        public bool IsValidTradeRoute(Planet planet)
        {
            if (TradeRoutes.IsEmpty)
                return true; // need at least 2 routes or AO for it to filter

            foreach (Guid g in TradeRoutes)
            {
                if (g == planet.guid)
                    return true;
            }
            return false;
        }

        public bool InTradingZones(Planet planet)
        {
            if (!loyalty.isPlayer)
                return true; // only player ships can have trade AO or trade routes

            if (TradeRoutes?.Count >= 2 && AreaOfOperation.NotEmpty) // ship has both AO trade routes, so check this or that.
                return IsValidTradeRoute(planet) || InsideAreaOfOperation(planet);

            return IsValidTradeRoute(planet) && InsideAreaOfOperation(planet);
        }

        public float FreighterValue(Empire empire, float fastVsBig)
        {
            float warpK           = MaxFTLSpeed / 1000;
            float movementWeight  = warpK + MaxSTLSpeed / 10 + RotationRadiansPerSecond.ToDegrees() - GetCost(empire) / 5;
            float cargoWeight     = CargoSpaceMax.Clamped(0, 80) - (float)SurfaceArea / 25;
            float lowCargoPenalty = CargoSpaceMax < SurfaceArea * 0.5f ? CargoSpaceMax / SurfaceArea : 1;
            float score           = movementWeight * fastVsBig + cargoWeight * (1 - fastVsBig);

            // For faster , cheaper ships vs big and maybe slower ships
            return score * lowCargoPenalty;
        }

        public void DownloadTradeRoutes(Array<Guid> tradeRoutes)
        {
            TradeRoutes = tradeRoutes;
        }

        public float CheckExpectedGoods(Goods goods)
        {
            if (AI.FindGoal(ShipAI.Plan.DropOffGoods, out _))
                return GetCargo(goods);

            return 0;
        }
    }
}
