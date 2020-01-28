using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ship_Game.GameScreens.DiplomacyScreen
{
    public sealed class ItemToOffer : ScrollListItem<ItemToOffer>
    {
        public string Words;
        public string SpecialInquiry = "";
        public string Response;
        public bool Selected;

        public ItemToOffer(string words, Vector2 cursor) : base (words)
        {
            Words = words;
            SpriteFont font = Fonts.Arial12Bold;
            int width = (int)font.MeasureString(words).X;
            Rect = new Rectangle((int)cursor.X, (int)cursor.Y, width, font.LineSpacing);
        }

        public ItemToOffer(string words, string response, Vector2 cursor) : this(words, cursor)
        {
            Response = response;
        }

        public ItemToOffer(LocalizedText header) : base (header.Text)
        {
            Words = header.Text;
        }

        public ItemToOffer(int localization, string response, Vector2 cursor)
            : this(Localizer.Token(localization), response, cursor)
        {
        }

        public override string ToString()
            => $"{TypeName} Response:\"{Response}\"  Words:\"{Words}\"  Inquiry:\"{SpecialInquiry}\"";

        public void ChangeSpecialInquiry(Array<string> items)
        {
            if (Selected)
                items.Add(SpecialInquiry);
            else
                items.Remove(SpecialInquiry);
        }

        public override void Draw(SpriteBatch batch)
        {
            Color textColor = Selected ? Color.Orange
                            : Hovered ? Color.White
                            : Color.White.Alpha(0.7f);

            if (Hovered)
                batch.FillRectangle(Rect, Color.Black.Alpha(0.5f));

            batch.DrawString(Fonts.Arial12Bold, Words, Pos, textColor);
        }

        public override bool HandleInput(InputState input)
        {
            bool captured = base.HandleInput(input);
            if (Hovered)
            {
                if (Response == "NAPact")
                {
                    ToolTip.CreateTooltip(129);
                }
                else if (Response == "OpenBorders")
                {
                    ToolTip.CreateTooltip(130);
                }
                else if (Response == "Peace Treaty")
                {
                    ToolTip.CreateTooltip(131);
                }
                else if (Response == "TradeTreaty")
                {
                    ToolTip.CreateTooltip(132);
                }
                else if (Response == "OfferAlliance")
                {
                    ToolTip.CreateTooltip(133);
                }
                if (input.LeftMouseClick)
                {
                    Selected = !Selected;
                }
            }
            return captured;
        }
    }
}