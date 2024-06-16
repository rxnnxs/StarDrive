﻿using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;
using Microsoft.Xna.Framework.Graphics;
using Ship_Game.Graphics;

namespace Ship_Game.GameScreens.EspionageNew
{
    public class InfiltrationOpsLevel1 : UIElementContainer
    {
        readonly InfiltrationScreen Screen;
        readonly Empire Player;
        readonly Font Font;
        readonly UILabel LevelDescription;

        public InfiltrationOpsLevel1(InfiltrationScreen screen, Empire player, in Rectangle rect)
            : base(rect)
        {
            Screen = screen;
            Player = player;
            Font = screen.LowRes ? Fonts.Arial8Bold : Fonts.Arial12;
            LevelDescription = Add(new UILabel("", Font, Color.Wheat));
        }

        public override void PerformLayout()
        {
            base.PerformLayout();
            LevelDescription.Pos  = new Vector2(Rect.X + 5, Rect.Y + 100);
            string description    = Font.ParseText(Localizer.Token(GameText.InfiltrationLevel1Desc), Rect.Width - 10);
            LevelDescription.Text = description;
        }

        public override void Update(float fixedDeltaTime)
        {
            base.Update(fixedDeltaTime);
            if (Screen.SelectedEmpire.isPlayer)
                return;

            Ship_Game.Espionage espionage = Player.GetRelations(Screen.SelectedEmpire).Espionage;
            //LevelDescription.Visible = espionage.Level < 1;
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            base.Draw(batch, elapsed);
        }
    }
}
