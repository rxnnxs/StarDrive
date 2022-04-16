using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Ship_Game.AI;
using Ship_Game.Gameplay;
using Ship_Game.Ships;
using System;
using Ship_Game.Data.Mesh;
using Ship_Game.Ships.AI;
using Ship_Game.Fleets;
using Ship_Game.Graphics;

namespace Ship_Game
{
    public partial class UniverseScreen
    {
        static readonly Color FleetLineColor    = new Color(255, 255, 255, 20);
        static readonly Vector2 FleetNameOffset = new Vector2(10f, -6f);
        public static float PulseTimer { get; private set; }

        void DrawSystemAndPlanetBrackets(SpriteBatch batch)
        {
            if (SelectedSystem != null && !LookingAtPlanet)
            {
                Vector2 sysPos = SelectedSystem.Position;
                float sunRadius = 4500f;
                Vector2 nearPoint = Viewport.Project(new Vector3(sysPos, 0f), Projection, View, Matrix.Identity).ToVec2();
                Vector2 farPoint  = Viewport.Project(new Vector3(sysPos.X + sunRadius, sysPos.Y, 0.0f), Projection, View, Matrix.Identity).ToVec2();
                float radius = farPoint.Distance(nearPoint);
                if (radius < 5f)
                    radius = 5f;
                batch.BracketRectangle(new Vector2d(nearPoint), radius, Color.White);
            }
            if (SelectedPlanet != null && !LookingAtPlanet &&  viewState < UnivScreenState.GalaxyView)
            {
                Vector2 planetPos = SelectedPlanet.Center;
                float planetRadius = SelectedPlanet.ObjectRadius;
                Vector2 center = Viewport.Project(new Vector3(planetPos, 2500f), Projection, View, Matrix.Identity).ToVec2();
                Vector2 edge = Viewport.Project(new Vector3(planetPos.X + planetRadius, planetPos.Y, 2500f), Projection, View, Matrix.Identity).ToVec2();
                float radius = center.Distance(edge);
                if (radius < 8f)
                    radius = 8f;
                batch.BracketRectangle(new Vector2d(center), radius, SelectedPlanet.Owner?.EmpireColor ?? Color.Gray);
            }
        }

        private void DrawFogNodes()
        {
            var uiNode = ResourceManager.Texture("UI/node");
            var viewport = Viewport;

            foreach (FogOfWarNode fogOfWarNode in FogNodes)
            {
                if (!fogOfWarNode.Discovered)
                    continue;

                Vector3 vector3_1 = viewport.Project(fogOfWarNode.Position.ToVec3(), Projection, View,
                    Matrix.Identity);
                Vector2 vector2 = vector3_1.ToVec2();
                Vector3 vector3_2 = viewport.Project(
                    new Vector3(fogOfWarNode.Position.PointFromAngle(90f, fogOfWarNode.Radius * 1.5f), 0.0f),
                    Projection, View, Matrix.Identity);
                float num = Math.Abs(new Vector2(vector3_2.X, vector3_2.Y).X - vector2.X);
                Rectangle destinationRectangle =
                    new Rectangle((int) vector2.X, (int) vector2.Y, (int) num * 2, (int) num * 2);
                ScreenManager.SpriteBatch.Draw(uiNode, destinationRectangle, new Color(70, 255, 255, 255), 0.0f,
                    uiNode.CenterF, SpriteEffects.None, 1f);
            }
        }

        void DrawInfluenceNodes(SpriteBatch batch)
        {
            var uiNode= ResourceManager.Texture("UI/node");
            var viewport = Viewport;

            var sensorNodes = Player.SensorNodes;
            for (int i = 0; i < sensorNodes.Length; ++i)
            {
                ref Empire.InfluenceNode @in = ref sensorNodes[i];
                Vector2d screenPos = ProjectToScreenPosition(@in.Position);
                Vector3 local_4 = viewport.Project(
                    new Vector3(@in.Position.PointFromAngle(90f, @in.Radius * 1.5f), 0.0f), Projection,
                    View, Matrix.Identity);

                double local_6 = Math.Abs(new Vector2(local_4.X, local_4.Y).X - screenPos.X) * 2.59999990463257;
                Rectangle local_7 = new Rectangle((int)screenPos.X, (int)screenPos.Y, (int)local_6, (int)local_6);

                batch.Draw(uiNode, local_7, Color.White, 0.0f, uiNode.CenterF, SpriteEffects.None, 1f);
            }
        }

        // Draws SSP - Subspace Projector influence
        void DrawColoredEmpireBorders(SpriteBatch batch, GraphicsDevice graphics)
        {
            DrawBorders.Start();

            graphics.SetRenderTarget(0, BorderRT);
            graphics.Clear(Color.TransparentBlack);
            batch.Begin(SpriteBlendMode.AlphaBlend, SpriteSortMode.Immediate, SaveStateMode.None);

            RenderStates.BasicBlendMode(graphics, additive:false, depthWrite:false);
            RenderStates.EnableSeparateAlphaBlend(graphics, Blend.One, Blend.One);

            // the node texture has a smooth fade, so we need to scale it by a lot to match the actual SSP radius
            float scale = 1.5f;
            var nodeTex = ResourceManager.Texture("UI/node");
            var connectTex = ResourceManager.Texture("UI/nodeconnect"); // simple horizontal gradient

            Empire[] empires = EmpireManager.Empires.Sorted(e=> e.MilitaryScore);
            foreach (Empire empire in empires)
            {
                if (!Debug && empire != Player && !Player.IsKnown(empire))
                    continue;

                Color empireColor = empire.EmpireColor;
                Empire.InfluenceNode[] nodes = empire.BorderNodes;
                for (int x = 0; x < nodes.Length; x++)
                {
                    ref Empire.InfluenceNode inf = ref nodes[x];
                    if (!inf.KnownToPlayer || !Frustum.Contains(inf.Position, inf.Radius))
                        continue;

                    RectF screenRect = ProjectToScreenRectF(RectF.FromPointRadius(inf.Position, inf.Radius));
                    screenRect = screenRect.ScaledBy(scale);
                    batch.Draw(nodeTex, screenRect, empireColor);
                    //batch.DrawRectangle(screenRect, Color.Red); // DEBUG
                    //DrawCircleProjected(inf.Position, inf.Radius, Color.Orange, 2); // DEBUG
                    //batch.Draw(nodeCorrected, rect, empireColor, 0.0f, nodeCorrected.CenterF, SpriteEffects.None, 1f);

                    // make connections from Larger nodes to Smaller ones
                    for (int i = 0; i < nodes.Length; i++)
                    {
                        ref Empire.InfluenceNode in2 = ref nodes[i];
                        if (in2.KnownToPlayer && 
                            in2.SourceObject != inf.SourceObject && // ignore self
                            in2.Radius > inf.Radius && // this check ensures we only connect from Larger -> Smaller
                            in2.Position.InRadius(inf.Position, inf.Radius + in2.Radius + 150000.0f))
                        {
                            // The Connection is made of two rectangles, with O marking the influence centers
                            // +-width-+O-width-+
                            // |       ||       |
                            // |       ||       |length
                            // |       ||       |
                            // +-------O+-------+
                            float width = inf.Radius * scale;
                            float length = inf.Position.Distance(in2.Position);

                            // from Bigger towards Smaller (only one side)
                            RectF connect1 = ProjectToScreenRectF(new RectF(in2.Position, width, length));
                            float angle1 = inf.Position.RadiansToTarget(in2.Position);
                            batch.Draw(connectTex, connect1, empireColor, angle1, Vector2.Zero);

                            // from Smaller towards Bigger (the other side now)
                            RectF connect2 = ProjectToScreenRectF(new RectF(inf.Position, width, length));
                            float angle2 = in2.Position.RadiansToTarget(inf.Position);
                            batch.Draw(connectTex, connect2, empireColor, angle2, Vector2.Zero);

                            //DrawLineProjected(in2.Position, inf.Position, Color.Blue); // DEBUG
                            //batch.DrawRectangle(connect1, angle1, Color.Green, 2); // DEBUG
                            //batch.DrawRectangle(connect2, angle2, Color.Green, 2); // DEBUG
                        }
                    }
                }
            }

            RenderStates.DisableSeparateAlphaChannelBlend(graphics);
            batch.End();

            DrawBorders.Stop();
        }

