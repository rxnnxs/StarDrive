﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Ship_Game.Commands.Goals;
using Ship_Game.Ships;

namespace Ship_Game
{
    public partial class ColonyScreen
    {
        #region Populat lists

        class ShipCategory
        {
            public string Name;
            public Array<Ship> Ships = new Array<Ship>();
            public ModuleHeader Header;
            public int Size;

            public override string ToString() => $"Category {Name} Size={Size} Count={Ships.Count}";
        }

        void PopulateBuildableShips()
        {
            var categoryMap = new Map<string, ShipCategory>();

            foreach (Ship ship in P.Owner.ShipsWeCanBuild
                                .Select(shipName => ResourceManager.GetShipTemplate(shipName))
                                .Where(ship => ship.IsBuildableByPlayer))
            {
                string name = Localizer.GetRole(ship.DesignRole, P.Owner);
                if (!categoryMap.TryGetValue(name, out ShipCategory c))
                {
                    c = new ShipCategory {Name = name, Header = new ModuleHeader(name), Size = ship.SurfaceArea};
                    categoryMap.Add(name, c);
                }
                c.Ships.Add(ship);
            }

            // first sort the categories by name:
            ShipCategory[] categories = categoryMap.Values.ToArray();
            Array.Sort(categories, (a, b) =>
            {
                int d = a.Size - b.Size; // rank by smaller size categories first
                if (d != 0) return d;
                return string.CompareOrdinal(a.Name, b.Name);
            });

            // and then sort each ship category individually:
            foreach (ShipCategory category in categories)
            {
                category.Ships.Sort((a, b) =>
                {
                    // rank better ships as first:
                    int diff = a.GetTechScore(out int[] _) - b.GetTechScore(out int[] _);
                    if (diff != 0) return diff;
                    return string.CompareOrdinal(a.Name, b.Name);
                });

                // and add to Build list
                ScrollList.Entry entry = buildSL.AddItem(category.Header);
                foreach (Ship ship in category.Ships)
                    entry.AddSubItem(ship, addAndEdit:true);
            }
        }

        void PopulateRecruitableTroops()
        {
            string[] troopTypes = P.Owner.GetTroopsWeCanBuild();
            foreach (string troopType in troopTypes)
            {
                buildSL.AddItem(ResourceManager.GetTroopTemplate(troopType), true, false);
            }
        }

        bool ShouldResetBuildList<T>()
        {
            if (!Reset && buildSL.NumEntries != 0 && buildSL.EntryAt(0).item is T)
                return false;
            Reset = false;
            buildSL.Reset();
            return true;
        }

        #endregion


