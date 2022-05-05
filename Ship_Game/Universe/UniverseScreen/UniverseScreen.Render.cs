using System;
using System.Linq;
using Microsoft.Xna.Framework.Graphics;
using SDGraphics;
using Ship_Game.Empires.Components;
using Ship_Game.Gameplay;
using Ship_Game.Graphics;
using Ship_Game.Ships;
using Ship_Game.ExtensionMethods;
using Vector3 = SDGraphics.Vector3;
using Vector2 = SDGraphics.Vector2;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using BoundingFrustum = Microsoft.Xna.Framework.BoundingFrustum;

namespace Ship_Game
{
    public partial class UniverseScreen
    {
        public void DrawStarField(SpriteBatch batch)
        {
            if (GlobalStats.DrawStarfield)
            {
                bg?.Draw(batch);
            }
        }

        public void DrawNebulae(GraphicsDevice device)
        {
            if (GlobalStats.DrawNebulas)
            {
                bg3d?.Draw(device);
            }
        }

        void RenderBackdrop(SpriteBatch batch)
        {
            BackdropPerf.Start();

            DrawStarField(batch);
            DrawNebulae(Device);

            batch.Begin();

            // if we're zoomed in enough, display solar system overlays with orbits
            if (viewState < UnivScreenState.GalaxyView)
            {
                DrawSolarSystemsWithOrbits();
            }

            batch.End();

            BackdropPerf.Stop();
        }

        void DrawSolarSystemsWithOrbits()
        {
            for (int i = 0; i < UState.Systems.Count; i++)
            {
                SolarSystem solarSystem = UState.Systems[i];
                if (Frustum.Contains(solarSystem.Position, solarSystem.Radius))
                {
                    ProjectToScreenCoords(solarSystem.Position, 4500f, out Vector2d sysScreenPos, out double sysScreenPosDisToRight);
                    Vector2 screenPos = sysScreenPos.ToVec2f();
                    DrawSolarSysWithOrbits(solarSystem, screenPos);
                }
            }
        }

        // This draws the hi-res 3D sun and orbital circles
        void DrawSolarSysWithOrbits(SolarSystem sys, Vector2 sysScreenPos)
        {
            RenderStates.BasicBlendMode(Device, additive:false, depthWrite:true);
            sys.Sun.DrawSunMesh(sys, View, Projection);
            //DrawSunMesh(sys, sys.Zrotate);
            //if (sys.Sun.DoubleLayered) // draw second sun layer
            //    DrawSunMesh(sys, sys.Zrotate / -2.0f);

            if (!sys.IsExploredBy(EmpireManager.Player))
                return;

            for (int i = 0; i < sys.PlanetList.Count; i++)
            {
                Planet planet = sys.PlanetList[i];
                Vector2 planetScreenPos = ProjectToScreenPosition(planet.Center3D).ToVec2f();
                float planetOrbitRadius = sysScreenPos.Distance(planetScreenPos);

                if (viewState > UnivScreenState.ShipView && !IsCinematicModeEnabled)
                {
                    var transparentDarkGray = new Color(50, 50, 50, 90);
                    DrawCircle(sysScreenPos, planetOrbitRadius, transparentDarkGray, 3f);

                    if (planet.Owner == null)
                    {
                        DrawCircle(sysScreenPos, planetOrbitRadius, transparentDarkGray, 3f);
                    }
                    else
                    {
                        var empireColor = new Color(planet.Owner.EmpireColor, 100);
                        DrawCircle(sysScreenPos, planetOrbitRadius, empireColor, 3f);
                    }
                }
            }
        }

        // @todo This is unused??? Maybe some legacy code?
        // I think this draws a big galaxy texture
        void RenderGalaxyBackdrop()
        {
            ScreenManager.SpriteBatch.Begin();
            for (int index = 0; index < 41; ++index)
            {
                Vector2d a = ProjectToScreenPosition(new Vector3((float)(index * (double)UState.Size / 40.0), 0f, 0f));
                Vector2d b = ProjectToScreenPosition(new Vector3((float)(index * (double)UState.Size / 40.0), UState.Size, 0f));
                ScreenManager.SpriteBatch.DrawLine(a, b, new Color(211, 211, 211, 70));
            }
            for (int index = 0; index < 41; ++index)
            {
                Vector2d a = ProjectToScreenPosition(new Vector3(0f, (float)(index * (double)UState.Size / 40.0), 40f));
                Vector2d b = ProjectToScreenPosition(new Vector3(UState.Size, (float)(index * (double)UState.Size / 40.0), 0f));
                ScreenManager.SpriteBatch.DrawLine(a, b, new Color(211, 211, 211, 70));
            }
            ScreenManager.SpriteBatch.End();
        }