        void DrawDebugPlanetBudgets()
        {
            if (viewState < UnivScreenState.SectorView)
            {
                foreach (Empire empire in EmpireManager.Empires)
                {
                    if (empire.GetEmpireAI().PlanetBudgets != null)
                    {
                        foreach (var budget in empire.GetEmpireAI().PlanetBudgets)
                            budget.DrawBudgetInfo(this);
                    }
                }
            }
        }

        void DrawExplosions(SpriteBatch batch)
        {
            DrawExplosionsPerf.Start();
            batch.Begin(SpriteBlendMode.Additive);
            ExplosionManager.DrawExplosions(batch, View, Projection);
            #if DEBUG
            DrawDebugPlanetBudgets();
            #endif
            batch.End();
            DrawExplosionsPerf.Stop();
        }

        void DrawOverlayShieldBubbles(SpriteBatch batch)
        {
            if (ShowShipNames && !LookingAtPlanet)
            {
                for (int i = 0; i < ClickableShips.Length; i++)
                {
                    ref ClickableShip clickable = ref ClickableShips[i];
                    if (clickable.Ship.IsVisibleToPlayer)
                        clickable.Ship.DrawShieldBubble(this);
                }
            }
        }

        void DrawFogInfluences(SpriteBatch batch, GraphicsDevice device)
        {
            DrawFogInfluence.Start();

            device.SetRenderTarget(0, FogMapTarget);
            device.Clear(Color.TransparentWhite);
            batch.Begin(SpriteBlendMode.Additive);
            batch.Draw(FogMap, new Rectangle(0, 0, 512, 512), Color.White);
            double universeWidth = UState.Size * 2.0;
            double worldSizeToMaskSize = (512.0 / universeWidth);

            var uiNode = ResourceManager.Texture("UI/node");
            var ships = Player.OwnedShips;
            var shipSensorMask = new Color(255, 0, 0, 255);
            foreach (Ship ship in ships)
            {
                if (ship != null && ship.InFrustum)
                {
                    double posX = ship.Position.X * worldSizeToMaskSize + 256;
                    double posY = ship.Position.Y * worldSizeToMaskSize + 256;
                    double size = (ship.SensorRange * 2.0) * worldSizeToMaskSize;
                    var rect = new RectF(posX, posY, size, size);
                    batch.Draw(uiNode, rect, shipSensorMask, 0f, uiNode.CenterF, SpriteEffects.None, 1f);
                }
            }
            batch.End();
            device.SetRenderTarget(0, null);
            FogMap = FogMapTarget.GetTexture();

            device.SetRenderTarget(0, LightsTarget);
            device.Clear(Color.White);

            batch.Begin(SpriteBlendMode.AlphaBlend);
            if (!Debug) // don't draw fog of war in debug
            {
                Rectangle fogRect = ProjectToScreenCoords(new Vector2(-UState.Size), UState.Size*2f);
                batch.FillRectangle(new Rectangle(0, 0, ScreenWidth, ScreenHeight), new Color(0, 0, 0, 170));
                batch.Draw(FogMap, fogRect, new Color(255, 255, 255, 55));
            }
            DrawFogNodes();
            DrawInfluenceNodes(batch);
            batch.End();
            device.SetRenderTarget(0, null);

            DrawFogInfluence.Stop();
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            DrawGroupTotalPerf.Start();
            PulseTimer -= elapsed.RealTime.Seconds;
            if (PulseTimer < 0) PulseTimer = 1;

            AdjustCamera(elapsed.RealTime.Seconds);

            Matrix matrix = Matrix.CreateRotationY(180f.ToRadians())
                          * Matrices.CreateLookAtDown(-CamPos.X, CamPos.Y, CamPos.Z);
            SetViewMatrix(matrix);

            GraphicsDevice graphics = ScreenManager.GraphicsDevice;
            graphics.SetRenderTarget(0, MainTarget);
            Render(batch, elapsed);
            graphics.SetRenderTarget(0, null);
            
            OverlaysGroupTotalPerf.Start();
            {
                DrawFogInfluences(batch, graphics);
                if (viewState >= UnivScreenState.SectorView) // draw colored empire borders only if zoomed out
                    DrawColoredEmpireBorders(batch, graphics);
                DrawFogOfWarEffect(batch, graphics);

                SetViewMatrix(matrix);
                if (GlobalStats.RenderBloom)
                    bloomComponent?.Draw();

                batch.Begin(SpriteBlendMode.AlphaBlend);
                RenderOverFog(batch);
                batch.End();
            }
            OverlaysGroupTotalPerf.Stop();

            // these are all background elements, such as ship overlays, fleet icons, etc..
            IconsGroupTotalPerf.Start();
            batch.Begin();
            {
                DrawShipsAndProjectiles(batch);
                DrawShipAndPlanetIcons(batch);
                DrawGeneralUI(batch, elapsed);
            }
            batch.End();
            IconsGroupTotalPerf.Stop();

            // Advance the simulation time just before we Notify
            if (!UState.Paused && IsActive)
            {
                AdvanceSimulationTargetTime(elapsed.RealTime.Seconds);
            }

            // Notify ProcessTurns that Drawing has finished and while SwapBuffers is blocking,
            // the game logic can be updated
            DrawCompletedEvt.Set();

            DrawGroupTotalPerf.Stop();
        }

