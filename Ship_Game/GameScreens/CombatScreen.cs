using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Ship_Game.Audio;
using Ship_Game.Ships;
using System;
using System.Linq;
using Ship_Game.SpriteSystem;

namespace Ship_Game
{
    public sealed class CombatScreen : PlanetScreen, IDisposable
    {
        public Planet p;
        private Menu2 TitleBar;
        private Vector2 TitlePos;
        private Menu2 CombatField;
        private Rectangle gridPos;
        private Menu1 OrbitalResources;
        private Submenu orbitalResourcesSub;
        private ScrollList OrbitSL;
        //private bool LowRes;
        private PlanetGridSquare HoveredSquare;
        private Rectangle SelectedItemRect;
        private Rectangle HoveredItemRect;
        private Rectangle AssetsRect;
        private OrbitalAssetsUIElement assetsUI;
        private TroopInfoUIElement tInfo;
        private TroopInfoUIElement hInfo;
        private UIButton LandAll;
        private UIButton LaunchAll;
        private Rectangle GridRect;
        private Array<PointSet> CenterPoints = new Array<PointSet>();
        private Array<PointSet> pointsList   = new Array<PointSet>();
        private bool ResetNextFrame;
        public PlanetGridSquare ActiveTile;
        private Selector selector;
        private float OrbitalAssetsTimer; // 2 seconds per update

        ScrollList.Entry draggedTroop;
        Array<PlanetGridSquare> ReversedList              = new Array<PlanetGridSquare>();
        BatchRemovalCollection<SmallExplosion> Explosions = new BatchRemovalCollection<SmallExplosion>();

        private float[] anglesByColumn = { (float)Math.Atan(0), (float)Math.Atan(0), (float)Math.Atan(0), (float)Math.Atan(0), (float)Math.Atan(0), (float)Math.Atan(0), (float)Math.Atan(0) };
        private float[] distancesByRow = { 437f, 379f, 311f, 229f, 128f, 0f };
        private float[] widthByRow     = { 110f, 120f, 132f, 144f, 162f, 183f };
        private float[] startXByRow    =  { 254f, 222f, 181f, 133f, 74f, 0f };


        private static bool popup;  //fbedard

        public CombatScreen(GameScreen parent, Planet p) : base(parent)
        {
            this.p                = p;
            int screenWidth       = ScreenManager.GraphicsDevice.PresentationParameters.BackBufferWidth;
            GridRect              = new Rectangle(screenWidth / 2 - 639, ScreenManager.GraphicsDevice.PresentationParameters.BackBufferHeight - 490, 1278, 437);
            Rectangle titleRect   = new Rectangle(screenWidth / 2 - 250, 44, 500, 80);
            TitleBar              = new Menu2(titleRect);
            TitlePos              = new Vector2(titleRect.X + titleRect.Width / 2 - Fonts.Laserian14.MeasureString("Ground Combat").X / 2f, titleRect.Y + titleRect.Height / 2 - Fonts.Laserian14.LineSpacing / 2);
            SelectedItemRect      = new Rectangle(screenWidth - 240, 100, 225, 205);
            AssetsRect            = new Rectangle(10, 48, 225, 200);
            HoveredItemRect       = new Rectangle(10, 248, 225, 200);
            assetsUI              = new OrbitalAssetsUIElement(AssetsRect, ScreenManager, Empire.Universe, p);
            tInfo                 = new TroopInfoUIElement(SelectedItemRect, ScreenManager, Empire.Universe);
            hInfo                 = new TroopInfoUIElement(HoveredItemRect, ScreenManager, Empire.Universe);
            Rectangle ColonyGrid  = new Rectangle(screenWidth / 2 - screenWidth * 2 / 3 / 2, 130, screenWidth * 2 / 3, screenWidth * 2 / 3 * 5 / 7);
            CombatField           = new Menu2(ColonyGrid);
            Rectangle OrbitalRect = new Rectangle(5, ColonyGrid.Y, (screenWidth - ColonyGrid.Width) / 2 - 20, ColonyGrid.Height+20);
            OrbitalResources      = new Menu1(OrbitalRect);
            Rectangle psubRect    = new Rectangle(AssetsRect.X + 225, AssetsRect.Y+23, 200, AssetsRect.Height * 2);
            orbitalResourcesSub   = new Submenu(psubRect);
            OrbitSL               = new ScrollList(orbitalResourcesSub);

            orbitalResourcesSub.AddTab("In Orbit");

            LandAll   = Button(orbitalResourcesSub.Menu.X + 20, orbitalResourcesSub.Menu.Y - 2, "Land All", OnLandAllClicked);
            LaunchAll = Button(orbitalResourcesSub.Menu.X + 20, LandAll.Rect.Y - 2 - LandAll.Rect.Height, "Launch All", OnLaunchAllClicked);
            LandAll.Tooltip   = Localizer.Token(1951);
            LaunchAll.Tooltip = Localizer.Token(1952);

            gridPos   = new Rectangle(ColonyGrid.X + 20, ColonyGrid.Y + 20, ColonyGrid.Width - 40, ColonyGrid.Height - 40);
            int xSize = gridPos.Width / 7;
            int ySize = gridPos.Height / 5;
            foreach (PlanetGridSquare pgs in p.TilesList)
            {
                pgs.ClickRect = new Rectangle(gridPos.X + pgs.x * xSize, gridPos.Y + pgs.y * ySize, xSize, ySize);
                foreach (var troop in pgs.TroopsHere)
                {
                    //@TODO HACK. first frame is getting overwritten or lost somewhere.
                    troop.WhichFrame = troop.first_frame;
                }
            }
            for (int row = 0; row < 6; row++)
            {
                for (int i = 0; i < 7; i++)
                {
                    var ps = new PointSet
                    {
                        point = new Vector2(GridRect.X + i * widthByRow[row] + widthByRow[row] / 2f + startXByRow[row], GridRect.Y + GridRect.Height - distancesByRow[row]),
                        row = row,
                        column = i
                    };
                    pointsList.Add(ps);
                }
            }

            foreach (PointSet ps in pointsList)
            {
                foreach (PointSet toCheck in pointsList)
                {
                    if (ps.column == toCheck.column && ps.row == toCheck.row - 1)
                    {
                        float distance = Vector2.Distance(ps.point, toCheck.point);
                        Vector2 vtt = toCheck.point - ps.point;
                        vtt = Vector2.Normalize(vtt);
                        Vector2 cPoint = ps.point + ((vtt * distance) / 2f);
                        var cp = new PointSet
                        {
                            point = cPoint,
                            row = ps.row,
                            column = ps.column
                        };
                        CenterPoints.Add(cp);
                    }
                }
            }

            foreach (PlanetGridSquare pgs in p.TilesList)
            {
                foreach (PointSet ps in CenterPoints)
                {
                    if (pgs.x == ps.column && pgs.y == ps.row)
                        pgs.ClickRect = new Rectangle((int) ps.point.X - 32, (int) ps.point.Y - 32, 64, 64);
                }
            }

            foreach (PlanetGridSquare pgs in p.TilesList)
                ReversedList.Add(pgs);
        }

