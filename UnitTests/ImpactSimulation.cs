﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Ship_Game;
using Keys = Microsoft.Xna.Framework.Input.Keys;

namespace UnitTests
{
    class SimObject
    {
        public string Name;
        public Vector2 Position;
        public Vector2 Velocity;
        public Color Color;
        public float Radius;

        public SimObject(string name, Vector2 p, Vector2 v, Color c, float r)
        {
            Name = name;
            Position = p;
            Velocity = v;
            Color = c;
            Radius = r;
        }

        public void Update(float deltaTime)
        {
            Position += Velocity * deltaTime;
        }

        public void Draw(SpriteBatch batch, Vector2 worldCenter, float scale)
        {
            Vector2 pos = worldCenter + Position*scale;
            batch.DrawCircle(pos, Radius*scale, Color, 1f);
            batch.DrawLine(pos, pos + Velocity*scale, Color, 1f);
            batch.DrawString(Fonts.Arial11Bold, Name, pos + new Vector2(5,5), Color);
        }
    }

    public class SimParameters
    {
        public float Step = 1.0f / 60.0f;
        public float DelayBetweenSteps = 0f;
        public float Duration = 60.0f;
        public float Scale = 1.0f;
        public Vector2 ProjectileVelocity;
        public bool EnablePauses = true;
    }

    public struct SimResult
    {
        public Vector2 Intersect;
        public float Time;

        public override string ToString()
        {
            return $"SimResult:  {Intersect}  Time:{Time.String(3)}s";
        }
    }

    internal class ImpactSimWindow : GameDummy
    {
        readonly AutoResetEvent Started;
        public readonly Vector2 ScreenCenter;
        public KeyboardState Keys;
        bool CachedVisibility;
        public bool Visible;

        ImpactSimWindow(AutoResetEvent started) : base(1024, 1024, true)
        {
            Started = started;
            CachedVisibility = Visible = true;
            Directory.SetCurrentDirectory("/Projects/BlackBox/StarDrive");
            GlobalStats.XRES = (int)ScreenSize.X; // Required for DrawLine...
            GlobalStats.YRES = (int)ScreenSize.Y;
            ScreenCenter = ScreenSize * 0.5f;
            IsFixedTimeStep = false;
        }
        
        protected override void BeginRun()
        {
            base.BeginRun();
            Fonts.LoadContent(Content);
            Started.Set();
        }

        protected override void Update(GameTime time)
        {
            if (CachedVisibility != Visible)
            {
                CachedVisibility = Visible;
                Form.Visible = Visible;
            }
            if (!Visible)
                return;

            Keys = Keyboard.GetState();
            base.Update(time);
        }

        protected override void Draw(GameTime time)
        {
            if (!Visible)
                return;

            GraphicsDevice.Clear(Color.Black);

            try
            {
                Batch.Begin();
                base.Draw(time);
            }
            finally
            {
                Batch.End();
            }
        }

        
        static ImpactSimWindow Instance;

        public static ImpactSimWindow GetOrStartInstance()
        {
            if (Instance != null)
                return Instance;

            var started = new AutoResetEvent(false);
            new Thread(() =>
            {
                try
                {
                    Instance = new ImpactSimWindow(started);
                    Instance.Run(); // this will only return once the window is closed
                }
                finally
                {
                    Instance = null; // clean up
                }
            }) { Name = "ImpactSimThread" }.Start();

            started.WaitOne(); // wait until Instance.BeginRun() is finished
            return Instance;
        }
    }

    internal enum SimState
    {
        Starting, Running, Exiting
    }

    class ImpactSimulation : IGameComponent, IDrawable, IUpdateable
    {
        Array<SimObject> Objects = new Array<SimObject>();
        SimObject Projectile, Target;

        SimState State = SimState.Starting;
        float StartCounter = 1f;
        float ExitCounter  = 1f;
        readonly AutoResetEvent Exit = new AutoResetEvent(false);
        SimResult Result;

        ImpactSimWindow Owner;
        SimParameters Sim;

        float Time;
        Vector2 Center;
        float PrevDistance;
        
        public bool Visible { get; } = true;
        public int DrawOrder { get; } = 0;
        public event EventHandler VisibleChanged;
        public event EventHandler DrawOrderChanged;

        public bool Enabled { get; } = true;
        public int UpdateOrder { get; } = 0;
        public event EventHandler EnabledChanged;
        public event EventHandler UpdateOrderChanged;

        public ImpactSimulation(TestImpactPredictor.Scenario s, SimParameters sim)
        {
            Owner = ImpactSimWindow.GetOrStartInstance();
            Sim   = sim;
            var us = new SimObject("Us", s.Us, s.UsVel, Color.Green, 32);
            Target = new SimObject("Target", s.Tgt, s.TgtVel, Color.Red, 32);
            Projectile = new SimObject("Projectile", s.Us, Sim.ProjectileVelocity, Color.Orange, 8);

            Objects.AddRange(new []{ us, Target, Projectile });
            PrevDistance = float.MaxValue;
        }
                