        private void DrawGeneralUI(SpriteBatch batch, DrawTimes elapsed)
        {
            DrawUI.Start();

            // in cinematic mode we disable all of these GUI elements
            bool showGeneralUI = !IsCinematicModeEnabled;
            if (showGeneralUI)
            {
                DrawPlanetInfo();
                EmpireUI.Draw(batch);
                if (LookingAtPlanet)
                {
                    workersPanel?.Draw(batch, elapsed);
                }
                else
                {
                    DeepSpaceBuildWindow.Draw(batch, elapsed);
                    pieMenu.Draw(batch, Fonts.Arial12Bold);
                    DrawShipUI(batch, elapsed);
                    NotificationManager.Draw(batch);
                }
            }

            batch.DrawRectangle(SelectionBox, Color.Green, 1f);

            // This uses the new UIElementV2 system to automatically toggle visibility of items
            // In general, a much saner way than the old cluster-f*ck of IF statements :)
            PlanetsInCombat.Visible = ShipsInCombat.Visible = showGeneralUI && !LookingAtPlanet;
            aw.Visible = showGeneralUI && aw.IsOpen && !LookingAtPlanet;

            minimap.Visible = showGeneralUI && (!LookingAtPlanet ||
                              LookingAtPlanet && workersPanel is UnexploredPlanetScreen ||
                              LookingAtPlanet && workersPanel is UnownedPlanetScreen);

            DrawSelectedItems(batch, elapsed);
            DrawSystemAndPlanetBrackets(batch);

            if (Debug)
                DebugWin?.Draw(batch, elapsed);

            DrawGeneralStatusText(batch, elapsed);

            if (Debug) ShowDebugGameInfo();
            else HideDebugGameInfo();

            base.Draw(batch, elapsed);  // UIElementV2 Draw

            DrawUI.Stop();
        }

        private void DrawFogOfWarEffect(SpriteBatch batch, GraphicsDevice graphics)
        {
            DrawFogOfWar.Start();

            Texture2D texture1 = MainTarget.GetTexture();
            Texture2D texture2 = LightsTarget.GetTexture();
            graphics.SetRenderTarget(0, null);
            graphics.Clear(Color.Black);
            basicFogOfWarEffect.Parameters["LightsTexture"].SetValue(texture2);


            batch.Begin(SpriteBlendMode.AlphaBlend, SpriteSortMode.Immediate, SaveStateMode.SaveState);
            basicFogOfWarEffect.Begin();
            basicFogOfWarEffect.CurrentTechnique.Passes[0].Begin();
            batch.Draw(texture1, new Rectangle(0, 0, graphics.PresentationParameters.BackBufferWidth,
                    graphics.PresentationParameters.BackBufferHeight), Color.White);
            basicFogOfWarEffect.CurrentTechnique.Passes[0].End();
            basicFogOfWarEffect.End();
            batch.End();

            DrawFogOfWar.Stop();
        }

        private void DrawShipAndPlanetIcons(SpriteBatch batch)
        {
            DrawIcons.Start();
            DrawProjectedGroup();
            if (!LookingAtPlanet)
                DeepSpaceBuildWindow.DrawBlendedBuildIcons(ClickableBuildGoals);
            DrawTacticalPlanetIcons(batch);
            DrawFTLInhibitionNodes();
            DrawShipRangeOverlay();
            DrawFleetIcons();
            DrawIcons.Stop();
        }

        void DrawTopCenterStatusText(SpriteBatch batch, in LocalizedText status, Color color, int lineOffset)
        {
            Graphics.Font font = Fonts.Pirulen16;
            string text = status.Text;
            var pos = new Vector2(ScreenCenter.X - font.TextWidth(text) / 2f, 45f + (font.LineSpacing + 2)*lineOffset);
            batch.DrawString(font, text, pos, color);
        }

        void DrawGeneralStatusText(SpriteBatch batch, DrawTimes elapsed)
        {
            if (UState.Paused)
            {
                DrawTopCenterStatusText(batch, GameText.Paused, Color.Gold, 0);
            }

            if (RandomEventManager.ActiveEvent != null && RandomEventManager.ActiveEvent.InhibitWarp)
            {
                DrawTopCenterStatusText(batch, "Hyperspace Flux", Color.Yellow, 1);
            }

            if (IsActive && SavedGame.IsSaving)
            {
                DrawTopCenterStatusText(batch, "Saving...", CurrentFlashColor, 2);
            }

            if (IsActive && !UState.GameSpeed.AlmostEqual(1)) //don't show "1.0x"
            {
                string speed = UState.GameSpeed.ToString("0.0##") + "x";
                var pos = new Vector2(ScreenWidth - Fonts.Pirulen16.TextWidth(speed) - 13f, 64f);
                batch.DrawString(Fonts.Pirulen16, speed, pos, Color.White);
            }

            if (IsCinematicModeEnabled && CinematicModeTextTimer > 0f)
            {
                CinematicModeTextTimer -= elapsed.RealTime.Seconds;
                DrawTopCenterStatusText(batch, "Cinematic Mode - Press F11 to exit", Color.White, 3);
            }

            if (!Player.Research.NoResearchLeft && Player.Research.NoTopic && !Player.AutoResearch && !Debug)
            {
                DrawTopCenterStatusText(batch, "No Research!",  ApplyCurrentAlphaToColor(Color.Red), 2);
            }
        }