        private void DetermineAttackAndMove()
        {
            foreach (PlanetGridSquare pgs in p.TilesList)
            {
                pgs.CanAttack = false;
                pgs.CanMoveTo = false;
                if (ActiveTile == null)
                pgs.ShowAttackHover = false;
            }
            if (ActiveTile == null)
            {
                //added by gremlin why two loops? moved hover clear to first loop and move null check to third loop.
                //foreach (PlanetGridSquare pgs in this.p.TilesList)
                //{
                //    pgs.CanMoveTo = false;
                //    pgs.CanAttack = false;
                //    pgs.ShowAttackHover = false;
                //}
            }
            if (ActiveTile != null)
            {
                foreach (PlanetGridSquare pgs in p.TilesList)
                {
                    if (pgs.CombatBuildingOnTile)
                        pgs.CanMoveTo = false;

                    if (ActiveTile != pgs)
                        continue;

                    if (ActiveTile.TroopsAreOnTile && ActiveTile.SingleTroop.CanAttack)
                    {
                        foreach (PlanetGridSquare nearby in p.TilesList)
                        {
                            if (nearby == pgs)
                                continue;

                            int xTotalDistance = Math.Abs(pgs.x - nearby.x);
                            int yTotalDistance = Math.Abs(pgs.y - nearby.y);
                            if (xTotalDistance > pgs.SingleTroop.Range || yTotalDistance > pgs.SingleTroop.Range || nearby.NoTroopsOnTile && (nearby.building == null || nearby.building.CombatStrength <= 0))
                                continue;

                            if ((nearby.TroopsAreOnTile && nearby.SingleTroop.Loyalty != EmpireManager.Player) || (nearby.CombatBuildingOnTile && p.Owner != EmpireManager.Player))  //fbedard: cannot attack allies !
                                nearby.CanAttack = true;
                        }
                    }
                    else if (ActiveTile.CombatBuildingOnTile && ActiveTile.building.CanAttack)
                    {
                        foreach (PlanetGridSquare nearby in p.TilesList)
                        {
                            if (nearby == pgs)
                                continue;

                            int xTotalDistance = Math.Abs(pgs.x - nearby.x);
                            int yTotalDistance = Math.Abs(pgs.y - nearby.y);
                            if (xTotalDistance > 1 || yTotalDistance > 1 || nearby.NoTroopsOnTile && (nearby.building == null || nearby.building.CombatStrength <= 0))
                                continue;

                            if ((nearby.TroopsAreOnTile && nearby.SingleTroop.Loyalty != EmpireManager.Player) || (nearby.CombatBuildingOnTile && p.Owner != EmpireManager.Player))  //fbedard: cannot attack allies !
                                nearby.CanAttack = true;
                        }
                    }
                    if (ActiveTile.NoTroopsOnTile || !ActiveTile.SingleTroop.CanAttack)
                        continue;

                    foreach (PlanetGridSquare nearby in p.TilesList)
                    {
                        if (nearby == pgs)
                            continue;

                        int xTotalDistance = Math.Abs(pgs.x - nearby.x);
                        int yTotalDistance = Math.Abs(pgs.y - nearby.y);
                        if (xTotalDistance > pgs.SingleTroop.Range || yTotalDistance > pgs.SingleTroop.Range || nearby.TroopsAreOnTile || nearby.BuildingOnTile && (nearby.NoBuildingOnTile || nearby.building.CombatStrength != 0))
                            continue;

                        nearby.CanMoveTo = true;
                    }
                }
            }
        }

        private void DrawTroopAsset(SpriteBatch batch, Vector2 bCursor, Troop t, float cursorY, Color nameColor, Color statsColor)
        {
            bCursor.Y = cursorY;
            batch.Draw(t.TextureDefault, new Rectangle((int)bCursor.X, (int)bCursor.Y, 29, 30), Color.White);
            var tCursor = new Vector2(bCursor.X + 40f, bCursor.Y + 3f);
            batch.DrawString(Fonts.Arial12Bold, t.Name, tCursor, nameColor);
            tCursor.Y += Fonts.Arial12Bold.LineSpacing;
            batch.DrawString(Fonts.Arial8Bold, t.StrengthText + ", Level: " + t.Level, tCursor, statsColor);
        }