        void DrawBuildingsWeCanBuild(SpriteBatch batch)
        {
            if (ShouldResetBuildList<Ship>() || buildSL.NumEntries != P.GetBuildingsCanBuild().Count)
                buildSL.SetItems(P.GetBuildingsCanBuild());

            foreach (ScrollList.Entry entry in buildSL.VisibleExpandedEntries)
            {
                var b = entry.Get<Building>();

                SubTexture icon = ResourceManager.Texture($"Buildings/icon_{b.Icon}_48x48");
                SubTexture iconProd = ResourceManager.Texture("NewUI/icon_production");

                bool unprofitable = !P.WeCanAffordThis(b, P.colonyType) && b.Maintenance > 0f;
                Color buildColor = unprofitable ? Color.IndianRed : Color.White;
                if (entry.Hovered) buildColor = Color.White; // hover color

                string descr = Localizer.Token(b.ShortDescriptionIndex) + (unprofitable ? " (unprofitable)" : "");
                descr = Font8.ParseText(descr, 280f);

                var position = new Vector2(build.Menu.X + 60f, entry.Y - 4f);

                batch.Draw(icon, new Rectangle(entry.X, entry.Y, 29, 30), buildColor);
                DrawText(ref position, b.NameTranslationIndex, buildColor);

                if (!entry.Hovered)
                {
                    batch.DrawString(Font8, descr, position, unprofitable ? Color.Chocolate : Color.Green);
                    position.X = (entry.Right - 100);
                    var r = new Rectangle((int) position.X, entry.CenterY - iconProd.Height / 2 - 5,
                        iconProd.Width, iconProd.Height);
                    batch.Draw(iconProd, r, Color.White);

                    // The Doctor - adds new UI information in the build menus for the per tick upkeep of building

                    position = new Vector2( (r.X - 60),
                         (1 + r.Y + r.Height / 2 -
                                 Font12.LineSpacing / 2));
                    string maintenance = b.Maintenance.ToString("F2");
                    batch.DrawString(Font8, string.Concat(maintenance, " BC/Y"), position, Color.Salmon);

                    // ~~~~

                    position = new Vector2((r.X + 26),
                        (r.Y + r.Height / 2 -
                                 Font12.LineSpacing / 2));
                    batch.DrawString(Font12, b.ActualCost.String(), position, Color.White);
                    entry.DrawPlus(batch);
                }
                else
                {
                    batch.DrawString(Font8, descr, position, Color.Orange);
                    position.X = (entry.Right - 100);
                    var r = new Rectangle((int) position.X, entry.CenterY - iconProd.Height / 2 - 5,
                        iconProd.Width, iconProd.Height);
                    batch.Draw(iconProd, r, Color.White);

                    // The Doctor - adds new UI information in the build menus for the per tick upkeep of building

                    position = new Vector2((r.X - 60),
                                           (1 + r.Y + r.Height / 2 - Font12.LineSpacing / 2));
                    float actualMaint = b.Maintenance + b.Maintenance * P.Owner.data.Traits.MaintMod;
                    string maintenance = actualMaint.ToString("F2");
                    batch.DrawString(Font8, string.Concat(maintenance, " BC/Y"), position, Color.Salmon);

                    // ~~~

                    position = new Vector2((r.X + 26),
                        (r.Y + r.Height / 2 -
                                 Font12.LineSpacing / 2));
                    batch.DrawString(Font12, b.ActualCost.String(), position, Color.White);
                    entry.DrawPlus(batch);
                }

                entry.CheckHover(Input.CursorPosition);
            }
        }



