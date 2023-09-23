using Microsoft.Xna.Framework.Graphics;
using SDGraphics;
using SDUtils;
using Ship_Game.AI;
using Ship_Game;
using Ship_Game.GameScreens.MainMenu;
using Ship_Game.GameScreens.NewGame;
using Ship_Game.GameScreens.ShipDesign;
using Ship_Game.Ships;
using Ship_Game.UI;
using Ship_Game.Universe;
using Ship_Game.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using Ship_Game.Fleets;
using static Ship_Game.FleetDesignScreen;
using Ship_Game.Audio;
using static Ship_Game.Fleets.Fleet;
using System.Diagnostics.Contracts;
using static Ship_Game.UniverseScreen;
using Microsoft.Xna.Framework.Input;

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
        TeamDropDown ArenaTeamDropDown;

        SubmenuScrollList<ArenaDesignShipListItem> ShipDesignsSubMenu;
        ScrollList<ArenaDesignShipListItem> ShipDesignsScrollList;
        ShipInfoOverlayComponent ShipInfoOverlay;

        List<TeamToSpawn> TeamsToSpawnList = new();

        IShipDesign ActiveShipDesign;

        TeamOptions teamOption;

        readonly List<ArenaScreen.ClickableNode> SelectedNodeList = new();
        readonly List<ArenaScreen.ClickableNode> HoveredNodeList = new();
        readonly List<ArenaScreen.ClickableNode> ClickableNodes = new();

        bool IsInFight = false;
        struct ClickableNode
        {
            public ShipToSpawn NodeToClick;
            public Vector2 ScreenPos;
            public float Radius;
            public TeamOptions TeamOptions;
        }

        public ArenaScreen() : base(null, toPause: null)
        {
            // create a miniature dummy universe
            string playerPreference = "United";
            int numOpponents = 1;
            Universe = DeveloperUniverse.Create(playerPreference, numOpponents);
            Player = Universe.UState.Player;

            foreach (var Opponent1 in Universe.UState.Empires)
            {
                foreach (var Opponent2 in Universe.UState.Empires)
                {
                    if (Opponent2 != Opponent1)
                    {
                        Opponent1.AI.DeclareWarOn(Opponent2, WarType.GenocidalWar);//Genocide? i love genocide)))
                    }
                }
            }

            foreach (TeamOptions item in Enum.GetValues(typeof(TeamOptions)).Cast<TeamOptions>())
            {
                TeamsToSpawnList.Add(new TeamToSpawn(item));
            }
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
            Universe.UState.CanShowDiplomacyScreen = false;

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
            Reset.OnClick = ResetArena;

            Vector2 designSelSize = new(SelectSize(260, 280, 320), SelectSize(250, 400, 500));
            var hullSelectPos = new Vector2(ScreenWidth - designSelSize.X, 100);
            RectF rect = new RectF(hullSelectPos, designSelSize);

            ShipDesignsSubMenu = Add(new SubmenuScrollList<ArenaDesignShipListItem>(rect, GameText.AvailableDesigns));
            ShipDesignsSubMenu.SetBackground(Colors.TransparentBlackFill);
            ShipDesignsSubMenu.SelectedIndex = 0;

            ShipDesignsScrollList = ShipDesignsSubMenu.List;
            ShipDesignsScrollList.EnableItemHighlight = true;
            ShipDesignsScrollList.OnClick = OnDesignShipItemClicked;
            ShipDesignsScrollList.OnHovered = (item) =>
            {
                if (item == null) // deselected?
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
            
            base.LoadContent();

            ResetShips();
        }

        public override void ExitScreen()
        {
            Universe.ExitScreen(); // cleanup Universe
            base.ExitScreen();
        }
        void StartFight(UIButton uIButton)
        {
            foreach(var team in TeamsToSpawnList)
            {
                foreach(var ship in team.SpawnList)
                {
                    Vector2 rotation;
                    switch (team.Team)
                    {
                        case TeamOptions.Team1:
                            rotation = Vector2.Right;
                            break;
                        case TeamOptions.Team2:
                            rotation = Vector2.Left;
                            break;
                        default:
                            rotation = Vector2.Zero;
                            break;
                    }

                    Ship.CreateShipAtPoint(Universe.UState, ship.Design.Name, Universe.UState.ActiveEmpires[(int)team.Team], ship.Pos).Rotation = rotation.ToRadians();
                    
                }
            }
            Universe.UState.Paused = false;
            ShipDesignsSubMenu.Hidden = true;
            IsInFight = true;
        }
        void ResetArena(UIButton uIButton)
        {
            IsInFight = false;
            ResetShips();
            Universe.UState.Paused = true;
            ShipDesignsSubMenu.Hidden = false;
        }
        void ResetShips()
        {
            foreach (var ship in Universe.UState.Ships)
            {
                ship.DebugDamage(100000000);
            }
        }

        public override bool HandleInput(InputState input)
        {
            if (!IsInFight && input.PauseGame)
                return true;

            if (base.HandleInput(input))
                return true;

            if (Universe.HandleInput(input))
                return true;

            if (input.LeftMouseClick && HandleNodeSelection(input))
                return true;

            if (ActiveShipDesign != null && HandleActiveShipDesignInput(input))
                return true;

            if (input.KeysCurr.GetPressedKeys().Contains(Keys.Delete) && HandleSelectShipDesignsDelete())
                return true;

            return false;
        }

        private bool HandleSelectShipDesignsDelete()
        {
            foreach (var spawnToDelete in SelectedNodeList)
            {
                foreach(var team in TeamsToSpawnList)
                {
                    team.SpawnList.Remove(spawnToDelete.NodeToClick);
                }
            }
            return true;
        }

        private bool HandleNodeSelection(InputState input)
        {
            foreach(var node in ClickableNodes)
            {
                if (input.CursorPosition.InRadius(node.ScreenPos, node.Radius))
                {
                    SelectedNodeList.Add(node);
                    return true;
                }
            }
            SelectedNodeList.Clear();
            return false;
        }

        bool HandleActiveShipDesignInput(InputState input)
        {
            if (input.LeftMouseClick && !ShipDesignsScrollList.HitTest(input.CursorPosition))
            {
                var Team = TeamsToSpawnList.First((t) =>
                {
                    return t.Team == teamOption;
                });
                Team.SpawnList.Add(new ShipToSpawn(ActiveShipDesign, Universe.CursorWorldPosition2D));

                // if we're holding shift key down, allow placing multiple designs
                if (!input.IsShiftKeyDown)
                    ActiveShipDesign = null;
            }

            if (input.RightMouseClick)
            {
                ActiveShipDesign = null;
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
            UpdateClickableNodes();
            // update our UI after universe UI
            base.Update(fixedDeltaTime);
        }
        void UpdateClickableNodes()
        {
            ClickableNodes.Clear();

            foreach (var team in TeamsToSpawnList)
            {
                foreach(var spawn in team.SpawnList)
                {
                    ClickableNodes.Add(new()
                    {
                        Radius = (float)Universe.ProjectToScreenSize(ResourceManager.GetShipTemplate(spawn.Design.Name).Radius),
                        ScreenPos = Universe.ProjectToScreenPosition(spawn.Pos).ToVec2f(),
                        NodeToClick = spawn,
                        TeamOptions = team.Team,
                    });
                }
            }
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
                if (!IsInFight)
                {
                    if (ActiveShipDesign != null)
                        DrawActiveShipDesign();

                    if (TeamsToSpawnList != null)
                        DrawShipsIcons();
                }

                base.Draw(batch, elapsed);
            }
            batch.SafeEnd();

        }

        void DrawShipsIcons()
        {
            foreach (var team in TeamsToSpawnList)
            {
                foreach (var spawn in team.SpawnList)
                {
                    Color color = GetTacticalIconColor(team, spawn);
                    DrawShipIcon(team.Team, spawn, color);
                }
            }
        }
        void DrawShipIcon(TeamOptions team, ShipToSpawn spawn, Color color)
        {
            DrawIcon(team, spawn.Design, color, Universe.ProjectToScreenPosition(spawn.Pos).ToVec2f());
        }
        void DrawActiveShipDesign()
        {
            DrawIcon(teamOption, ActiveShipDesign, Color.Blue, Input.CursorPosition);
            float radius = (float)Universe.ProjectToScreenSize(ResourceManager.GetShipTemplate(ActiveShipDesign.Name).Radius);
            float boundingR = Math.Max(radius * 1.5f, 16);
            DrawCircle(Input.CursorPosition, boundingR, Player.EmpireColor);
        }
        void DrawIcon(TeamOptions team, IShipDesign design, Color color, Vector2 screenPos)
        {
            TacticalIcon icon = design.GetTacticalIcon();

            float scale = (float)Universe.ProjectToScreenSize(ResourceManager.GetShipTemplate(design.Name).Radius) / 20;
            
            Vector2 rotation;
            switch (team)
            {
                case TeamOptions.Team1:
                    rotation = Vector2.Right;
                    break;
                case TeamOptions.Team2:
                    rotation = Vector2.Left;
                    break;
                default:
                    rotation = Vector2.Zero;
                    break;
            }
            DrawTexture(icon.Primary, screenPos, scale, rotation.ToRadians(), color);
            if (icon.Secondary != null)
                DrawTexture(icon.Secondary, screenPos, scale, rotation.ToRadians(), color);
        }
        Color GetTacticalIconColor(TeamToSpawn node, ShipToSpawn ship)
        {
            if (HoveredNodeList.Any((t) =>
            {
                return t.NodeToClick == ship;
            }) || SelectedNodeList.Any((t) =>
            {
                return t.NodeToClick == ship;
            }))
                return Color.White;

            switch(node.Team)
            {
                case TeamOptions.Team1:
                    return Color.Green;
                case TeamOptions.Team2:
                    return Color.Red;
            }
            return Color.Black;
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
                        categoryItem.AddSubItem(new ArenaDesignShipListItem(design));
                    }
                }
            }
        }
    }
}