        void DrawShipRangeOverlay()
        {
            if (showingRangeOverlay && !LookingAtPlanet)
            {
                var shipRangeTex = ResourceManager.Texture("UI/node_shiprange");
                foreach (ClickableShip clickable in ClickableShips)
                {
                    Ship ship = clickable.Ship;
                    if (ship != null &&  ship.IsVisibleToPlayer && ship.WeaponsMaxRange > 0f)
                    {
                        Color color = ship.Loyalty == EmpireManager.Player
                                        ? new Color(0, 200, 0, 30)
                                        : new Color(200, 0, 0, 30);

                        byte edgeAlpha = 70;
                        DrawCircleProjected(ship.Position, ship.WeaponsMaxRange, new Color(color, edgeAlpha));
                        if (SelectedShip == ship)
                        {
                            edgeAlpha = 70;
                            DrawTextureProjected(shipRangeTex, ship.Position, ship.WeaponsMaxRange, color);
                            DrawCircleProjected(ship.Position, ship.WeaponsAvgRange, new Color(Color.Orange, edgeAlpha));
                            DrawCircleProjected(ship.Position, ship.WeaponsMinRange, new Color(Color.Yellow, edgeAlpha));
                        }
                    }

                    if ((ship?.SensorRange ?? 0) > 0)
                    {
                        if (SelectedShip == ship)
                        {
                            Color color = (ship.Loyalty.isPlayer)
                                ? new Color(0, 100, 200, 20)
                                : new Color(200, 0, 0, 10);
                            byte edgeAlpha = 85;
                            DrawTextureProjected(shipRangeTex, ship.Position, ship.SensorRange, color);
                            DrawCircleProjected(ship.Position, ship.SensorRange, new Color(Color.Blue, edgeAlpha));
                        }
                    }
                }
            }
        }

        void DrawFTLInhibitionNodes()
        {
            if (showingFTLOverlay && GlobalStats.PlanetaryGravityWells && !LookingAtPlanet)
            {
                var inhibit = ResourceManager.Texture("UI/node_inhibit");
                foreach (ClickablePlanet cplanet in ClickablePlanets)
                {
                    float radius = cplanet.Planet.GravityWellRadius;
                    DrawCircleProjected(cplanet.Planet.Center, radius, new Color(255, 50, 0, 150), 1f,
                                        inhibit, new Color(200, 0, 0, 50));
                }

                foreach (ClickableShip ship in ClickableShips)
                {
                    if (ship.Ship != null && ship.Ship.InhibitionRadius > 0f && ship.Ship.IsVisibleToPlayer)
                    {
                        float radius = ship.Ship.InhibitionRadius;
                        DrawCircleProjected(ship.Ship.Position, radius, new Color(255, 50, 0, 150), 1f,
                                            inhibit, new Color(200, 0, 0, 40));
                    }
                }

                if (viewState >= UnivScreenState.SectorView)
                {
                    var transparentBlue = new Color(30, 30, 150, 150);
                    var transparentGreen = new Color(0, 200, 0, 20);
                    Empire.InfluenceNode[] borders = Player.BorderNodes;
                    for (int i = 0; i < borders.Length; ++i)
                    {
                        ref Empire.InfluenceNode @in = ref borders[i];
                        DrawCircleProjected(@in.Position, @in.Radius, transparentBlue, 1f, inhibit,
                                            transparentGreen);
                    }
                }
            }
        }

        bool ShowSystemInfoOverlay => SelectedSystem != null && !LookingAtPlanet && !IsCinematicModeEnabled
                                   && viewState == UnivScreenState.GalaxyView;

        bool ShowPlanetInfo => SelectedPlanet != null && !LookingAtPlanet && !IsCinematicModeEnabled;

        bool ShowShipInfo => SelectedShip != null && !LookingAtPlanet && !IsCinematicModeEnabled;

        bool ShowShipList => SelectedShipList.Count > 1 && SelectedFleet == null && !IsCinematicModeEnabled;

        bool ShowFleetInfo => SelectedFleet != null && !LookingAtPlanet && !IsCinematicModeEnabled;

        private void DrawSelectedItems(SpriteBatch batch, DrawTimes elapsed)
        {
            if (SelectedShipList.Count == 0)
                shipListInfoUI.ClearShipList();

            if (ShowSystemInfoOverlay)
            {
                SystemInfoOverlay.Draw(batch, elapsed);
            }

            if (ShowPlanetInfo)
            {
                pInfoUI.Draw(batch, elapsed);
            }
            else if (ShowShipInfo)
            {
                if (Debug && DebugWin != null)
                {
                    DebugWin.DrawCircleImm(SelectedShip.Position,
                        SelectedShip.AI.GetSensorRadius(), Color.Crimson);
                    for (int i = 0; i < SelectedShip.AI.NearByShips.Count; i++)
                    {
                        var target = SelectedShip.AI.NearByShips[i];
                        DebugWin.DrawCircleImm(target.Ship.Position,
                            target.Ship.AI.GetSensorRadius(), Color.Crimson);
                    }
                }

                ShipInfoUIElement.Draw(batch, elapsed);
            }
            else if (ShowShipList)
            {
                shipListInfoUI.Draw(batch, elapsed);
            }
            else if (SelectedItem != null)
            {
                Goal goal = SelectedItem.AssociatedGoal;
                EmpireAI ai = goal.empire.GetEmpireAI();
                if (ai.HasGoal(goal.Id))
                {
                    string titleText = $"({ResourceManager.GetShipTemplate(SelectedItem.UID).Name})";
                    string bodyText = goal.PlanetBuildingAt != null
                        ? Localizer.Token(GameText.UnderConstructionAt) + goal.PlanetBuildingAt.Name
                        : Fonts.Arial12Bold.ParseText(Localizer.Token(GameText.NoPortsFoundForBuild), 300);

                    vuiElement.Draw(titleText, bodyText);
                    DrawItemInfoForUI();
                }
                else
                    SelectedItem = null;
            }
            else if (ShowFleetInfo)
            {
                shipListInfoUI.Draw(batch, elapsed);
            }
        }