        void DrawBuildableShipsList(SpriteBatch batch)
        {
            if (ShouldResetBuildList<ModuleHeader>())
                PopulateBuildableShips();

            var topLeft = new Vector2((build.Menu.X + 20), (build.Menu.Y + 45));
            foreach (ScrollList.Entry entry in buildSL.VisibleExpandedEntries)
            {
                topLeft.Y = entry.Y;
                if (entry.TryGet(out ModuleHeader header))
                    header.Draw(ScreenManager, topLeft);
                else if (!entry.Hovered)
                {
                    var ship = entry.Get<Ship>();
                    batch.Draw(ship.BaseHull.Icon, new Rectangle((int) topLeft.X, (int) topLeft.Y, 29, 30), Color.White);
                    var position = new Vector2(topLeft.X + 40f, topLeft.Y + 3f);
                    batch.DrawString(Font12,
                        ship.shipData.Role == ShipData.RoleName.station || ship.shipData.Role == ShipData.RoleName.platform
                            ? ship.Name + " " + Localizer.Token(2041)
                            : ship.Name, position, Color.White);
                    position.Y += Font12.LineSpacing;

                    var role = ship.BaseHull.Name;
                    batch.DrawString(Font8, role, position, Color.Orange);
                    position.X = position.X + Font8.MeasureString(role).X + 8;
                    ship.GetTechScore(out int[] scores);
                    batch.DrawString(Font8,
                        $"Off: {scores[2]} Def: {scores[0]} Pwr: {Math.Max(scores[1], scores[3])}", position, Color.Orange);


                    //Forgive my hacks this code of nightmare must GO!
                    position.X = (entry.Right - 120);
                    var iconProd = ResourceManager.Texture("NewUI/icon_production");
                    var destinationRectangle2 = new Rectangle((int) position.X, entry.CenterY - iconProd.Height / 2 - 5,
                        iconProd.Width, iconProd.Height);
                    batch.Draw(iconProd, destinationRectangle2, Color.White);

                    // The Doctor - adds new UI information in the build menus for the per tick upkeep of ship

                    position = new Vector2((destinationRectangle2.X - 60),
                        (1 + destinationRectangle2.Y + destinationRectangle2.Height / 2 - Font12.LineSpacing / 2));
                    // Use correct upkeep method depending on mod settings
                    string upkeep;
                    if (GlobalStats.ActiveModInfo != null && GlobalStats.ActiveModInfo.useProportionalUpkeep)
                    {
                        upkeep = ship.GetMaintCostRealism(P.Owner).ToString("F2");
                    }
                    else
                    {
                        upkeep = ship.GetMaintCost(P.Owner).ToString("F2");
                    }

                    batch.DrawString(Font8, string.Concat(upkeep, " BC/Y"), position, Color.Salmon);

                    // ~~~

                    position = new Vector2(destinationRectangle2.X + 26,
                        destinationRectangle2.Y + destinationRectangle2.Height / 2 -
                        Font12.LineSpacing / 2);
                    batch.DrawString(Font12, ((int) (ship.GetCost(P.Owner) * P.ShipBuildingModifier)).ToString(),
                        position, Color.White);
                }
                else
                {
                    var ship = entry.Get<Ship>();

                    topLeft.Y = entry.Y;
                    batch.Draw(ship.BaseHull.Icon, new Rectangle((int) topLeft.X, (int) topLeft.Y, 29, 30), Color.White);
                    Vector2 position = new Vector2(topLeft.X + 40f, topLeft.Y + 3f);
                    batch.DrawString(Font12,
                        ship.shipData.Role == ShipData.RoleName.station || ship.shipData.Role == ShipData.RoleName.platform
                            ? ship.Name + " " + Localizer.Token(2041)
                            : ship.Name, position, Color.White);
                    position.Y += Font12.LineSpacing;

                    //var role = Localizer.GetRole(ship.shipData.HullRole, EmpireManager.Player);
                    var role = ship.BaseHull.Name;
                    batch.DrawString(Font8, role, position, Color.Orange);
                    position.X = position.X + Font8.MeasureString(role).X + 8;
                    ship.GetTechScore(out int[] scores);
                    batch.DrawString(Font8,
                        $"Off: {scores[2]} Def: {scores[0]} Pwr: {Math.Max(scores[1], scores[3])}", position, Color.Orange);

                    position.X = (entry.Right - 120);
                    SubTexture iconProd = ResourceManager.Texture("NewUI/icon_production");
                    var destinationRectangle2 = new Rectangle((int) position.X, entry.CenterY - iconProd.Height / 2 - 5,
                        iconProd.Width, iconProd.Height);
                    batch.Draw(iconProd, destinationRectangle2, Color.White);

                    // The Doctor - adds new UI information in the build menus for the per tick upkeep of ship

                    position = new Vector2((destinationRectangle2.X - 60),
                        (1 + destinationRectangle2.Y + destinationRectangle2.Height / 2 - Font12.LineSpacing / 2));
                    // Use correct upkeep method depending on mod settings
                    string upkeep;
                    if (GlobalStats.ActiveModInfo != null && GlobalStats.ActiveModInfo.useProportionalUpkeep)
                    {
                        upkeep = entry.Get<Ship>().GetMaintCostRealism(P.Owner).ToString("F2");
                    }
                    else
                    {
                        upkeep = entry.Get<Ship>().GetMaintCost(P.Owner).ToString("F2");
                    }

                    batch.DrawString(Font8, string.Concat(upkeep, " BC/Y"), position, Color.Salmon);

                    // ~~~

                    position = new Vector2((destinationRectangle2.X + 26),
                        (destinationRectangle2.Y + destinationRectangle2.Height / 2 - Font12.LineSpacing / 2));
                    batch.DrawString(Font12,
                        ((int) (entry.Get<Ship>().GetCost(P.Owner) * P.ShipBuildingModifier)).ToString(), position,
                        Color.White);
                    entry.DrawPlusEdit(batch);
                }
            }

            PlayerDesignsToggle.Draw(ScreenManager);
        }

