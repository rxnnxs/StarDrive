using Microsoft.Xna.Framework.Graphics;
using SDGraphics;
using SDUtils;
using Ship_Game.GameScreens.MainMenu;
using Ship_Game.GameScreens.NewGame;
using Ship_Game.Ships;
using Ship_Game.UI;
using Ship_Game.Universe;
using Ship_Game.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ship_Game
{
    public sealed class ArenaScreen : GameScreen
    {
        readonly UniverseScreen Universe;
        public readonly ProgressCounter Progress = new ProgressCounter();

        Empire Player;
        UIButton Fight;
        UIButton Reset;
        UIButton MainMenu;

        SubmenuScrollList<ArenaDesignShipListItem> ShipDesignsSubMenu;
        ScrollList<ArenaDesignShipListItem> ShipDesignsScrollList;

        public ArenaScreen() : base(null, toPause: null)
        {
            // create a miniature dummy universe
            string playerPreference = "United";
            int numOpponents = 1;
            Universe = DeveloperUniverse.Create(playerPreference, numOpponents);
            Player = Universe.UState.Player;
        }

        public override void LoadContent()
        {
            ScreenManager.ClearScene();

            Fight = Add(new UIButton(new ButtonStyle(), new Vector2((ScreenArea.X / 5 * 2) - 85, 0), "Fight!!!"));
            Reset = Add(new UIButton(new ButtonStyle(), new Vector2((ScreenArea.X / 5 * 3) - 85, 0), "Reset"));
            MainMenu = Add(new UIButton(new ButtonStyle(), new Vector2((ScreenArea.X) - 170, 0), "Main menu"));

            MainMenu.OnClick = (b) => ScreenManager.GoToScreen(new MainMenuScreen(), clear3DObjects: true);
            Fight.OnClick = StartFight;
            Reset.OnClick = ResetShips;

            RectF shipRect = new(ScreenWidth - 282, 140, 280, 80);
            RectF shipDesignsRect = new(ScreenWidth - shipRect.W - 2, shipRect.Bottom + 5, shipRect.W, 500);

            Vector2 hullSelSize = new(SelectSize(260, 280, 320), SelectSize(250, 400, 500));
            var hullSelectPos = new LocalPos(ScreenWidth - hullSelSize.X, 100);

            ShipDesignsSubMenu = Add(new SubmenuScrollList<ArenaDesignShipListItem>(hullSelectPos, hullSelSize, GameText.AvailableDesigns));
            ShipDesignsSubMenu.SetBackground(Colors.TransparentBlackFill);

            ShipDesignsScrollList = ShipDesignsSubMenu.List;
            ShipDesignsScrollList.EnableItemHighlight = true;

            RefreshDesignsList();
            //ShipDesignsScrollList.Update(0);

            Log.Info("Loaded Content");
            base.LoadContent();
        }

        public override void ExitScreen()
        {
            Universe.ExitScreen(); // cleanup Universe
            base.ExitScreen();
        }

        void StartFight(UIButton uIButton)
        {

        }

        void ResetShips(UIButton uIButton)
        {

        }

        public override bool HandleInput(InputState input)
        {
            return base.HandleInput(input);
        }

        public override void Update(float fixedDeltaTime)
        {
            base.Update(fixedDeltaTime);
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            ScreenManager.BeginFrameRendering(elapsed, ref View, ref Projection);
            ScreenManager.ClearScreen(Color.Black);
            batch.SafeBegin();
            {
                base.Draw(batch, elapsed); // draw automatic elements on top of everything else
            }
            batch.SafeEnd();
            ScreenManager.EndFrameRendering();
        }

        void RefreshDesignsList()
        {
            ShipDesignsScrollList.Reset();

            var categories = new Array<string>();
            // collect the role category titles, e.g. "Carrier"
            foreach (IShipDesign design in Player.ShipsWeCanBuild)
            {
                categories.AddUnique(design.Role.ToString());
            }

            categories.Sort();

            // then create list of ships by category
            foreach (string cat in categories)
            {
                var categoryItem = new ArenaDesignShipListItem(cat);
                ShipDesignsScrollList.AddItem(categoryItem);

                foreach (IShipDesign design in Player.ShipsWeCanBuild)
                {
                    if (cat == design.Role.ToString())
                    {
                        categoryItem.AddSubItem(new ArenaDesignShipListItem(design, cat));
                    }
                }
            }
        }

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
}