        void DrawFleetIcons()
        {
            ClickableFleetsList.Clear();
            if (viewState < UnivScreenState.SectorView)
                return;
            bool debug = Debug && SelectedShip == null;
            Empire empireLooking = Debug ? SelectedShip?.Loyalty ?? Player : Player;
            for (int i = 0; i < EmpireManager.Empires.Count; i++)
            { 
                Empire empire = EmpireManager.Empires[i];
                bool doDraw = debug || !(Player.DifficultyModifiers.HideTacticalData && empireLooking.IsEmpireAttackable(empire));
                if (!doDraw) 
                    continue;

                // not sure if this is the right way to do this but its hitting a crash here on collection change when the fleet loop is a foreach
                Fleet[] fleets = empire.GetFleetsDict().AtomicValuesArray();
                for (int j = 0; j < fleets.Length; j++)
                {
                    Fleet fleet = fleets[j];
                    if (fleet.Ships.Count <= 0)
                        continue;

                    Vector2 averagePos = fleet.CachedAveragePos;

                    var shipsVisible = fleet.Ships.Filter(s=> s?.KnownByEmpires.KnownBy(empireLooking) == true);

                    if (shipsVisible.Length < fleet.Ships.Count * 0.75f)
                        continue;

                    SubTexture icon = fleet.Icon;
                    Vector2 fleetCenterOnScreen = ProjectToScreenPosition(averagePos).ToVec2fRounded();

                    FleetIconLines(shipsVisible, fleetCenterOnScreen);

                    ClickableFleetsList.Add(new ClickableFleet
                    {
                        fleet       = fleet,
                        ScreenPos   = fleetCenterOnScreen,
                        ClickRadius = 15f
                    });
                    ScreenManager.SpriteBatch.Draw(icon, fleetCenterOnScreen, empire.EmpireColor, 0.0f, icon.CenterF, 0.35f, SpriteEffects.None, 1f);
                    if (!Player.DifficultyModifiers.HideTacticalData || debug || fleet.Owner.isPlayer || fleet.Owner.IsAlliedWith(empireLooking))
                        ScreenManager.SpriteBatch.DrawDropShadowText(fleet.Name, fleetCenterOnScreen + FleetNameOffset, Fonts.Arial8Bold);
                }
            }
        }

        void FleetIconLines(Ship[] ships, Vector2 fleetCenterOnScreen)
        {
            for (int i = 0; i < ships.Length; i++)
            {
                Ship ship = ships[i];
                if (ship == null || !ship.Active)
                    continue;

                if (Debug || ship.Loyalty.isPlayer || ship.Loyalty.IsAlliedWith(Player) || !Player.DifficultyModifiers.HideTacticalData)
                {
                    Vector2 shipScreenPos = ProjectToScreenPosition(ship.Position).ToVec2fRounded();
                    ScreenManager.SpriteBatch.DrawLine(shipScreenPos, fleetCenterOnScreen, FleetLineColor);
                }
            }
        }

        void DrawTacticalPlanetIcons(SpriteBatch batch)
        {
            if (LookingAtPlanet || viewState <= UnivScreenState.SystemView || viewState >= UnivScreenState.GalaxyView)
                return;

            for (int i = 0; i < UState.Systems.Count; i++)
            {
                SolarSystem system = UState.Systems[i];
                if (!system.IsExploredBy(Player) || !system.IsVisible)
                    continue;

                foreach (Planet planet in system.PlanetList)
                {
                    float fIconScale      = 0.1875f * (0.7f + ((float) (Math.Log(planet.Scale)) / 2.75f));
                    Vector2 planetIconPos = ProjectToScreenPosition(planet.Center3D).ToVec2fRounded();
                    Vector2 flagIconPos   = planetIconPos - new Vector2(0, 15);

                    SubTexture planetTex = planet.PlanetTexture;
                    if (planet.Owner != null && (Player.IsKnown(planet.Owner) || planet.Owner == Player))
                    {
                        batch.Draw(planetTex, planetIconPos, Color.White, 0.0f, planetTex.CenterF, fIconScale, SpriteEffects.None, 1f);
                        SubTexture flag = ResourceManager.Flag(planet.Owner);
                        batch.Draw(flag, flagIconPos, planet.Owner.EmpireColor, 0.0f, flag.CenterF, 0.2f, SpriteEffects.None, 1f);
                    }
                    else
                    {
                        batch.Draw(planetTex, planetIconPos, Color.White, 0.0f, planetTex.CenterF, fIconScale, SpriteEffects.None, 1f);
                    }
                }
            }
        }

        void DrawItemInfoForUI()
        {
            var goal = SelectedItem?.AssociatedGoal;
            if (goal == null) return;
            if (!LookingAtPlanet)
                DrawCircleProjected(goal.BuildPosition, 50f, goal.empire.EmpireColor);
        }

        void DrawShipUI(SpriteBatch batch, DrawTimes elapsed)
        {
            if (DefiningAO || DefiningTradeRoutes)
                return; // FB dont show fleet list when selected AOs and Trade Routes

            foreach (FleetButton fleetButton in FleetButtons)
            {
                var buttonSelector = new Selector(fleetButton.ClickRect, Color.TransparentBlack);
                var housing = new Rectangle(fleetButton.ClickRect.X + 6, fleetButton.ClickRect.Y + 6,
                    fleetButton.ClickRect.Width - 12, fleetButton.ClickRect.Width - 12);

                bool inCombat = false;
                foreach (Ship ship in fleetButton.Fleet.Ships)
                {
                    if (!ship.OnLowAlert)
                    {
                        inCombat = true;
                        break;
                    }
                }
                
                Font fleetFont = Fonts.Pirulen12;
                Color fleetKey  = Color.Orange;
                bool needShadow = false;
                var keyPos = new Vector2(fleetButton.ClickRect.X + 4, fleetButton.ClickRect.Y + 4);
                if (SelectedFleet == fleetButton.Fleet)
                {
                    fleetKey   = Color.White;
                    fleetFont  = Fonts.Pirulen16;
                    needShadow = true;
                    keyPos     = new Vector2(keyPos.X, keyPos.Y - 2);
                }

                batch.Draw(ResourceManager.Texture("NewUI/rounded_square"),
                    fleetButton.ClickRect, inCombat ? ApplyCurrentAlphaToColor(new Color(255, 0, 0))
                                                    : new Color( 0,  0,  0,  80));

                if (fleetButton.Fleet.AutoRequisition)
                {
                    Rectangle autoReq = new Rectangle(fleetButton.ClickRect.X - 18, fleetButton.ClickRect.Y + 5, 15, 20);
                    batch.Draw(ResourceManager.Texture("NewUI/AutoRequisition"), autoReq, EmpireManager.Player.EmpireColor);
                }

                buttonSelector.Draw(batch, elapsed);
                batch.Draw(fleetButton.Fleet.Icon, housing, EmpireManager.Player.EmpireColor);
                if (needShadow)
                    batch.DrawString(fleetFont, fleetButton.Key.ToString(), new Vector2(keyPos.X + 2, keyPos.Y + 2), Color.Black);

                batch.DrawString(fleetFont, fleetButton.Key.ToString(), keyPos, fleetKey);
                DrawFleetShipIcons(batch, fleetButton);
            }
        }

