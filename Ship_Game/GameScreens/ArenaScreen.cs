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
            Universe.UState.Paused = true; // force it back to paused
            Player = Universe.UState.Player;
        }

        public override void LoadContent()
        {
            ScreenManager.ClearScene();

            // load everything needed for the universe UI
            Universe.LoadContent();

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
            // first this screens input
            if (base.HandleInput(input))
                return true;
            // and finally the background universe input
            return Universe.HandleInput(input);
        }

        public override void Update(float fixedDeltaTime)
        {
            // this updates everything in the universe UI
            // the actual simulation is done in the universe sim background thread
            // it can be paused via Universe.UState.Paused = true
            Universe.Update(fixedDeltaTime);

            // update our UI after universe UI
            base.Update(fixedDeltaTime);
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            // draw the universe behind everything
            // universe manages its own sprite batching
            // it also clears the screen and draws 3D objects for us
            Universe.Draw(batch, elapsed);

            batch.SafeBegin();
            {
                // draw our UIElementV2 elements ontop of everything
                base.Draw(batch, elapsed);
            }
            batch.SafeEnd();
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
