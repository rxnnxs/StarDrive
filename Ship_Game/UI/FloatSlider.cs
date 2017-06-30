using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ship_Game
{
    public enum SliderStyle
    {
        Decimal, // example: 42000
        Percent, // example: 51%
    }

    public sealed class FloatSlider : UIElementV2
    {
        private Rectangle SliderRect; // colored slider
        private Rectangle KnobRect;
        public string Text;
        public int ToolTipId;

        private bool Hover;
        private bool Dragging;
        private readonly float Min;
        private readonly float Max;

        private float Value = 0.5f;

        public SliderStyle Style = SliderStyle.Decimal;

        public float Range => Max-Min;

        public float AbsoluteValue
        {
            get => Min + RelativeValue * Range;
            set
            {
                RelativeValue = (value.Clamp(Min, Max) - Min) / Range;
                UpdateKnobRect();
            }
        }

        public float RelativeValue
        {
            get => Value;
            set
            {
                Value = value.Clamp(0f, 1f);
                UpdateKnobRect();
            }
        }


        private static readonly Color TextColor   = new Color(255, 239, 208);
        private static readonly Color HoverColor  = new Color(164, 154, 133);
        private static readonly Color NormalColor = new Color(72, 61, 38);

        private static Texture2D SliderKnob;
        private static Texture2D SliderKnobHover;
        private static Texture2D SliderMinute;
        private static Texture2D SliderMinuteHover;
        private static Texture2D SliderGradient;   // background gradient for the slider

        
        public FloatSlider(Rectangle r, int text) : this(r, Localizer.Token(text))
        {
        }

        public FloatSlider(Rectangle r, string text, float min = 0f, float max = 10000f, float value = 5000f) : base(r)
        {
            if (SliderKnob == null)
            {
                SliderKnob        = ResourceManager.Texture("NewUI/slider_crosshair");
                SliderKnobHover   = ResourceManager.Texture("NewUI/slider_crosshair_hover");
                SliderMinute      = ResourceManager.Texture("NewUI/slider_minute");
                SliderMinuteHover = ResourceManager.Texture("NewUI/slider_minute_hover");
                SliderGradient    = ResourceManager.Texture("NewUI/slider_grd_green");
            }

            Text = text;
            Min = min;
            Max = max;

            SliderRect = new Rectangle(r.X, r.Y + r.Height/2 + 3, r.Width - 20, 6);
            AbsoluteValue = value;
        }

        public FloatSlider(SliderStyle style, Rectangle r, string text, float min, float max, float value)
            : this(r, text, min, max, value)
        {
            Style = style;
        }

        private void UpdateKnobRect()
        {
            KnobRect = new Rectangle(SliderRect.X + (int)(SliderRect.Width * Value), 
                SliderRect.Y + SliderRect.Height / 2 - SliderKnob.Height / 2, 
                SliderKnob.Width, SliderKnob.Height);
        }

        public string StyledValue
        {
            get
            {
                if (Style == SliderStyle.Decimal)
                {
                    return ((int)AbsoluteValue).ToString();
                }
                return (RelativeValue * 100f).ToString("00") + "%";
            }
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            spriteBatch.DrawString(Fonts.Arial12Bold, Text, Pos, TextColor);

            var gradient = new Rectangle(SliderRect.X, SliderRect.Y, (int)(RelativeValue * SliderRect.Width), 6);
            spriteBatch.Draw(SliderGradient, gradient, gradient, Color.White);
            spriteBatch.DrawRectangle(SliderRect, Hover ? HoverColor : NormalColor);

            for (int i = 0; i < 11; i++)
            {
                var tickCursor = new Vector2(SliderRect.X + SliderRect.Width / 10 * i, SliderRect.Y + SliderRect.Height + 2);
                spriteBatch.Draw(Hover ? SliderMinuteHover : SliderMinute, tickCursor, Color.White);
            }

            Rectangle knobRect = KnobRect;
            knobRect.X -= knobRect.Width / 2;
            spriteBatch.Draw(Hover ? SliderKnobHover : SliderKnob, knobRect, Color.White);

            var textPos = new Vector2(SliderRect.X + SliderRect.Width + 8, SliderRect.Y + SliderRect.Height / 2 - Fonts.Arial12Bold.LineSpacing / 2);
            spriteBatch.DrawString(Fonts.Arial12Bold, StyledValue, textPos, new Color(255, 239, 208));

            if (Hover && ToolTipId != 0)
                ToolTip.CreateTooltip(Localizer.Token(ToolTipId));
        }

        public override bool HandleInput(InputState input)
        {
            Hover = Rect.HitTest(input.CursorPosition);

            Rectangle clickCursor = KnobRect;
            clickCursor.X -= KnobRect.Width / 2;

            if (clickCursor.HitTest(input.CursorPosition) && input.LeftMouseHeldDown)
                Dragging = true;

            if (Dragging)
            {
                KnobRect.X = (int)input.CursorPosition.X;
                if (KnobRect.X > SliderRect.X + SliderRect.Width) KnobRect.X = SliderRect.X + SliderRect.Width;
                else if (KnobRect.X < SliderRect.X)               KnobRect.X = SliderRect.X;

                if (input.LeftMouseReleased)
                    Dragging = false;

                RelativeValue = 1f - (SliderRect.X + SliderRect.Width - KnobRect.X) / (float)SliderRect.Width;
            }
            return Dragging;
        }

        public override string ToString() => $"Slider r:{Value} a:{AbsoluteValue} [{Min}..{Max}] {Text}";
    }
}