﻿using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ship_Game
{
    // Visual style of the ScrollList
    public enum ListStyle
    {
        Default,
        Blue, // used in research screen
    }

    public enum DragEvent
    {
        Begin,
        End,
    }

    public abstract class ScrollListBase : UIElementContainer
    {
        // Top and Bottom padding for list items
        const int PaddingTop   = 24;
        const int PaddingBot   = 12;
        const int PaddingLeft  = 12;
        const int PaddingRight = 24;
        const int PaddingItem  = 4;

        protected Rectangle ItemsRect; // inner housing rect for scroll list items
        protected Rectangle ScrollUp, ScrollUpClickArea;
        protected Rectangle ScrollDown, ScrollDownClickArea;
        protected Rectangle ScrollBar, ScrollBarClickArea;
        protected Rectangle ScrollHousing;
        protected int ScrollBarHover;
        protected bool ScrollUpHover, ScrollDownHover;
        
        protected int DragStartMousePos;
        protected int DragStartScrollPos;
        protected bool DraggingScrollBar;
        protected bool ScrollBarPosChanged;
        protected bool ShouldDrawScrollBar;

        protected float ClickTimer;
        protected const float TimerDelay = 0.05f;
        
        // this controls the visual style of the ScrollList
        // can be freely changed at any point
        public ListStyle Style;

        // Automatic Selection highlight
        public Selector Highlight;

        // Index of the currently highlighted ScrollList Item, -1 if no highlighted item
        public int HighlightedIndex = -1;

        // If TRUE, automatically draws Selection highlight around each ScrollList Item
        public bool EnableItemHighlight;

        // If TRUE, items will trigger click events
        public bool EnableItemEvents = true;

        // If set to a valid UIElement instance, then this element will be drawn in the background
        UIElementV2 TheBackground;
        public UIElementV2 Background
        {
            set
            {
                if (TheBackground != value)
                {
                    TheBackground?.RemoveFromParent();
                    TheBackground = value;
                    if (value != null)
                    {
                        // attach with relative position, so it moves along with us
                        Add(value).SetRelPos(Pos - value.Pos);
                    }
                }
            }
        }

        // If TRUE, allows automatic dragging of ScrollList Items
        public bool EnableDragEvents = false;

        public abstract void OnItemHovered(ScrollListItemBase item);
        public abstract void OnItemClicked(ScrollListItemBase item);
        public abstract void OnItemDoubleClicked(ScrollListItemBase item);
        public abstract void OnItemDragged(ScrollListItemBase item, DragEvent evt);

        public virtual void OnItemExpanded(ScrollListItemBase item, bool expanded)
        {
            // NOTE: Modify the index when opening/closing headers, to make list usage more convenient
            if (expanded)
            {
                float relClickPos = (float) item.VisibleIndex / MaxVisibleItems;
                if (relClickPos >= 0.5f)
                {
                    VisibleItemsBegin += Math.Min(MaxVisibleItems / 2, MaxVisibleItems);
                }
            }
            else
            {
                //VisibleItemsBegin = Math.Max(0, VisibleItemsBegin - item.NumSubItems);
            }

            RequiresLayout = true;
            HighlightedIndex = -1; // remove highlight when closing/expanding
            Highlight = null;
        }

        #region ScrollList HandleInput

        int ScrollButtonAmount(bool held) => !held ? EntryHeight / 2 : EntryHeight / 6;
        int ScrollWheelAmount => EntryHeight / 3;

        bool HandleInputScrollButtons(InputState input)
        {
            ScrollUpHover   = ScrollUpClickArea.HitTest(input.CursorPosition);
            ScrollDownHover = ScrollDownClickArea.HitTest(input.CursorPosition);
            bool held = input.LeftMouseHeld(0.1f);
            if ((held || input.LeftMouseClick) && (ScrollUpHover || ScrollDownHover))
            {
                ScrollByScrollBar(ScrollButtonAmount(held) * (ScrollUpHover ? -1 : +1));
                return true;
            }
            return false;
        }

        bool HandleInputScrollDrag(InputState input)
        {
            ScrollBarHover = 0; // no highlight

            if (ScrollBarClickArea.HitTest(input.CursorPosition))
            {
                ScrollBarHover = 1; // gentle focus
                if (!DraggingScrollBar && input.LeftMouseHeld(0.1f))
                {
                    DraggingScrollBar = true;
                    DragStartMousePos = (int)input.CursorPosition.Y;
                    DragStartScrollPos = ScrollBar.Y;
                }
            }

            if (DraggingScrollBar && input.LeftMouseDown)
            {
                ScrollBarHover = 2; // full active highlight

                float difference = input.CursorPosition.Y - DragStartMousePos;
                if (Math.Abs(difference) > 0f)
                    SetScrollBarPosition((int)Math.Round(DragStartScrollPos + difference));
                return true;
            }

            DraggingScrollBar = false;
            return false;
        }

        bool HandleInputMouseWheel(InputState input)
        {
            if (Rect.HitTest(input.CursorPosition))
            {
                int amount = 0;
                if (input.ScrollIn) amount = -ScrollWheelAmount;
                if (input.ScrollOut) amount = ScrollWheelAmount;
                if (amount != 0)
                {
                    ScrollByScrollBar(amount);
                    return true;
                }
            }
            return false;
        }

        bool HandleInputScrollBar(InputState input)
        {
            bool hit = HandleInputScrollButtons(input);
            hit |= HandleInputScrollDrag(input);
            hit |= HandleInputMouseWheel(input);
            return hit;
        }

        protected ScrollListItemBase DraggedEntry;
        protected Vector2 DraggedOffset;

        protected abstract void HandleDraggable(InputState input);
        protected abstract void HandleElementDragging(InputState input);

        public override bool HandleInput(InputState input)
        {
            if (!Visible || !Enabled)
                return false;

            HandleDraggable(input);
            HandleElementDragging(input);
            
            // NOTE: We do not early return, because we want to update hover state for all ScrollList items
            bool captured = base.HandleInput(input) || HandleInputScrollBar(input);
            bool anyHovered = false;

            if (!captured && Rect.HitTest(input.CursorPosition))
            {
                // input wasn't captured by other items or scroll bar
                // so update hover state of visible items
                for (int i = VisibleItemsBegin; i < VisibleItemsEnd; i++)
                {
                    ScrollListItemBase item = FlatEntries[i];
                    if (item.UpdateHoverState(input))
                    {
                        anyHovered = true;
                        if (!item.IsHeader && EnableItemHighlight)
                        {
                            HighlightedIndex = i;
                            Highlight = new Selector(item.Rect.Bevel(4, 2), useRealRect:true);
                        }
                    }
                }

                // now capture input
                for (int i = VisibleItemsBegin; i < VisibleItemsEnd; i++)
                {
                    ScrollListItemBase item = FlatEntries[i];
                    if (item.HandleInput(input))
                    {
                        captured = true;
                        break; // it's safe to early break here
                    }
                }
            }

            // Not hovering over any items? Clear the SelectionBox
            if (!anyHovered && EnableItemHighlight)
            {
                HighlightedIndex = -1;
                Highlight = null;
            }

            return captured;
        }

        #endregion

        #region ScrollList Update / PerformLayout
        
        public ScrollListStyleTextures GetStyle() => ScrollListStyleTextures.Get(Style);


        // flattened entries
        protected readonly Array<ScrollListItemBase> FlatEntries = new Array<ScrollListItemBase>();
        
        // visible range is [begin, end)
        protected int VisibleItemsBegin, VisibleItemsEnd;

        protected int EntryHeight;
        protected int MaxVisibleItems;
        
        protected abstract void FlattenEntries();

        // Updates the visible index range
        // @return TRUE if this caused FirstVisibleIndex or LastVisibleIndex to change
        void UpdateVisibleIndex(float indexFraction, int maxVisibleItems)
        {
            float fraction = indexFraction.Clamped(0, FlatEntries.Count);
            int begin = (int)Math.Floor(fraction);
            int end   = (int)Math.Ceiling(fraction + 0.5f + maxVisibleItems);
            VisibleItemsBegin = begin.Clamped(0, Math.Max(0, FlatEntries.Count - maxVisibleItems));
            VisibleItemsEnd   = end.Clamped(0, Math.Max(0, FlatEntries.Count));
        }

        public override void PerformLayout()
        {
            base.PerformLayout();

            ScrollListStyleTextures s = GetStyle();

            ItemsRect = new Rectangle((int)X + PaddingLeft,
                                      (int)Y + PaddingTop,
                                      (int)Width - (PaddingLeft + PaddingRight),
                                      (int)Height - (PaddingTop + PaddingBot));

            SubTexture up = s.ScrollBarArrowUp.Normal;
            SubTexture dn = s.ScrollBarArrowDown.Normal;
            ScrollUp   = new Rectangle((int)(Right - (up.Width + 5)), (int)Y + PaddingTop, up.Width, up.Height);
            ScrollDown = new Rectangle((int)(Right - (dn.Width + 5)), (int)Bottom - PaddingBot - dn.Height, dn.Width, dn.Height);
            ScrollHousing = new Rectangle(ScrollUp.X + 1, ScrollUp.Bottom + 3, s.ScrollBarMid.Normal.Width, ScrollDown.Y - ScrollUp.Bottom - 6);
            ScrollBar.X = ScrollHousing.X;
            ScrollUpClickArea   = ScrollUp.Bevel(5);
            ScrollDownClickArea = ScrollDown.Bevel(5);

            FlattenEntries();

            if (FlatEntries.Count > 0)
            {
                int height = FlatEntries.First.ItemHeight;
                if (height != 0) EntryHeight = height;
            }

            // PaddingBot gives padding bottom of the items when scrolling
            float maxVisibleItemsF = (float)(ItemsRect.Height - PaddingBot) / (EntryHeight + PaddingItem);
            MaxVisibleItems = (int)Math.Ceiling(maxVisibleItemsF);
            ShouldDrawScrollBar = FlatEntries.Count > (int)Math.Floor(maxVisibleItemsF);

            int nextItemY = (ItemsRect.Y + PaddingItem);
            if (ScrollBarPosChanged)
            {
                ScrollBarPosChanged = false;
                // when scrollbar was moved being dragged by input, use it to update the visible index
                float relScrollPos = GetRelativeScrollPosFromScrollBar();
                float scrolledIndexF = Math.Max(0, FlatEntries.Count - maxVisibleItemsF) * relScrollPos;
                UpdateVisibleIndex(scrolledIndexF, MaxVisibleItems);

                float remainder = (scrolledIndexF - VisibleItemsBegin) % 1f;
                int scrollOffset = (int)Math.Floor(remainder * EntryHeight);
                nextItemY -= scrollOffset;
                //Log.Info($"pos={relScrollPos} idxF={scrolledIndexF} rem={remainder} off={scrollOffset}");
            }
            else // otherwise, update/clamp visible indices and recalculate scrollbar
            {
                int begin = VisibleItemsBegin;
                UpdateVisibleIndex(begin, MaxVisibleItems);
                UpdateScrollBarToCurrentIndex(MaxVisibleItems);
                //Log.Info($"Before=[{begin},{VisibleItemsEnd}) After=[{VisibleItemsBegin},{VisibleItemsEnd})");
            }

            int visibleIndex = 0;
            for (int i = 0; i < FlatEntries.Count; i++)
            {
                ScrollListItemBase e = FlatEntries[i];
                if (VisibleItemsBegin <= i && i < VisibleItemsEnd)
                {
                    e.VisibleIndex = visibleIndex++;
                    e.Rect = new Rectangle(ItemsRect.X, nextItemY, ItemsRect.Width, EntryHeight);
                    e.PerformLayout();
                    nextItemY += (EntryHeight + PaddingItem);
                }
                else
                {
                    e.ClearItemLayout();
                }
            }
        }

        // this is the relative position of the scrollbar [0, 1] inside the scrollbar housing
        float GetRelativeScrollPosFromScrollBar()
        {
            float scrollBarPos = (ScrollBar.Y - ScrollHousing.Y);
            float scrollSpan   = ScrollHousing.Height - ScrollBar.Height;
            return (scrollBarPos / scrollSpan);
        }

        void UpdateScrollBarToCurrentIndex(int maxVisibleItems)
        {
            int startOffset = (int)(ScrollHousing.Height * (VisibleItemsBegin / (float)FlatEntries.Count));
            int barHeight   = (int)(ScrollHousing.Height * (maxVisibleItems / (float)FlatEntries.Count));

            ScrollBar = new Rectangle(ScrollHousing.X, ScrollHousing.Y + startOffset,
                                      GetStyle().ScrollBarMid.Normal.Width, barHeight);
            ScrollBarClickArea = ScrollBar.Widen(5);
        }

        // set scrollbar to requested position
        void SetScrollBarPosition(int newScrollY)
        {
            if (newScrollY < ScrollHousing.Y)
                newScrollY = ScrollHousing.Y;
            else if ((newScrollY + ScrollBar.Height) > ScrollHousing.Bottom)
                newScrollY = ScrollHousing.Bottom - ScrollBar.Height;
            
            // This enables smooth scrollbar dragging: with every pixel changed we request layout
            if (ScrollBar.Y != newScrollY)
            {
                ScrollBar.Y = ScrollBarClickArea.Y = newScrollY;
                RequiresLayout = true;
                ScrollBarPosChanged = true;
            }
        }

        // move the scrollbar by requested amount of pixels up (-) or down (+)
        void ScrollByScrollBar(int deltaScroll) => SetScrollBarPosition(ScrollBar.Y + deltaScroll);

        public override void Update(float deltaTime)
        {
            if (!Visible)
                return;
            
            base.Update(deltaTime);

            for (int i = VisibleItemsBegin; i < VisibleItemsEnd; i++)
            {
                FlatEntries[i].Update(deltaTime);
            }
        }

        #endregion


        #region ScrollList Draw

        void DrawScrollBar(SpriteBatch batch)
        {
            ScrollListStyleTextures s = GetStyle();
            int upDownSize = (ScrollBar.Height - s.ScrollBarMid.Normal.Height) / 2;
            var up  = new Rectangle(ScrollBar.X, ScrollBar.Y, ScrollBar.Width, upDownSize);
            var mid = new Rectangle(ScrollBar.X, ScrollBar.Y + upDownSize, ScrollBar.Width, s.ScrollBarMid.Normal.Height);
            var bot = new Rectangle(ScrollBar.X, mid.Y + mid.Height, ScrollBar.Width, upDownSize);

            bool parentHovered      = (ScrollBarHover == 1);
            bool controlItemHovered = (ScrollBarHover == 2);

            s.ScrollBarUpDown.Draw(batch, up, parentHovered, controlItemHovered);
            s.ScrollBarMid   .Draw(batch, mid, parentHovered, controlItemHovered);
            s.ScrollBarUpDown.Draw(batch, bot, parentHovered, controlItemHovered);
            s.ScrollBarArrowUp.Draw(batch, ScrollUp, parentHovered, ScrollUpHover);
            s.ScrollBarArrowDown.Draw(batch, ScrollDown, parentHovered, ScrollDownHover);
        }

        public override void Draw(SpriteBatch batch)
        {
            //if (!FirstUpdateDone)
            //    Log.Error($"{TypeName}.Update() has not been called. This is a bug!"
            //              +" Make sure the ScrollList is being updated in GameScreen.Update() or screen.Add(list) for automatic update.");
            base.Draw(batch);

            if (ShouldDrawScrollBar)
                DrawScrollBar(batch);
            
            // use a scissor to clip smooth scroll items
            batch.End();
            batch.Begin(SpriteBlendMode.AlphaBlend, SpriteSortMode.Deferred, SaveStateMode.None);

            for (int i = VisibleItemsBegin; i < VisibleItemsEnd; ++i)
            {
                FlatEntries[i].Draw(batch);
            }

            //batch.DrawRectangle(ScrollHousing, Color.Red);
            //batch.DrawRectangle(ItemsRect, Color.Red);

            if (EnableItemHighlight)
                Highlight?.Draw(batch);

            batch.GraphicsDevice.RenderState.ScissorTestEnable = true;
            batch.GraphicsDevice.ScissorRectangle = new Rectangle(ItemsRect.X - 10, ItemsRect.Y - 5, ItemsRect.Width + 20, ItemsRect.Height + 5);
            batch.End();
            batch.Begin();
            batch.GraphicsDevice.RenderState.ScissorTestEnable = false;

            if (DraggedEntry != null)
            {
                DraggedEntry.Pos = GameBase.ScreenManager.input.CursorPosition + DraggedOffset;
                batch.FillRectangle(DraggedEntry.Rect, new Color(0, 0, 0, 150));
                DraggedEntry.Draw(batch);
            }
        }

        #endregion
    }
}