        void RenderOverFog(SpriteBatch batch)
        {
            DrawOverFog.Start();
            if (viewState >= UnivScreenState.SectorView) // draw colored empire borders only if zoomed out
            {
                // set the alpha value depending on camera height
                int maxAlpha = 180;
                int alpha = (int)(maxAlpha * CamPos.Z / (double)UnivScreenState.SectorView);
                if (alpha > maxAlpha) alpha = maxAlpha;
                else if (alpha < 5) alpha = 0;

                var color = new Color(255, 255, 255, (byte) alpha);
                batch.Draw(BorderRT.GetTexture(), new Rectangle(0, 0, ScreenWidth, ScreenHeight), color);
            }

            foreach (SolarSystem sys in UState.Systems)
            {
                if (viewState >= UnivScreenState.SectorView)
                {
                    DrawSolarSystemSectorView(sys);
                }
                if (viewState >= UnivScreenState.GalaxyView) // super zoomed out
                {
                    sys.Sun.DrawLowResSun(batch, sys, View, Projection);
                }
            }

            if (viewState > UnivScreenState.SectorView)
            {
                var currentEmpire = SelectedShip?.Loyalty ?? Player;
                var enemies = EmpireManager.GetEnemies(currentEmpire);
                var ssps    = EmpireManager.Player.GetProjectors();
                for (int i = 0; i < ssps.Count; i++)
                {
                    var ssp = ssps[i];
                    int spacing = 1;
                    for (int x = 0; x < enemies.Count; x++)
                    {
                        var empire = enemies[x];
                        if (ssp.HasSeenEmpires.KnownBy(empire))
                        {
                            var screenPos = ProjectToScreenPosition(ssp.Position);
                            var flag = empire.data.Traits.FlagIndex;
                            int xPos = (int)screenPos.X + (15 + GlobalStats.IconSize) * spacing;
                            Rectangle rectangle2 = new Rectangle(xPos, (int)screenPos.Y, 15 + GlobalStats.IconSize, 15 + GlobalStats.IconSize);
                            batch.Draw(ResourceManager.Flag(flag), rectangle2, ApplyCurrentAlphaToColor(empire.EmpireColor));
                            spacing++;
                        }
                    }
                }
                foreach (IncomingThreat threat in Player.SystemsWithThreat)
                {
                    if (threat.ThreatTimedOut) continue;

                    var system = threat.TargetSystem;
                    float pulseRad = PulseTimer * (threat.TargetSystem.Radius * 1.5f );

                    var red = new Color(Color.Red, 40);
                    var black = new Color(Color.Black, 40);

                    DrawCircleProjected(system.Position, pulseRad, red, 10);
                    DrawCircleProjected(system.Position, pulseRad * 1.001f, black, 5);
                    DrawCircleProjected(system.Position, pulseRad * 1.3f, red, 10);
                    DrawCircleProjected(system.Position, pulseRad * 1.301f, black, 5);

                    //batch.DrawCircle(system.Position, pulseRad, new Color(Color.Red, 255 - 255 * threat.PulseTime), pulseRad);
                    //batch.DrawCircle(system.Position, pulseRad, new Color(Color.Red, 40 * threat.PulseTime), pulseRad);
                }
            }
            DrawOverFog.Stop();
        }

