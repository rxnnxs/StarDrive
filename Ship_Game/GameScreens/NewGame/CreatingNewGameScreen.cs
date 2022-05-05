using Microsoft.Xna.Framework.Graphics;
using Ship_Game.Gameplay;
using Ship_Game.Ships;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Ship_Game.Audio;
using Ship_Game.GameScreens.MainMenu;
using Ship_Game.GameScreens.NewGame;
using Ship_Game.Universe;
using Vector2 = SDGraphics.Vector2;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace Ship_Game
{
    public sealed class CreatingNewGameScreen : GameScreen
    {
        readonly MainMenuScreen MainMenu;
        Texture2D LoadingScreenTexture;
        string AdviceText;

        readonly UniverseGenerator Generator;
        TaskResult<UniverseScreen> BackgroundTask;

        public CreatingNewGameScreen(MainMenuScreen menu, UniverseGenerator.Params p)
            : base(null, toPause: null)
        {
            CanEscapeFromScreen = false;
            MainMenu = menu;
            Generator = new UniverseGenerator(p);
        }

        public override void LoadContent()
        {
            ScreenManager.ClearScene();
            LoadingScreenTexture = ResourceManager.LoadRandomLoadingScreen(TransientContent);
            AdviceText = Fonts.Arial12Bold.ParseText(ResourceManager.LoadRandomAdvice(), 500f);

            BackgroundTask = Generator.GenerateAsync();
            base.LoadContent();
        }

        public override bool HandleInput(InputState input)
        {
            if (BackgroundTask?.IsComplete != true || !input.InGameSelect)
                return false;

            UniverseScreen us = BackgroundTask.Result;
            GameAudio.StopGenericMusic(immediate: false);
            ScreenManager.AddScreenAndLoadContent(us);

            Log.Info("CreatingNewGameScreen.Objects.Update(0.01)");
            us.UState.Objects.Update(new FixedSimTime(0.01f));

            ScreenManager.Music.Stop();
            ScreenManager.RemoveScreen(MainMenu);

            ExitScreen();
            return true;
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            ScreenManager.GraphicsDevice.Clear(Color.Black);

            if (BackgroundTask?.IsComplete == false)
            {
                // heavily throttle main draw thread, so the worker thread can turbo
                Thread.Sleep(33);
                if (IsDisposed) // just in case we tried to ALT+F4 during loading
                    return;
            }

            batch.Begin();
            int width = ScreenWidth;
            int height = ScreenHeight;
            if (LoadingScreenTexture != null)
                batch.Draw(LoadingScreenTexture, new Rectangle(width / 2 - 960, height / 2 - 540, 1920, 1080), Color.White);

            var r = new Rectangle(width / 2 - 150, height - 25, 300, 25);
            new ProgressBar(r) { Max = 100f, Progress = Generator.Progress.Percent * 100f }.Draw(batch);

            var position = new Vector2(ScreenCenter.X - 250f, (float)(r.Y - Fonts.Arial12Bold.MeasureString(AdviceText).Y - 5.0));
            batch.DrawString(Fonts.Arial12Bold, AdviceText, position, Color.White);

            if (BackgroundTask?.IsComplete == true)
            {
                position.Y = (float)(position.Y - Fonts.Pirulen16.LineSpacing - 10.0);
                string token = Localizer.Token(GameText.ClickToContinue);
                position.X = ScreenCenter.X - Fonts.Pirulen16.MeasureString(token).X / 2f;

                batch.DrawString(Fonts.Pirulen16, token, position, CurrentFlashColor);
            }

            batch.End();
        }

        protected override void Destroy()
        {
            LoadingScreenTexture?.Dispose(ref LoadingScreenTexture);
            base.Destroy();
        }
    }
}