        void DrawFleetShipIcons(SpriteBatch batch, FleetButton fleetButton)
        {
            int x = fleetButton.ClickRect.X + 55; // Offset from the button
            int y = fleetButton.ClickRect.Y;

            if (fleetButton.Fleet.Ships.Count <= 30)
                DrawFleetShipIcons30(batch, fleetButton, x, y);
            else 
                DrawFleetShipIconsSums(batch, fleetButton, x, y);
        }

        void DrawFleetShipIcons30(SpriteBatch batch, FleetButton fleetButton, int x, int y)
        {
            // Draw ship icons to right of button
            Vector2 shipSpacingH = new Vector2(x, y);
            for (int i = 0; i < fleetButton.Fleet.Ships.Count; ++i)
            {
                Ship ship       = fleetButton.Fleet.Ships[i];
                var iconHousing = new Rectangle((int)shipSpacingH.X, (int)shipSpacingH.Y, 15, 15);
                shipSpacingH.X += 18f;
                if (shipSpacingH.X > 237) // 10 Ships per row
                {
                    shipSpacingH.X  = x;
                    shipSpacingH.Y += 18f;
                }

                (SubTexture icon, SubTexture secondary, Color statColor) = ship.TacticalIconWithStatusColor();
                if (statColor != Color.Black)
                    batch.Draw(ResourceManager.Texture("TacticalIcons/symbol_status"), iconHousing, ApplyCurrentAlphaToColor(statColor));

                Color iconColor = ship.Resupplying ? Color.Gray : fleetButton.Fleet.Owner.EmpireColor;
                batch.Draw(icon, iconHousing, iconColor);
                if (secondary != null)
                    batch.Draw(secondary, iconHousing, iconColor);
            }
        }

        void DrawFleetShipIconsSums(SpriteBatch batch, FleetButton fleetButton, int x, int y)
        {
            Color color  = fleetButton.Fleet.Owner.EmpireColor;
            var sums = new Map<string, int>();
            for (int i = 0; i < fleetButton.Fleet.Ships.Count; ++i)
            {
                Ship ship   = fleetButton.Fleet.Ships[i];
                string icon = GetFullTacticalIconPaths(ship);
                if (sums.TryGetValue(icon, out int value))
                    sums[icon] = value + 1;
                else
                    sums.Add(icon, 1);
            }

            Vector2 shipSpacingH = new Vector2(x, y);
            int roleCounter = 1;
            Color sumColor = Color.Goldenrod;
            if (sums.Count > 12) // Switch to default sum views if too many icon sums
            {
                sums = RecalculateExcessIcons(sums);
                sumColor = Color.Gold;
            }

            foreach (string iconPaths in sums.Keys.ToArray())
            {
                var iconHousing = new Rectangle((int)shipSpacingH.X, (int)shipSpacingH.Y, 15, 15);
                string space = sums[iconPaths] < 9 ? "  " : "";
                string sum = $"{space}{sums[iconPaths]}x";
                batch.DrawString(Fonts.Arial10, sum, iconHousing.X, iconHousing.Y, sumColor);
                float ident = Fonts.Arial10.MeasureString(sum).X;
                shipSpacingH.X += ident;
                iconHousing.X  += (int)ident;
                DrawIconSums(iconPaths, iconHousing);
                shipSpacingH.X += 25f;
                if (roleCounter % 4 == 0) // 4 roles per line
                {
                    shipSpacingH.X  = x;
                    shipSpacingH.Y += 15f;
                }

                roleCounter += 1;
            }

            // Ignore secondary icons and returns only the hull role icons
            Map<string, int> RecalculateExcessIcons(Map<string, int> excessSums)
            {
                Map<string, int> recalculated = new Map<string, int>();
                foreach (string iconPaths in excessSums.Keys.ToArray())
                {
                    var hullPath = iconPaths.Split('|')[0];
                    if (recalculated.TryGetValue(hullPath, out _))
                        recalculated[hullPath] += excessSums[iconPaths];
                    else
                        recalculated.Add(hullPath, excessSums[iconPaths]);
                }

                return recalculated;
            }

            string GetFullTacticalIconPaths(Ship s)
            {
                (SubTexture icon, SubTexture secondary) = s.TacticalIcon();
                string iconStr = $"TacticalIcons/{icon.Name}";
                if (secondary != null)
                    iconStr = $"{iconStr}|TacticalIcons/{secondary.Name}";

                return iconStr;
            }

            void DrawIconSums(string iconPaths, Rectangle r)
            {
                var paths = iconPaths.Split('|');
                batch.Draw(ResourceManager.Texture(paths[0]), r, color);
                if (paths.Length > 1)
                    batch.Draw(ResourceManager.Texture(paths[1]), r, color);
            }
        }

        void DrawShipsAndProjectiles(SpriteBatch batch)
        {
            Ship[] ships = UState.Objects.VisibleShips;

            if (viewState <= UnivScreenState.PlanetView)
            {
                DrawProj.Start();
                RenderStates.BasicBlendMode(Device, additive:true, depthWrite:true);

                Projectile[] projectiles = UState.Objects.VisibleProjectiles;
                Beam[] beams = UState.Objects.VisibleBeams;

                for (int i = 0; i < projectiles.Length; ++i)
                {
                    Projectile proj = projectiles[i];
                    proj.Draw(batch, this);
                }

                if (beams.Length > 0)
                    Beam.UpdateBeamEffect(this);

                for (int i = 0; i < beams.Length; ++i)
                {
                    Beam beam = beams[i];
                    beam.Draw(this);
                }

                DrawProj.Stop();
            }

            RenderStates.BasicBlendMode(Device, additive:false, depthWrite:false);

            DrawShips.Start();
            for (int i = 0; i < ships.Length; ++i)
            {
                Ship ship = ships[i];
                if (ship.InFrustum && ship.InSensorRange)
                {
                    if (!IsCinematicModeEnabled)
                        DrawTacticalIcon(ship);

                    DrawOverlay(ship);

                    if (SelectedShip == ship || SelectedShipList.Contains(ship))
                    {
                        Color color = Color.LightGreen;
                        if (Player != ship.Loyalty)
                            color = Player.IsEmpireAttackable(ship.Loyalty) ? Color.Red : Color.Yellow;
                        else if (ship.Resupplying)
                            color = Color.Gray;

                        ProjectToScreenCoords(ship.Position, ship.Radius,
                            out Vector2d shipScreenPos, out double screenRadius);

                        double radius = screenRadius < 7f ? 7f : screenRadius;
                        batch.BracketRectangle(shipScreenPos, radius, color);
                    }
                }
            }
            DrawShips.Stop();
        }