        void DrawSolarSystemSectorView(SolarSystem solarSystem)
        {
            if (!Frustum.Contains(solarSystem.Position, 10f))
                return;

            SpriteBatch batch = ScreenManager.SpriteBatch;

            Vector2d solarSysPos = ProjectToScreenPosition(solarSystem.Position.ToVec3());
            Vector2d b = ProjectToScreenPosition(new Vector3(solarSystem.Position.PointFromAngle(90f, 25000f), 0f));
            float num2 = (float)solarSysPos.Distance(b);
            Vector2 vector2 = solarSysPos.ToVec2f();

            if ((solarSystem.IsExploredBy(Player) || Debug) && SelectedSystem != solarSystem)
            {
                if (Debug)
                {
                    solarSystem.SetExploredBy(Player);
                    foreach (Planet planet in solarSystem.PlanetList)
                        planet.SetExploredBy(Player);
                }

                var groundAttack = ResourceManager.Texture("Ground_UI/Ground_Attack");
                var enemyHere =ResourceManager.Texture("Ground_UI/EnemyHere");

                Vector2d p = ProjectToScreenPosition(new Vector3(new Vector2(100000f, 0f) + solarSystem.Position, 0f));
                float radius = (float)p.Distance(solarSysPos);
                if (viewState == UnivScreenState.SectorView)
                {
                    vector2.Y += radius;
                    var transparentDarkGray = new Color(50, 50, 50, 90);
                    DrawCircle(solarSysPos, radius, transparentDarkGray);
                }
                else
                {
                    vector2.Y += num2;
                }

                vector2.X -= SolarsystemOverlay.SysFont.MeasureString(solarSystem.Name).X / 2f;
                Vector2 pos = Input.CursorPosition;

                Array<Empire> owners = new Array<Empire>();
                bool wellKnown = false;

                foreach (Empire e in solarSystem.OwnerList)
                {
                    EmpireManager.Player.GetRelations(e, out Relationship ssRel);
                    wellKnown = Debug || e.isPlayer || ssRel.Treaty_Alliance;
                    if (wellKnown) break;
                    if (ssRel.Known) // (ssRel.Treaty_Alliance || ssRel.Treaty_Trade || ssRel.Treaty_OpenBorders))
                        owners.Add(e);
                }

                if (wellKnown)
                {
                    owners = solarSystem.OwnerList.ToArrayList();
                }

                if (owners.Count == 0)
                {
                    if (SelectedSystem != solarSystem ||
                        viewState < UnivScreenState.GalaxyView)
                        ScreenManager.SpriteBatch.DrawString(SolarsystemOverlay.SysFont,
                            solarSystem.Name, vector2, Color.Gray);
                    int num3 = 0;
                    --vector2.Y;
                    vector2.X += SolarsystemOverlay.SysFont.MeasureString(solarSystem.Name).X + 6f;
                    bool flag = false;
                    foreach (Planet planet in solarSystem.PlanetList)
                    {
                        if (planet.IsExploredBy(Player))
                        {
                            for (int index = 0; index < planet.BuildingList.Count; ++index)
                            {
                                if (planet.BuildingList[index].EventHere)
                                {
                                    flag = true;
                                    break;
                                }
                            }

                            if (flag)
                                break;
                        }
                    }

                    if (flag)
                    {
                        vector2.Y -= 2f;
                        Rectangle rectangle2 = new Rectangle((int) vector2.X, (int) vector2.Y, 15, 15);
                        ScreenManager.SpriteBatch.Draw(
                            ResourceManager.Texture("UI/icon_anomaly_small"), rectangle2, CurrentFlashColor);
                        if (rectangle2.HitTest(pos))
                            ToolTip.CreateTooltip(GameText.IndicatesThatAnAnomalyHas);
                        ++num3;
                    }

                    if (EmpireManager.Player.KnownEnemyStrengthIn(solarSystem).Greater(0))
                    {
                        vector2.X += num3 * 20;
                        vector2.Y -= 2f;
                        Rectangle rectangle2 = new Rectangle((int)vector2.X, (int)vector2.Y, enemyHere.Width, enemyHere.Height);
                        ScreenManager.SpriteBatch.Draw(enemyHere, rectangle2, CurrentFlashColor);
                        if (rectangle2.HitTest(pos))
                            ToolTip.CreateTooltip(GameText.IndicatesThatHostileForcesWere);
                        ++num3;

                        if (solarSystem.HasPlanetsOwnedBy(EmpireManager.Player) && solarSystem.PlanetList.Any(p => p.SpaceCombatNearPlanet))
                        {
                            if (num3 == 1 || num3 == 2)
                                vector2.X += 20f;
                            Rectangle rectangle3 = new Rectangle((int)vector2.X, (int)vector2.Y, groundAttack.Width,groundAttack.Height);
                            ScreenManager.SpriteBatch.Draw(groundAttack, rectangle3, CurrentFlashColor);
                            if (rectangle3.HitTest(pos))
                                ToolTip.CreateTooltip(GameText.IndicatesThatSpaceCombatIs);
                        }
                    }
                }
                else
                {
                    int num3 = 0;
                    if (owners.Count == 1)
                    {
                        if (SelectedSystem != solarSystem ||
                            viewState < UnivScreenState.GalaxyView)
                            HelperFunctions.DrawDropShadowText(batch, solarSystem.Name,
                                vector2, SolarsystemOverlay.SysFont,
                                owners.ToList()[0].EmpireColor);
                    }
                    else if (SelectedSystem != solarSystem ||
                             viewState < UnivScreenState.GalaxyView)
                    {
                        Vector2 Pos = vector2;
                        int length = solarSystem.Name.Length;
                        int num4 = length / owners.Count;
                        int index1 = 0;
                        for (int index2 = 0; index2 < length; ++index2)
                        {
                            if (index2 + 1 > num4 + num4 * index1)
                                ++index1;
                            HelperFunctions.DrawDropShadowText(batch,
                                solarSystem.Name[index2].ToString(), Pos, SolarsystemOverlay.SysFont,
                                owners.Count > index1
                                    ? owners[index1].EmpireColor
                                    : owners.Last
                                    .EmpireColor);
                            Pos.X += SolarsystemOverlay.SysFont
                                .MeasureString(solarSystem.Name[index2].ToString())
                                .X;
                        }
                    }

                    --vector2.Y;
                    vector2.X += SolarsystemOverlay.SysFont.MeasureString(solarSystem.Name).X + 6f;
                    bool flag = false;
                    foreach (Planet planet in solarSystem.PlanetList)
                    {
                        if (planet.IsExploredBy(Player))
                        {
                            for (int index = 0; index < planet.BuildingList.Count; ++index)
                            {
                                if (planet.BuildingList[index].EventHere)
                                {
                                    flag = true;
                                    break;
                                }
                            }

                            if (flag)
                                break;
                        }
                    }

                    if (flag)
                    {
                        vector2.Y -= 2f;
                        Rectangle rectangle2 = new Rectangle((int) vector2.X, (int) vector2.Y, 15, 15);
                        ScreenManager.SpriteBatch.Draw(
                            ResourceManager.Texture("UI/icon_anomaly_small"), rectangle2, CurrentFlashColor);
                        if (rectangle2.HitTest(pos))
                            ToolTip.CreateTooltip(GameText.IndicatesThatAnAnomalyHas);
                        ++num3;
                    }

                    if (EmpireManager.Player.KnownEnemyStrengthIn(solarSystem).Greater(0))
                    {
                        vector2.X += num3 * 20;
                        vector2.Y -= 2f;
                        Rectangle rectangle3 = new Rectangle((int)vector2.X, (int)vector2.Y, enemyHere.Width, enemyHere.Height);
                        ScreenManager.SpriteBatch.Draw(enemyHere, rectangle3, CurrentFlashColor);
                        if (rectangle3.HitTest(pos))
                            ToolTip.CreateTooltip(GameText.IndicatesThatHostileForcesWere);
                        ++num3;


                        if (solarSystem.HasPlanetsOwnedBy(EmpireManager.Player) && solarSystem.PlanetList.Any(p => p.SpaceCombatNearPlanet))
                        {
                            if (num3 == 1 || num3 == 2)
                                vector2.X += 20f;
                            var rectangle2 = new Rectangle((int)vector2.X, (int)vector2.Y, groundAttack.Width, groundAttack.Height);
                            ScreenManager.SpriteBatch.Draw(groundAttack, rectangle2, CurrentFlashColor);
                            if (rectangle2.HitTest(pos))
                                ToolTip.CreateTooltip(GameText.IndicatesThatSpaceCombatIs);
                        }
                    }
                }
            }
            else
                vector2.X -= SolarsystemOverlay.SysFont.MeasureString(solarSystem.Name).X / 2f;
        }