        public override void Draw(SpriteBatch batch)
        {
            GameTime gameTime = StarDriveGame.Instance.GameTime;
            batch.Draw(ResourceManager.Texture($"PlanetTiles/{p.PlanetTileId}_tilt"), GridRect, Color.White);
            batch.Draw(ResourceManager.Texture("Ground_UI/grid"), GridRect, Color.White);

            if (assetsUI.LandTroops.Toggled)
            {
                OrbitSL.Draw(batch);
                var bCursor = new Vector2((orbitalResourcesSub.Menu.X + 25), 350f);
                foreach (ScrollList.Entry e in OrbitSL.VisibleExpandedEntries)
                {
                    if (e.item is Troop t)
                    {
                        Color nameColor = Color.LightGray;
                        Color statsColor = nameColor;
                        if (e.Hovered)
                        {
                            nameColor  = Color.Gold;
                            statsColor = Color.Orange;
                        }

                        DrawTroopAsset(batch, bCursor, t, e.Y, nameColor, statsColor);
                    }
                    e.CheckHover(Input.CursorPosition);
                }
            }

            LaunchAll.Draw(batch);
            LandAll.Draw(batch);

            foreach (PlanetGridSquare pgs in ReversedList)
            {
                if (pgs.BuildingOnTile)
                {
                    var bRect = new Rectangle(pgs.ClickRect.X + pgs.ClickRect.Width / 2 - 32, pgs.ClickRect.Y + pgs.ClickRect.Height / 2 - 32, 64, 64);
                    batch.Draw(ResourceManager.Texture($"Buildings/icon_{pgs.building.Icon}_64x64"), bRect, Color.White);
                }
            }
            foreach (PlanetGridSquare pgs in ReversedList)
            {
                DrawTileIcons(pgs);
                DrawCombatInfo(pgs);
            }
            if (ActiveTile != null)
            {
                tInfo.Draw(gameTime);
            }

            assetsUI.Draw(gameTime);
            if (draggedTroop != null)
            {
                foreach (PlanetGridSquare pgs in ReversedList)
                {
                    if ((pgs.building == null && pgs.TroopsHere.Count == 0) ||
                        (pgs.building != null && pgs.building.CombatStrength == 0 && pgs.TroopsHere.Count == 0))
                    {
                        Vector2 center = pgs.ClickRect.Center();
                        DrawCircle(center, 5f, Color.White, 5f);
                        DrawCircle(center, 5f, Color.Black);
                    }
                }

                Troop troop = draggedTroop.TryGet(out Ship ship) && ship.TroopList.Count > 0
                            ? ship.TroopList.First : draggedTroop.Get<Troop>();

                SubTexture icon = troop.TextureDefault;
                batch.Draw(icon, Input.CursorPosition, Color.White, 0f, icon.CenterF, 0.65f, SpriteEffects.None, 1f);
            }
            if (Empire.Universe.IsActive)
            {
                ToolTip.Draw(batch);
            }
            batch.End();

            batch.Begin(SpriteBlendMode.Additive);
            using (Explosions.AcquireReadLock())
            foreach (SmallExplosion exp in Explosions)
                exp.Draw(batch);
            batch.End();

            batch.Begin();

            if (ScreenManager.NumScreens == 2)
                popup = true;
        }

        private void DrawCombatInfo(PlanetGridSquare pgs)
        {
            if ((ActiveTile == null || ActiveTile != pgs) &&
                (pgs.building == null || pgs.building.CombatStrength <= 0 || ActiveTile == null ||
                 ActiveTile != pgs))
                return;

            var activeSel = new Rectangle(pgs.TroopClickRect.X - 5, pgs.TroopClickRect.Y - 5, pgs.TroopClickRect.Width + 10, pgs.TroopClickRect.Height + 10);
            ScreenManager.SpriteBatch.Draw(ResourceManager.Texture("Ground_UI/GC_Square Selection"), activeSel, Color.White);
            foreach (PlanetGridSquare nearby in ReversedList)
            {
                if (nearby != pgs && nearby.ShowAttackHover)
                    ScreenManager.SpriteBatch.Draw(ResourceManager.Texture("Ground_UI/GC_Attack_Confirm"),
                        nearby.TroopClickRect, Color.White);
            }
        }

