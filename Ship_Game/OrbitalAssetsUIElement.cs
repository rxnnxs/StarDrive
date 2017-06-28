using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Ship_Game.Gameplay;
using System;
using System.Collections.Generic;
using Ship_Game.AI;

namespace Ship_Game
{
	public sealed class OrbitalAssetsUIElement : UIElement
	{
		private Rectangle SliderRect;

		private Rectangle clickRect;

		private UniverseScreen screen;

		private Rectangle LeftRect;

		private Rectangle RightRect;

		private Rectangle flagRect;

		private Rectangle DefenseRect;

		private Rectangle SoftAttackRect;

		private Rectangle HardAttackRect;

		private Rectangle ItemDisplayRect;

		private Selector sel;

		private Planet p;

		public DanButton BombardButton;

		public DanButton LandTroops;

		private Array<OrbitalAssetsUIElement.TippedItem> ToolTipItems = new Array<OrbitalAssetsUIElement.TippedItem>();

		new private Color tColor = new Color(255, 239, 208);

		//private string fmt = "0.#";

		public OrbitalAssetsUIElement(Rectangle r, Ship_Game.ScreenManager sm, UniverseScreen screen, Planet p)
		{
			this.p = p;
			this.screen = screen;
			this.ScreenManager = sm;
			this.ElementRect = r;
			this.sel = new Selector(r, Color.Black);
			base.TransitionOnTime = TimeSpan.FromSeconds(0.25);
			base.TransitionOffTime = TimeSpan.FromSeconds(0.25);
			this.SliderRect = new Rectangle(r.X + r.Width - 100, r.Y + r.Height - 40, 500, 40);
			this.clickRect = new Rectangle(this.ElementRect.X + this.ElementRect.Width - 16, this.ElementRect.Y + this.ElementRect.Height / 2 - 11, 11, 22);
			this.LeftRect = new Rectangle(r.X, r.Y + 44, 200, r.Height - 44);
			this.RightRect = new Rectangle(r.X + 200, r.Y + 44, 200, r.Height - 44);
			this.BombardButton = new DanButton(new Vector2((float)(this.LeftRect.X + 20), (float)(this.LeftRect.Y + 25)), Localizer.Token(1431))
			{
				IsToggle = true,
				ToggledText = Localizer.Token(1426)
			};
			this.LandTroops = new DanButton(new Vector2((float)(this.LeftRect.X + 20), (float)(this.LeftRect.Y + 75)), Localizer.Token(1432))
			{
				IsToggle = true,
				ToggledText = Localizer.Token(1433)
			};
			this.flagRect = new Rectangle(r.X + r.Width - 31, r.Y + 22 - 13, 26, 26);
			this.DefenseRect = new Rectangle(this.LeftRect.X + 12, this.LeftRect.Y + 18, 22, 22);
			this.SoftAttackRect = new Rectangle(this.LeftRect.X + 12, this.DefenseRect.Y + 22 + 5, 16, 16);
			this.HardAttackRect = new Rectangle(this.LeftRect.X + 12, this.SoftAttackRect.Y + 16 + 5, 16, 16);
			this.DefenseRect.X = this.DefenseRect.X - 3;
			this.ItemDisplayRect = new Rectangle(this.LeftRect.X + 85, this.LeftRect.Y + 5, 85, 85);
			OrbitalAssetsUIElement.TippedItem bomb = new OrbitalAssetsUIElement.TippedItem()
			{
				r = this.BombardButton.r,
				TIP_ID = 32
			};
			this.ToolTipItems.Add(bomb);
			bomb = new OrbitalAssetsUIElement.TippedItem()
			{
				r = this.LandTroops.r,
				TIP_ID = 36
			};
			this.ToolTipItems.Add(bomb);
		}

		public override void Draw(GameTime gameTime)
		{
			MathHelper.SmoothStep(0f, 1f, base.TransitionPosition);
			this.ScreenManager.SpriteBatch.FillRectangle(this.sel.Rect, Color.Black);
			float x = (float)Mouse.GetState().X;
			MouseState state = Mouse.GetState();
			Vector2 vector2 = new Vector2(x, (float)state.Y);
			Header slant = new Header(new Rectangle(this.sel.Rect.X, this.sel.Rect.Y, this.sel.Rect.Width, 41), "Orbital Assets");
			Body body = new Body(new Rectangle(slant.leftRect.X, this.sel.Rect.Y + 44, this.sel.Rect.Width, this.sel.Rect.Height - 44));
			slant.Draw(this.ScreenManager);
			body.Draw(this.ScreenManager);
			this.BombardButton.DrawBlue(this.ScreenManager);
			this.LandTroops.DrawBlue(this.ScreenManager);
		}

		public override bool HandleInput(InputState input)
		{
			if (this.BombardButton.HandleInput(input))
			{
				if (!this.BombardButton.Toggled)
				{
					foreach (Ship ship in p.system.ShipList)
					{
						if (ship.loyalty != EmpireManager.Player || ship.AI.State != AIState.Bombard)
						{
							continue;
						}
						ship.AI.OrderQueue.Clear();
						ship.AI.State = AIState.AwaitingOrders;
					}
				}
				else
				{
					foreach (Ship ship in p.system.ShipList)
					{
						if (ship.loyalty != EmpireManager.Player || ship.BombBays.Count <= 0 || Vector2.Distance(ship.Center, this.p.Center) >= 15000f)
						{
							continue;
						}
						ship.AI.OrderBombardPlanet(p);
					}
				}
			}
			this.LandTroops.HandleInput(input);
			foreach (OrbitalAssetsUIElement.TippedItem ti in this.ToolTipItems)
			{
				if (!ti.r.HitTest(input.CursorPosition))
				{
					continue;
				}
				ToolTip.CreateTooltip(ti.TIP_ID);
			}
			return false;
		}

		private struct TippedItem
		{
			public Rectangle r;

			public int TIP_ID;
		}
	}
}