        void DrawBuildTroopsList(SpriteBatch batch)
        {
            if (ShouldResetBuildList<Troop>())
                PopulateRecruitableTroops();

            SubTexture iconProd = ResourceManager.Texture("NewUI/icon_production");
            var tl = new Vector2(build.Menu.X + 20, 0f);
            foreach (ScrollList.Entry entry in buildSL.VisibleEntries)
            {
                tl.Y = entry.Y;
                var troop = entry.Get<Troop>();
                if (!entry.Hovered)
                {
                    troop.Draw(batch, new Rectangle((int) tl.X, (int) tl.Y, 29, 30));
                    var position = new Vector2(tl.X + 40f, tl.Y + 3f);
                    batch.DrawString(Font12, troop.DisplayNameEmpire(P.Owner), position, Color.White);
                    position.Y += Font12.LineSpacing;
                    batch.DrawString(Fonts.Arial8Bold, troop.Class, position, Color.Orange);

                    position.X = entry.Right - 100;
                    var dest2 = new Rectangle((int) position.X, entry.CenterY - iconProd.Height / 2 - 5, iconProd.Width, iconProd.Height);
                    batch.Draw(iconProd, dest2, Color.White);
                    position = new Vector2(dest2.X + 26, dest2.Y + dest2.Height / 2 - Font12.LineSpacing / 2);
                    batch.DrawString(Font12, ((int)troop.ActualCost).ToString(), position, Color.White);
                    entry.DrawPlusEdit(batch);
                }
                else
                {
                    tl.Y = entry.Y;
                    troop.Draw(batch, new Rectangle((int) tl.X, (int) tl.Y, 29, 30));
                    Vector2 position = new Vector2(tl.X + 40f, tl.Y + 3f);
                    batch.DrawString(Font12, troop.DisplayNameEmpire(P.Owner), position, Color.White);
                    position.Y += Font12.LineSpacing;
                    batch.DrawString(Font8, troop.Class, position, Color.Orange);
                    position.X = entry.Right - 100;
                    Rectangle destinationRectangle2 = new Rectangle((int) position.X, entry.CenterY - iconProd.Height / 2 - 5,
                        iconProd.Width, iconProd.Height);
                    batch.Draw(iconProd, destinationRectangle2, Color.White);
                    position = new Vector2(destinationRectangle2.X + 26,
                        destinationRectangle2.Y + destinationRectangle2.Height / 2 -
                        Font12.LineSpacing / 2);
                    batch.DrawString(Font12, ((int) troop.ActualCost).ToString(), position, Color.White);
                    entry.DrawPlusEdit(batch);
                }
            }
        }

        void DrawConstructionQueue(SpriteBatch batch)
        {
            QSL.SetItems(P.ConstructionQueue);
            QSL.DrawDraggedEntry(batch);

            foreach (ScrollList.Entry entry in QSL.VisibleExpandedEntries)
            {
                entry.CheckHoverNoSound(Input.CursorPosition);

                var qi = entry.Get<QueueItem>();
                var position = new Vector2(entry.X + 40f, entry.Y);
                DrawText(ref position, qi.DisplayText);
                var r = new Rectangle((int)position.X, (int)position.Y, 150, 18);

                if (qi.isBuilding)
                {
                    SubTexture icon = ResourceManager.Texture($"Buildings/icon_{qi.Building.Icon}_48x48");
                    batch.Draw(icon, new Rectangle(entry.X, entry.Y, 29, 30), Color.White);
                    new ProgressBar(r, qi.Cost, qi.productionTowards).Draw(batch);
                }
                else if (qi.isShip)
                {
                    batch.Draw(qi.sData.Icon, new Rectangle(entry.X, entry.Y, 29, 30), Color.White);
                    new ProgressBar(r, qi.Cost * P.ShipBuildingModifier, qi.productionTowards).Draw(batch);
                }
                else if (qi.isTroop)
                {
                    Troop template = ResourceManager.GetTroopTemplate(qi.troopType);
                    template.Draw(batch, new Rectangle(entry.X, entry.Y, 29, 30));
                    new ProgressBar(r, qi.Cost, qi.productionTowards).Draw(batch);
                }

                entry.DrawUpDownApplyCancel(batch, Input);
                entry.DrawPlus(batch);
            }

            QSL.Draw(batch);
        }