        private void DrawTileIcons(PlanetGridSquare pgs)
        {
            SpriteBatch batch = ScreenManager.SpriteBatch;

            float width = (pgs.y * 15 + 64);
            if (width > 128f)
                width = 128f;
            if (pgs.building != null && pgs.building.CombatStrength > 0)
                width = 64f;

            pgs.TroopClickRect = new Rectangle(pgs.ClickRect.X + pgs.ClickRect.Width / 2 - (int)width / 2, pgs.ClickRect.Y + pgs.ClickRect.Height / 2 - (int)width / 2, (int)width, (int)width);
            if (pgs.TroopsAreOnTile)
            {
                Troop troop = pgs.SingleTroop;
                Rectangle troopClickRect = pgs.TroopClickRect;
                if (troop.MovingTimer > 0f)
                {
                    float amount          = 1f - troop.MovingTimer;
                    troopClickRect.X      = (int)MathHelper.Lerp(troop.FromRect.X, pgs.TroopClickRect.X, amount);
                    troopClickRect.Y      = (int)MathHelper.Lerp(troop.FromRect.Y, pgs.TroopClickRect.Y, amount);
                    troopClickRect.Width  = (int)MathHelper.Lerp(troop.FromRect.Width, pgs.TroopClickRect.Width, amount);
                    troopClickRect.Height = (int)MathHelper.Lerp(troop.FromRect.Height, pgs.TroopClickRect.Height, amount);
                }
                troop.Draw(batch, troopClickRect);
                var moveRect = new Rectangle(troopClickRect.X + troopClickRect.Width + 2, troopClickRect.Y + 38, 12, 12);
                if (troop.AvailableMoveActions <= 0)
                {
                    int moveTimer = (int)troop.MoveTimer + 1;
                    HelperFunctions.DrawDropShadowText1(batch, moveTimer.ToString(), new Vector2((moveRect.X + 4), moveRect.Y), Fonts.Arial12, Color.White);
                }
                else
                {
                    batch.Draw(ResourceManager.Texture("Ground_UI/Ground_Move"), moveRect, Color.White);
                }
                var attackRect = new Rectangle(troopClickRect.X + troopClickRect.Width + 2, troopClickRect.Y + 23, 12, 12);
                if (troop.AvailableAttackActions <= 0)
                {
                    int attackTimer = (int)troop.AttackTimer + 1;
                    HelperFunctions.DrawDropShadowText1(batch, attackTimer.ToString(), new Vector2((attackRect.X + 4), attackRect.Y), Fonts.Arial12, Color.White);
                }
                else
                {
                    batch.Draw(ResourceManager.Texture("Ground_UI/Ground_Attack"), attackRect, Color.White);
                }

                var strengthRect = new Rectangle(troopClickRect.X + troopClickRect.Width + 2, troopClickRect.Y + 5,
                                                 Fonts.Arial12.LineSpacing + 8, Fonts.Arial12.LineSpacing + 4);
                DrawTroopData(batch, strengthRect, troop, troop.Strength.String(1), Color.White);

                //Fat Bastard - show TroopLevel
                if (pgs.SingleTroop.Level > 0)
                {
                    var levelRect = new Rectangle(troopClickRect.X + troopClickRect.Width + 2, troopClickRect.Y + 52,
                                                  Fonts.Arial12.LineSpacing + 8, Fonts.Arial12.LineSpacing + 4);
                    DrawTroopData(batch, levelRect, troop, troop.Level.ToString(), Color.Gold);
                }

                if (ActiveTile != null && ActiveTile == pgs)
                {
                    if (ActiveTile.SingleTroop.AvailableAttackActions > 0)
                    {
                        foreach (PlanetGridSquare nearby in p.TilesList)
                        {
                            if (nearby == pgs || !nearby.CanAttack)
                                continue;

                            batch.Draw(ResourceManager.Texture("Ground_UI/GC_Potential_Attack"), nearby.ClickRect, Color.White);
                        }
                    }

                    if (ActiveTile.SingleTroop.CanMove)
                    {
                        foreach (PlanetGridSquare nearby in p.TilesList)
                        {
                            if (nearby == pgs || !nearby.CanMoveTo)
                            {
                                continue;
                            }
                            batch.FillRectangle(nearby.ClickRect, new Color(255, 255, 255, 30));
                            Vector2 center = nearby.ClickRect.Center();
                            DrawCircle(center, 5f, Color.White, 5f);
                            DrawCircle(center, 5f, Color.Black);
                        }
                    }
                }
            }
            else if (pgs.BuildingOnTile)
            {
                if (pgs.building.CombatStrength <= 0)
                {
                    var bRect = new Rectangle(pgs.ClickRect.X + pgs.ClickRect.Width / 2 - 32, pgs.ClickRect.Y + pgs.ClickRect.Height / 2 - 32, 64, 64);
                    var strengthRect = new Rectangle(bRect.X + bRect.Width + 2, bRect.Y + 5, Fonts.Arial12.LineSpacing + 8, Fonts.Arial12.LineSpacing + 4);
                    batch.FillRectangle(strengthRect, new Color(0, 0, 0, 200));
                    batch.DrawRectangle(strengthRect, p.Owner?.EmpireColor ?? Color.Gray);
                    var cursor = new Vector2((strengthRect.X + strengthRect.Width / 2) - Fonts.Arial12.MeasureString(pgs.building.Strength.ToString()).X / 2f,
                                             (1 + strengthRect.Y + strengthRect.Height / 2 - Fonts.Arial12.LineSpacing / 2));
                    batch.DrawString(Fonts.Arial12, pgs.building.Strength.ToString(), cursor, Color.White);
                }
                else
                {
                    var attackRect = new Rectangle(pgs.TroopClickRect.X + pgs.TroopClickRect.Width + 2, pgs.TroopClickRect.Y + 23, 12, 12);
                    if (pgs.building.AvailableAttackActions <= 0)
                    {
                        int num = (int)pgs.building.AttackTimer + 1;
                        batch.DrawString(Fonts.Arial12, num.ToString(), new Vector2((attackRect.X + 4), attackRect.Y), Color.White);
                    }
                    else
                    {
                        batch.Draw(ResourceManager.Texture("Ground_UI/Ground_Attack"), attackRect, Color.White);
                    }
                    var strengthRect = new Rectangle(pgs.TroopClickRect.X + pgs.TroopClickRect.Width + 2, pgs.TroopClickRect.Y + 5, Fonts.Arial12.LineSpacing + 8, Fonts.Arial12.LineSpacing + 4);
                    batch.FillRectangle(strengthRect, new Color(0, 0, 0, 200));
                    batch.DrawRectangle(strengthRect, p.Owner?.EmpireColor ?? Color.LightGray);
                    var cursor = new Vector2((strengthRect.X + strengthRect.Width / 2) - Fonts.Arial12.MeasureString(pgs.building.CombatStrength.ToString()).X / 2f,
                                             (1 + strengthRect.Y + strengthRect.Height / 2 - Fonts.Arial12.LineSpacing / 2));
                    batch.DrawString(Fonts.Arial12, pgs.building.CombatStrength.ToString(), cursor, Color.White);
                }

                if (ActiveTile != null && ActiveTile == pgs && ActiveTile.building.AvailableAttackActions > 0)
                {
                    foreach (PlanetGridSquare nearby in p.TilesList)
                    {
                        if (nearby == pgs || !nearby.CanAttack)
                        {
                            continue;
                        }
                        batch.Draw(ResourceManager.Texture("Ground_UI/GC_Potential_Attack"), nearby.ClickRect, Color.White);
                    }
                }
            }
        }

