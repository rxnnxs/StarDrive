using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Ship_Game.AI;
using Ship_Game.Audio;
using Ship_Game.Ships;

namespace Ship_Game
{
    public delegate void QueueItemCompleted(bool success);

    public class QueueItem : ScrollList<QueueItem>.Entry
    {
        public Planet Planet;
        public bool isBuilding;
        public bool isShip;
        public bool isOrbital;
        public bool isTroop;
        public ShipData sData;
        public Building Building;
        public string TroopType;
        public Array<Guid> TradeRoutes         = new Array<Guid>();
        public Array<Rectangle> AreaOfOperation = new Array<Rectangle>();
        public Rectangle rect;
        public Rectangle removeRect;
        public int QueueNumber;
        public int ShipLevel;
        public PlanetGridSquare pgs;
        public string DisplayName;
        public float Cost;
        public float ProductionSpent;
        public Goal Goal;
        public bool NotifyOnEmpty = true;
        public bool IsPlayerAdded = false;
        public bool TransportingColonists;
        public bool TransportingFood;
        public bool TransportingProduction;
        public bool AllowInterEmpireTrade;

        // Event action for when this QueueItem is finished
        public QueueItemCompleted OnComplete;

        // production still needed until this item is finished
        public float ProductionNeeded => ActualCost - ProductionSpent;

        // is this item finished constructing?
        public bool IsComplete => ProductionSpent.GreaterOrEqual(ActualCost); // float imprecision

        public QueueItem(Planet planet)
        {
            Planet = planet;
        }

        void SwapConstructionQueueItems(int swapTo, int currentIndex)
        {
            swapTo       = swapTo.Clamped(0, Planet.ConstructionQueue.Count-1);
            currentIndex = currentIndex.Clamped(0, Planet.ConstructionQueue.Count-1);

            QueueItem item = Planet.ConstructionQueue[swapTo];
            Planet.ConstructionQueue[swapTo] = Planet.ConstructionQueue[currentIndex];
            Planet.ConstructionQueue[currentIndex] = item;
            GameAudio.AcceptClick();
        }

        void MoveToConstructionQueuePosition(int moveTo, int currentIndex)
        {
            QueueItem item = Planet.ConstructionQueue[currentIndex];
            Planet.ConstructionQueue.RemoveAt(currentIndex);
            Planet.ConstructionQueue.Insert(moveTo, item);
            GameAudio.AcceptClick();
        }

        public override bool HandleInput(InputState input)
        {
            bool captured = base.HandleInput(input);
            if (input.LeftMouseClick)
            {
                int index = Planet.ConstructionQueue.IndexOf(this);

                if (WasUpHovered(input))
                {
                    ToolTip.CreateTooltip(63);
                    if (input.IsCtrlKeyDown) MoveToConstructionQueuePosition(0, index); // move to top
                    else                     SwapConstructionQueueItems(index - 1, index); // move up by one
                }
                else if (WasDownHovered(input))
                {
                    ToolTip.CreateTooltip(64);
                    if (input.IsCtrlKeyDown) MoveToConstructionQueuePosition(Planet.ConstructionQueue.Count-1, index); // move to bottom
                    else                     SwapConstructionQueueItems(index + 1, index); // move down by one
                }
                else if (WasApplyHovered(input))
                {
                    float maxAmount = input.IsCtrlKeyDown ? 10000f : 10f;
                    if (Planet.Construction.RushProduction(index, maxAmount))
                        GameAudio.AcceptClick();
                    else
                        GameAudio.NegativeClick();
                }
                else if (WasCancelHovered(input))
                {
                    Planet.Construction.Cancel(this);
                    GameAudio.AcceptClick();
                }
            }
            return captured;
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);
        }

