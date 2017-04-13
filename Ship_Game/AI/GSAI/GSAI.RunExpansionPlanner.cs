using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Ship_Game.Gameplay;

namespace Ship_Game.AI {
    public sealed partial class GSAI
    {
        private int desired_ColonyGoals = 2;
        private Array<Planet> DesiredPlanets = new Array<Planet>();

        public void CheckClaim(KeyValuePair<Empire, Relationship> Them, Planet claimedPlanet)
        {
            if (this.empire == Empire.Universe.PlayerEmpire)
            {
                return;
            }
            if (this.empire.isFaction)
            {
                return;
            }
            if (!Them.Value.Known)
            {
                return;
            }
            if (Them.Value.WarnedSystemsList.Contains(claimedPlanet.system.guid) && claimedPlanet.Owner == Them.Key &&
                !Them.Value.AtWar)
            {
                bool TheyAreThereAlready = false;
                foreach (Planet p in claimedPlanet.system.PlanetList)
                {
                    if (p.Owner == null || p.Owner != Empire.Universe.PlayerEmpire)
                    {
                        continue;
                    }
                    TheyAreThereAlready = true;
                }
                if (TheyAreThereAlready && Them.Key == Empire.Universe.PlayerEmpire)
                {
                    Relationship item = empire.GetRelations(Them.Key);
                    item.Anger_TerritorialConflict = item.Anger_TerritorialConflict +
                                                     (5f + (float) Math.Pow(5,
                                                          (double) empire.GetRelations(Them.Key).NumberStolenClaims));
                    empire.GetRelations(Them.Key).UpdateRelationship(this.empire, Them.Key);
                    Relationship numberStolenClaims = empire.GetRelations(Them.Key);
                    numberStolenClaims.NumberStolenClaims = numberStolenClaims.NumberStolenClaims + 1;
                    if (empire.GetRelations(Them.Key).NumberStolenClaims == 1 && !empire.GetRelations(Them.Key)
                            .StolenSystems.Contains(claimedPlanet.guid))
                    {
                        Empire.Universe.ScreenManager.AddScreen(new DiplomacyScreen(Empire.Universe, empire,
                            Empire.Universe.PlayerEmpire, "Stole Claim", claimedPlanet.system));
                    }
                    else if (empire.GetRelations(Them.Key).NumberStolenClaims == 2 &&
                             !empire.GetRelations(Them.Key).HaveWarnedTwice && !empire.GetRelations(Them.Key)
                                 .StolenSystems.Contains(claimedPlanet.system.guid))
                    {
                        Empire.Universe.ScreenManager.AddScreen(new DiplomacyScreen(Empire.Universe, empire,
                            Empire.Universe.PlayerEmpire, "Stole Claim 2", claimedPlanet.system));
                        empire.GetRelations(Them.Key).HaveWarnedTwice = true;
                    }
                    else if (empire.GetRelations(Them.Key).NumberStolenClaims >= 3 &&
                             !empire.GetRelations(Them.Key).HaveWarnedThrice && !empire.GetRelations(Them.Key)
                                 .StolenSystems.Contains(claimedPlanet.system.guid))
                    {
                        Empire.Universe.ScreenManager.AddScreen(new DiplomacyScreen(Empire.Universe, empire,
                            Empire.Universe.PlayerEmpire, "Stole Claim 3", claimedPlanet.system));
                        empire.GetRelations(Them.Key).HaveWarnedThrice = true;
                    }
                    empire.GetRelations(Them.Key).StolenSystems.Add(claimedPlanet.system.guid);
                }
            }
        }