        private void DrawTroopData(SpriteBatch batch, Rectangle rect, Troop troop, string data, Color color)
        {
            SpriteFont font = Fonts.Arial12;
            batch.FillRectangle(rect, new Color(0, 0, 0, 200));
            batch.DrawRectangle(rect, troop.Loyalty.EmpireColor);
            var cursor = new Vector2((rect.X + rect.Width / 2) - font.MeasureString(troop.Strength.String(1)).X / 2f,
                (1 + rect.Y + rect.Height / 2 - font.LineSpacing / 2));
            batch.DrawString(font, data, cursor, color);
        }

        private void OnLandAllClicked(UIButton b)
        {
            GameAudio.TroopLand();
            foreach (ScrollList.Entry e in OrbitSL.AllEntries)
            {
                if (e.item is Troop troop)
                    troop.TryLandTroop(p);
            }

            OrbitSL.Reset();
        }

        private void OnLaunchAllClicked(UIButton b)
        {
            bool play = false;
            foreach (PlanetGridSquare pgs in p.TilesList)
            {
                if (pgs.NoTroopsOnTile || pgs.SingleTroop.Loyalty != Empire.Universe.player || !pgs.SingleTroop.CanMove)
                    continue;

                try
                {
                    pgs.SingleTroop.UpdateAttackActions(-pgs.SingleTroop.MaxStoredActions);
                    pgs.SingleTroop.UpdateMoveActions(-pgs.SingleTroop.MaxStoredActions);
                    pgs.SingleTroop.ResetLaunchTimer();
                    pgs.SingleTroop.ResetAttackTimer();
                    pgs.SingleTroop.ResetMoveTimer();
                    play = true;
                    pgs.SingleTroop.Launch(pgs);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Troop Launch Crash");
                }
            }

            if (!play)
                GameAudio.NegativeClick();
            else
            {
                GameAudio.TroopTakeOff();
                ResetNextFrame = true;
            }
        }

