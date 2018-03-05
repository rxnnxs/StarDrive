using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SynapseGaming.LightingSystem.Core;
using SynapseGaming.LightingSystem.Editor;
using SynapseGaming.LightingSystem.Rendering;
using System;
using SynapseGaming.LightingSystem.Lights;
using System.Collections.Generic;

namespace Ship_Game
{
    public sealed class ScreenManager : IDisposable
    {
        private readonly Array<GameScreen> Screens = new Array<GameScreen>();
        public InputState input = new InputState();
        private readonly IGraphicsDeviceService GraphicsDeviceService;
        private Texture2D BlankTexture;
        public LightingSystemManager LightSysManager;
        public LightingSystemEditor editor;
        private readonly SceneState GameSceneState;
        public SceneEnvironment environment;
        private SplashScreenGameComponent SplashScreen;
        private readonly SceneInterface SceneInter;
        private readonly object InterfaceLock = new object();
        private Game1 GameInstance;
        //public SceneInterface buffer1;
        //public SceneInterface buffer2;
        //public SceneInterface renderBuffer;
        public AudioHandle Music;
        public GraphicsDevice GraphicsDevice;
        public SpriteBatch SpriteBatch;

        

        public float exitScreenTimer
        {
            get => input.ExitScreenTimer;
            set => input.ExitScreenTimer = value;
        }


        //public float exitScreenTimer
        //{
            
        //} input.ExitScreenTimer;

        public Rectangle TitleSafeArea { get; private set; }
        public int NumScreens => Screens.Count;

        public ScreenManager(Game1 game, GraphicsDeviceManager graphics)
        {
            GameInstance = game;
            GraphicsDevice = graphics.GraphicsDevice;
            GraphicsDeviceService = (IGraphicsDeviceService)game.Services.GetService(typeof(IGraphicsDeviceService));
            if (GraphicsDeviceService == null)
            {
                throw new InvalidOperationException("No graphics device service.");
            }
            LightSysManager = new LightingSystemManager(game.Services);
            GameSceneState = new SceneState();
            SceneInter = new SceneInterface(graphics);
            SceneInter.CreateDefaultManagers(false, false, true);
            editor = new LightingSystemEditor(game.Services, graphics, game)
            {
                UserHandledView = true
            };
            SceneInter.AddManager(editor);

            SplashScreen = new SplashScreenGameComponent(game, graphics);
            game.Components.Add(SplashScreen);
        }

        public void UpdatePreferences(LightingSystemPreferences prefs)
        {
            SceneInter.ApplyPreferences(prefs);
        }

        public void UpdateViewports()
        {
            for (int i = 0; i < Screens.Count; ++i)
                Screens[i].UpdateViewport();
        }

        public void AddScreen(GameScreen screen)
        {
            foreach (GameScreen gs in Screens)
                if (gs is DiplomacyScreen)
                    return;
            if (GraphicsDeviceService?.GraphicsDevice != null)
                screen.LoadContent();
            Screens.Add(screen);
        }

        public void AddScreenNoLoad(GameScreen screen)
        {
            foreach (GameScreen gs in Screens)
                if (gs is DiplomacyScreen)
                    return;
            Screens.Add(screen);
        }

        
        public void HideSplashScreen()
        {
            if (SplashScreen == null)
                return;
            SplashScreen.Visible = false;
            GameInstance.Components.Remove(SplashScreen);
            SplashScreen = null;
        }

        ////////////////////////////////////////////////////////////////////////////////////

        public void AddObject(ISceneObject so)
        {
            if (so == null) return;
            lock (InterfaceLock)
                SceneInter.ObjectManager.Submit(so);
        }

        public void AddObjects(IEnumerable<ISceneObject> sceneObjects)
        {
            lock (InterfaceLock)
                foreach (ISceneObject so in sceneObjects)
                    SceneInter.ObjectManager.Submit(so);
        }

        public void RemoveObject(ISceneObject so)
        {
            if (so == null) return;
            lock (InterfaceLock)
                SceneInter.ObjectManager.Remove(so);
        }

        public void RemoveAllObjects()
        {
            lock (InterfaceLock)
                SceneInter.ObjectManager.Clear();
        }

        ////////////////////////////////////////////////////////////////////////////////////

        public void AddLight(ILight light)
        {
            if (light == null) return;
            lock (InterfaceLock)
                SceneInter.LightManager.Submit(light);
        }

        public void RemoveLight(ILight light)
        {
            if (light == null) return;
            lock (InterfaceLock)
                SceneInter.LightManager.Remove(light);
        }

        public void RefreshLight(ILight light)
        {
            if (light == null) return;
            lock (InterfaceLock)
            {
                SceneInter.LightManager.Remove(light);
                SceneInter.LightManager.Submit(light);
            }
        }

        public void RemoveAllLights()
        {
            lock (InterfaceLock)
                SceneInter.LightManager.Clear();
        }