        void RenderThrusters()
        {
            if (viewState > UnivScreenState.ShipView)
                return;

            Ship[] ships = UState.Objects.VisibleShips;
            for (int i = 0; i < ships.Length; ++i)
            {
                Ship ship = ships[i];
                if (ship.InSensorRange)
                {
                    ship.RenderThrusters(ref View, ref Projection);
                }
            }
        }

        public void DrawZones(Graphics.Font font, string text, ref int cursorY, Color color)
        {
            Vector2 rect = new Vector2(SelectedStuffRect.X, cursorY);
            ScreenManager.SpriteBatch.DrawString(font, text, rect, color);
            cursorY += font.LineSpacing + 2;
        }

        public void DrawShipAOAndTradeRoutes()
        {
            if (DefiningAO && Input.LeftMouseDown)
                DrawRectangleProjected(new RectF(AORect), Color.Orange);

            if ((DefiningAO || DefiningTradeRoutes) && SelectedShip != null)
            {
                string title  = DefiningAO ? Localizer.Token(GameText.AssignAreaOfOperation) + " (ESC to exit)" : Localizer.Token(GameText.AssignPlanetsToTradeRoute);
                int cursorY   = 100;
                int numAo     = SelectedShip.AreaOfOperation.Count;
                int numRoutes = SelectedShip.TradeRoutes.Count;

                DrawZones(Fonts.Pirulen20, title, ref cursorY, Color.Red);
                if (numAo > 0)
                    DrawZones(Fonts.Pirulen16, $"Current Area of Operation Number: {numAo}", ref cursorY, Color.Pink);

                if (numRoutes > 0)
                    DrawZones(Fonts.Pirulen16, $"Current list of planets in trade route: {numRoutes}", ref cursorY, Color.White);

                foreach (Rectangle ao in SelectedShip.AreaOfOperation)
                    DrawRectangleProjected(new RectF(ao), Color.Red, new Color(Color.Red, 50));

                // Draw Specific Trade Routes to planets
                if (SelectedShip.IsFreighter)
                {
                    foreach (int planetId in SelectedShip.TradeRoutes)
                    {
                        Planet planet = UState.GetPlanet(planetId);
                        if (planet.Owner != null)
                        {
                            DrawLineToPlanet(SelectedShip.Position, planet.Center, planet.Owner.EmpireColor);
                            DrawZones(Fonts.Arial14Bold, $"- {planet.Name}", ref cursorY, planet.Owner.EmpireColor);
                        }
                    }
                }
            }
            else
            {
                DefiningAO          = false;
                DefiningTradeRoutes = false;
            }
        }

