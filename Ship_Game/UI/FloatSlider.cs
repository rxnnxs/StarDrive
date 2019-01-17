using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ship_Game
{
    public enum SliderStyle
    {
        Decimal, // example: 42000
        Percent // example: 51%
    }

    public sealed class FloatSlider : UIElementV2
    {
        Rectangle SliderRect; // colored slider
        Rectangle KnobRect;   // knob area used to move the slider value
        public string Text;
        public string Tooltip;
        public ToolTip Tip;

        public Action<FloatSlider> OnChange;

        // Sets the tooltip string from Localization/GameText_EN.xml
        public int LocalizeTooltipId
        {
            set => Tooltip = Localizer.Token(value);
        }

        // Sets the Tip to a more complex ToolTip instance
        public int TooltipId
        {
            set => Tip = ResourceManager.GetToolTip(value);
        }

        bool Hover, Dragging;
        float Min, Max, Value;
        public SliderStyle Style = SliderStyle.Decimal;

        public float Range => Max-Min;
        public float AbsoluteValue
        {
            get => Min + RelativeValue * Range;
            set
            {
                RelativeValue = (value.Clamped(Min, Max) - Min) / Range;
                RequiresLayout = true;
                PerformLegacyLayout(Pos);
            }
        }
        public float RelativeValue
        {
            get => Value;
            set
            {
                Value = value.Clamped(0f, 1f);
                RequiresLayout = true;
                PerformLegacyLayout(Pos);
                OnChange?.Invoke(this);
            }
        }

        static readonly Color TextColor   = new Color(255, 239, 208);
        static readonly Color HoverColor  = new Color(164, 154, 133);
        static readonly Color NormalColor = new Color(72, 61, 38);

        static int ContentId;
        static SubTexture SliderKnob;
        static SubTexture SliderKnobHover;
        static SubTexture SliderMinute;
        static SubTexture SliderMinuteHover;
        static SubTexture SliderGradient;   // background gradient for the slider

        public FloatSlider(UIElementV2 parent, Rectangle r, string text, float min = 0f, float max = 10000f, float value = 5000f)
            : base(parent, r)
        {
            if (SliderKnob == null || ContentId != ResourceManager.ContentId)
            {
                ContentId = ResourceManager.ContentId;
                SliderKnob        = ResourceManager.Texture("NewUI/slider_crosshair");
                SliderKnobHover   = ResourceManager.Texture("NewUI/slider_crosshair_hover");
                SliderMinute      = ResourceManager.Texture("NewUI/slider_minute");
                SliderMinuteHover = ResourceManager.Texture("NewUI/slider_minute_hover");
                SliderGradient    = ResourceManager.Texture("NewUI/slider_grd_green");
            }

            Text  = text;
            Min   = min;
            Max   = max;
            Value = (value.Clamped(Min, Max) - Min) / Range;
            PerformLegacyLayout(Pos);
        }

        public FloatSlider(UIElementV2 parent, SliderStyle style, Rectangle r, string text, float min, float max, float value)
            : this(parent, r, text, min, max, value)
        {
            Style = style;
        }

        void PerformLegacyLayout(Vector2 pos)
        {
            SliderRect = new Rectangle((int)pos.X, (int)pos.Y + (int)Height/2 + 3, (int)Width - 20, 6);
            KnobRect = new Rectangle(SliderRect.X + (int)(SliderRect.Width * Value), 
                                     SliderRect.Y + SliderRect.Height / 2 - SliderKnob.Height / 2, 
                                     SliderKnob.Width, SliderKnob.Height);
        }

        public override void PerformLayout()
        {
            if (!Visible)
                return;

            base.PerformLayout();
            PerformLegacyLayout(Pos);
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

        public override void Draw(SpriteBatch batch)
        {
            if (!Visible)
                return;

            batch.DrawString(Fonts.Arial12Bold, Text, Pos, TextColor);

            var gradient = new Rectangle(SliderRect.X, SliderRect.Y, (int)(RelativeValue * SliderRect.Width), 6);
            batch.Draw(SliderGradient, gradient, Color.White);
            batch.DrawRectangle(SliderRect, Hover ? HoverColor : NormalColor);

            for (int i = 0; i < 11; i++)
            {
                var tickCursor = new Vector2(SliderRect.X + SliderRect.Width / 10 * i, SliderRect.Y + SliderRect.Height + 2);
                batch.Draw(Hover ? SliderMinuteHover : SliderMinute, tickCursor, Color.White);
            }

            Rectangle knobRect = KnobRect;
            knobRect.X -= knobRect.Width / 2;
            batch.Draw(Hover ? SliderKnobHover : SliderKnob, knobRect, Color.White);

            var textPos = new Vector2(SliderRect.X + SliderRect.Width + 8, SliderRect.Y + SliderRect.Height / 2 - Fonts.Arial12Bold.LineSpacing / 2);
            batch.DrawString(Fonts.Arial12Bold, StyledValue, textPos, new Color(255, 239, 208));

            if (Hover)
            {
                if (Tip != null)
                    ToolTip.CreateTooltip(Tip.TIP_ID);
                else if (Tooltip.NotEmpty())
                    ToolTip.CreateTooltip(Tooltip);
            }
        }
        public bool HandleInput(InputState input, ref float currentValue, float dynamicMaxValue)
        {
            Max = Math.Min(500000f, dynamicMaxValue);
           
            if (!Rect.HitTest(input.CursorPosition) || !input.LeftMouseHeld())
            {
                AbsoluteValue = currentValue;
                return false;
            }
            HandleInput(input);
            currentValue = AbsoluteValue;
            return true;

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