        void AddBuildingToConstructionQueue(Building building, PlanetGridSquare where, bool playerAdded = true)
        {
            var qi = new QueueItem(P)
            {
                isBuilding = true,
                Building = building,
                IsPlayerAdded = playerAdded,
                Cost = building.ActualCost,
                productionTowards = 0f,
                pgs = @where
            };
            where.QItem = qi;
            P.ConstructionQueue.Add(qi);
            ResetLists();
        }


        #region Handle Inputs

        void Build(Building b)
        {
            P.AddBuildingToCQ(b, true);
            ResetLists(); // @note we reset because most buildings are single-use
            GameAudio.AcceptClick();
        }

        void Build(Ship ship)
        {
            P.ConstructionQueue.Add(new QueueItem(P)
            {
                isShip = true, sData = ship.shipData, Cost = ship.GetCost(P.Owner), productionTowards = 0f
            });
            GameAudio.AcceptClick();
        }

        void Build(Troop troop)
        {
            P.ConstructionQueue.Add(new QueueItem(P)
            {
                isTroop = true, troopType = troop.Name, Cost = troop.ActualCost, productionTowards = 0f
            });
            GameAudio.AcceptClick();
        }

        void HandleBuildListClicks(InputState input)
        {
            foreach (ScrollList.Entry e in buildSL.VisibleExpandedEntries)
            {
                if (e.item is ModuleHeader header)
                {
                    if (header.HandleInput(input, e))
                        break;
                }
                else if (e.CheckHover(input))
                {
                    Selector = e.CreateSelector();

                    if (input.LeftMouseHeldDown && e.item is Building && ActiveBuildingEntry == null)
                        ActiveBuildingEntry = e;

                    if (input.LeftMouseReleased)
                    {
                        if (ClickTimer >= TimerDelay)
                            ClickTimer = 0f;
                        else if (!e.WasPlusHovered(input))
                        {
                            if      (e.TryGet(out Building b)) Build(b);
                            else if (e.TryGet(out Ship ship))  Build(ship);
                            else if (e.TryGet(out Troop t))    Build(t);
                        }
                    }
                }

                if (e.CheckPlus(input))
                {
                    ToolTip.CreateTooltip(51);
                    if (input.LeftMouseClick)
                    {
                        if      (e.item is Building b) Build(b);
                        else if (e.item is Ship ship)  Build(ship);
                        else if (e.item is Troop t)    Build(t);
                    }
                }

                if (e.CheckEdit(input))
                {
                    ToolTip.CreateTooltip(52);
                    if (input.LeftMouseClick)
                    {
                        var sdScreen = new ShipDesignScreen(Empire.Universe, eui);
                        ScreenManager.AddScreen(sdScreen);
                        sdScreen.ChangeHull(e.Get<Ship>().shipData);
                    }
                }
            }
        }