        // Deferred SceneObject loading jobs use a double buffered queue.
        readonly Array<Ship> SceneObjFrontQueue = new Array<Ship>(32);
        readonly Array<Ship> SceneObjBackQueue  = new Array<Ship>(32);

        public void QueueSceneObjectCreation(Ship ship)
        {
            lock (SceneObjFrontQueue)
            {
                SceneObjFrontQueue.Add(ship);
            }
        }

        // Only create ship scene objects on the main UI thread
        void CreateShipSceneObjects()
        {
            lock (SceneObjFrontQueue)
            {
                SceneObjBackQueue.AddRange(SceneObjFrontQueue);
                SceneObjFrontQueue.Clear();
            }

            for (int i = SceneObjBackQueue.Count - 1; i >= 0; --i)
            {
                Ship ship = SceneObjBackQueue[i];
                if (!ship.Active) // dead or removed
                {
                    SceneObjBackQueue.RemoveAtSwapLast(i);
                }
                else if (ship.GetSO() != null) // already created
                {
                    SceneObjBackQueue.RemoveAtSwapLast(i);
                }
                else if (ship.IsVisibleToPlayer)
                {
                    ship.CreateSceneObject();
                    SceneObjBackQueue.RemoveAtSwapLast(i);
                }
                // else: we keep it in the back queue until it dies or comes into frustum
            }
        }

