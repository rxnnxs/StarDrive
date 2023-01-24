using Ship_Game.AI;
using Ship_Game.Debug;
using Ship_Game.Ships;
using System;
using System.Collections.Generic;
using System.Linq;
using SDGraphics;
using SDUtils;
using Ship_Game.Audio;
using Ship_Game.Fleets;
using Ship_Game.Gameplay;
using Ship_Game.GameScreens;
using Ship_Game.Spatial;
using Keys = SDGraphics.Input.Keys;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
    public partial class UniverseScreen
    {
        bool HandleGUIClicks(InputState input)
        {
            bool captured = DeepSpaceBuildWindow.HandleInput(input);

            if (MinimapDisplayRect.HitTest(input.CursorPosition) && !SelectingWithBox)
            {
                HandleScrolls(input);
                if (input.LeftMouseDown)
                {
                    Vector2 pos = input.CursorPosition - new Vector2(MinimapDisplayRect.X, MinimapDisplayRect.Y);
                    float num = MinimapDisplayRect.Width / (UState.Size * 2);
                    CamDestination.X = -UState.Size + (pos.X / num); //Fixed clicking on the mini-map on location with negative coordinates -Gretman
                    CamDestination.Y = -UState.Size + (pos.Y / num);
                    snappingToShip = false;
                    ViewingShip = false;
                }
                captured = true;
            }

            // @note Make sure HandleInputs are called here
            if (!LookingAtPlanet)
            {
                captured |= SelectedShip != null && ShipInfoUIElement.HandleInput(input);
                captured |= SelectedPlanet != null && pInfoUI.HandleInput(input);
                captured |= SelectedShipList != null && shipListInfoUI.HandleInput(input);
            }

            if (SelectedSystem == null)
            {
                SystemInfoOverlay.SelectionTimer = 0.0f;
            }
            else
            {
                captured |= !LookingAtPlanet && SystemInfoOverlay.HandleInput(input);
            }

            if (NotificationManager.HandleInput(input))
                return true;

            // @todo Why are these needed??
            captured |= ShipsInCombat.Rect.HitTest(input.CursorPosition);
            captured |= PlanetsInCombat.Rect.HitTest(input.CursorPosition);

            return captured;
        }

        void HandleInputNotLookingAtPlanet(InputState input)
        {
            mouseWorldPos = UnprojectToWorldPosition(input.CursorPosition);

            if (input.DeepSpaceBuildWindow) InputOpenDeepSpaceBuildWindow();
            if (input.FTLOverlay)       ToggleUIComponent("sd_ui_accept_alt3", ref ShowingFTLOverlay);
            if (input.RangeOverlay)     ToggleUIComponent("sd_ui_accept_alt3", ref ShowingRangeOverlay);
            if (input.AutomationWindow && !Debug) aw.ToggleVisibility();
            if (input.PlanetListScreen)  ScreenManager.AddScreen(new PlanetListScreen(this, EmpireUI, "sd_ui_accept_alt3"));
            if (input.ShipListScreen)    ScreenManager.AddScreen(new ShipListScreen(this, EmpireUI, "sd_ui_accept_alt3"));
            if (input.FleetDesignScreen) ScreenManager.AddScreen(new FleetDesignScreen(this, EmpireUI, "sd_ui_accept_alt3"));
            if (input.ZoomToShip) InputZoomToShip();
            if (input.ZoomOut) InputZoomOut();
            if (input.Escaped) DefaultZoomPoints();
            if (input.Tab && !input.LeftCtrlShift) ShowShipNames = !ShowShipNames;

            HandleFleetSelections(input);
            HandleShipSelectionAndOrders();

            if (input.LeftMouseDoubleClick)
                HandleDoubleClickShipsAndSolarObjects(input);

            if (!LookingAtPlanet)
            {
                LeftClickOnClickableItem(input);
                ShipPieMenuClear();
                HandleSelectionBox(input);
            }

            HandleScrolls(input);

            if (Debug)
                HandleDebugEvents(input);
        }

        void HandleDebugEvents(InputState input)
        {
            Empire player = Player;

            if (input.EmpireToggle) 
                player  = input.RemnantToggle ? UState.Remnants : UState.Corsairs;

            if (input.SpawnShip)
                Ship.CreateShipAtPoint(UState, "Bondage-Class Mk IIIa Cruiser", player, mouseWorldPos);

            if (input.SpawnFleet2) HelperFunctions.CreateFirstFleetAt(UState, "Fleet 2", player, mouseWorldPos);
            if (input.SpawnFleet1) HelperFunctions.CreateFirstFleetAt(UState, "Fleet 1", player, mouseWorldPos);

            if (SelectedShip != null)
            {
                if (input.DebugKillShip) // 'X' or 'Delete'
                {
                    // Apply damage as a percent of module health to all modules.
                    var damage = input.IsShiftKeyDown ? 0.9f : 1f;
                    SelectedShip.DebugDamage(damage);
                }

                if (input.BlowExplodingModule) // "N" key
                {
                    if (input.IsShiftKeyDown)
                        SelectedShip.DebugBlowSmallestExplodingModule();
                    else
                        SelectedShip.DebugBlowBiggestExplodingModule();
                }
            }
            else if (SelectedPlanet != null && input.DebugKillShip)
            {
                foreach (string troopType in ResourceManager.TroopTypes)
                    if (ResourceManager.TryCreateTroop(troopType, UState.Remnants, out Troop t))
                        t.TryLandTroop(SelectedPlanet);
            }

            if (input.SpawnRemnant)
                UState.Remnants.Remnants.DebugSpawnRemnant(input, mouseWorldPos);

            if (input.ToggleSpatialManagerType)
                UState.Spatial.ToggleSpatialType();

            if (input.IsShiftKeyDown && input.KeyPressed(Keys.B))
                StressTestShipLoading();
        }

        void HandleInputLookingAtPlanet(InputState input)
        {
            if (input.Tab)
                ShowShipNames = !ShowShipNames;

            var colonyScreen = workersPanel as ColonyScreen;
            bool dismiss = (input.Escaped || input.RightMouseClick) && colonyScreen?.ClickedTroop == false;
            if (dismiss || !workersPanel.IsActive)
            {
                AdjustCamTimer = 1f;
                if (returnToShip)
                {
                    ViewingShip = true;
                    returnToShip = false;
                    snappingToShip = true;
                    CamDestination.Z = transitionStartPosition.Z;
                }
                else
                {
                    CamDestination = transitionStartPosition;
                }
                transitionElapsedTime = 0.0f;
                LookingAtPlanet = false;
            }
        }

        void HandleFleetButtonClick(InputState input)
        {
            InputCheckPreviousShip();
            SelectedShip = null;
            SelectedShipList.Clear();
            SelectedFleet = null;
            foreach (FleetButton fleetButton in FleetButtons)
            {
                if (!fleetButton.ClickRect.HitTest(input.CursorPosition))
                    continue;

                SelectedFleet = fleetButton.Fleet;
                SelectedShipList.Clear();
                for (int j = 0; j < SelectedFleet.Ships.Count; j++)
                {
                    Ship ship = SelectedFleet.Ships[j];
                    if (ship.InPlayerSensorRange)
                        SelectedShipList.AddUnique(ship);
                }
                if (SelectedShipList.Count == 1)
                {
                    InputCheckPreviousShip(SelectedShipList.First);
                    SelectedShip = SelectedShipList.First;
                    ShipInfoUIElement.SetShip(SelectedShip);
                    SelectedShipList.Clear();
                }
                else if (SelectedShipList.Count > 1)
                {
                    shipListInfoUI.SetShipList(SelectedShipList, true);
                }

                SelectedSomethingTimer = 3f;

                if (Input.LeftMouseDoubleClick)
                {
                    ViewingShip = false;
                    AdjustCamTimer = 0.5f;
                    CamDestination = SelectedFleet.AveragePosition().ToVec3d(CamPos.Z);
                    if (viewState < UnivScreenState.SystemView)
                        CamDestination.Z = GetZfromScreenState(UnivScreenState.SystemView);

                    CamDestination.Z = GetZfromScreenState(UnivScreenState.ShipView);
                    return;
                }
            }
        }

        public void ToggleDebugWindow() // toggle Debug Window overlay
        {
            if (DebugWin == null)
                DebugWin = Add(new DebugInfoScreen(this));
            else
                HideDebugWindow();
        }

        public void HideDebugWindow()
        {
            DebugWin?.RemoveFromParent();
            DebugWin = null;
        }

        public override bool HandleInput(InputState input)
        {
            Input = input;

            if (input.PauseGame && !GlobalStats.TakingInput)
                UState.Paused = !UState.Paused;

            if (input.DebugMode)
            {
                UState.SetDebugMode(!UState.Debug);

                foreach (SolarSystem solarSystem in UState.Systems)
                {
                    solarSystem.SetExploredBy(Player);
                    foreach (Planet planet in solarSystem.PlanetList)
                        planet.SetExploredBy(Player);

                    solarSystem.UpdateFullyExploredBy(Player);
                }
            }

            if (Debug)
            {
                if (input.ShowDebugWindow)
                {
                    ToggleDebugWindow();
                }

                if (input.GetMemory)
                {
                    GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
                }
            }

            // ensure universe has the correct light rig
            ResetLighting(forceReset: false);

            HandleEdgeDetection(input);

            UpdateVisibleShields();
            UpdateClickableSystemsPlanetsAndVisibleShields();

            if (HandleDragAORect(input))
                return true;

            if (HandleTradeRoutesDefinition(input))
                return true;

            // Handle new UIElementV2 items
            if (base.HandleInput(input))
                return true;

            for (int i = SelectedShipList.Count - 1; i >= 0; --i)
            {
                Ship ship = SelectedShipList[i];
                if (ship?.Active != true)
                    SelectedShipList.RemoveSwapLast(ship);
            }

            // CG: previous target code.
            if (previousSelection != null && input.PreviousTarget)
                PreviousTargetSelection(input);

            // fbedard: Set camera chase on ship
            if (input.MiddleMouseClick)
            {
                if (ViewingShip)
                    ToggleViewingShip(); // exit easily
                else if (input.IsCtrlKeyDown) // only enter if ctrl key is down
                    ToggleViewingShip();
            }

            if (input.CinematicMode)
                ToggleCinematicMode();

            ShowTacticalCloseup = input.TacticalIcons;

            if (input.QuickSave && !IsSaving)
            {
                SaveDuringNextUpdate($"Quicksave, {Player.data.Traits.Name}, {UState.StarDate.String()}");
            }

            if (input.UseRealLights)
            {
                UseRealLights = !UseRealLights; // toggle real lights
                ResetLighting(forceReset: true);
            }
            if (input.ShowExceptionTracker)
            {
                UState.Paused = true;
                Log.OpenURL(GlobalStats.VanillaDefaults.URL);
            }

            HandleGameSpeedChange(input);

            if (!LookingAtPlanet)
            {
                if (HandleGUIClicks(input))
                    return true;
            }
            else
            {
                SelectedFleet = null;
                InputCheckPreviousShip();
                SelectedShip = null;
                SelectedShipList.Clear();
                SelectedItem = null;
                SelectedSystem = null;
            }

            if (input.ScrapShip && (SelectedItem != null && SelectedItem.AssociatedGoal.Owner == Player))
                OnScrapSelectedItem();

            pickedSomethingThisFrame = false;

            ShipsInCombat.Visible = !LookingAtPlanet;
            PlanetsInCombat.Visible = !LookingAtPlanet;

            if (LookingAtPlanet && workersPanel.HandleInput(input))
                return true;

            if (IsActive && EmpireUI.HandleInput(input))
                return true;

            if (!LookingAtPlanet)
            {
                HandleInputNotLookingAtPlanet(input);
            }
            else
            {
                HandleInputLookingAtPlanet(input);
            }

            if (input.InGameSelect && !pickedSomethingThisFrame && (!input.IsShiftKeyDown && !pieMenu.Visible))
                HandleFleetButtonClick(input);

            return false;
        }

        protected override GameCursor GetCurrentCursor()
        {
            if (IsCinematicModeEnabled)
                return GameCursors.Cinematic;

            if (SelectedFleet != null || SelectedShip != null || SelectedShipList.NotEmpty)
            {
                MoveOrder mo = ShipCommands.GetMoveOrderType();
                if (mo.IsSet(MoveOrder.AddWayPoint))
                {
                    if (mo.IsSet(MoveOrder.Aggressive)) return GameCursors.AggressiveNav;
                    if (mo.IsSet(MoveOrder.StandGround)) return GameCursors.StandGroundNav;
                    return GameCursors.RegularNav;
                }
                else
                {
                    if (mo.IsSet(MoveOrder.Aggressive)) return GameCursors.Aggressive;
                    if (mo.IsSet(MoveOrder.StandGround)) return GameCursors.StandGround;
                    return GameCursors.Regular;
                }
            }
            return GameCursors.Regular;
        }

        static int InputFleetSelection(InputState input)
        {
            if (input.Fleet1) return 1;
            if (input.Fleet2) return 2;
            if (input.Fleet3) return 3;
            if (input.Fleet4) return 4;
            if (input.Fleet5) return 5;
            if (input.Fleet6) return 6;
            if (input.Fleet7) return 7;
            if (input.Fleet8) return 8;
            if (input.Fleet9) return 9;
            return -1;
        }

        void HandleFleetSelections(InputState input)
        {
            int index = InputFleetSelection(input);
            if (index == -1) 
                return;

            Fleet selectedFleet = Player.GetFleetOrNull(index);

            if (input.ReplaceFleet)
            {
                CreateNewFleet(selectedFleet, index);
            }
            else if (input.AddToFleet)
            {
                AddShipsToExistingFleet(selectedFleet, index);
            }
            else
            {
                ShowSelectedFleetInfo(selectedFleet);
            }
        }

        void CreateNewFleet(Fleet selectedFleet, int index)
        {
            // clear the fleet if no ships selected and pressing Ctrl + NumKey[1-9]
            if (SelectedShipList.Count == 0)
            {
                selectedFleet?.Reset();
                RecomputeFleetButtons(true);
                return;
            }

            // else: we have selected some ships, delete old fleet
            selectedFleet?.Reset(returnShipsToEmpireAI: true, clearOrders: false);

            // create new fleet
            Fleet fleet = CreateNewFleet(index, SelectedShipList);

            RecomputeFleetButtons(true);
            UpdateFleetSelection(fleet);
        }

        void AddShipsToExistingFleet(Fleet selectedFleet, int index)
        {
            if (SelectedShipList.Count == 0)
            {
                GameAudio.NegativeClick();
                return;
            }

            Fleet fleet;
            if (selectedFleet?.Ships.Count > 0)
            {
                // create a list of ships that are not part of the target fleet.
                var newShips = SelectedShipList.Filter(s => s.Fleet != selectedFleet);
                if (newShips.Length == 0) // nothing to add
                {
                    GameAudio.NegativeClick();
                    return;
                }

                fleet = AddShipsToFleet(selectedFleet, newShips);
            }
            else
            {
                fleet = CreateNewFleet(index, SelectedShipList);
            }

            UpdateFleetSelection(fleet);

            if (fleet.Name.IsEmpty() || fleet.Name.Contains("Fleet"))
                fleet.Name = Fleet.GetDefaultFleetName(index);

            fleet.Update(FixedSimTime.Zero /*paused during init*/);

            RecomputeFleetButtons(true);
        }

        void ShowSelectedFleetInfo(Fleet selectedFleet)
        {
            bool snapToFleet = SelectedFleet == selectedFleet; // user pressed fleet Number twice
            InputCheckPreviousShip();

            SelectedPlanet = null;
            SelectedShip = null;
            SelectedFleet = null;

            // nothing selected
            if (selectedFleet == null)
                return;

            if (selectedFleet.Ships.Count > 0)
            {
                SelectedFleet = selectedFleet;
                UpdateFleetSelection(selectedFleet);
                GameAudio.FleetClicked();
            }

            if (SelectedShipList.Count == 1)
            {
                InputCheckPreviousShip(SelectedShipList.First);
            }

            if (SelectedFleet != null && snapToFleet)
            {
                ViewingShip = false;
                AdjustCamTimer = 0.5f;
                CamDestination = SelectedFleet.AveragePosition().ToVec3d(CamDestination.Z);

                if (CamPos.Z < GetZfromScreenState(UnivScreenState.SystemView))
                    CamDestination.Z = GetZfromScreenState(UnivScreenState.PlanetView);
            }
        }

        void UpdateFleetSelection(Fleet newlySelectedFleet)
        {
            SelectedFleet = newlySelectedFleet;
            SelectedShipList.Assign(newlySelectedFleet.Ships);
            SelectedSomethingTimer = 3f;

            if (newlySelectedFleet.Ships.Count == 1)
            {
                SelectedShip = SelectedShipList.First;
            }
            else
            {
                shipListInfoUI.SetShipList(SelectedShipList, isFleet: true);
            }
        }

        void UpdateVisibleShields()
        {
            Array<Shield> shields = new();

            Ship[] ships = UState.Objects.VisibleShips;
            for (int i = 0; i < ships.Length; i++)
            {
                Ship ship = ships[i];
                if (ship.Active && ship.ShieldMax > 0f && ship.IsVisibleToPlayerInMap)
                {
                    shields.AddRange(ship.GetActiveShields().Select(s => s.Shield));
                }
            }

            // TODO: this needs to be rewritten
            Shields.SetVisibleShields(shields.ToArr());
        }

        void UpdateClickableSystemsPlanetsAndVisibleShields()
        {
            if (viewState <= UnivScreenState.SectorView)
            {
                Array<Shield> visibleShields = new();

                Planet[] planets = UState.GetVisiblePlanets();
                foreach (Planet planet in planets)
                    if (planet.Shield != null && planet.IsExploredBy(Player))
                        visibleShields.Add(planet.Shield);

                Shields.SetVisiblePlanetShields(visibleShields.ToArr());
            }
        }

        bool CanClickOnShip(SpatialObjectBase go)
        {
            return go is Ship { InPlayerSensorRange: true } ship
                // feature: if we're zoomed OUT a lot, ignore subspace projector clicks
                && (!ship.IsSubspaceProjector || CamPos.Z <= 1_200_000.0);
        }

        public Ship[] GetVisibleShipsInScreenRect(in RectF screenRect, int maxResults = 1024)
        {
            AABoundingBox2D worldRect = UnprojectToWorldRect(new(screenRect));
            SearchOptions opt = new(worldRect, GameObjectType.Ship)
            {
                MaxResults = maxResults,
                SortByDistance = true, // only care about closest results
                FilterFunction = CanClickOnShip
            };
            return UState.Spatial.FindNearby(ref opt).FastCast<SpatialObjectBase, Ship>();
        }

        Ship FindClickedShip(InputState input)
        {
            const float ClickRadius = 5f;
            AABoundingBox2D clickRect = UnprojectToWorldRect(new(input.CursorPosition, ClickRadius));
            SearchOptions opt = new(clickRect, GameObjectType.Ship)
            {
                MaxResults = 32,
                SortByDistance = true, // only care about closest results
                FilterFunction = CanClickOnShip
            };
            return UState.Spatial.FindNearby(ref opt).FirstOrDefault() as Ship;
        }

        Planet FindPlanetUnderCursor(float searchRadius = 500)
        {
            Vector3d worldPos = UnprojectToWorldPosition3D(Input.CursorPosition, ZPlane: 2500);
            Planet p = UState.FindPlanetAt(worldPos.ToVec2f(), searchRadius: searchRadius);
            return p != null && p.ParentSystem.IsExploredBy(Player) ? p : null;
        }

        // should be called for >= SectorView
        SolarSystem FindSolarSystemUnderCursor()
        {
            float hitRadius = 10_000;
            if (CamPos.Z >= 1_500_000)
                hitRadius = 25_000;
            return UState.FindSolarSystemAt(CursorWorldPosition2D, hitRadius: hitRadius);
        }

        Fleet CheckFleetClicked()
        {
            foreach(ClickableFleet clickableFleet in ClickableFleetsList)
            {
                if (!Input.CursorPosition.InRadius(clickableFleet.ScreenPos, clickableFleet.ClickRadius)) continue;
                return clickableFleet.fleet;
            }
            return null;
        }

        ClickableSpaceBuildGoal GetSpaceBuildGoalUnderCursor()
        {
            var goals = ClickableBuildGoals;
            for (int i = 0; i < goals.Length; ++i)
            {
                ClickableSpaceBuildGoal goal = goals[i];
                if (Input.CursorPosition.InRadius(goal.ScreenPos, goal.Radius))
                    return goal;
            }
            return null;
        }

        bool ShipPieMenu(Ship ship)
        {
            if (ship == null || ship != SelectedShip || SelectedShip.IsHangarShip ||
                SelectedShip.IsConstructor) return false;

            LoadShipMenuNodes(ship.Loyalty == Player ? 1 : 0);
            if (!pieMenu.Visible)
            {
                pieMenu.RootNode = shipMenu;
                pieMenu.Show(pieMenu.Position);
            }
            else
                pieMenu.ChangeTo(null);
            return true;
        }

        bool ShipPieMenuClear()
        {
            if (SelectedShip != null || SelectedShipList.Count != 0 || SelectedPlanet == null || !Input.ShipPieMenu)
                return false;
            if (!pieMenu.Visible)
            {
                pieMenu.RootNode = planetMenu;
                if (SelectedPlanet.Owner == null && SelectedPlanet.Habitable)
                    LoadMenuNodes(false, true);
                else
                    LoadMenuNodes(false, false);
                pieMenu.Show(pieMenu.Position);
            }
            else
                pieMenu.ChangeTo(null);
            return true;
        }

        bool UnselectableShip(Ship ship = null)
        {
            ship = ship ?? SelectedShip;
            if (!ship.IsConstructor && !ship.IsSupplyShuttle)
                return false;

            GameAudio.NegativeClick();
            return true;
        }

        bool SelectShipClicks(InputState input)
        {
            Ship ship = FindClickedShip(input);
            if (ship != null && !pickedSomethingThisFrame)
            {
                pickedSomethingThisFrame = true;
                GameAudio.ShipClicked();
                SelectedSomethingTimer = 3f;

                if (SelectedShipList.Count > 0 && input.IsShiftKeyDown)
                {
                    // remove existing ship?
                    if (SelectedShipList.RemoveRef(ship))
                        return true;

                    // ok, no, add a new ship instead?
                    return SelectedShipList.AddUniqueRef(ship);
                }

                SelectedShipList.Clear();
                SelectedShipList.AddUniqueRef(ship);
                SelectedShip = ship;
                return true;
            }
            return false;
        }

        void LeftClickOnClickableItem(InputState input)
        {
            if (input.ShipPieMenu)
            {
                ShipPieMenu(SelectedShip);
            }

            pieMenu.HandleInput(input);
            if (!input.LeftMouseClick || pieMenu.Visible)
                return;

            if (SelectedShip != null && previousSelection != SelectedShip) //fbedard
                previousSelection = SelectedShip;

            SelectedShip    = null;
            SelectedPlanet  = null;
            SelectedFleet   = null;
            CurrentGroup    = null;
            SelectedSystem  = null;
            SelectedItem    = null;
            Project.Started = false;

            if (viewState >= UnivScreenState.SectorView)
            {
                if ((SelectedSystem = FindSolarSystemUnderCursor()) != null)
                {
                    GameAudio.MouseOver();
                    SystemInfoOverlay.SetSystem(SelectedSystem);
                    return;
                }
            }

            if ((SelectedFleet = CheckFleetClicked()) != null)
            {
                SelectedShipList.Clear();
                shipListInfoUI.ClearShipList();
                pickedSomethingThisFrame = true;
                GameAudio.FleetClicked();
                SelectedShipList.AddRange(SelectedFleet.Ships);
                shipListInfoUI.SetShipList(SelectedShipList, false);
                return;
            }

            SelectShipClicks(input);

            if (SelectedShip != null && SelectedShipList.Count > 0)
                ShipInfoUIElement.SetShip(SelectedShip);
            else if (SelectedShipList.Count > 1)
                shipListInfoUI.SetShipList(SelectedShipList, false);


            if (SelectedShipList.Count == 1)
            {
                LoadShipMenuNodes(SelectedShipList[0].Loyalty == Player ? 1 : 0);
                return;
            }

            float planetSelectRadius = (viewState <= UnivScreenState.SystemView) ? 500 : 4000;
            SelectedPlanet = FindPlanetUnderCursor(planetSelectRadius);
            if (SelectedPlanet != null)
            {
                SelectedSomethingTimer = 3f;
                pInfoUI.SetPlanet(SelectedPlanet);
                if (input.LeftMouseDoubleClick)
                {
                    SnapViewColony(SelectedPlanet.Owner != Player && !Debug);
                    SelectionBox = new();
                }
                else
                    GameAudio.PlanetClicked();
                return;
            }

            if ((SelectedItem = GetSpaceBuildGoalUnderCursor()) != null)
                GameAudio.BuildItemClicked();
        }

        void HandleSelectionBox(InputState input)
        {
            if (SelectedShipList.Count == 1)
            {
                if (SelectedShip != null && previousSelection != SelectedShip && SelectedShip != SelectedShipList[0])
                    previousSelection = SelectedShip;
                SelectedShip = SelectedShipList[0];
            }

            if (input.LeftMouseHeld(0.1f)) // we started dragging selection box
            {
                SelectionBox = input.LeftHold.GetSelectionBox();
                SelectingWithBox = true;
                return;
            }

            if (!SelectingWithBox) // mouse released, but we weren't selecting
                return;

            if (SelectingWithBox) // trigger! mouse released after selecting
                SelectingWithBox = false;

            SelectedShipList = GetAllShipsInArea(SelectionBox, input, out Fleet fleet);
            if (SelectedShipList.Count == 0)
            {
                SelectionBox = new(0, 0, -1, -1);
                return;
            }

            SelectedPlanet = null;
            SelectedShip   = null;
            shipListInfoUI.SetShipList(SelectedShipList, fleet != null);
            SelectedFleet = fleet;

            if (SelectedShipList.Count == 1)
            {
                if (SelectedShip != null && previousSelection != SelectedShip &&
                    SelectedShip != SelectedShipList[0]) //fbedard
                {
                    previousSelection = SelectedShip;
                }

                SelectedShip = SelectedShipList[0];
                ShipInfoUIElement.SetShip(SelectedShip);
                LoadShipMenuNodes(SelectedShipList[0]?.Loyalty == Player ? 1 : 0);
            }

            SelectionBox = new(0, 0, -1, -1);
        }

        static bool IsCombatShip(Ship ship)
        {
            return NonCombatShip(ship) == false;
        }

        static bool NonCombatShip(Ship ship)
        {
            return ship != null
                && (ship.ShipData.Role <= RoleName.freighter 
                    || ship.ShipData.ShipCategory == ShipCategory.Civilian 
                    || ship.DesignRole == RoleName.troop
                    || ship.Weapons.Count == 0 && !ship.Carrier.HasFighterBays
                    || ship.AI.State == AIState.Colonize);
        }

        Array<Ship> GetAllShipsInArea(in RectF screenArea, InputState input, out Fleet fleet)
        {
            fleet = null;
            Ship[] potentialShips = GetVisibleShipsInScreenRect(screenArea);
            if (potentialShips.Length == 0)
                return new();

            bool hasCombatShips = potentialShips.Any(IsCombatShip);

            // TODO: These are not documented to the players
            bool addToSelection = input.IsShiftKeyDown;
            bool selectAll      = input.IsCtrlKeyDown || !hasCombatShips;
            bool nonPlayer      = input.IsAltKeyDown || !potentialShips.Any(s => s.Loyalty.isPlayer);
            bool onlyPlayer     = !nonPlayer && potentialShips.Any(s => s.Loyalty.isPlayer);

            var ships = new Array<Ship>();
            if (addToSelection)
                ships.AddRange(SelectedShipList);

            foreach (Ship ship in potentialShips)
            {
                if       (onlyPlayer && ship.Loyalty.isPlayer) ships.AddUnique(ship);
                else if  (nonPlayer && !ship.Loyalty.isPlayer) ships.AddUnique(ship);
            }

            if (onlyPlayer && !selectAll && fleet == null) // Need to remove non combat ship.
            {
                ships.RemoveAll(NonCombatShip);
            }

            if (onlyPlayer && !hasCombatShips)
            {
                // if we selected a bunch of civilian ships, but some of them are troop transports
                // then discard all ships that aren't troop transports
                bool hasTroopTransports = potentialShips.Any(s => s.IsSingleTroopShip);
                if (hasTroopTransports)
                    ships.RemoveAll(s => !s.IsSingleTroopShip);
            }

            if (onlyPlayer && ships.Count > 0 && ships.First.Fleet != null)
            {
                Fleet groupFleet = ships.Any(s => s.Fleet != ships.First.Fleet) ? null : ships.First.Fleet;
                if (groupFleet != null && groupFleet.Ships.Count == ships.Count)
                    fleet = groupFleet; // All the fleet was selected
            }

            return ships;
        }

        public void UpdateClickableItems()
        {
            var buildGoals = new Array<ClickableSpaceBuildGoal>();
            EmpireAI playerAI = Player.AI;

            // ToArray() used for thread safety
            foreach (Goal goal in playerAI.Goals.ToArr())
            {
                if (goal.IsDeploymentGoal)
                {
                    ProjectToScreenCoords(goal.BuildPosition, 100f, out Vector2d buildPos, out double clickableRadius);
                    buildGoals.Add(new ClickableSpaceBuildGoal
                    {
                        ScreenPos = buildPos.ToVec2f(),
                        BuildPos = goal.BuildPosition,
                        Radius = (float)(clickableRadius + 10),
                        UID = goal.ToBuild.Name,
                        AssociatedGoal = goal
                    });
                }
            }
            ClickableBuildGoals = buildGoals.ToArray();
        }

        bool HandleTradeRoutesDefinition(InputState input)
        {
            if (!DefiningTradeRoutes)
                return false;

            DefiningTradeRoutes = !DefiningAO;
            HandleScrolls(input); // allow exclusive scrolling during Trade Route define
            if (!LookingAtPlanet && HandleGUIClicks(input))
                return true;

            if (input.LeftMouseClick || input.RightMouseClick)
                InputPlanetsForTradeRoutes(input); // add or remove a planet from the list

            if (SelectedShip == null || input.Escaped) // exit the trade routes mode
            {
                DefiningTradeRoutes = false;
                return true;
            }
            return true;
        }

        void InputPlanetsForTradeRoutes(InputState input)
        {
            if (viewState > UnivScreenState.SystemView)
                return;

            Planet planet = FindPlanetUnderCursor();
            if (planet != null)
            {
                if (input.LeftMouseClick)
                {
                    if (SelectedShip.AddTradeRoute(planet))
                        GameAudio.AcceptClick();
                    else
                        GameAudio.NegativeClick();
                }
                else
                {
                    SelectedShip.RemoveTradeRoute(planet);
                    GameAudio.AffirmativeClick();
                }
            }
        }

        bool HandleDragAORect(InputState input)
        {
            if (!DefiningAO)
                return false;

            DefiningAO = !DefiningTradeRoutes;
            HandleScrolls(input); // allow exclusive scrolling during AO define
            if (!LookingAtPlanet && HandleGUIClicks(input))
                return true;

            if (input.RightMouseClick) // erase existing AOs
            {
                Vector2 cursorWorld = UnprojectToWorldPosition(input.CursorPosition);
                SelectedShip.AreaOfOperation.RemoveFirst(ao => ao.HitTest(cursorWorld));
                return true;
            }

            // no ship selection? abort
            // Easier out from defining an AO. Used to have to left and Right click at the same time.    -Gretman
            if (SelectedShip == null || input.Escaped)
            {
                DefiningAO = false;
                return true;
            }

            if (input.LeftMouseHeld(0.1f))
            {
                Vector2 start = UnprojectToWorldPosition(input.StartLeftHold);
                Vector2 end   = UnprojectToWorldPosition(input.EndLeftHold);
                AORect = new Rectangle((int)Math.Min(start.X, end.X),  (int)Math.Min(start.Y, end.Y), 
                                       (int)Math.Abs(end.X - start.X), (int)Math.Abs(end.Y - start.Y));
            }
            else if ((AORect.Width+AORect.Height) > 1000 && input.LeftMouseReleased)
            {
                if (AORect.Width >= 5000 && AORect.Height >= 5000)
                {
                    GameAudio.EchoAffirmative();
                    SelectedShip.AreaOfOperation.Add(AORect);
                }
                else
                {
                    GameAudio.NegativeClick(); // eek-eek! AO not big enough!
                }
                AORect = Rectangle.Empty;
            }
            return true;
        }

        void HandleDoubleClickShipsAndSolarObjects(InputState input)
        {
            SelectedShipList.Clear();
            if (SelectedShip != null && previousSelection != SelectedShip) //fbedard
                previousSelection = SelectedShip;
            
            SelectedShip = null;

            if (viewState <= UnivScreenState.SystemView)
            {
                SelectedPlanet = FindPlanetUnderCursor();
                if (SelectedPlanet != null)
                {
                    GameAudio.SubBassWhoosh();
                    SnapViewColony(SelectedPlanet.Owner != Player && !Debug);
                }
            }

            SelectMultipleShipsByClickingOnShip(input);

            if (viewState >= UnivScreenState.SectorView)
            {
                SolarSystem system = FindSolarSystemUnderCursor();
                if (system != null)
                {
                    if (system.IsExploredBy(Player))
                    {
                        GameAudio.SubBassWhoosh();
                        ViewSystem(system);
                    }
                    else
                    {
                        GameAudio.NegativeClick();
                    }
                }
            }
        }

        void SelectMultipleShipsByClickingOnShip(InputState input)
        {
            Ship clicked = FindClickedShip(input);
            if (clicked != null)
            {
                pickedSomethingThisFrame = true;
                SelectedShipList.AddUnique(clicked);
                
                Ship[] ships = UState.Objects.VisibleShips;
                foreach (Ship ship in ships)
                {
                    if (clicked == ship || ship.Loyalty != clicked.Loyalty)
                        continue;

                    bool sameHull   = ship.BaseHull == clicked.BaseHull;
                    bool sameRole   = ship.DesignRole == clicked.DesignRole;
                    bool sameDesign = ship.Name == clicked.Name;
                    
                    // TODO: These are not documented to the players
                    if (input.SelectSameDesign) // Ctrl+Alt+DoubleClick
                    {
                        if (sameDesign)
                            SelectedShipList.AddUnique(ship);
                    }
                    else if (input.SelectSameRoleAndHull) // Ctrl+DoubleClick
                    {
                        if (sameRole && sameHull)
                            SelectedShipList.AddUnique(ship);
                    }
                    else if (input.SelectSameHull) // Alt+DoubleClick
                    {
                        if (sameHull)
                            SelectedShipList.AddUnique(ship);
                    }
                    else // simple DoubleClick, select Same Role
                    {
                        if (sameRole)
                            SelectedShipList.AddUnique(ship);
                    }
                }
            }
        }

        void PreviousTargetSelection(InputState input)
        {
            if (previousSelection.Active)
            {
                Ship tempship = previousSelection;
                if (SelectedShip != null && SelectedShip != previousSelection)
                    previousSelection = SelectedShip;
                SelectedShip = tempship;
                ShipInfoUIElement.SetShip(SelectedShip);
                SelectedFleet  = null;
                SelectedItem   = null;
                SelectedSystem = null;
                SelectedPlanet = null;
                SelectedShipList.Clear();
                SelectedShipList.Add(SelectedShip);
                ViewingShip = false;
            }
            else
                previousSelection = null;  //fbedard: remove inactive ship
        }
        
        void CyclePlanetsInCombat(UIButton b)
        {
            if (Player.empirePlanetCombat > 0)
            {
                Planet planetToView = null;
                int planetIdx = 0;

                // try to select the next planet which is in combat
                foreach (SolarSystem system in UState.Systems)
                {
                    foreach (Planet p in system.PlanetList)
                    {
                        if (p.IsExploredBy(Player) && p.RecentCombat)
                        {
                            if (p.Owner?.isPlayer == true || p.Troops.WeHaveTroopsHere(UState.Player))
                            {
                                if (planetIdx == nextPlanetCombat)
                                    planetToView = p;
                                ++planetIdx;
                            }
                        }
                    }
                }
                
                ++nextPlanetCombat;
                if (nextPlanetCombat >= Player.empirePlanetCombat)
                    nextPlanetCombat = 0;

                if (planetToView == null) return;
                if (SelectedShip != null && previousSelection != SelectedShip) //fbedard
                    previousSelection = SelectedShip;
                SelectedShip = null;
                SelectedFleet = null;
                SelectedItem = null;
                SelectedSystem = null;
                SelectedPlanet = planetToView;
                SelectedShipList.Clear();
                pInfoUI.SetPlanet(planetToView);

                CamDestination = new Vector3d(SelectedPlanet.Position.X, SelectedPlanet.Position.Y, 9000.0);
                transitionStartPosition = CamPos;
                transitionElapsedTime = 0.0f;
                LookingAtPlanet = false;
                AdjustCamTimer = 2f;
                transDuration = 5f;
                returnToShip = false;
                ViewingShip = false;
                snappingToShip = false;
                SelectedItem = null;
            }
  
        }

        void InputCheckPreviousShip(Ship ship = null)
        {
            // previously selected ship is not null, and we selected a new ship, and new ship is not previous ship
            if (SelectedShip != null && previousSelection != SelectedShip && SelectedShip != ship)
            {
                previousSelection = SelectedShip;
            }
        }

        void OnScrapSelectedItem()
        {
            Player.AI.RemoveGoal(SelectedItem.AssociatedGoal);

            bool found = false;
            var ships = Player.OwnedShips;
            foreach (Ship ship in ships)
            {
                if (ship.IsConstructor && ship.AI.OrderQueue.NotEmpty)
                {
                    for (int i = 0; i < ship.AI.OrderQueue.Count; ++i)
                    {
                        if (ship.AI.OrderQueue[i].Goal == SelectedItem.AssociatedGoal)
                        {
                            found = true;
                            ship.AI.OrderScrapShip();
                            break;
                        }
                    }
                }
            }

            if (!found)
            {
                foreach (Planet planet in Player.GetPlanets())
                {
                    foreach (QueueItem qi in planet.ConstructionQueue)
                    {
                        if (qi.Goal == SelectedItem.AssociatedGoal)
                        {
                            qi.IsCancelled = true; // cancel on next SBProduction update
                        }
                    }
                }
            }

            if (ClickableBuildGoals.ContainsRef(SelectedItem))
            {
                GameAudio.BlipClick();
            }

            SelectedItem = null;
        }

        Fleet CreateNewFleet(int fleetId, IReadOnlyList<Ship> ships)
        {
            if (ships.Count == 0)
                return null;

            Fleet newFleet = Player.CreateFleet(fleetId, null);
            AddShipsToFleet(newFleet, ships);
            return newFleet;
        }

        Fleet AddShipsToFleet(Fleet fleet, IReadOnlyList<Ship> ships)
        {
            if (ships.Count != 0)
            {
                ClearShipFleetsWithDataNodes(ships);
                fleet.AddShips(ships);

                fleet.SetCommandShip(null);
                fleet.AutoArrange(); // arrange new ships into formation
                fleet.Update(FixedSimTime.Zero/*paused during init*/);

                GameAudio.FleetClicked();
                InputCheckPreviousShip();
                return fleet;
            }
            return fleet;
        }

        // to handle the case where a ship is being reassigned,
        // the original datanodes must be cleared as well, which is only necessary
        // during reassignment
        void ClearShipFleetsWithDataNodes(IReadOnlyList<Ship> ships)
        {
            foreach (Ship ship in ships)
            {
                // remove the DataNode
                ship.Fleet?.DataNodes.RemoveFirst(n => n.Ship == ship);
                ship.ClearFleet(returnToManagedPools: false, clearOrders: false);
            }
        }

        // move to thread safe update
        public void RecomputeFleetButtons(bool forceUpdate)
        {
            ++FBTimer;
            if (FBTimer <= 60 && !forceUpdate)
                return;
            if (IsExiting)
                return;

            var buttons = new Array<FleetButton>();

            // if we're showing debug window, move the fleet buttons down a bit
            bool showingDebugTabs = Debug && DebugWin?.ModesTab.Visible == true;
            int startY = showingDebugTabs ? 120 : 60;
            int index = 0;
            
            var fleets = Player.ActiveFleets.ToArrayList().Sorted(f => f.Key);
            foreach (Fleet fleet in fleets)
            {
                buttons.Add(new FleetButton
                {
                    ClickRect = new Rectangle(20, startY + index * 60, 52, 48),
                    Fleet = fleet,
                    Key = fleet.Key
                });
                ++index;
            }
            FBTimer = 0;
            FleetButtons = buttons.ToArray();
        }

        Vector2 StartDragPos;

        void HandleEdgeDetection(InputState input)
        {
            if (LookingAtPlanet)
                return;

            float worldWidthOnScreen = (float)VisibleWorldRect.Width;

            float x = input.CursorX, y = input.CursorY;
            float outer = -50f;
            float inner = +5.0f;
            float minLeft = outer, maxLeft = inner;
            float minTop  = outer, maxTop  = inner;
            float minRight  = ScreenWidth  - inner, maxRight  = ScreenWidth  - outer;
            float minBottom = ScreenHeight - inner, maxBottom = ScreenHeight - outer;

            bool InRange(float pos, float min, float max)
            {
                return min <= pos && pos <= max;
            }

            bool enableKeys = !ViewingShip;
            bool arrowKeys = Debug == false;

            if (!input.IsCtrlKeyDown && input.MiddleMouseClick)
            {
                StartDragPos = input.CursorPosition;
            }

            if (input.MiddleMouseHeld())
            {
                float dx = input.CursorPosition.X - StartDragPos.X;
                float dy = input.CursorPosition.Y - StartDragPos.Y;
                StartDragPos = input.CursorPosition;
                CamDestination.X += -dx * worldWidthOnScreen * 0.001f;
                CamDestination.Y += -dy * worldWidthOnScreen * 0.001f;
                snappingToShip = false;
                ViewingShip    = false;
            }
            else
            {
                if (InRange(x, minLeft, maxLeft) || (enableKeys && input.KeysLeftHeld(arrowKeys)))
                {
                    CamDestination.X -= 0.008f * worldWidthOnScreen;
                    snappingToShip = false;
                    ViewingShip    = false;
                }
                if (InRange(x, minRight, maxRight) || (enableKeys && input.KeysRightHeld(arrowKeys)))
                {
                    CamDestination.X += 0.008f * worldWidthOnScreen;
                    snappingToShip = false;
                    ViewingShip    = false;
                }
                if (InRange(y, minTop, maxTop) || (enableKeys && input.KeysUpHeld(arrowKeys)))
                {
                    CamDestination.Y -= 0.008f * worldWidthOnScreen;
                    snappingToShip = false;
                    ViewingShip    = false;
                }
                if (InRange(y, minBottom, maxBottom) || (enableKeys && input.KeysDownHeld(arrowKeys)))
                {
                    CamDestination.Y += 0.008f * worldWidthOnScreen;
                    snappingToShip = false;
                    ViewingShip    = false;
                }
            }

            CamDestination.X = CamDestination.X.Clamped(-UState.Size, UState.Size);
            CamDestination.Y = CamDestination.Y.Clamped(-UState.Size, UState.Size);
        }

        void HandleScrolls(InputState input)
        {
            if (AdjustCamTimer >= 0f)
                return;

            double scrollAmount = 1000.0;
            double camDestZ = CamDestination.Z;

            if ((input.ScrollOut || input.BButtonHeld) && !LookingAtPlanet)
            {
                // gradually adjust scroll-out based on CamPos.Z
                
                if      (camDestZ >= 5_000_000) scrollAmount = 2000_000;
                if      (camDestZ >= 1200_000) scrollAmount = 1000_000;
                else if (camDestZ >= 600_000)  scrollAmount = 400_000;
                else if (camDestZ >= 250_000)  scrollAmount = 96_000; // 250_000: SystemView
                else if (camDestZ >= 100_000)  scrollAmount = 40_000;
                else if (camDestZ >= 35_000)   scrollAmount = 20_000; // 35_000: PlanetView
                else if (camDestZ >= 15_000)   scrollAmount = 7_000;  // 15_000: ShipView
                else if (camDestZ >= 7_000)    scrollAmount = 4_000;  // 7_000:  DetailView
                else if (camDestZ >= 3_000)    scrollAmount = 1_500;

                CamDestination.Z = (camDestZ + scrollAmount).Clamped(MinCamHeight, MaxCamHeight);
                //Log.Info($"scrollAmount: {scrollAmount}  Z={CamDestination.Z}");

                // turbo zoom out when Ctrl key is down
                if (input.IsCtrlKeyDown)
                {
                    // zoom out in two stages
                    CamDestination.Z = camDestZ < 55000.0 ? 60000.0 : MaxCamHeight;
                    AdjustCamTimer = 1f; // animated camera transition over 1sec
                    transitionElapsedTime = 0f;
                }
            }
            else if ((input.ScrollIn || input.YButtonHeld) && !LookingAtPlanet)
            {
                // gradually adjusts scroll-in based on CamPos.Z
                if      (camDestZ >= 3200_000) scrollAmount = 1800_000;
                else if (camDestZ >= 1200_000) scrollAmount = 400_000;
                else if (camDestZ >= 600_000)  scrollAmount = 150_000;
                else if (camDestZ >= 300_000)  scrollAmount = 96_000;
                else if (camDestZ >= 100_000)  scrollAmount = 44_000;
                else if (camDestZ >= 60_000)   scrollAmount = 24_000;
                else if (camDestZ >= 35_000)   scrollAmount = 15_000; // 35_000: PlanetView
                else if (camDestZ >= 15_000)   scrollAmount = 7_500;  // 15_000: ShipView
                else if (camDestZ >= 7_000)    scrollAmount = 3_500;  // 7_000:  DetailView
                else if (camDestZ >= 3_000)    scrollAmount = 1_500;  // 7_000:  DetailView

                CamDestination.Z = (camDestZ - scrollAmount).Clamped(MinCamHeight, MaxCamHeight);
                //Log.Info($"scrollAmount: {scrollAmount}  Z={CamDestination.Z}");

                // turbo zoom in when Ctrl key is down
                if (input.IsCtrlKeyDown && camDestZ > 10000.0)
                {
                    CamDestination.Z = camDestZ <= 65000.0 ? 10000.0 : 60000.0;
                }

                // if we're not view-following a ship, adjust the camera towards target
                if (!ViewingShip)
                {
                    //fbedard: add a scroll on selected object
                    if ((!input.IsShiftKeyDown && GlobalStats.ZoomTracking) || (input.IsShiftKeyDown && !GlobalStats.ZoomTracking))
                        CamDestination = GetZoomTrackingTarget(input, CamDestination.Z);
                    else
                        CamDestination = GetCameraPosFromCursorTarget(input, CamDestination.Z);
                }
            }
        }

        Vector3d GetZoomTrackingTarget(InputState input, double camDestZ)
        {
            if (SelectedShip is { Active: true })
                return new(SelectedShip.Position, camDestZ);

            if (SelectedPlanet != null)
                return new(SelectedPlanet.Position, camDestZ);

            if (SelectedFleet != null && SelectedFleet.Ships.NotEmpty)
                return new(SelectedFleet.AveragePosition(), camDestZ);

            if (SelectedShipList.NotEmpty && SelectedShipList[0]?.Active == true)
                return new(SelectedShipList[0].Position, camDestZ);

            return GetCameraPosFromCursorTarget(input, camDestZ);
        }

        public bool IsShipUnderFleetIcon(Ship ship, Vector2 screenPos, float fleetIconScreenRadius)
        {
            foreach (ClickableFleet clickableFleet in ClickableFleetsList)
                if (clickableFleet.fleet == ship.Fleet && screenPos.InRadius(clickableFleet.ScreenPos, fleetIconScreenRadius))
                    return true;
            return false;
        }
    }
}