using Microsoft.Xna.Framework.Graphics;
using SDGraphics;
using SDUtils;
using Ship_Game.AI;
using Ship_Game.GameScreens.MainMenu;
using Ship_Game.GameScreens.NewGame;
using Ship_Game.GameScreens.ShipDesign;
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
        ShipInfoOverlayComponent ShipInfoOverlay;

        TeamDropDown ArenaTeamDropDown;

        IShipDesign ActiveShipDesign;

        TeamOptions teamOption;

        public ArenaScreen() : base(null, toPause: null)
        {
            // create a miniature dummy universe
            string playerPreference = "United";
            int numOpponents = 2;
            Universe = DeveloperUniverse.Create(playerPreference, numOpponents);
            Universe.UState.Paused = true; // force it back to paused
            Player = Universe.UState.Player;
        }

        public override void LoadContent()
        {
            ScreenManager.ClearScene();
            // load everything needed for the universe UI
            Universe.LoadContent();

            //disable all unnesesery
            Universe.EmpireUI.IsActive = false;
            Universe.UState.IsFogVisible = false;
            Universe.Player.Research.SetNoResearchLeft(true);
            Universe.Player.IsEconomyEnabled = false;

            ArenaTeamDropDown = Add(new TeamDropDown(new Rectangle((int)ScreenArea.X / 4 - 50, 0, 100,  20)));

            foreach (TeamOptions item in Enum.GetValues(typeof(TeamOptions)).Cast<TeamOptions>())
                ArenaTeamDropDown.AddOption(item.ToString(), item);

            ArenaTeamDropDown.PropertyBinding = () => teamOption;

            ShipInfoOverlay = Add(new ShipInfoOverlayComponent(this, Universe.UState));

            Fight = Add(new UIButton(new ButtonStyle(), new Vector2((ScreenArea.X / 5 * 2) - 85, 0), "Fight!!!"));
            Reset = Add(new UIButton(new ButtonStyle(), new Vector2((ScreenArea.X / 5 * 3) - 85, 0), "Reset"));
            MainMenu = Add(new UIButton(new ButtonStyle(), new Vector2((ScreenArea.X) - 170, 0), "Main menu"));

            MainMenu.OnClick = (b) => ScreenManager.GoToScreen(new MainMenuScreen(), clear3DObjects: true);
            Fight.OnClick = StartFight;
            Reset.OnClick = ResetShips;

            RectF shipRect = new(ScreenWidth - 282, 140, 280, 80);
            RectF shipDesignsRect = new(ScreenWidth - shipRect.W - 2, shipRect.Bottom + 5, shipRect.W, 500);

            Vector2 designSelSize = new(SelectSize(260, 280, 320), SelectSize(250, 400, 500));
            var hullSelectPos = new Vector2(ScreenWidth - designSelSize.X, 100);
            RectF rect = new RectF(hullSelectPos, designSelSize);

            ShipDesignsSubMenu = Add(new SubmenuScrollList<ArenaDesignShipListItem>(rect, GameText.AvailableDesigns));
            ShipDesignsSubMenu.SetBackground(Colors.TransparentBlackFill);
            //ShipDesignsSubMenu.Color = new(0, 0, 0, 130);
            ShipDesignsSubMenu.SelectedIndex = 0;

            ShipDesignsScrollList = ShipDesignsSubMenu.List;
            ShipDesignsScrollList.EnableItemHighlight = true;
            ShipDesignsScrollList.OnClick = OnDesignShipItemClicked;
            
            ShipDesignsScrollList.OnHovered = (item) =>
            {
                if (item.Design == null) // deselected?
                {
                    ToolTip.Clear();
                    ShipInfoOverlay.ShowToLeftOf(Vector2.Zero, null); // hide it
                    return;
                }
                string tooltip = "Drag and drop this Ship into the Arena";
                ToolTip.CreateTooltip(tooltip, "", item.BotLeft, minShowTime: 2f);
                ShipInfoOverlay.ShowToLeftOf(item.Pos, item.Design);
            };

            AddAllDesignsToList();

            ShipDesignsSubMenu.PerformLayout();

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
            if (Universe.HandleInput(input))
                return true;
            if (input.LeftMouseClick && ActiveShipDesign != null)
            {

                //Ship.CreateShipAtPoint(Universe.UState, Ship.CreateShipAtPoint(Universe.UState, ActiveShipDesign.Name,
                //    Universe.UState.ActiveEmpires[(int)teamOption], Universe.CursorWorldPosition.ToVec2()), Universe.UState.ActiveEmpires[(int)teamOption], Universe.CursorWorldPosition.ToVec2());
                Ship.CreateShipAtPoint(Universe.UState, ActiveShipDesign.Name,
                    Universe.UState.ActiveEmpires[(int)teamOption], Universe.CursorWorldPosition.ToVec2());
                return true;
            }
            return false;
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
        void OnDesignShipItemClicked(ArenaDesignShipListItem item)
        {
            // set the design as active so it can be placed
            if (item.Design != null)
                ActiveShipDesign = item.Design;
        }
        void AddAllDesignsToList()
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