        void HandleDragBuildingOntoTile(InputState input)
        {
            if (!(ActiveBuildingEntry?.item is Building building))
                return;

            foreach (PlanetGridSquare pgs in P.TilesList)
            {
                if (!pgs.ClickRect.HitTest(MousePos) || !input.LeftMouseReleased)
                    continue;

                if (pgs.Habitable && pgs.building == null && pgs.QItem == null && !building.IsBiospheres)
                {
                    AddBuildingToConstructionQueue(building, pgs, playerAdded: true);
                    ActiveBuildingEntry = null;
                    break;
                }

                if (pgs.Habitable || pgs.Biosphere || pgs.QItem != null || !building.CanBuildAnywhere)
                {
                    GameAudio.NegativeClick();
                    ActiveBuildingEntry = null;
                    break;
                }

                AddBuildingToConstructionQueue(building, pgs, playerAdded: true);
                ActiveBuildingEntry = null;
                break;
            }

            if (ActiveBuildingEntry != null)
            {
                foreach (QueueItem qi in P.ConstructionQueue)
                {
                    if (qi.isBuilding && qi.Building.Name == building.Name && building.Unique)
                    {
                        ActiveBuildingEntry = null;
                        break;
                    }
                }
            }

            if (input.RightMouseClick)
            {
                ClickedTroop = true;
                ActiveBuildingEntry = null;
            }

            if (input.LeftMouseClick)
            {
                ClickedTroop = true;
                ActiveBuildingEntry = null;
            }
        }


        
        void HandleConstructionQueueInput(InputState input)
        {
            int i = QSL.FirstVisibleIndex;
            foreach (ScrollList.Entry e in QSL.VisibleExpandedEntries)
            {
                if (e.CheckHover(Input.CursorPosition))
                {
                    Selector = e.CreateSelector();
                }

                if (e.WasUpHovered(input))
                {
                    ToolTip.CreateTooltip(63);
                    if (!input.IsCtrlKeyDown || input.LeftMouseDown || input.LeftMouseReleased)
                    {
                        if (input.LeftMouseClick && i > 0)
                        {
                            QueueItem item = P.ConstructionQueue[i - 1];
                            P.ConstructionQueue[i - 1] = P.ConstructionQueue[i];
                            P.ConstructionQueue[i] = item;
                            GameAudio.AcceptClick();
                        }
                    }
                    else if (i > 0)
                    {
                        QueueItem item = P.ConstructionQueue[i];
                        P.ConstructionQueue.Remove(item);
                        P.ConstructionQueue.Insert(0, item);
                        GameAudio.AcceptClick();
                        break;
                    }
                }

                if (e.WasDownHovered(input))
                {
                    ToolTip.CreateTooltip(64);
                    if (!input.IsCtrlKeyDown || input.LeftMouseDown || input.LeftMouseReleased) // @todo WTF??
                    {
                        if (input.LeftMouseClick && i + 1 < QSL.NumExpandedEntries)
                        {
                            QueueItem item = P.ConstructionQueue[i + 1];
                            P.ConstructionQueue[i + 1] = P.ConstructionQueue[i];
                            P.ConstructionQueue[i] = item;
                            GameAudio.AcceptClick();
                        }
                    }
                    else if (i + 1 < QSL.NumExpandedEntries)
                    {
                        QueueItem item = P.ConstructionQueue[i];
                        P.ConstructionQueue.Remove(item);
                        P.ConstructionQueue.Insert(0, item);
                        GameAudio.AcceptClick();
                        break;
                    }
                }

                if (e.WasApplyHovered(input) && !P.RecentCombat && P.CrippledTurns <= 0)
                {
                    if (!input.IsCtrlKeyDown || input.LeftMouseDown || input.LeftMouseReleased) // @todo WTF??
                    {
                        if (input.LeftMouseClick)
                        {
                            if (P.ApplyStoredProduction(i)) GameAudio.AcceptClick();
                            else                            GameAudio.NegativeClick();
                        }
                    }
                    else if (P.ProdHere == 0f)
                    {
                        GameAudio.NegativeClick();
                    }
                    else
                    {
                        P.ApplyAllStoredProduction(i);
                        GameAudio.AcceptClick();
                    }
                }

                if (e.WasCancelHovered(input) && input.LeftMouseClick)
                {
                    var item = e.Get<QueueItem>();
                    P.ProdHere += item.productionTowards;

                    if (item.pgs != null)
                    {
                        item.pgs.QItem = null;
                    }

                    if (item.Goal != null)
                    {
                        if (item.Goal is BuildConstructionShip)
                        {
                            P.Owner.GetEmpireAI().Goals.Remove(item.Goal);
                        }

                        if (item.Goal.GetFleet() != null)
                            P.Owner.GetEmpireAI().Goals.Remove(item.Goal);
                    }

                    P.ConstructionQueue.Remove(item);
                    GameAudio.AcceptClick();
                }
                ++i;
            }

            QSL.HandleInput(input, P);
        }



        #endregion
    }
}