        public void Initialize()
        {
        }

        public SimResult RunAndWaitForResult()
        {
            Owner.Components.Add(this);
            Owner.Visible = true;

            Exit.WaitOne();

            Owner.Components.Remove(this);
            Owner.Visible = false;
            return Result;
        }

        public void Update(GameTime time)
        {
            switch (State)
            {
                case SimState.Starting: WaitingToStart(time); break;
                case SimState.Running:  UpdateSimulation();   break;
                case SimState.Exiting:  WaitingToExit(time);  break;
            }
        }

        void WaitingToExit(GameTime time)
        {
            if (!Sim.EnablePauses || Owner.Keys.IsKeyDown(Keys.Space))
                ExitCounter = 0f;

            ExitCounter -= (float) time.ElapsedRealTime.TotalSeconds;
            if (ExitCounter <= 0f)
                Exit.Set();
        }

        void WaitingToStart(GameTime time)
        {
            if (!Sim.EnablePauses || Owner.Keys.IsKeyDown(Keys.Space))
                State = SimState.Running;

            if (State == SimState.Running)
                return;

            StartCounter -= (float)time.ElapsedRealTime.TotalSeconds;
            if (StartCounter > 0f)
                return;

            State = SimState.Running;
        }

        void UpdateSimulation()
        {
            if (Sim.DelayBetweenSteps > 0f)
            {
                Thread.Sleep((int)(Sim.DelayBetweenSteps * 1000));
            }

            Time += Sim.Step;

            foreach (SimObject o in Objects)
                o.Update(Sim.Step);

            float distance = Projectile.Position.Distance(Target.Position);
            if (distance <= (Projectile.Radius + Target.Radius))
            {
                State = SimState.Exiting;

                // final simulation correction towards Target
                float speed = Projectile.Velocity.Length();
                float timeAdjust = distance / speed;
                timeAdjust *= 1.09f; // additional heuristic precision adjustment

                Result.Intersect = Projectile.Position + Projectile.Velocity * timeAdjust;
                Result.Time = Time + timeAdjust;
                return;
            }

            if (distance > PrevDistance)
                Projectile.Name = "Projectile MISS";
            PrevDistance = distance;

            if (Time >= Sim.Duration)
            {
                State = SimState.Exiting;
                return;
            }

            (Vector2 min, Vector2 max) = GetSimulationBounds();
            float width = min.Distance(max);
            Center = (min + max) / 2f;
            Sim.Scale = (Owner.ScreenSize.X - 200f) / (width * 2.0f);
            Sim.Scale = Sim.Scale.Clamped(0.01f, 2.0f);
        }

        public void Draw(GameTime time)
        {
            SpriteBatch batch = Owner.Batch;

            Vector2 center = -Center*Sim.Scale + Owner.ScreenCenter;
            foreach (SimObject o in Objects)
                o.Draw(batch, center, Sim.Scale);

            DrawText(5,  5, $"Simulation Time {Time.String(2)}s / {Sim.Duration.String(2)}s");
            DrawText(5, 25, $"  Scale      {Sim.Scale.String(2)}");
            for (int i = 0; i < Objects.Count; ++i)
            {
                SimObject o = Objects[i];
                DrawText(5, 45 + i*20, $"  {o.Name,-16}  {o.Velocity.Length().String(),-3}m/s  {o.Position}");
            }
            DrawText(5,105, $"  {Result}");

            if (State == SimState.Exiting && Result.Intersect.NotZero())
            {
                Vector2 pos = center + Result.Intersect*Sim.Scale;
                batch.DrawCircle(pos, 10f*Sim.Scale, Color.Yellow, 2);
            }
            if (State == SimState.Exiting)
            {
                DrawText(300,5, $"Exit in {ExitCounter.String(1)}s");
            }
            if (State == SimState.Starting)
            {
                DrawText(300,5, $"Start in {StartCounter.String(1)}s");
            }
        }

        void DrawText(float x, float y, string text)
        {
            Owner.Batch.DrawString(Fonts.Arial14Bold, text, new Vector2(x,y), Color.White);
        }
        
        (Vector2 Min, Vector2 Max) GetSimulationBounds()
        {
            Vector2 min = default, max = default;
            foreach (SimObject o in Objects)
            {
                Vector2 p = o.Position;
                if (p.X < min.X) min.X = p.X;
                if (p.Y < min.Y) min.Y = p.Y;

                if (p.X > max.X) max.X = p.X;
                if (p.Y > max.Y) max.Y = p.Y;
            }
            return (min, max);
        }
    }
}