        public override bool HandleInput(InputState input)
        {
            bool selectedSomethingThisFrame = false;
            assetsUI.HandleInput(input);
            if (Empire.Universe?.Debug == true && input.SpawnRemnant)
            {
                if (EmpireManager.Remnants == null)
                    Log.Warning("Remnant faction missing!");
                else
                {
                    Troop troop = ResourceManager.CreateTroop("Wyvern", EmpireManager.Remnants);
                    if (!troop.TryLandTroop(p))
                        return false; // eek-eek
                }
            }

            if (ActiveTile != null && tInfo.HandleInput(input))
                selectedSomethingThisFrame = true;

            selector      = null;
            HoveredSquare = null;
            foreach (PlanetGridSquare pgs in p.TilesList)
            {
                if (!pgs.ClickRect.HitTest(input.CursorPosition) || pgs.TroopsHere.Count == 0 && pgs.building == null)
                    continue;

                HoveredSquare = pgs;
            }

            UpdateLaunchAllButton(p.TroopsHere.Count(t => t.Loyalty == Empire.Universe.player && t.CanMove));
            OrbitSL.HandleInput(input);
            foreach (ScrollList.Entry e in OrbitSL.AllExpandedEntries)
            {
                if (!e.CheckHover(Input.CursorPosition))
                    continue;

                selector = e.CreateSelector();
                if (input.LeftMouseClick)
                    draggedTroop = e;
            }

            if (draggedTroop != null && input.LeftMouseClick)
            {
                bool foundPlace = false;
                foreach (PlanetGridSquare pgs in p.TilesList)
                {
                    if (!pgs.ClickRect.HitTest(Input.CursorPosition))
                        continue;

                    if (!(draggedTroop.item is Ship) || (draggedTroop.item as Ship).TroopList.Count <= 0)
                    {
                        if (!(draggedTroop.item is Troop) || (pgs.building != null || pgs.TroopsHere.Count != 0) && (pgs.building == null || pgs.building.CombatStrength != 0 || pgs.TroopsHere.Count != 0))
                            continue;

                        GameAudio.TroopLand();
                        pgs.TroopsHere.Add(draggedTroop.item as Troop);
                        pgs.SingleTroop.UpdateAttackActions(-pgs.SingleTroop.MaxStoredActions);
                        pgs.SingleTroop.UpdateMoveActions(-pgs.SingleTroop.MaxStoredActions);
                        pgs.SingleTroop.ResetLaunchTimer();
                        pgs.SingleTroop.ResetAttackTimer();
                        pgs.SingleTroop.ResetMoveTimer();

                        p.TroopsHere.Add(draggedTroop.item as Troop);
                        (draggedTroop.item as Troop).SetPlanet(p);
                        OrbitSL.Remove(draggedTroop);
                        (draggedTroop.item as Troop).HostShip.TroopList.Remove(draggedTroop.item as Troop);
                        foundPlace = true;
                        draggedTroop = null;
                    }
                    else
                    {
                        if ((pgs.BuildingOnTile || pgs.TroopsAreOnTile) && (pgs.NoBuildingOnTile || pgs.CombatBuildingOnTile || pgs.TroopsAreOnTile))
                            continue;

                        GameAudio.TroopLand();
                        pgs.TroopsHere.Add((draggedTroop.item as Ship).TroopList[0]);
                        pgs.SingleTroop.UpdateAttackActions(-pgs.SingleTroop.MaxStoredActions);
                        pgs.SingleTroop.UpdateMoveActions(-pgs.SingleTroop.MaxStoredActions);
                        pgs.SingleTroop.ResetLaunchTimer();
                        pgs.SingleTroop.ResetAttackTimer();
                        pgs.SingleTroop.ResetMoveTimer();
                        p.TroopsHere.Add((draggedTroop.item as Ship).TroopList[0]);
                        (draggedTroop.item as Ship).TroopList[0].SetPlanet(p);
                        pgs.CheckAndTriggerEvent(p, pgs.SingleTroop.Loyalty);
                        OrbitSL.Remove(draggedTroop);
                        (draggedTroop.item as Ship).QueueTotalRemoval();
                        foundPlace = true;
                        draggedTroop = null;
                    }
                }

                if (!foundPlace)
                {
                    draggedTroop = null;
                    GameAudio.NegativeClick();
                }
            }

            foreach (PlanetGridSquare pgs in p.TilesList)
            {
                if (!pgs.ClickRect.HitTest(Input.CursorPosition))
                    pgs.Highlighted = false;
                else
                {
                    if (!pgs.Highlighted)
                        GameAudio.ButtonMouseOver();

                    pgs.Highlighted = true;
                }

                if (pgs.CanAttack)
                {
                    if (!pgs.CanAttack || ActiveTile == null)
                        continue;

                    if (!pgs.TroopClickRect.HitTest(Input.CursorPosition))
                        pgs.ShowAttackHover = false;
                    else if (ActiveTile.TroopsHere.Count <= 0)
                    {
                        if (ActiveTile.NoBuildingOnTile || ActiveTile.building.CombatStrength <= 0 || !ActiveTile.building.CanAttack || p.Owner == null || p.Owner != EmpireManager.Player)
                            continue;

                        if (Input.LeftMouseClick)
                        {
                            ActiveTile.building.UpdateAttackActions(-1);
                            ActiveTile.building.ResetAttackTimer();
                            StartCombat(ActiveTile, pgs);
                        }

                        pgs.ShowAttackHover = true;
                    }
                    else
                    {
                        if (!ActiveTile.SingleTroop.CanAttack || ActiveTile.SingleTroop.Loyalty != EmpireManager.Player)
                            continue;

                        if (Input.LeftMouseClick)
                        {
                            if      (pgs.x > ActiveTile.x) ActiveTile.SingleTroop.facingRight = true;
                            else if (pgs.x < ActiveTile.x) ActiveTile.SingleTroop.facingRight = false;

                            Troop item = ActiveTile.SingleTroop;
                            item.UpdateAttackActions(-1);
                            ActiveTile.SingleTroop.ResetAttackTimer();
                            Troop availableMoveActions = ActiveTile.SingleTroop;
                            availableMoveActions.UpdateMoveActions(-1);
                            ActiveTile.SingleTroop.ResetMoveTimer();
                            StartCombat(ActiveTile, pgs);
                        }

                        pgs.ShowAttackHover = true;
                    }
                }
                else
                {
                    if (pgs.TroopsAreOnTile)
                    {
                        if (pgs.TroopClickRect.HitTest(Input.CursorPosition) && Input.LeftMouseClick)
                        {
                            if (pgs.SingleTroop.Loyalty != EmpireManager.Player)
                            {
                                ActiveTile = pgs;
                                tInfo.SetPGS(pgs);
                                selectedSomethingThisFrame = true;
                            }
                            else
                            {
                                foreach (PlanetGridSquare p1 in p.TilesList)
                                {
                                    p1.CanAttack = false;
                                    p1.CanMoveTo = false;
                                    p1.ShowAttackHover = false;
                                }

                                ActiveTile = pgs;
                                tInfo.SetPGS(pgs);
                                selectedSomethingThisFrame = true;
                            }
                        }
                    }
                    else if (pgs.building != null && !pgs.CanMoveTo && pgs.TroopClickRect.HitTest(Input.CursorPosition) && Input.LeftMouseClick)
                    {
                        if (p.Owner != EmpireManager.Player)
                        {
                            ActiveTile = pgs;
                            tInfo.SetPGS(pgs);
                            selectedSomethingThisFrame = true;
                        }
                        else
                        {
                            foreach (PlanetGridSquare p1 in p.TilesList)
                            {
                                p1.CanAttack = false;
                                p1.CanMoveTo = false;
                                p1.ShowAttackHover = false;
                            }
                            ActiveTile = pgs;
                            tInfo.SetPGS(pgs);
                            selectedSomethingThisFrame = true;
                        }
                    }
                    if (ActiveTile == null || !pgs.CanMoveTo || ActiveTile.NoTroopsOnTile || !pgs.ClickRect.HitTest(Input.CursorPosition) || ActiveTile.SingleTroop.Loyalty != EmpireManager.Player || Input.LeftMouseReleased || !ActiveTile.SingleTroop.CanMove)
                        continue;

                    if (Input.LeftMouseClick)
                    {
                        if (pgs.x > ActiveTile.x)
                            ActiveTile.SingleTroop.facingRight = true;
                        else if (pgs.x < ActiveTile.x)
                            ActiveTile.SingleTroop.facingRight = false;

                        pgs.TroopsHere.Add(ActiveTile.SingleTroop);
                        Troop troop = pgs.SingleTroop;
                        troop.UpdateMoveActions(-1);
                        pgs.SingleTroop.ResetMoveTimer();
                        pgs.SingleTroop.MovingTimer = 0.75f;
                        pgs.SingleTroop.SetFromRect(ActiveTile.TroopClickRect);
                        GameAudio.PlaySfxAsync(pgs.SingleTroop.MovementCue);
                        ActiveTile.TroopsHere.Clear();
                        ActiveTile = null;
                        ActiveTile = pgs;
                        pgs.CanMoveTo = false;
                        selectedSomethingThisFrame = true;
                    }
                }
            }
            if (ActiveTile != null && !selectedSomethingThisFrame && Input.LeftMouseClick && !SelectedItemRect.HitTest(input.CursorPosition))
                ActiveTile = null;

            if (ActiveTile != null)
                tInfo.pgs = ActiveTile;

            DetermineAttackAndMove();
            hInfo.SetPGS(HoveredSquare);

            if (popup)
            {
                if (input.MouseCurr.RightButton != ButtonState.Released || input.MousePrev.RightButton != ButtonState.Released)
                        return true;

                    popup = false;
            }
            else if (input.MouseCurr.RightButton != ButtonState.Released || input.MousePrev.RightButton != ButtonState.Released)
            {
                Empire.Universe.ShipsInCombat.Visible = true;
                Empire.Universe.PlanetsInCombat.Visible = true;
            }
            return base.HandleInput(input);
        }

