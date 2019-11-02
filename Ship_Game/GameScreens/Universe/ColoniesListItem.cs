using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Ship_Game.Audio;

namespace Ship_Game
{
    public sealed class ColoniesListItem : ScrollListItem<ColoniesListItem>
    {
        public Planet p;
        public Rectangle TotalEntrySize { get => Rect; set => Rect = value; }
        public Rectangle SysNameRect;
        public Rectangle PlanetNameRect;
        public Rectangle SliderRect;
        public Rectangle StorageRect;
        public Rectangle QueueRect;
        public Rectangle PopRect;
        public Rectangle FoodRect;
        public Rectangle ProdRect;
        public Rectangle ResRect;
        public Rectangle MoneyRect;

        ColonySliderGroup Sliders;

        ProgressBar FoodStorage;
        ProgressBar ProdStorage;
        Rectangle ApplyProductionRect;
        DropDownMenu foodDropDown;
        DropDownMenu prodDropDown;
        Rectangle foodStorageIcon;
        Rectangle prodStorageIcon;
        EmpireManagementScreen Screen;

        bool ApplyProdHover;

        public ColoniesListItem(Planet planet, int x, int y, int width1, int height, EmpireManagementScreen screen)
        {
            int sliderWidth = 375;
            Screen = screen;
            p = planet;
            Rect = new Rectangle(x, y, width1 - 60, height);
            SysNameRect = new Rectangle(x, y, (int)((Rect.Width - (sliderWidth + 150)) * 0.17f) - 30, height);
            PlanetNameRect = new Rectangle(x + SysNameRect.Width, y, (int)((Rect.Width - (sliderWidth + 150)) * 0.17f), height);
            PopRect     = new Rectangle(PlanetNameRect.Right,      y, 30, height);
            FoodRect    = new Rectangle(PlanetNameRect.Right + 30, y, 30, height);
            ProdRect    = new Rectangle(PlanetNameRect.Right + 60, y, 30, height);
            ResRect     = new Rectangle(PlanetNameRect.Right + 90, y, 30, height);
            MoneyRect   = new Rectangle(PlanetNameRect.Right + 120, y, 30, height);
            SliderRect  = new Rectangle(PlanetNameRect.Right + 150, y, sliderWidth, height);
            StorageRect = new Rectangle(PlanetNameRect.Right + SliderRect.Width + 150, y, (int)((Rect.Width - (sliderWidth + 120)) * 0.33f), height);
            QueueRect = new Rectangle(PlanetNameRect.Right + SliderRect.Width + StorageRect.Width + 150, y, (int)((Rect.Width - (sliderWidth + 150)) * 0.33f), height);
            int width = (int)(SliderRect.Width * 0.8f);
            width = width.RoundUpToMultipleOf(10);

            Sliders = Add(new ColonySliderGroup(SliderRect));
            Sliders.Create(SliderRect.X + 10, SliderRect.Y, width, (int)(0.25 * SliderRect.Height), drawIcons:false);
            Sliders.SetPlanet(planet);

            FoodStorage = new ProgressBar(new Rectangle(StorageRect.X + 50, SliderRect.Y + (int)(0.25 * SliderRect.Height), (int)(0.4f * StorageRect.Width), 18))
            {
                Max = p.Storage.Max,
                Progress = p.FoodHere,
                color = "green"
            };
            int ddwidth = (int)(0.2f * StorageRect.Width);
            if (GlobalStats.IsGermanOrPolish)
            {
                ddwidth = (int)Fonts.Arial12.MeasureString(Localizer.Token(330)).X + 22;
            }
            foodDropDown = new DropDownMenu(new Rectangle(StorageRect.X + 50 + (int)(0.4f * StorageRect.Width) + 20, FoodStorage.pBar.Y + FoodStorage.pBar.Height / 2 - 9, ddwidth, 18));
            foodDropDown.AddOption(Localizer.Token(329));
            foodDropDown.AddOption(Localizer.Token(330));
            foodDropDown.AddOption(Localizer.Token(331));
            foodDropDown.ActiveIndex = (int)p.FS;
            foodStorageIcon = new Rectangle(StorageRect.X + 20, FoodStorage.pBar.Y + FoodStorage.pBar.Height / 2 - ResourceManager.Texture("NewUI/icon_food").Height / 2, ResourceManager.Texture("NewUI/icon_food").Width, ResourceManager.Texture("NewUI/icon_food").Height);
            ProdStorage = new ProgressBar(new Rectangle(StorageRect.X + 50, FoodStorage.pBar.Y + FoodStorage.pBar.Height + 10, (int)(0.4f * StorageRect.Width), 18))
            {
                Max = p.Storage.Max,
                Progress = p.ProdHere
            };
            prodStorageIcon = new Rectangle(StorageRect.X + 20, ProdStorage.pBar.Y + ProdStorage.pBar.Height / 2 - ResourceManager.Texture("NewUI/icon_production").Height / 2, ResourceManager.Texture("NewUI/icon_production").Width, ResourceManager.Texture("NewUI/icon_production").Height);
            prodDropDown = new DropDownMenu(new Rectangle(StorageRect.X + 50 + (int)(0.4f * StorageRect.Width) + 20, ProdStorage.pBar.Y + FoodStorage.pBar.Height / 2 - 9, ddwidth, 18));
            prodDropDown.AddOption(Localizer.Token(329));
            prodDropDown.AddOption(Localizer.Token(330));
            prodDropDown.AddOption(Localizer.Token(331));
            prodDropDown.ActiveIndex = (int)p.PS;
            ApplyProductionRect = new Rectangle(QueueRect.X + QueueRect.Width - 50, QueueRect.Y + QueueRect.Height / 2 - ResourceManager.Texture("NewUI/icon_queue_rushconstruction").Height / 2, ResourceManager.Texture("NewUI/icon_queue_rushconstruction").Width, ResourceManager.Texture("NewUI/icon_queue_rushconstruction").Height);
        }

