﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ship_Game.AI;
using Ship_Game.Ships;

namespace Ship_Game.Commands.Goals
{
    public class BuildScout : Goal
    {
        public const string ID = "Build Scout";
        public override string UID => ID;

        public BuildScout() : base(GoalType.BuildScout)
        {
        }
        public BuildScout(Empire empire) : base(GoalType.BuildScout)
        {
            this.empire = empire;
        }

        public override void Evaluate()
        {
            if (Held)
                return;

            switch (this.Step)
            {
                case 0:
                    Array<Planet> list1 = new Array<Planet>();
                    foreach (Planet planet in this.empire.GetPlanets())
                    {
                        if (planet.HasShipyard)
                            list1.Add(planet);
                    }
                    Planet planet1 = (Planet)null;
                    int num1 = 9999999;
                    foreach (Planet planet2 in list1)
                    {
                        int num2 = 0;
                        foreach (QueueItem queueItem in (Array<QueueItem>)planet2.ConstructionQueue)
                            num2 += (int)(((double)queueItem.Cost - (double)queueItem.productionTowards) / (double)planet2.NetProductionPerTurn);
                        if (num2 < num1)
                        {
                            num1 = num2;
                            planet1 = planet2;
                        }
                    }
                    if (planet1 == null)
                        break;
                    if (EmpireManager.Player == this.empire && ResourceManager.ShipsDict.ContainsKey(EmpireManager.Player.data.CurrentAutoScout))
                    {
                        planet1.ConstructionQueue.Add(new QueueItem()
                        {
                            isShip = true,
                            QueueNumber = planet1.ConstructionQueue.Count,
                            sData = ResourceManager.ShipsDict[EmpireManager.Player.data.CurrentAutoScout].GetShipData(),
                            Goal = this,
                            Cost = ResourceManager.ShipsDict[EmpireManager.Player.data.CurrentAutoScout].GetCost(this.empire),
                            NotifyOnEmpty = false
                        });
                        ++this.Step;
                        break;
                    }
                    else
                    {
                        Array<Ship> list2 = new Array<Ship>();
                        foreach (string index in this.empire.ShipsWeCanBuild)
                        {
                            if (ResourceManager.ShipsDict[index].shipData.Role == ShipData.RoleName.scout)
                                list2.Add(ResourceManager.ShipsDict[index]);
                        }
                        IOrderedEnumerable<Ship> orderedEnumerable = (list2).OrderByDescending<Ship, float>((Func<Ship, float>)(ship => ship.PowerFlowMax - ship.ModulePowerDraw));
                        if (!orderedEnumerable.Any())
                            break;
                        planet1.ConstructionQueue.Add(new QueueItem()
                        {
                            isShip = true,
                            QueueNumber = planet1.ConstructionQueue.Count,
                            sData = ResourceManager.ShipsDict[((IEnumerable<Ship>)orderedEnumerable).First<Ship>().Name].GetShipData(),
                            Goal = this,
                            Cost = ResourceManager.ShipsDict[((IEnumerable<Ship>)orderedEnumerable).First<Ship>().Name].GetCost(this.empire)
                        });
                        ++this.Step;
                        break;
                    }
                case 2:
                    bool flag = false;
                    foreach (Ship ship in (Array<Ship>)this.empire.GetShips())
                    {
                        if ((ship.shipData.Role == ShipData.RoleName.scout || ship.Name == EmpireManager.Player.data.CurrentAutoScout) && !ship.PlayerShip)
                        {
                            this.freighter = ship;
                            flag = true;
                        }
                    }
                    if (!flag)
                        break;
                    this.freighter.AI.OrderExplore();
                    this.empire.ReportGoalComplete(this);
                    break;
            }
        }
    }
}