        public void AssignLightRig(LightRig rig)
        {
            lock (InterfaceLock)
            {
                SceneInter.LightManager.Clear();
                SceneInter.LightManager.Submit(rig);
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////
         
        public void ClearScene()
        {
            RemoveAllObjects();
            RemoveAllLights();
        }

        public void UnloadSceneObjects()
        {
            SceneInter.Unload();
            LightSysManager.Unload();
        }

        ////////////////////////////////////////////////////////////////////////////////////

        public void UpdateSceneObjects(GameTime gameTime)
        {
            lock (InterfaceLock)
                SceneInter.Update(gameTime);
        }

        public void RenderSceneObjects()
        {
            lock (InterfaceLock)
                SceneInter.RenderManager.Render();
        }

        public void BeginFrameRendering(GameTime gameTime, ref Matrix view, ref Matrix projection)
        {
            lock (InterfaceLock)
            {
                GameSceneState.BeginFrameRendering(ref view, ref projection, gameTime, environment, true);
                editor.BeginFrameRendering(GameSceneState);
                SceneInter.BeginFrameRendering(GameSceneState);
            }
        }

        public void EndFrameRendering()
        {
            lock (InterfaceLock)
            {
                SceneInter.EndFrameRendering();
                editor.EndFrameRendering();
                GameSceneState.EndFrameRendering();
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////

        public void Draw(GameTime gameTime)
        {
            SpriteBatch spriteBatch = SpriteBatch;
            try
            {
                for (int i = 0; i < Screens.Count; ++i)
                {
                    GameScreen screen = Screens[i];
                    if (screen.ScreenState != ScreenState.Hidden)
                        screen.Draw(spriteBatch);
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "ScreenManager.Draw Crashed");
                try { spriteBatch.End(); } catch { }
            }
        }

        public void ExitAll()
        {
            foreach (GameScreen screen in Screens.ToArray())
                screen.ExitScreen();
        }

        public void ExitAllExcept(GameScreen except)
        {
            foreach (GameScreen screen in Screens.ToArray())
                if (screen != except)
                    screen.ExitScreen();
        }

        public void FadeBackBufferToBlack(int alpha, SpriteBatch spriteBatch)
        {
            Viewport viewport = Game1.Instance.Viewport;
            spriteBatch.Draw(BlankTexture, new Rectangle(0, 0, viewport.Width, viewport.Height), new Color(0, 0, 0, (byte)alpha));
        }

        public void FadeBackBufferToBlack(int alpha)
        {
            Viewport viewport = Game1.Instance.Viewport;
            SpriteBatch.Begin();
            SpriteBatch.Draw(BlankTexture, new Rectangle(0, 0, viewport.Width, viewport.Height), new Color(0, 0, 0, (byte)alpha));
            SpriteBatch.End();
        }

        public int ScreenCount => Screens.Count;

        public void LoadContent()
        {
            SpriteBatch = new SpriteBatch(GraphicsDevice);
            BlankTexture = ResourceManager.LoadTexture("blank");
            foreach (GameScreen screen in Screens)
            {
                screen.LoadContent();
            }

            Viewport viewport = Game1.Instance.Viewport;
            TitleSafeArea = new Rectangle(
                (int)(viewport.X + viewport.Width  * 0.05f),
                (int)(viewport.Y + viewport.Height * 0.05f),
                (int)(viewport.Width  * 0.9f),
                (int)(viewport.Height * 0.9f));
        }

        public void RemoveScreen(GameScreen screen)
        {
            if (GraphicsDeviceService?.GraphicsDevice != null)
                screen.UnloadContent();            
            Screens.Remove(screen);
            exitScreenTimer = .25f;
        }

        public void Update(GameTime gameTime)
        {
            input.Update(gameTime);

            bool otherScreenHasFocus = !Game1.Instance.IsActive;
            bool coveredByOtherScreen = false;

            for (int i = Screens.Count-1; i >= 0; --i)
            {
                GameScreen screen = Screens[i];
                screen.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);
                if (screen.ScreenState != ScreenState.TransitionOn && screen.ScreenState != ScreenState.Active)
                    continue;
                if (!otherScreenHasFocus)
                {
                    screen.HandleInput(input);
                    otherScreenHasFocus = true;
                }
                if (screen.IsPopup)
                    continue;
                coveredByOtherScreen = true;
            }
        }

        public bool UpdateExitTimeer(bool stopFurtherInput)
        {
            if (!stopFurtherInput)
            {
                //exitScreenTimer -= .0016f;
                if (exitScreenTimer > 0f)
                    return true;
            }
            //else exitScreenTimer = .025f;
            return false;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~ScreenManager() { Dispose(false); }

        private void Dispose(bool disposing)
        {
            SpriteBatch?.Dispose(ref SpriteBatch);
        }
    }
}