        public override void PerformLayout()
        {
            base.PerformLayout();
            int x = (int)X;
            int y = (int)Y;
            int sliderWidth = Screen.ScreenWidth <= 1366 ? 250 : 375;

            p.UpdateIncomes(false);
            TotalEntrySize = new Rectangle(x, y, TotalEntrySize.Width, TotalEntrySize.Height);
            SysNameRect    = new Rectangle(x, y, (int)((TotalEntrySize.Width - (sliderWidth + 150)) * 0.17f) - 30, TotalEntrySize.Height);
            PlanetNameRect = new Rectangle(x + SysNameRect.Width, y, (int)((TotalEntrySize.Width - (sliderWidth + 150)) * 0.17f), TotalEntrySize.Height);
            PopRect     = new Rectangle(PlanetNameRect.Right,      y, 30, TotalEntrySize.Height);
            FoodRect    = new Rectangle(PlanetNameRect.Right + 30, y, 30, TotalEntrySize.Height);
            ProdRect    = new Rectangle(PlanetNameRect.Right + 60, y, 30, TotalEntrySize.Height);
            ResRect     = new Rectangle(PlanetNameRect.Right + 90, y, 30, TotalEntrySize.Height);
            MoneyRect   = new Rectangle(PlanetNameRect.Right + 120, y, 30, TotalEntrySize.Height);
            SliderRect  = new Rectangle(PlanetNameRect.Right + 150, y, SliderRect.Width, TotalEntrySize.Height);
            StorageRect = new Rectangle(PlanetNameRect.Right + SliderRect.Width + 150, y, StorageRect.Width, TotalEntrySize.Height);
            QueueRect   = new Rectangle(PlanetNameRect.Right + SliderRect.Width + StorageRect.Width + 150, y, QueueRect.Width, TotalEntrySize.Height);
            Sliders.UpdatePos(SliderRect.X + 10, SliderRect.Y);

            FoodStorage = new ProgressBar(new Rectangle(StorageRect.X + 50, SliderRect.Y + (int)(0.25 * SliderRect.Height), (int)(0.4f * StorageRect.Width), 18))
            {
                Max = p.Storage.Max,
                Progress = p.FoodHere,
                color = "green"
            };

            int ddwidth = (int)(0.2f * StorageRect.Width);
            if (GlobalStats.IsGermanOrPolish)
            {
                ddwidth = (int)Fonts.Arial12.MeasureString(Localizer.Token(330)).X + 22;
            }

            foodDropDown = new DropDownMenu(new Rectangle(StorageRect.X + 50 + (int)(0.4f * StorageRect.Width) + 20, FoodStorage.pBar.Y + FoodStorage.pBar.Height / 2 - 9, ddwidth, 18));
            foodDropDown.AddOption(Localizer.Token(329));
            foodDropDown.AddOption(Localizer.Token(330));
            foodDropDown.AddOption(Localizer.Token(331));
            foodDropDown.ActiveIndex = (int)p.FS;
            foodStorageIcon = new Rectangle(StorageRect.X + 20, FoodStorage.pBar.Y + FoodStorage.pBar.Height / 2 - ResourceManager.Texture("NewUI/icon_food").Height / 2, ResourceManager.Texture("NewUI/icon_food").Width, ResourceManager.Texture("NewUI/icon_food").Height);
            ProdStorage = new ProgressBar(new Rectangle(StorageRect.X + 50, FoodStorage.pBar.Y + FoodStorage.pBar.Height + 10, (int)(0.4f * StorageRect.Width), 18))
            {
                Max = p.Storage.Max,
                Progress = p.ProdHere
            };
            prodStorageIcon = new Rectangle(StorageRect.X + 20, ProdStorage.pBar.Y + ProdStorage.pBar.Height / 2 - ResourceManager.Texture("NewUI/icon_production").Height / 2, ResourceManager.Texture("NewUI/icon_production").Width, ResourceManager.Texture("NewUI/icon_production").Height);
            prodDropDown = new DropDownMenu(new Rectangle(StorageRect.X + 50 + (int)(0.4f * StorageRect.Width) + 20, ProdStorage.pBar.Y + FoodStorage.pBar.Height / 2 - 9, ddwidth, 18));
            prodDropDown.AddOption(Localizer.Token(329));
            prodDropDown.AddOption(Localizer.Token(330));
            prodDropDown.AddOption(Localizer.Token(331));
            prodDropDown.ActiveIndex = (int)p.PS;
            ApplyProductionRect = new Rectangle(QueueRect.X + QueueRect.Width - 50, QueueRect.Y + QueueRect.Height / 2 - ResourceManager.Texture("NewUI/icon_queue_rushconstruction").Height / 2, ResourceManager.Texture("NewUI/icon_queue_rushconstruction").Width, ResourceManager.Texture("NewUI/icon_queue_rushconstruction").Height);
        }

