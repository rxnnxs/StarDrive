using System;
using Microsoft.Xna.Framework.Graphics;
using Ship_Game.Ships;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
    class ArenaDesignShipListItem : ScrollListItem<ArenaDesignShipListItem>
    {
        public readonly IShipDesign Design;

        public int NestingLevel = 0;

        // draw generic headerText item
        public ArenaDesignShipListItem(string headerText) : base(headerText)
        {

        }

        public ArenaDesignShipListItem(string headerText, int nestinglevel) : base(headerText)
        {
            NestingLevel = nestinglevel;
        }
        // draw ship design
        public ArenaDesignShipListItem(IShipDesign design)
        {
            Design = design;
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            base.Draw(batch, elapsed);

            if (NestingLevel == 0)
            {
                if (Design != null)
                {
                    batch.Draw(Design.Icon, new Rectangle((int)X, (int)Y, 29, 30), Color.White);

                    var tCursor = new Vector2(X + 40f, Y + 3f);
                    batch.DrawString(Fonts.Arial12Bold, Design.Name, tCursor, Color.White);
                    tCursor.Y += Fonts.Arial12Bold.LineSpacing;

                    var roleFont = Hovered ? Fonts.Arial11Bold : Fonts.Arial12Bold;
                    batch.DrawString(roleFont, Design.GetRole(), tCursor, Color.Orange);
                }
            }
            else
            {
                int width = Math.Min(HeaderMaxWidth, (int)Width);
                var r = new Rectangle((int)X, (int)Y + 4, width, (int)Height - 10);

                if (HeaderText != null)
                {
                    Color bkgColor = !Enabled ? Color.Gray
                                    : Hovered ? new Color(95, 82, 47)
                                    : new Color(53, 50, 28);
                    new Selector(r, bkgColor).Draw(batch, elapsed);

                    var textPos = new Vector2(r.X + 10, r.CenterY() - Fonts.Pirulen12.LineSpacing / 2);
                    batch.DrawString(Fonts.Pirulen12, HeaderText, textPos, Color.White);
                }

                if (SubEntries != null && SubEntries.NotEmpty)
                {
                    string open = Expanded ? "-" : "+";
                    var textPos = new Vector2(r.Right - 26, r.CenterY() - Fonts.Arial20Bold.LineSpacing / 2 - 2);
                    batch.DrawString(Fonts.Arial20Bold, open, textPos, Color.White);
                }
            }
        }
    }
}
