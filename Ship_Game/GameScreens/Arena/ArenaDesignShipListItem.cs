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

        // draw generic headerText item
        public ArenaDesignShipListItem(string headerText) : base(headerText)
        {
        }

        // draw ship design
        public ArenaDesignShipListItem(IShipDesign design, string headerText) : base(headerText)
        {
            Design = design;
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            base.Draw(batch, elapsed);

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
    }
}
