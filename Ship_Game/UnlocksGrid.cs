using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Ship_Game.Ships;

namespace Ship_Game
{
    public enum UnlockType
    {
        SHIPMODULE,
        TROOP,
        BUILDING,
        HULL,
        ADVANCE
    }

    public sealed class UnlockItem
    {
        public UnlockType Type;
        public string HullUnlocked;
        public string privateName;
        public Building building;
        public ShipModule module;
        public Troop troop;
        public string Description;
    }

    public sealed class UnlocksGrid
	{
        public struct GridItem
        {
            public UnlockItem item;
            public Rectangle rect;
        }

        public Array<GridItem> GridOfUnlocks = new Array<GridItem>();

        public UnlocksGrid(Array<UnlockItem> Unlocks, Rectangle r)
		{
			Vector2 Cursor = new Vector2(r.X, r.Y);
			int Column = 0;
			int Row = 0;
			foreach (UnlockItem item in Unlocks)
			{
				GridItem gi = new GridItem
				{
					rect = new Rectangle((int)Cursor.X + 32 * Column, (int)Cursor.Y + 32 * Row, 32, 32),
					item = item
				};
				GridOfUnlocks.Add(gi);
				Row++;
				if (Row != 2)
				{
					continue;
				}
				Row = 0;
				Column++;
			}
		}

		public void Draw(SpriteBatch batch)
		{
			foreach (GridItem gi in GridOfUnlocks)
			{
				UnlockItem unlock = gi.item;
				if (unlock.Type == UnlockType.SHIPMODULE)
				{
					var iconRect = new Rectangle(gi.rect.X, gi.rect.Y, 16 * unlock.module.XSIZE, 16 * unlock.module.YSIZE);
                    iconRect.X = iconRect.X + 16 - iconRect.Width / 2;
                    iconRect.Y = gi.rect.Y + gi.rect.Height / 2 - iconRect.Height / 2;

					while (iconRect.Height > gi.rect.Height)
					{
						iconRect.Height = iconRect.Height - unlock.module.YSIZE;
						iconRect.Width = iconRect.Width - unlock.module.XSIZE;
						iconRect.X = gi.rect.X + 16 - iconRect.Width / 2;
						iconRect.Y = gi.rect.Y + gi.rect.Height / 2 - iconRect.Height / 2;
					}

					batch.Draw(unlock.module.ModuleTexture, iconRect, Color.White);
				}

				if (unlock.Type == UnlockType.TROOP)
				{
                    var iconRect = new Rectangle(gi.rect.X, gi.rect.Y, 32, 32);
					unlock.troop.DrawIcon(batch, iconRect);
				}
				else if (unlock.Type == UnlockType.BUILDING)
				{
                    var iconRect = new Rectangle(gi.rect.X, gi.rect.Y, 32, 32);
					batch.Draw(ResourceManager.Texture($"Buildings/icon_{unlock.building.Icon}_64x64"), iconRect, Color.White);
				}
				else if (unlock.Type == UnlockType.HULL)
				{
					var iconRect = new Rectangle(gi.rect.X, gi.rect.Y, 32, 32);
					batch.Draw(ResourceManager.Hull(unlock.privateName).Icon, iconRect, Color.White);
				}
				else if (unlock.Type == UnlockType.ADVANCE)
				{
                    var iconRect2 = new Rectangle(gi.rect.X, gi.rect.Y, 32, 32);
                    batch.Draw(ResourceManager.Texture("TechIcons/star"), iconRect2, Color.White);
				}
			}
		}
	}
}