        public override void Draw(SpriteBatch batch)
        {
            //batch.FillRectangle(Rect, new Color(0, 0, 0, 150));

            Vector2 bCursor = Mouse.GetState().Pos() + List.DraggedOffset;
            var tCursor = new Vector2(bCursor.X + 40f, bCursor.Y);
            var r = new Rectangle((int)bCursor.X, (int)bCursor.Y, 29, 30);
            var pbRect = new Rectangle((int)tCursor.X, (int)tCursor.Y + Fonts.Arial12Bold.LineSpacing, 150, 18);
            var pb = new ProgressBar(pbRect, Cost, ProductionSpent);

            if (isBuilding)
            {
                batch.Draw(ResourceManager.Texture($"Buildings/icon_{Building.Icon}_48x48"), r, Color.White);
                batch.DrawString(Fonts.Arial12Bold, Building.Name, tCursor, Color.White);
                pb.Draw(batch);
            }
            else if (isShip)
            {
                batch.Draw(sData.Icon, r, Color.White);
                batch.DrawString(Fonts.Arial12Bold, sData.Name, tCursor, Color.White);
                pb.Draw(batch);
            }
            else if (isTroop)
            {
                Troop template = ResourceManager.GetTroopTemplate(TroopType);
                template.Draw(batch, r);
                batch.DrawString(Fonts.Arial12Bold, TroopType, tCursor, Color.White);
                pb.Draw(batch);
            }

            //foreach (ScrollList.Entry entry in CQueue.VisibleExpandedEntries)
            //{
            //    entry.CheckHoverNoSound(Input.CursorPosition);

            //    var qi = entry.Get<QueueItem>();
            //    var position = new Vector2(entry.X + 40f, entry.Y);
            //    DrawText(ref position, qi.DisplayText);
            //    var r = new Rectangle((int)position.X, (int)position.Y, 150, 18);
            //    var progress = new ProgressBar(r, qi.Cost, qi.ProductionSpent);

            //    if (qi.isBuilding)
            //    {
            //        SubTexture icon = ResourceManager.Texture($"Buildings/icon_{qi.Building.Icon}_48x48");
            //        batch.Draw(icon, new Rectangle(entry.X, entry.Y, 29, 30), Color.White);
            //        progress.Draw(batch);
            //    }
            //    else if (qi.isShip)
            //    {
            //        batch.Draw(qi.sData.Icon, new Rectangle(entry.X, entry.Y, 29, 30), Color.White);
            //        progress.Draw(batch);
            //    }
            //    else if (qi.isTroop)
            //    {
            //        Troop template = ResourceManager.GetTroopTemplate(qi.TroopType);
            //        template.Draw(batch, new Rectangle(entry.X, entry.Y, 29, 30));
            //        progress.Draw(batch);
            //    }
            //}

            base.Draw(batch);
        }

        public int TurnsUntilComplete
        {
            get
            {
                float production = Planet.Prod.NetIncome;
                if (production <= 0f)
                    return 999;
                float turns = ProductionNeeded / production;
                return (int)Math.Ceiling(turns);
            }
        }

        public float ActualCost
        {
            get
            {
                float cost = Cost;
                if (isShip) cost *= Planet.ShipBuildingModifier;
                return (int)cost; // FB - int to avoid float issues in release which prevent items from being complete
            }
        }

        public string DisplayText
        {
            get
            {
                if (isBuilding)
                    return Localizer.Token(Building.NameTranslationIndex);
                if (isShip || isOrbital)
                    return DisplayName ?? sData.Name;
                if (isTroop)
                    return TroopType;
                return "";
            }
        }

        public override string ToString() => DisplayText;

        public SavedGame.QueueItemSave Serialize()
        {
            var qi = new SavedGame.QueueItemSave
            {
                isBuilding  = isBuilding,
                Cost        = Cost,
                isShip      = isShip,
                DisplayName = DisplayName,
                isTroop     = isTroop,
                ProgressTowards = ProductionSpent,
                isPlayerAdded   = IsPlayerAdded,
                TradeRoutes     = TradeRoutes,
                AreaOfOperation = AreaOfOperation.Select(r => new RectangleData(r)),
                TransportingColonists  = TransportingColonists,
                TransportingFood       = TransportingFood,
                TransportingProduction = TransportingProduction,
                AllowInterEmpireTrade  = AllowInterEmpireTrade
        };
            if (qi.isBuilding) qi.UID = Building.Name;
            if (qi.isShip)     qi.UID = sData.Name;
            if (qi.isTroop)    qi.UID = TroopType;
            if (Goal != null) qi.GoalGUID  = Goal.guid;
            if (pgs != null)  qi.pgsVector = new Vector2(pgs.x, pgs.y);
            return qi;
        }
    }
}