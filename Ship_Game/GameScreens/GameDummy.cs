﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Ship_Game.Data;
using SynapseGaming.LightingSystem.Core;

namespace Ship_Game
{
    // A simplified dummy game setup
    // for minimal testing
    public class GameDummy : GameBase
    {
        LightingSystemManager LightSysManager;
        SpriteBatch batch;
        public SpriteBatch Batch => batch ?? (batch = new SpriteBatch(GraphicsDevice));

        public GameDummy(int width = 800, int height = 600, bool show = false)
        {
            GraphicsSettings settings = GraphicsSettings.FromGlobalStats();
            settings.Width  = width;
            settings.Height = height;
            settings.Mode = WindowMode.Borderless;
            ApplyGraphics(settings);

            if (show) Show();
        }

        public void Show()
        {
            Form.Visible = true;
        }

        public void Hide()
        {
            Form.Visible = false;
        }

        public void Create()
        {
            var manager = Services.GetService(typeof(IGraphicsDeviceManager)) as IGraphicsDeviceManager;
            manager?.CreateDevice();
            LightSysManager = new LightingSystemManager(Services);
            base.Initialize();
        }
    }
}