        public override bool HandleInput(InputState input)
        {
            p.UpdateIncomes(false);

            ApplyProdHover = ApplyProductionRect.HitTest(input.CursorPosition);

            if (ApplyProductionRect.HitTest(input.CursorPosition))
                ToolTip.CreateTooltip(50);

            if (base.HandleInput(input))
                return true;

            if (input.LeftMouseClick)
            {
                if (ApplyProdHover && p.IsConstructing)
                {
                    float maxAmount = input.IsCtrlKeyDown ? 10000f : 10f;
                    if (p.Construction.RushProduction(0, maxAmount))
                        GameAudio.AcceptClick();
                    else
                        GameAudio.NegativeClick();
                }

                if (p.NonCybernetic && foodDropDown.r.HitTest(input.CursorPosition))
                {
                    GameAudio.AcceptClick();
                    foodDropDown.Toggle();
                    p.FS = (Planet.GoodState) ((int) p.FS + (int) Planet.GoodState.IMPORT);
                    if (p.FS > Planet.GoodState.EXPORT)
                        p.FS = Planet.GoodState.STORE;
                }

                if (prodDropDown.r.HitTest(input.CursorPosition))
                {
                    GameAudio.AcceptClick();
                    prodDropDown.Toggle();
                    p.PS = (Planet.GoodState) ((int) p.PS + (int) Planet.GoodState.IMPORT);
                    if (p.PS > Planet.GoodState.EXPORT)
                        p.PS = Planet.GoodState.STORE;
                }
                return true;
            }
            return false;
        }