        void DrawProjectedGroup()
        {
            if (!Project.Started || CurrentGroup == null)
                return;

            var projectedColor = new Color(0, 255, 0, 100);
            foreach (Ship ship in CurrentGroup.Ships)
            {
                if (ship.Active)
                    DrawShipProjectionIcon(ship, ship.ProjectedPosition, CurrentGroup.ProjectedDirection, projectedColor);
            }
        }

        void DrawShipProjectionIcon(Ship ship, Vector2 position, Vector2 direction, Color color)
        {
            (SubTexture symbol, SubTexture secondary) = ship.TacticalIcon();
            double num = ship.SurfaceArea / (30.0 + symbol.Width);
            double scale = (num * 4000.0 / CamPos.Z).UpperBound(1);

            if (scale <= 0.1)
                scale = ship.ShipData.Role != RoleName.platform || viewState < UnivScreenState.SectorView ? 0.15 : 0.08;

            DrawTextureProjected(symbol, position, (float)scale, direction.ToRadians(), color);
            if (secondary != null)
                DrawTextureProjected(secondary, position, (float)scale, direction.ToRadians(), color);
        }

        void DrawOverlay(Ship ship)
        {
            if (ship.InFrustum && ship.Active && !ship.Dying && !LookingAtPlanet && viewState <= UnivScreenState.DetailView)
            {
                // if we check for a missing model here we can show the ship modules instead. 
                // that will solve invisible ships when the ship model load hits an OOM.
                if (ShowShipNames || ship.GetSO()?.HasMeshes == false)
                {
                    ship.DrawModulesOverlay(this, CamPos.Z,
                        showDebugSelect:Debug && ship == SelectedShip,
                        showDebugStats: Debug && DebugWin?.IsOpen == true);
                }
            }
        }

        void DrawTacticalIcon(Ship ship)
        {
            if (!LookingAtPlanet && (!ship.IsPlatform  && !ship.IsSubspaceProjector || 
                                     ((showingFTLOverlay || viewState != UnivScreenState.GalaxyView) &&
                                      (!showingFTLOverlay || ship.IsSubspaceProjector))))
            {
                ship.DrawTacticalIcon(this, viewState);
            }
        }

        void DrawBombs()
        {
            using (BombList.AcquireReadLock())
            {
                for (int i = 0; i < BombList.Count; i++)
                {
                    Bomb bomb = BombList[i];
                    DrawTransparentModel(bomb.Model, bomb.World, bomb.Texture, 0.5f);
                }
            }
        }

        void DrawShipGoalsAndWayPoints(Ship ship, byte alpha)
        {
            if (ship == null)
                return;
            Vector2 start = ship.Position;

            if (ship.OnLowAlert || ship.AI.HasPriorityOrder)
            {
                Color color = Colors.Orders(alpha);
                if (ship.AI.State == AIState.Ferrying)
                {
                    DrawLineProjected(start, ship.AI.EscortTarget.Position, color);
                    return;
                }
                if (ship.AI.State == AIState.ReturnToHangar)
                {
                    if (ship.IsHangarShip)
                        DrawLineProjected(start, ship.Mothership.Position, color);
                    else
                        ship.AI.State = AIState.AwaitingOrders; //@todo this looks like bug fix hack. investigate and fix.
                    return;
                }
                if (ship.AI.State == AIState.Escort && ship.AI.EscortTarget != null)
                {
                    DrawLineProjected(start, ship.AI.EscortTarget.Position, color);
                    return;
                }

                if (ship.AI.State == AIState.Explore && ship.AI.ExplorationTarget != null)
                {
                    DrawLineProjected(start, ship.AI.ExplorationTarget.Position, color);
                    return;
                }

                if (ship.AI.State == AIState.Colonize && ship.AI.ColonizeTarget != null)
                {
                    Vector2d screenPos = DrawLineProjected(start, ship.AI.ColonizeTarget.Center, color, 2500f, 0);
                    string text = $"Colonize\nSystem : {ship.AI.ColonizeTarget.ParentSystem.Name}\nPlanet : {ship.AI.ColonizeTarget.Name}";
                    DrawPointerWithText(screenPos.ToVec2f(), ResourceManager.Texture("UI/planetNamePointer"), color, text, new Color(ship.Loyalty.EmpireColor, alpha));
                    return;
                }
                if (ship.AI.State == AIState.Orbit && ship.AI.OrbitTarget != null)
                {
                    DrawLineProjected(start, ship.AI.OrbitTarget.Center, color, 2500f , 0);
                    return;
                }
                if (ship.AI.State == AIState.Rebase)
                {
                    DrawWayPointLines(ship, color);
                    return;
                }
                ShipAI.ShipGoal goal = ship.AI.OrderQueue.PeekFirst;
                if (ship.AI.State == AIState.Bombard && goal?.TargetPlanet != null)
                {
                    DrawLineProjected(ship.Position, goal.TargetPlanet.Center, Colors.CombatOrders(alpha), 2500f);
                    DrawWayPointLines(ship, Colors.CombatOrders(alpha));
                }
            }
            if (!ship.AI.HasPriorityOrder &&
                (ship.AI.State == AIState.AttackTarget || ship.AI.State == AIState.Combat) && ship.AI.Target is Ship)
            {
                DrawLineProjected(ship.Position, ship.AI.Target.Position, Colors.Attack(alpha));
                if (ship.AI.TargetQueue.Count > 1)
                {
                    for (int i = 0; i < ship.AI.TargetQueue.Count - 1; ++i)
                    {
                        var target = ship.AI.TargetQueue[i];
                        if (target == null || !target.Active)
                            continue;
                        DrawLineProjected(target.Position, ship.AI.TargetQueue[i].Position,
                            Colors.Attack((byte) (alpha * .5f)));
                    }
                }
                return;
            }
            if (ship.AI.State == AIState.Boarding && ship.AI.EscortTarget != null)
            {
                DrawLineProjected(start, ship.AI.EscortTarget.Position, Colors.CombatOrders(alpha));
                return;
            }

            var planet = ship.AI.OrbitTarget;
            if (ship.AI.State == AIState.AssaultPlanet && planet != null)
            {
                int spots = planet.GetFreeTiles(EmpireManager.Player);
                if (spots > 4)
                    DrawLineToPlanet(start, planet.Center, Colors.CombatOrders(alpha));
                else if (spots > 0)
                {
                    DrawLineToPlanet(start, planet.Center, Colors.Warning(alpha));
                    ToolTip.PlanetLandingSpotsTip($"{planet.Name}: Warning!", spots);
                }
                else
                {
                    DrawLineToPlanet(start, planet.Center, Colors.Error(alpha));
                    ToolTip.PlanetLandingSpotsTip($"{planet.Name}: Critical!", spots);
                }
                DrawWayPointLines(ship, new Color(Color.Lime, alpha));
                return;
            }

            if (ship.AI.State == AIState.SystemTrader && 
                ship.AI.OrderQueue.TryPeekLast(out ShipAI.ShipGoal g) && g.Trade != null)
            {
                Planet importPlanet = g.Trade.ImportTo;
                Planet exportPlanet = g.Trade.ExportFrom;

                if (g.Plan == ShipAI.Plan.PickupGoods)
                {
                    DrawLineToPlanet(start, exportPlanet.Center, Color.Blue);
                    DrawLineToPlanet(exportPlanet.Center, importPlanet.Center, Color.Gold);
                }
                else
                    DrawLineToPlanet(start, importPlanet.Center, Color.Gold);
            }

            DrawWayPointLines(ship, Colors.WayPoints(alpha));
        }