        public void StartCombat(PlanetGridSquare attacker, PlanetGridSquare defender)
        {
            Combat c = new Combat
            {
                AttackTile = attacker
            };

            if (attacker.TroopsHere.Count <= 0)
            {
                GameAudio.PlaySfxAsync("sd_weapon_bigcannon_01");
                GameAudio.PlaySfxAsync("uzi_loop");
            }
            else
            {
                attacker.SingleTroop.DoAttack();
                GameAudio.PlaySfxAsync(attacker.SingleTroop.sound_attack);
            }

            c.DefenseTile = defender;
            p.ActiveCombats.Add(c);
        }

        public static void StartCombat(PlanetGridSquare attacker, PlanetGridSquare defender, Planet p)
        {
            Combat c = new Combat
            {
                AttackTile = attacker
            };

            if (attacker.TroopsAreOnTile)
                attacker.SingleTroop.DoAttack();

            c.DefenseTile = defender;
            p.ActiveCombats.Add(c);
        }

        public static bool TroopCanAttackSquare(PlanetGridSquare ourTile, PlanetGridSquare tileToAttack, Planet p)
        {
            if (ourTile == null)
                return false;

            if (ourTile.TroopsHere.Count != 0)
            {
                foreach (PlanetGridSquare planetGridSquare1 in p.TilesList)
                {
                    if (ourTile != planetGridSquare1) continue;
                    foreach (PlanetGridSquare planetGridSquare2 in p.TilesList)
                    {
                        if (planetGridSquare2 != ourTile && planetGridSquare2 == tileToAttack)
                        {
                            //Added by McShooterz: Prevent troops from firing on own buildings
                            if (planetGridSquare2.TroopsHere.Count == 0 &&
                                (planetGridSquare2.building == null ||
                                 (planetGridSquare2.building != null &&
                                  planetGridSquare2.building.CombatStrength == 0) ||
                                 p.Owner?.IsEmpireAttackable(ourTile.SingleTroop.Loyalty) == false
                                ))
                                return false;
                            int num1 = Math.Abs(planetGridSquare1.x - planetGridSquare2.x);
                            int num2 = Math.Abs(planetGridSquare1.y - planetGridSquare2.y);
                            if (planetGridSquare2.TroopsHere.Count > 0)
                            {
                                if (planetGridSquare1.TroopsHere.Count != 0 &&
                                    num1 <= planetGridSquare1.SingleTroop.Range &&
                                    (num2 <= planetGridSquare1.SingleTroop.Range &&
                                     planetGridSquare2.SingleTroop.Loyalty.IsEmpireAttackable(ourTile.SingleTroop.Loyalty)
                                    ))
                                    return true;
                            }
                            else if (planetGridSquare2.building != null &&
                                     planetGridSquare2.building.CombatStrength > 0 &&
                                     (num1 <= planetGridSquare1.SingleTroop.Range &&
                                      num2 <= planetGridSquare1.SingleTroop.Range))
                            {
                                if (p.Owner == null)
                                    return false;
                                if (p.Owner?.IsEmpireAttackable(ourTile.SingleTroop.Loyalty) == true)
                                    return true;
                            }
                        }
                    }
                }
            }
            else if (ourTile.building != null && ourTile.building.CombatStrength > 0)
            {
                foreach (PlanetGridSquare planetGridSquare1 in p.TilesList)
                {
                    if (ourTile == planetGridSquare1)
                    {
                        foreach (PlanetGridSquare planetGridSquare2 in p.TilesList)
                        {
                            if (planetGridSquare2 != ourTile && planetGridSquare2 == tileToAttack)
                            {
                                //Added by McShooterz: Prevent buildings from firing on buildings
                                if (planetGridSquare2.TroopsHere.Count == 0)
                                    return false;
                                int num1 = Math.Abs(planetGridSquare1.x - planetGridSquare2.x);
                                int num2 = Math.Abs(planetGridSquare1.y - planetGridSquare2.y);
                                if (planetGridSquare2.TroopsHere.Count > 0)
                                {
                                    if (num1 <= 1 && num2 <= 1 &&
                                        p.Owner?.IsEmpireAttackable(planetGridSquare2.SingleTroop.Loyalty) == true)
                                        return true;
                                }
                                else if (planetGridSquare2.building != null && planetGridSquare2.building.CombatStrength > 0 && (num1 <= 1 && num2 <= 1))
                                    return p.Owner != null;
                            }
                        }
                    }
                }
            }
            return false;
        }