        private void RunExpansionPlanner()
        {
            int numColonyGoals = 0;
            this.desired_ColonyGoals = ((int) Empire.Universe.GameDifficulty + 3);
            foreach (Goal g in this.Goals)
            {
                if (g.type != GoalType.Colonize)
                {
                    continue;
                }
                //added by Gremlin: Colony expansion changes
                Planet markedPlanet = g.GetMarkedPlanet();
                if (markedPlanet != null && markedPlanet.ParentSystem != null)
                {
                    if (markedPlanet.ParentSystem.ShipList.Any(ship => ship.loyalty != null && ship.loyalty.isFaction))
                        --numColonyGoals;
                    ++numColonyGoals;
                }
            }
            if (numColonyGoals < this.desired_ColonyGoals +
                (this.empire.data.EconomicPersonality != null
                    ? this.empire.data.EconomicPersonality.ColonyGoalsPlus
                    : 0)) //
            {
                Planet toMark = null;
                float DistanceInJumps = 0;
                Vector2 WeightedCenter = new Vector2();
                int numPlanets = 0;
                foreach (Planet p in this.empire.GetPlanets())
                {
                    for (int i = 0; (float) i < p.Population / 1000f; i++)
                    {
                        WeightedCenter = WeightedCenter + p.Position;
                        numPlanets++;
                    }
                }
                WeightedCenter = WeightedCenter / (float) numPlanets;
                Array<Goal.PlanetRanker> ranker = new Array<Goal.PlanetRanker>();
                Array<Goal.PlanetRanker> allPlanetsRanker = new Array<Goal.PlanetRanker>();
                foreach (SolarSystem s in UniverseScreen.SolarSystemList)
                {
                    //added by gremlin make non offensive races act like it.
                    bool systemOK = true;
                    if (!this.empire.isFaction && this.empire.data != null &&
                        this.empire.data.DiplomaticPersonality != null
                        && !(
                            (this.empire.AllRelations.Where(war => war.Value.AtWar).Count() > 0 &&
                             this.empire.data.DiplomaticPersonality.Name != "Honorable")
                            || this.empire.data.DiplomaticPersonality.Name == "Agressive"
                            || this.empire.data.DiplomaticPersonality.Name == "Ruthless"
                            || this.empire.data.DiplomaticPersonality.Name == "Cunning")
                    )
                    {
                        foreach (Empire enemy in s.OwnerList)
                        {
                            if (enemy != this.empire && !enemy.isFaction &&
                                !this.empire.GetRelations(enemy).Treaty_Alliance)
                            {
                                systemOK = false;

                                break;
                            }
                        }
                    }
                    if (!systemOK) continue;
                    if (!s.ExploredDict[this.empire])
                    {
                        continue;
                    }
                    float str = this.ThreatMatrix.PingRadarStr(s.Position, 300000f, this.empire, true);
                    if (str > 0f)
                    {
                        //Log.Info("Colonization ignored in " + s.Name + " Incorrect pin str :" +str.ToString() );
                        continue;
                    }
                    foreach (Planet planetList in s.PlanetList)
                    {
                        bool ok = true;
                        foreach (Goal g in this.Goals)
                        {
                            if (g.type != GoalType.Colonize || g.GetMarkedPlanet() != planetList)
                            {
                                continue;
                            }
                            ok = false;
                        }
                        if (!ok)
                        {
                            continue;
                        }
                        str = this.ThreatMatrix.PingRadarStr(planetList.Position, 50000f, this.empire);
                        if (str > 0)
                            continue;
                        IOrderedEnumerable<AO> sorted =
                            from ao in this.empire.GetGSAI().AreasOfOperations
                            orderby Vector2.Distance(planetList.Position, ao.Position)
                            select ao;
                        if (sorted.Count<AO>() > 0)
                        {
                            AO ClosestAO = sorted.First<AO>();
                            if (Vector2.Distance(planetList.Position, ClosestAO.Position) > ClosestAO.Radius * 2f)
                            {
                                continue;
                            }
                        }
                        int commodities = 0;
                        //Added by gremlin adding in commodities
                        foreach (Building commodity in planetList.BuildingList)
                        {
                            if (!commodity.IsCommodity) continue;
                            commodities += 1;
                        }


                        if (planetList.ExploredDict[this.empire]
                            && planetList.habitable
                            && planetList.Owner == null)
                        {
                            Goal.PlanetRanker r2 = new Goal.PlanetRanker()
                            {
                                Distance = Vector2.Distance(WeightedCenter, planetList.Position)
                            };
                            DistanceInJumps = r2.Distance / 400000f;
                            if (DistanceInJumps < 1f)
                            {
                                DistanceInJumps = 1f;
                            }
                            r2.planet = planetList;
//Cyberbernetic planet picker
                            if (this.empire.data.Traits.Cybernetic != 0)
                            {
                                r2.PV = (commodities + planetList.MineralRichness + planetList.MaxPopulation / 1000f) /
                                        DistanceInJumps;
                            }
                            else
                            {
                                r2.PV = (commodities + planetList.MineralRichness + planetList.Fertility +
                                         planetList.MaxPopulation / 1000f) / DistanceInJumps;
                            }

                            if (commodities > 0)
                                ranker.Add(r2);

                            if (planetList.Type == "Barren"
                                && commodities > 0
                                || this.empire.GetBDict()["Biospheres"]
                                || this.empire.data.Traits.Cybernetic != 0
                            )
                            {
                                ranker.Add(r2);
                            }
                            else if (planetList.Type != "Barren"
                                     && commodities > 0
                                     || ((double) planetList.Fertility >= .5f ||
                                         (this.empire.data.Traits.Cybernetic != 0 &&
                                          (double) planetList.MineralRichness >= .5f))
                                     || (this.empire.GetTDict()["Aeroponics"].Unlocked))
                                //|| (this.empire.data.Traits.Cybernetic != 0 && this.empire.GetBDict()["Biospheres"]))
                            {
                                ranker.Add(r2);
                            }
                            else if (planetList.Type != "Barren")
                            {
                                if (this.empire.data.Traits.Cybernetic == 0)
                                    foreach (Planet food in this.empire.GetPlanets())
                                    {
                                        if (food.FoodHere > food.MAX_STORAGE * .7f &&
                                            food.fs == Planet.GoodState.EXPORT)
                                        {
                                            ranker.Add(r2);
                                            break;
                                        }
                                    }
                                else
                                {
                                    if (planetList.MineralRichness < .5f)
                                    {
                                        foreach (Planet food in this.empire.GetPlanets())
                                        {
                                            if (food.ProductionHere > food.MAX_STORAGE * .7f ||
                                                food.ps == Planet.GoodState.EXPORT)
                                            {
                                                ranker.Add(r2);
                                                break;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        ranker.Add(r2);
                                    }
                                }
                            }
                        }
                        if (!planetList.ExploredDict[this.empire]
                            || !planetList.habitable
                            || planetList.Owner == this.empire
                            || this.empire == EmpireManager.Player
                            && this.ThreatMatrix.PingRadarStr(planetList.Position, 50000f, this.empire) > 0f)
                        {
                            continue;
                        }
                        Goal.PlanetRanker r = new Goal.PlanetRanker()
                        {
                            Distance = Vector2.Distance(WeightedCenter, planetList.Position)
                        };
                        DistanceInJumps = r.Distance / 400000f;
                        if (DistanceInJumps < 1f)
                        {
                            DistanceInJumps = 1f;
                        }
                        r.planet = planetList;
                        if (this.empire.data.Traits.Cybernetic != 0)
                        {
                            r.PV = (commodities + planetList.MineralRichness + planetList.MaxPopulation / 1000f) /
                                   DistanceInJumps;
                        }
                        else
                        {
                            r.PV = (commodities + planetList.MineralRichness + planetList.Fertility +
                                    planetList.MaxPopulation / 1000f) / DistanceInJumps;
                        }
                        //if (planetList.Type == "Barren" && (commodities > 0 || this.empire.GetTDict()["Biospheres"].Unlocked || (this.empire.data.Traits.Cybernetic != 0 && (double)planetList.MineralRichness >= .5f)))
                        //if (!(planetList.Type == "Barren") || !this.empire.GetTDict()["Biospheres"].Unlocked)
                        if (planetList.Type == "Barren"
                            && commodities > 0
                            || this.empire.GetBDict()["Biospheres"]
                            || this.empire.data.Traits.Cybernetic != 0)

                        {
                            if (!(planetList.Type != "Barren")
                                || planetList.Fertility < .5f
                                && !this.empire.GetTDict()["Aeroponics"].Unlocked
                                && this.empire.data.Traits.Cybernetic == 0)
                            {
                                foreach (Planet food in this.empire.GetPlanets())
                                {
                                    if (food.FoodHere > food.MAX_STORAGE * .9f && food.fs == Planet.GoodState.EXPORT)
                                    {
                                        allPlanetsRanker.Add(r);
                                        break;
                                    }
                                }

                                continue;
                            }

                            allPlanetsRanker.Add(r);
                        }
                        else
                        {
                            allPlanetsRanker.Add(r);
                        }
                    }
                }
                if (ranker.Count > 0)
                {
                    Goal.PlanetRanker winner = new Goal.PlanetRanker();
                    float highest = 0f;
                    foreach (Goal.PlanetRanker pr in ranker)
                    {
                        if (pr.PV <= highest)
                        {
                            continue;
                        }
                        bool ok = true;
                        foreach (Goal g in this.Goals)
                        {
                            if (g.GetMarkedPlanet() == null || g.GetMarkedPlanet() != pr.planet)
                            {
                                if (!g.Held || g.GetMarkedPlanet() == null ||
                                    g.GetMarkedPlanet().system != pr.planet.system)
                                {
                                    continue;
                                }
                                ok = false;
                                break;
                            }
                            else
                            {
                                ok = false;
                                break;
                            }
                        }
                        if (!ok)
                        {
                            continue;
                        }
                        winner = pr;
                        highest = pr.PV;
                    }
                    toMark = winner.planet;
                }
                if (allPlanetsRanker.Count > 0)
                {
                    DesiredPlanets.Clear();
                    IOrderedEnumerable<Goal.PlanetRanker> sortedList =
                        from ran in allPlanetsRanker
                        orderby ran.PV descending
                        select ran;
                    foreach (Goal.PlanetRanker planetRanker in sortedList)
                        DesiredPlanets.Add(planetRanker.planet);
                }
                if (toMark != null)
                {
                    bool ok = true;
                    foreach (Goal g in this.Goals)
                    {
                        if (g.type != GoalType.Colonize || g.GetMarkedPlanet() != toMark)
                        {
                            continue;
                        }
                        ok = false;
                    }
                    if (ok)
                    {
                        Goal cgoal = new Goal(toMark, this.empire)
                        {
                            GoalName = "MarkForColonization"
                        };
                        this.Goals.Add(cgoal);
                        numColonyGoals++;
                    }
                }
            }
        }
    }
}