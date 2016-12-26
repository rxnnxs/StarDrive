using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using SynapseGaming.LightingSystem.Lights;

namespace Ship_Game
{
	public abstract class GameScreen
	{
		public bool IsLoaded;
	    public bool AlwaysUpdate;
	    private bool OtherScreenHasFocus;
        protected readonly Array<UIButton> Buttons = new Array<UIButton>();
        protected Texture2D BtnDefault;
        protected Texture2D BtnHovered;
        protected Texture2D BtnPressed;

        public bool IsActive => !OtherScreenHasFocus
                                && ScreenState == ScreenState.TransitionOn 
                                || ScreenState == ScreenState.Active;

	    public bool IsExiting { get; protected set; }
	    public bool IsPopup   { get; protected set; }

	    public ScreenManager ScreenManager { get; internal set; }
	    public ScreenState   ScreenState   { get; protected set; }
	    public TimeSpan TransitionOffTime { get; protected set; } = TimeSpan.Zero;
	    public TimeSpan TransitionOnTime  { get; protected set; } = TimeSpan.Zero;
	    public float TransitionPosition   { get; protected set; } = 1f;

        public byte TransitionAlpha => (byte)(255f - TransitionPosition * 255f);

        protected GameScreen()
		{
        }

		public abstract void Draw(GameTime gameTime);

		public virtual void ExitScreen()
		{
			ScreenManager.exitScreenTimer =.024f;
            if (TransitionOffTime != TimeSpan.Zero)
			{
				IsExiting = true;
				return;
			}
			ScreenManager.RemoveScreen(this);
		}

		public virtual void HandleInput(InputState input)
		{
		}

		public virtual void LoadContent()
		{
            BtnDefault = ResourceManager.TextureDict["EmpireTopBar/empiretopbar_btn_168px"];
            BtnHovered = ResourceManager.TextureDict["EmpireTopBar/empiretopbar_btn_168px_hover"];
            BtnPressed = ResourceManager.TextureDict["EmpireTopBar/empiretopbar_btn_168px_pressed"];
        }

		public virtual void UnloadContent()
		{
		}

		public virtual void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
		{
			OtherScreenHasFocus = otherScreenHasFocus;
			if (!IsExiting)
			{
				if (coveredByOtherScreen)
				{
					ScreenState = UpdateTransition(gameTime, TransitionOffTime, 1)
                                ? ScreenState.TransitionOff : ScreenState.Hidden;
					return;
				}
			    ScreenState = UpdateTransition(gameTime, TransitionOnTime, -1)
			                ? ScreenState.TransitionOn : ScreenState.Active;
			}
			else
			{
				ScreenState = ScreenState.TransitionOff;
			    if (UpdateTransition(gameTime, TransitionOffTime, 1))
                    return;
			    ScreenManager.RemoveScreen(this);
			    IsExiting = false;
			}
		}

		private bool UpdateTransition(GameTime gameTime, TimeSpan time, int direction)
		{
		    float transitionDelta = (time != TimeSpan.Zero ? (float)(gameTime.ElapsedGameTime.TotalMilliseconds / time.TotalMilliseconds) : 1f);
			TransitionPosition += transitionDelta * direction;
			if (TransitionPosition > 0f && TransitionPosition < 1f)
				return true;

			TransitionPosition = MathHelper.Clamp(TransitionPosition, 0f, 1f);
			return false;
		}

        // Shared utility functions:
        protected UIButton Button(ref Vector2 pos, string launches, int localization)
        {
            return Button(ref pos, launches, Localizer.Token(localization));
        }

        protected UIButton Button(ref Vector2 pos, string launches, string text)
        {
            var button = new UIButton
            {
                NormalTexture  = BtnDefault,
                HoverTexture   = BtnHovered,
                PressedTexture = BtnPressed,
                Launches       = launches,
                Text           = text
            };
            Layout(ref pos, button);
            Buttons.Add(button);
            return button;
        }

        protected void Layout(ref Vector2 pos, UIButton button)
        {
            button.Rect = new Rectangle((int)pos.X, (int)pos.Y, BtnDefault.Width, BtnDefault.Height);
            pos.Y += BtnDefault.Height + 15;
        }
    }
}