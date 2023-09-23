using Microsoft.Xna.Framework.Graphics;
using SDUtils;
using Ship_Game.Audio;
using Ship_Game.GameScreens.MainMenu;
using Ship_Game.GameScreens.NewGame;
using Ship_Game.Universe;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;
using NAudio.SoundFont;
using Ship_Game.Utils;

namespace Ship_Game
{
    internal class LoadArenaScreen : GameScreen
    {
        private ArenaScreen ArenaScreen;

        Texture2D LoadingScreenTexture;
        string AdviceText;

        TaskResult ArenaLoadTask;

        public LoadArenaScreen()
            : base(null, toPause: null)
        {
            CanEscapeFromScreen = false;
            ArenaScreen = new ArenaScreen();
        }

        public override void LoadContent()
        {
            ScreenManager.ClearScene();
            LoadingScreenTexture = ResourceManager.LoadRandomLoadingScreen(ArenaScreen.Random, TransientContent);
            AdviceText = Fonts.Arial12Bold.ParseText(ResourceManager.LoadRandomAdvice(ArenaScreen.Random), 500f);

            ArenaLoadTask = Parallel.Run(() =>
            {
                ArenaScreen.LoadContent();
            });
            base.LoadContent();
        }

        protected override void Dispose(bool disposing)
        {
            if (IsDisposed)
                return;
            Mem.Dispose(ref LoadingScreenTexture);
            Mem.Dispose(ref ArenaLoadTask);
            base.Dispose(disposing);
        }
        public override bool HandleInput(InputState input)
        {
            if (ArenaLoadTask.IsComplete && input.InGameSelect)
            {
                ScreenManager.ExitAll(clear3DObjects: true);
                ScreenManager.AddScreenNoLoad(ArenaScreen);
                return true;
            }
            return false;
        }


        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            if (ArenaLoadTask?.IsComplete == false)
            {
                // heavily throttle main thread, so the worker thread can turbo
                Thread.Sleep(33);
                if (IsDisposed) // just in case we died
                    return;
            }

            if (!GameBase.Base.IsDeviceGood)
                return; // device is unavailable

            ScreenManager.ClearScreen(Color.Black);
            if (!batch.SafeBegin())
                return; // something failed bad

            var artRect = new Rectangle(ScreenWidth / 2 - 960, ScreenHeight / 2 - 540, 1920, 1080);
            batch.Draw(LoadingScreenTexture, artRect, Color.White);
            var meterBar = new Rectangle(ScreenWidth / 2 - 150, ScreenHeight - 25, 300, 25);

            var pb = new ProgressBar(meterBar)
            {
                Max = 100f,
                Progress = ArenaScreen.Progress.Percent * 100f
            };
            pb.Draw(batch);

            var cursor = new Vector2(ScreenCenter.X - 250f, meterBar.Y - Fonts.Arial12Bold.MeasureString(AdviceText).Y - 5f);
            batch.DrawString(Fonts.Arial12Bold, AdviceText, cursor, Color.White);

            if (ArenaLoadTask?.IsComplete == true)
            {
                cursor.Y -= Fonts.Pirulen16.LineSpacing;
                const string begin = "Click to Continue!";
                cursor.X = ScreenCenter.X - Fonts.Pirulen16.MeasureString(begin).X / 2f;
                batch.DrawString(Fonts.Pirulen16, begin, cursor, CurrentFlashColor);
            }
            batch.SafeEnd();
        }
    }
}