        public override void Draw(SpriteBatch batch)
        {
            var TextColor2 = new Color(118, 102, 67, 50);
            var smallHighlight = new Color(118, 102, 67, 25);
            if (ItemIndex % 2 == 0)
            {
                batch.FillRectangle(TotalEntrySize, smallHighlight);
            }
            if (p == Screen.SelectedPlanet)
            {
                batch.FillRectangle(TotalEntrySize, TextColor2);
            }

            Color TextColor = Colors.Cream;
            if (Fonts.Pirulen16.MeasureString(p.ParentSystem.Name).X <= SysNameRect.Width)
            {
                Vector2 SysNameCursor = new Vector2(SysNameRect.X + SysNameRect.Width / 2 - Fonts.Pirulen16.MeasureString(p.ParentSystem.Name).X / 2f, SysNameRect.Y + SysNameRect.Height / 2 - Fonts.Pirulen16.LineSpacing / 2);
                batch.DrawString(Fonts.Pirulen16, p.ParentSystem.Name, SysNameCursor, TextColor);
            }
            else
            {
                Vector2 SysNameCursor = new Vector2(SysNameRect.X + SysNameRect.Width / 2 - Fonts.Pirulen12.MeasureString(p.ParentSystem.Name).X / 2f, SysNameRect.Y + SysNameRect.Height / 2 - Fonts.Pirulen12.LineSpacing / 2);
                batch.DrawString(Fonts.Pirulen12, p.ParentSystem.Name, SysNameCursor, TextColor);
            }
            Rectangle planetIconRect = new Rectangle(PlanetNameRect.X + 5, PlanetNameRect.Y + 25, PlanetNameRect.Height - 50, PlanetNameRect.Height - 50);
            batch.Draw(p.PlanetTexture, planetIconRect, Color.White);
            var cursor = new Vector2(PopRect.X + PopRect.Width - 5, PlanetNameRect.Y + PlanetNameRect.Height / 2 - Fonts.Arial12.LineSpacing / 2);
            float population = p.PopulationBillion;
            string popstring = population.String();
            cursor.X = cursor.X - Fonts.Arial12.MeasureString(popstring).X;
            HelperFunctions.ClampVectorToInt(ref cursor);
            batch.DrawString(Fonts.Arial12, popstring, cursor, Color.White);
            cursor = new Vector2(FoodRect.X + FoodRect.Width - 5, PlanetNameRect.Y + PlanetNameRect.Height / 2 - Fonts.Arial12.LineSpacing / 2);

            string fstring = p.Food.NetIncome.String();
            cursor.X -= Fonts.Arial12.MeasureString(fstring).X;
            HelperFunctions.ClampVectorToInt(ref cursor);
            batch.DrawString(Fonts.Arial12, fstring, cursor, (p.Food.NetIncome >= 0f ? Color.White : Color.LightPink));
            
            cursor = new Vector2(ProdRect.X + FoodRect.Width - 5, PlanetNameRect.Y + PlanetNameRect.Height / 2 - Fonts.Arial12.LineSpacing / 2);
            string pstring = p.Prod.NetIncome.String();
            cursor.X -= Fonts.Arial12.MeasureString(pstring).X;
            HelperFunctions.ClampVectorToInt(ref cursor);
            bool pink = p.Prod.NetIncome < 0f;
            batch.DrawString(Fonts.Arial12, pstring, cursor, (pink ? Color.LightPink : Color.White));
            
            cursor = new Vector2(ResRect.X + FoodRect.Width - 5, PlanetNameRect.Y + PlanetNameRect.Height / 2 - Fonts.Arial12.LineSpacing / 2);
            string rstring = p.Res.NetIncome.String();
            cursor.X = cursor.X - Fonts.Arial12.MeasureString(rstring).X;
            HelperFunctions.ClampVectorToInt(ref cursor);
            batch.DrawString(Fonts.Arial12, rstring, cursor, Color.White);
            
            cursor = new Vector2(MoneyRect.X + FoodRect.Width - 5, PlanetNameRect.Y + PlanetNameRect.Height / 2 - Fonts.Arial12.LineSpacing / 2);
            float money = p.Money.NetRevenue;
            string mstring = money.String();
            cursor.X = cursor.X - Fonts.Arial12.MeasureString(mstring).X;
            HelperFunctions.ClampVectorToInt(ref cursor);
            batch.DrawString(Fonts.Arial12, mstring, cursor, (money >= 0f ? Color.White : Color.LightPink));
            
            if (Fonts.Pirulen16.MeasureString(p.Name).X + planetIconRect.Width + 10f <= PlanetNameRect.Width)
            {
                var a = new Vector2(planetIconRect.X + planetIconRect.Width + 10, SysNameRect.Y + SysNameRect.Height / 2 - Fonts.Pirulen16.LineSpacing / 2);
                batch.DrawString(Fonts.Pirulen16, p.Name, a, TextColor);
            }
            else if (Fonts.Pirulen12.MeasureString(p.Name).X + planetIconRect.Width + 10f <= PlanetNameRect.Width)
            {
                var b = new Vector2(planetIconRect.X + planetIconRect.Width + 10, SysNameRect.Y + SysNameRect.Height / 2 - Fonts.Pirulen12.LineSpacing / 2);
                batch.DrawString(Fonts.Pirulen12, p.Name, b, TextColor);
            }
            else
            {
                var c = new Vector2(planetIconRect.X + planetIconRect.Width + 10, SysNameRect.Y + SysNameRect.Height / 2 - Fonts.Arial8Bold.LineSpacing / 2);
                batch.DrawString(Fonts.Arial8Bold, p.Name, c, TextColor);
            }

            base.Draw(batch);

            if (p.Owner.data.Traits.Cybernetic != 0)
            {
                FoodStorage.DrawGrayed(batch);
                foodDropDown.DrawGrayed(batch);
            }
            else
            {
                FoodStorage.Draw(batch);
                foodDropDown.Draw(batch);
            }

            ProdStorage.Draw(batch);
            prodDropDown.Draw(batch);
            batch.Draw(ResourceManager.Texture("NewUI/icon_food"), foodStorageIcon, (p.Owner.NonCybernetic ? Color.White : new Color(110, 110, 110, 255)));
            batch.Draw(ResourceManager.Texture("NewUI/icon_production"), prodStorageIcon, Color.White);

            if (foodStorageIcon.HitTest(Screen.Input.CursorPosition))
            {
                ToolTip.CreateTooltip(p.Owner.IsCybernetic ? 77 : 73);
            }

            if (prodStorageIcon.HitTest(Screen.Input.CursorPosition))
            {
                ToolTip.CreateTooltip(74);
            }

            if (p.ConstructionQueue.Count > 0)
            {
                QueueItem qi = p.ConstructionQueue[0];
                qi.DrawAt(batch, new Vector2(QueueRect.X + 10, QueueRect.Y + QueueRect.Height / 2 - 15));

                batch.Draw((ApplyProdHover ? ResourceManager.Texture("NewUI/icon_queue_rushconstruction_hover1") : ResourceManager.Texture("NewUI/icon_queue_rushconstruction")), ApplyProductionRect, Color.White);
            }
            
            batch.DrawRectangle(TotalEntrySize, TextColor2);
        }
    }
}