        public override void Update(float elapsedTime)
        {
            if (ResetNextFrame)
            {
                OrbitalAssetsTimer = 2;
                ResetNextFrame     = false;
            }
            UpdateOrbitalAssets(elapsedTime);

            foreach (PlanetGridSquare pgs in p.TilesList)
            {
                if (pgs.NoTroopsOnTile)
                    continue;

                pgs.SingleTroop.Update(elapsedTime);
            }
            using (Explosions.AcquireWriteLock())
            {
                foreach (SmallExplosion exp in Explosions)
                {
                    if (exp.Update(elapsedTime))
                        Explosions.QueuePendingRemoval(exp);
                }
                Explosions.ApplyPendingRemovals();
            }
            p.ActiveCombats.ApplyPendingRemovals();
            base.Update(elapsedTime);
        }

        public void UpdateOrbitalAssets(float elapsedTime)
        {
            OrbitalAssetsTimer += elapsedTime;
            if (OrbitalAssetsTimer.LessOrEqual(2))
                return;

            OrbitalAssetsTimer = 0;
            OrbitSL.Reset();
            using (EmpireManager.Player.GetShips().AcquireReadLock())
                foreach (Ship ship in EmpireManager.Player.GetShips())
                {
                    if (ship == null)
                        continue;

                    if (Vector2.Distance(p.Center, ship.Center) >= p.ObjectRadius + ship.Radius + 1500f)
                        continue;

                    if (ship.shipData.Role != ShipData.RoleName.troop) 
                    {
                        if (ship.TroopList.Count <= 0 || (!ship.Carrier.HasTroopBays && !ship.Carrier.HasTransporters && !(p.HasSpacePort && p.Owner == ship.loyalty)))  // fbedard
                            continue; // if the ship has no troop bays and there is no other means of landing them (like a spaceport)
                         
                        int landingLimit = LandingLimit(ship);
                        for (int i = 0; i < ship.TroopList.Count && landingLimit > 0; i++)
                        {
                            if (ship.TroopList[i] != null && ship.TroopList[i].Loyalty == ship.loyalty)
                            {
                                OrbitSL.AddItem(ship.TroopList[i]);
                                landingLimit--;
                            }
                        }
                    }
                    else if (ship.AI.State != AI.AIState.Rebase && ship.AI.State != AI.AIState.AssaultPlanet)
                        OrbitSL.AddItem(ship.TroopList[0]); // this the default 1 troop ship
                }

            UpdateLandAllButton(OrbitSL.NumEntries);
        }

        private int LandingLimit(Ship ship)
        {
            int landingLimit;
            if (p.HasSpacePort && p.Owner == ship.loyalty)
                landingLimit = ship.TroopList.Count;  // fbedard: Allows to unload all troops if there is a shipyard
            else
            {
                landingLimit  = ship.Carrier.AllActiveTroopBays.Count(bay => bay.hangarTimer <= 0);
                landingLimit += ship.Carrier.AllTransporters.Where(module => module.TransporterTimer <= 1).Sum(m => m.TransporterTroopLanding);
            }
            return landingLimit;
        }

        private void UpdateLandAllButton(int numTroops)
        {
            string text;
            if (numTroops > 0)
            {
                LandAll.Enabled = true;
                text            = $"Land All ({Math.Min(OrbitSL.NumEntries, p.FreeTiles)})";
            }
            else
            {
                LandAll.Enabled = false;
                text            = "Land All";
            }

            LandAll.Text = text;
        }

        private void UpdateLaunchAllButton(int numTroopsCanLaunch)
        {
            string text;
            if (numTroopsCanLaunch > 0)
            {
                LaunchAll.Enabled = true;
                text              = $"Launch All ({numTroopsCanLaunch})";
            }
            else
            {
                LaunchAll.Enabled = false;
                text              = "Launch All";
            }
            LaunchAll.Text = text;
        }

        public void AddExplosion(Rectangle grid, int size)
        {
            var exp = new SmallExplosion(grid, size);
            using (Explosions.AcquireWriteLock())
                Explosions.Add(exp);
        }

        private struct PointSet
        {
            public Vector2 point;
            public int row;
            public int column;
        }

        // small explosion in planetary combat screen
        public class SmallExplosion
        {
            float Time;
            int Frame;
            const float Duration = 2.25f;
            readonly TextureAtlas Animation;
            readonly Rectangle Grid;

            public SmallExplosion(Rectangle grid, int size)
            {
                Grid = grid;
                string anim = size <= 3 ? "Textures/sd_explosion_12a_bb" : "Textures/sd_explosion_14a_bb";
                Animation = ResourceManager.RootContent.LoadTextureAtlas(anim);
            }

            public bool Update(float elapsedTime)
            {
                Time += elapsedTime;
                if (Time > Duration)
                    return true;

                int frame = (int)(Time / Duration * Animation.Count) ;
                Frame = frame.Clamped(0, Animation.Count-1);
                return false;
            }

            public void Draw(SpriteBatch batch)
            {
                batch.Draw(Animation[Frame], Grid, Color.White);
            }
        }

        protected override void Destroy()
        {
            Explosions?.Dispose(ref Explosions);
            base.Destroy();
        }
    }
}