        void Render(SpriteBatch batch, DrawTimes elapsed)
        {
            RenderGroupTotalPerf.Start();
            if (Frustum == null)
                Frustum = new BoundingFrustum(View * Projection);
            else
                Frustum.Matrix = View * Projection;

            CreateShipSceneObjects();

            BeginSunburnPerf.Start();
            ScreenManager.BeginFrameRendering(elapsed, ref View, ref Projection);
            BeginSunburnPerf.Stop();

            RenderBackdrop(batch);

            RenderStates.BasicBlendMode(Device, additive:false, depthWrite:true);
            RenderStates.EnableMultiSampleAA(Device);

            batch.Begin();
            DrawShipAOAndTradeRoutes();
            SelectShipLinesToDraw();
            batch.End();

            DrawBombs();

            SunburnDrawPerf.Start();
            {
                ScreenManager.RenderSceneObjects();
            }
            SunburnDrawPerf.Stop();

            DrawAnomalies(Device);
            DrawPlanets();

            EndSunburnPerf.Start();
            {
                ScreenManager.EndFrameRendering();
            }
            EndSunburnPerf.Stop();

            // render shield and particle effects after Sunburn 3D models
            DrawShields();
            DrawAndUpdateParticles(elapsed, Device);
            DrawExplosions(batch);
            DrawOverlayShieldBubbles(batch);

            RenderGroupTotalPerf.Stop();

            RenderStates.EnableDepthWrite(Device);
        }

        private void DrawAnomalies(GraphicsDevice device)
        {
            if (anomalyManager == null)
                return;

            RenderStates.BasicBlendMode(device, additive:true, depthWrite:true);

            for (int x = 0; x < anomalyManager.AnomaliesList.Count; x++)
            {
                Anomaly anomaly = anomalyManager.AnomaliesList[x];
                anomaly.Draw();
            }
        }

        private void DrawAndUpdateParticles(DrawTimes elapsed, GraphicsDevice device)
        {
            DrawParticles.Start();

            RenderStates.BasicBlendMode(device, additive:true, depthWrite:false);

            if (viewState < UnivScreenState.SectorView)
            {
                RenderThrusters();
                FTLManager.DrawFTLModels(ScreenManager.SpriteBatch, this);
            }

            Particles.Draw(View, Projection, nearView: viewState < UnivScreenState.SectorView);

            if (!UState.Paused) // Particle pools need to be updated
            {
                Particles.Update(CurrentSimTime);
            }

            DrawParticles.Stop();
        }

        private void DrawPlanets()
        {
            DrawPlanetsPerf.Start();
            if (viewState < UnivScreenState.SectorView)
            {
                var r = ResourceManager.Planets.Renderer;
                r.BeginRendering(Device, CamPos.ToVec3f(), View, Projection);

                foreach (SolarSystem system in UState.Systems)
                {
                    if (system.IsVisible)
                    {
                        foreach (Planet p in system.PlanetList)
                            if (p.IsVisible)
                                r.Render(p);
                    }
                }

                r.EndRendering();
            }
            DrawPlanetsPerf.Stop();
        }

        private void SelectShipLinesToDraw()
        {
            byte alpha = (byte)Math.Max(0f, 150f * SelectedSomethingTimer / 3f);
            if (alpha > 0)
            {
                if (SelectedShip != null && (Debug
                                             || SelectedShip.Loyalty.isPlayer
                                             || !Player.DifficultyModifiers.HideTacticalData 
                                             || Player.IsAlliedWith(SelectedShip.Loyalty)
                                             || SelectedShip.AI.Target != null))
                {
                    DrawShipGoalsAndWayPoints(SelectedShip, alpha);
                }
                else 
                {
                    for (int i = 0; i < SelectedShipList.Count; ++i)
                    {
                        Ship ship = SelectedShipList[i];
                        if (ship.Loyalty.isPlayer
                            || Player.IsAlliedWith(ship.Loyalty)
                            || Debug
                            || !Player.DifficultyModifiers.HideTacticalData
                            || ship.AI.Target != null)
                        {
                            DrawShipGoalsAndWayPoints(ship, alpha);
                        }
                    }
                }
            }
        }
    }
}