        void DrawWayPointLines(Ship ship, Color color)
        {
            if (!ship.AI.HasWayPoints)
                return;

            WayPoint[] wayPoints = ship.AI.CopyWayPoints();

            DrawLineProjected(ship.Position, wayPoints[0].Position, color);

            for (int i = 1; i < wayPoints.Length; ++i)
            {
                DrawLineProjected(wayPoints[i-1].Position, wayPoints[i].Position, color);
            }

            // Draw tactical icons after way point lines (looks better this way)
            var tactical = new Color(color, (byte)(color.A + 70));

            WayPoint wp = wayPoints[0];
            DrawShipProjectionIcon(ship, wp.Position, wp.Direction, tactical);
            for (int i = 1; i < wayPoints.Length; ++i)
            {
                wp = wayPoints[i];
                DrawShipProjectionIcon(ship, wp.Position, wp.Direction, tactical);
            }
        }

        private void DrawShields()
        {
            DrawShieldsPerf.Start();
            if (viewState < UnivScreenState.SectorView)
            {
                RenderStates.BasicBlendMode(Device, additive:true, depthWrite:false);
                ShieldManager.Draw(this, View, Projection);
            }
            DrawShieldsPerf.Stop();
        }

        private void DrawPlanetInfo()
        {
            if (LookingAtPlanet || viewState > UnivScreenState.SectorView || viewState < UnivScreenState.ShipView)
                return;
            Vector2 mousePos = Input.CursorPosition;
            SubTexture planetNamePointer = ResourceManager.Texture("UI/planetNamePointer");
            SubTexture icon_fighting_small = ResourceManager.Texture("UI/icon_fighting_small");
            SubTexture icon_spy_small = ResourceManager.Texture("UI/icon_spy_small");
            SubTexture icon_anomaly_small = ResourceManager.Texture("UI/icon_anomaly_small");
            SubTexture icon_troop = ResourceManager.Texture("UI/icon_troop");
            for (int k = 0; k < UState.Systems.Count; k++)
            {
                SolarSystem solarSystem = UState.Systems[k];
                if (!solarSystem.IsExploredBy(Player) || !solarSystem.IsVisible)
                    continue;

                for (int j = 0; j < solarSystem.PlanetList.Count; j++)
                {
                    Planet planet = solarSystem.PlanetList[j];
                    Vector2 screenPosPlanet = ProjectToScreenPosition(planet.Center3D).ToVec2fRounded();
                    Vector2 posOffSet = screenPosPlanet;
                    posOffSet.X += 20f;
                    posOffSet.Y += 37f;
                    int drawLocationOffset = 0;
                    Color textColor = planet.Owner?.EmpireColor ?? Color.Gray;

                    DrawPointerWithText(screenPosPlanet, planetNamePointer, Color.Green, planet.Name, textColor);

                    posOffSet = new Vector2(screenPosPlanet.X + 10f, screenPosPlanet.Y + 60f);

                    if (planet.RecentCombat)
                    {
                        DrawTextureWithToolTip(icon_fighting_small, Color.White, GameText.IndicatesThatAnAnomalyWas, mousePos,
                                               (int)posOffSet.X, (int)posOffSet.Y, 14, 14);
                        ++drawLocationOffset;
                    }
                    if (Player.data.MoleList.Count > 0)
                    {
                        for (int i = 0; i < Player.data.MoleList.Count; i++)
                        {
                            Mole mole = Player.data.MoleList[i];
                            if (mole.PlanetId == planet.Id)
                            {
                                posOffSet.X += (18 * drawLocationOffset);
                                DrawTextureWithToolTip(icon_spy_small, Color.White, GameText.IndicatesThatAFriendlyAgent, mousePos,
                                                       (int)posOffSet.X, (int)posOffSet.Y, 14, 14);
                                ++drawLocationOffset;
                                break;
                            }
                        }
                    }
                    for (int i = 0; i < planet.BuildingList.Count; i++)
                    {
                        Building building = planet.BuildingList[i];
                        if (!building.EventHere) continue;
                        posOffSet.X += (18 * drawLocationOffset);
                        DrawTextureWithToolTip(icon_anomaly_small, Color.White, building.DescriptionText, mousePos,
                                               (int)posOffSet.X, (int)posOffSet.Y, 14, 14);
                        break;
                    }
                    int troopCount = planet.CountEmpireTroops(Player);
                    if (troopCount > 0)
                    {
                        posOffSet.X += (18 * drawLocationOffset);
                        DrawTextureWithToolTip(icon_troop, Color.TransparentWhite, $"Troops {troopCount}", mousePos,
                                               (int)posOffSet.X, (int)posOffSet.Y, 14, 14);
                        ++drawLocationOffset;
                    }
                }
            }
        }

        public void DrawPointerWithText(Vector2 screenPos, SubTexture planetNamePointer, Color pointerColor, string text,
            Color textColor, Graphics.Font font = null, float xOffSet = 20f, float yOffSet = 37f)
        {
            font = font ?? Fonts.Tahoma10;
            DrawTextureRect(planetNamePointer, screenPos, pointerColor);
            Vector2 posOffSet = screenPos;
            posOffSet.X += xOffSet;
            posOffSet.Y += yOffSet;
            HelperFunctions.ClampVectorToInt(ref posOffSet);
            ScreenManager.SpriteBatch.DrawString(font, text, posOffSet, textColor);
        }

    }
}
