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
        public readonly SeededRandom Random = new SeededRandom();
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
            Universe = DeveloperUniverse.Create(playerPreference, numOpponents, false);
            Universe.UState.Paused = true; // force it back to paused
            Player = Universe.UState.Player;
        }

        public override void LoadContent()
        {
            ScreenManager.ClearScene();
            // load everything needed for the universe UI
            Universe.LoadContent();

            Universe.EmpireUI.IsActive = false;
            Universe.UState.IsFogVisible = false;
            Universe.Player.Research.SetNoResearchLeft(true);
            Universe.Player.IsEconomyEnabled = false;

            Fight = Add(new UIButton(new ButtonStyle(), new Vector2((ScreenArea.X / 5 * 2) - 85, 0), "Fight!!!"));
            Reset = Add(new UIButton(new ButtonStyle(), new Vector2((ScreenArea.X / 5 * 3) - 85, 0), "Reset"));
            MainMenu = Add(new UIButton(new ButtonStyle(), new Vector2((ScreenArea.X) - 170, 0), "Main menu"));

            MainMenu.OnClick = (b) => ScreenManager.GoToScreen(new MainMenuScreen(), clear3DObjects: true);
            Fight.OnClick = StartFight;
            Reset.OnClick = ResetShips;

            RectF shipRect = new(ScreenWidth - 282, 140, 280, 80);
            RectF shipDesignsRect = new(ScreenWidth - shipRect.W - 2, shipRect.Bottom + 5, shipRect.W, 500);

            Vector2 designSelSize = new(SelectSize(260, 280, 320), SelectSize(250, 400, 500));
            var hullSelectPos = new LocalPos(ScreenWidth - designSelSize.X, 100);

            ShipDesignsSubMenu = Add(new SubmenuScrollList<ArenaDesignShipListItem>(hullSelectPos, designSelSize, GameText.AvailableDesigns));
            ShipDesignsSubMenu.SetBackground(Colors.TransparentBlackFill);

            ShipDesignsScrollList = ShipDesignsSubMenu.List;
            ShipDesignsScrollList.EnableItemHighlight = true;

            RefreshDesignsList();

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
            //I want add this screan to menu, cause i want to have ability to compare all ships with each other, not only PlayerCanBuid
            foreach (IShipDesign design in ResourceManager.Ships.Designs)
            {
                categories.AddUnique(design.Role.ToString());
            }

            categories.Sort();

            // then create list of ships by category
            foreach (string cat in categories)
            {
                var categoryItem = new ArenaDesignShipListItem(cat);
                ShipDesignsScrollList.AddItem(categoryItem);

                foreach (IShipDesign design in ResourceManager.Ships.Designs)
                {
                    if (cat == design.Role.ToString())
                    {
                        categoryItem.AddSubItem(new ArenaDesignShipListItem(design, cat));
                    }
                }
            }
        }
    }
}
