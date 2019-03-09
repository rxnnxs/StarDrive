﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ship_Game
{
    public static class SpriteExtensions
    {
        [Conditional("DEBUG")] static void CheckTextureDisposed(Texture2D texture)
        {
            if (texture.IsDisposed)
                throw new ObjectDisposedException($"Texture2D '{texture.Name}'");
        }
        [Conditional("DEBUG")] static void CheckSubTextureDisposed(SubTexture texture)
        {
            if (texture.Texture.IsDisposed)
                throw new ObjectDisposedException($"SubTexture '{texture.Name}' in Texture2D '{texture.Texture.Name}'");
        }

        public static void Draw(this SpriteBatch batch, SubTexture texture, 
                                Vector2 position, Color color)
        {
            CheckSubTextureDisposed(texture);
            batch.Draw(texture.Texture, position, texture.Rect, color);
        }

        public static void Draw(this SpriteBatch batch, SubTexture texture, 
                                in Rectangle destRect, Color color)
        {
            CheckSubTextureDisposed(texture);
            batch.Draw(texture.Texture, destRect, texture.Rect, color);
        }

        public static void Draw(
            this SpriteBatch batch, SubTexture texture, in Rectangle destRect,
            Color color, float rotation, Vector2 origin, SpriteEffects effects, float layerDepth)
        {
            CheckSubTextureDisposed(texture);
            batch.Draw(texture.Texture, destRect, texture.Rect,
                       color, rotation, origin, effects, layerDepth);
        }

        public static void Draw(
            this SpriteBatch batch, SubTexture texture, Vector2 position, Color color,
            float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)
        {
            CheckSubTextureDisposed(texture);
            batch.Draw(texture.Texture, position, texture.Rect, color, 
                       rotation, origin, scale, effects, layerDepth);
        }

        public static void Draw(this SpriteBatch batch, SubTexture texture, Vector2 position, Vector2 size)
        {
            CheckSubTextureDisposed(texture);
            var r = new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y);
            batch.Draw(texture.Texture, r, texture.Rect, Color.White);
        }

        public static void Draw(this SpriteBatch batch, Texture2D texture, Vector2 position, Vector2 size)
        {
            CheckTextureDisposed(texture);
            var r = new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y);
            batch.Draw(texture, r, Color.White);
        }

        public static void Draw(this SpriteBatch batch, SubTexture texture, in Rectangle rect, 
                                float rotation)
        {
            CheckSubTextureDisposed(texture);
            batch.Draw(texture.Texture, rect, texture.Rect, Color.White, 
                       rotation, texture.CenterF, SpriteEffects.None, 1f);
        }

        public static void Draw(this SpriteBatch batch, SubTexture texture, in Rectangle rect, 
                                float rotation, float scale, float z)
        {
            CheckSubTextureDisposed(texture);
            Rectangle r = rect.ScaledBy(scale);
            batch.Draw(texture.Texture, r, texture.Rect, Color.White, 
                       rotation, texture.CenterF, SpriteEffects.None, z);
        }

        static Rectangle AdjustedToSubTexture(SubTexture texture, Rectangle srcRect)
        {
            Rectangle subRect = texture.Rect;
            return new Rectangle(
                subRect.X + srcRect.X,
                subRect.Y + srcRect.Y,
                srcRect.Width,
                srcRect.Height
            );
        }

        public static void Draw(this SpriteBatch batch, SubTexture texture, Rectangle destRect,
                                Rectangle srcRect, Color color)
        {
            CheckSubTextureDisposed(texture);
            Rectangle adjustedSrcRect = AdjustedToSubTexture(texture, srcRect);
            batch.Draw(texture.Texture, destRect, adjustedSrcRect, color);
        }

        public static void Draw(
            this SpriteBatch batch, SubTexture texture, Rectangle destRect, Rectangle srcRect,
            Color color, float rotation, Vector2 origin, SpriteEffects effects, float layerDepth)
        {
            CheckSubTextureDisposed(texture);
            Rectangle adjustedSrcRect = AdjustedToSubTexture(texture, srcRect);
            batch.Draw(texture.Texture, destRect, adjustedSrcRect,
                       color, rotation, origin, effects, layerDepth);
        }

        public static void DrawString(
            this SpriteBatch batch, SpriteFont font, string text, float x, float y)
        {
            batch.DrawString(font, text, new Vector2(x, y), Color.White);
        }
    }
}
