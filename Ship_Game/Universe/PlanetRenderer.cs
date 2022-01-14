﻿using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Ship_Game.Data;
using Ship_Game.Data.Mesh;

namespace Ship_Game.Universe.SolarBodies
{
    public class PlanetRenderer : IDisposable
    {
        PlanetTypes Types;
        GraphicsDevice Device;

        public Model MeshSphere;
        Model MeshRings;
        Model MeshGlowRing;
        Model MeshGlowFresnel;
        Model MeshAtmosphere;

        BasicEffect FxRings;
        BasicEffect FxClouds;
        BasicEffect FxAtmoColor;
        BasicEffect FxGlow;
        BasicEffect FxFresnel;
        Effect PlanetHaloFx;

        Texture2D TexRings;
        Texture2D TexAtmosphere;
        Texture2D TexGlow;
        Texture2D TexFresnel;

        Vector3 CamPos;

        public PlanetRenderer(GameContentManager content, PlanetTypes types)
        {
            Types = types;
            MeshSphere = content.LoadModel("Model/SpaceObjects/planet_sphere.obj");
            MeshRings = content.LoadModel("Model/SpaceObjects/planet_rings.obj");
            MeshGlowRing = content.LoadModel("Model/SpaceObjects/planet_glow_ring.obj");
            MeshGlowFresnel = content.LoadModel("Model/SpaceObjects/planet_glow_fresnel.obj");
            MeshAtmosphere = content.LoadModel("Model/SpaceObjects/atmo_sphere.obj");

            TexRings = content.RawContent.LoadTexture("Model/SpaceObjects/planet_rings.dds");
            TexAtmosphere = content.RawContent.LoadTexture("Model/SpaceObjects/AtmosphereColor.dds");

            TexGlow = content.RawContent.LoadAlphaTexture("Model/SpaceObjects/planet_glow.png", preMultiplied:false);
            TexFresnel = content.RawContent.LoadAlphaTexture("Model/SpaceObjects/planet_fresnel.png", preMultiplied:false);

            FxRings = new BasicEffect(content.Device, null);
            FxRings.TextureEnabled = true;
            FxRings.DiffuseColor = new Vector3(1f, 1f, 1f);

            FxClouds = new BasicEffect(content.Device, null);
            FxClouds.TextureEnabled = true;
            FxClouds.DiffuseColor = new Vector3(1f, 1f, 1f);
            FxClouds.LightingEnabled = true;
            FxClouds.DirectionalLight0.DiffuseColor  = new Vector3(1f, 1f, 1f);
            FxClouds.DirectionalLight0.SpecularColor = new Vector3(1f, 1f, 1f);
            FxClouds.SpecularPower = 4;

            FxAtmoColor = new BasicEffect(content.Device, null);
            FxAtmoColor.TextureEnabled = true;
            FxAtmoColor.LightingEnabled = true;
            FxAtmoColor.DirectionalLight0.DiffuseColor = new Vector3(1f, 1f, 1f);
            FxAtmoColor.DirectionalLight0.Enabled = true;
            FxAtmoColor.DirectionalLight0.SpecularColor = new Vector3(1f, 1f, 1f);
            FxAtmoColor.DirectionalLight0.Direction = new Vector3(0.98f, -0.025f, 0.2f);
            FxAtmoColor.DirectionalLight1.DiffuseColor = new Vector3(1f, 1f, 1f);
            FxAtmoColor.DirectionalLight1.Enabled = true;
            FxAtmoColor.DirectionalLight1.SpecularColor = new Vector3(1f, 1f, 1f);
            FxAtmoColor.DirectionalLight1.Direction = new Vector3(0.98f, -0.025f, 0.2f);

            FxGlow = new BasicEffect(content.Device, null);
            FxGlow.TextureEnabled = true;

            FxFresnel = new BasicEffect(content.Device, null);
            FxFresnel.TextureEnabled = true;

            PlanetHaloFx = content.Load<Effect>("Effects/PlanetHalo");
        }

        public void Dispose()
        {
            MeshSphere = null;
            MeshRings = null;
            MeshGlowRing = null;
            MeshAtmosphere = null;

            FxRings?.Dispose(ref FxRings);
            FxClouds?.Dispose(ref FxClouds);
            FxAtmoColor?.Dispose(ref FxAtmoColor);
            FxGlow?.Dispose(ref FxGlow);
            FxFresnel?.Dispose(ref FxFresnel);

            TexRings?.Dispose(ref TexRings);
            TexAtmosphere?.Dispose(ref TexAtmosphere);
            TexGlow?.Dispose(ref TexGlow);
            TexFresnel?.Dispose(ref TexFresnel);

            Device = null;
        }

        static void SetViewProjection(BasicEffect fx, in Matrix view, in Matrix projection)
        {
            fx.View = view;
            fx.Projection = projection;
        }

        // update shaders
        public void BeginRendering(GraphicsDevice device, Vector3 cameraPos, in Matrix view, in Matrix projection)
        {
            Device = device;
            CamPos = cameraPos;
            SetViewProjection(FxClouds, view, projection);
            SetViewProjection(FxGlow, view, projection);
            SetViewProjection(FxFresnel, view, projection);
            SetViewProjection(FxAtmoColor, view, projection);
            SetViewProjection(FxRings, view, projection);
            PlanetHaloFx.Parameters["View"].SetValue(view);
            PlanetHaloFx.Parameters["Projection"].SetValue(projection);

            RenderState rs = device.RenderState;
            device.SamplerStates[0].AddressU = TextureAddressMode.Wrap;
            device.SamplerStates[0].AddressV = TextureAddressMode.Wrap;
            rs.AlphaBlendEnable = true;
            rs.AlphaBlendOperation = BlendFunction.Add;
            rs.SourceBlend = Blend.SourceAlpha;
            rs.DestinationBlend = Blend.InverseSourceAlpha;
            rs.DepthBufferWriteEnable = false;
        }

        public void EndRendering()
        {
            RenderState rs = Device.RenderState;
            rs.DepthBufferWriteEnable = true;
            rs.CullMode = CullMode.CullCounterClockwiseFace;
            rs.AlphaBlendEnable = false;
        }

        // This draws the clouds and atmosphere layers:
        // 1. layer: clouds sphere              (if PlanetType.Clouds == true)
        // 2. layer: fake fresnel effect of the atmosphere
        // 3. layer: fake glow effect around the planet
        // 4. layer: blueish transparent sphere (if PlanetType.Atmosphere == true)
        // 5. layer: subtle halo effect         (if PlanetType.Atmosphere == true)
        // 6. layer: rings                      (if any)
        public void Render(Planet p)
        {
            PlanetType type = p.Type;
            bool drawPlanetGlow = CamPos.Z < 300000.0f && type.Glow;

            if (!p.HasRings && !type.Clouds && !drawPlanetGlow)
                return;

            Vector3 sunToPlanet = (p.Center - p.ParentSystem.Position).ToVec3().Normalized();

            // tilted a bit differently than PlanetMatrix, and they constantly rotate
            Matrix cloudMatrix = default;
            var pos3d = Matrix.CreateTranslation(p.Center3D);
            var tilt = Matrix.CreateRotationX(-RadMath.Deg45AsRads);
            Matrix baseScale = p.ScaleMatrix;

            if (type.Clouds)
            {
                cloudMatrix = baseScale * Matrix.CreateRotationZ(-p.Zrotate / 1.5f) * tilt * pos3d;

                // default is CCW, this means we draw the clouds as usual
                Device.RenderState.CullMode = CullMode.CullCounterClockwiseFace;

                FxClouds.World = Types.CloudsScaleMatrix * cloudMatrix;
                FxClouds.DirectionalLight0.Direction = sunToPlanet;
                FxClouds.DirectionalLight0.Enabled = true;
                StaticMesh.Draw(MeshSphere, FxClouds, type.CloudsMap);

                // for blue atmosphere and planet halo, use CW, which means the sphere is inverted
                Device.RenderState.CullMode = CullMode.CullClockwiseFace;

                if (type.NoAtmosphere == false)
                {
                    // draw blueish transparent atmosphere sphere
                    // it is better visible near planet edges
                    FxAtmoColor.World = Types.AtmosphereScaleMatrix * cloudMatrix;
                    FxAtmoColor.DirectionalLight0.Direction = sunToPlanet;
                    FxAtmoColor.DirectionalLight0.Enabled = true;
                    StaticMesh.Draw(MeshSphere, FxAtmoColor, TexAtmosphere);
                }
            }

            if (drawPlanetGlow)
            {
                RenderPlanetGlow(p, type, pos3d, baseScale);
            }

            if (type.Clouds && type.NoHalo == false) // draw the halo effect
            {
                // inverted sphere
                Device.RenderState.CullMode = CullMode.CullClockwiseFace;
                // This is a small shine effect on top of the atmosphere
                // It is very subtle
                //var diffuseLightDirection = new Vector3(-0.98f, 0.425f, -0.4f);
                //Vector3 camPosition = CamPos.ToVec3f();
                var camPosition = new Vector3(0.0f, 0.0f, 1500f);
                Vector3 diffuseLightDirection = -sunToPlanet;
                PlanetHaloFx.Parameters["World"].SetValue(Types.HaloScaleMatrix * cloudMatrix);
                PlanetHaloFx.Parameters["CameraPosition"].SetValue(camPosition);
                PlanetHaloFx.Parameters["DiffuseLightDirection"].SetValue(diffuseLightDirection);
                StaticMesh.Draw(MeshSphere, PlanetHaloFx);
            }

            if (p.HasRings)
            {
                Device.RenderState.CullMode = CullMode.None;
                FxRings.World = Types.RingsScaleMatrix * baseScale * Matrix.CreateRotationX(p.RingTilt) * pos3d;
                StaticMesh.Draw(MeshRings, FxRings, TexRings);
            }
        }

        void RenderPlanetGlow(Planet p, PlanetType type, in Matrix pos3d, in Matrix baseScale)
        {
            Device.RenderState.CullMode = CullMode.CullCounterClockwiseFace;

            // rotate the glow effect always towards the camera by getting direction from camera to planet
            // TODO: our camera works in coordinate space where +Z is out of the screen and -Z is background
            // TODO: but our 3D coordinate system works with -Z out of the screen and +Z is background
            // HACK: planetPos Z is flipped
            Vector3 planetPos = p.Center3D * new Vector3(1, 1, -1);

            // HACK: flip XZ so the planet glow mesh faces correctly towards us
            Vector3 camToPlanet = planetPos - CamPos;
            camToPlanet.X = -camToPlanet.X;
            camToPlanet.Z = -camToPlanet.Z;

            var rot = Matrix.CreateLookAt(Vector3.Zero, camToPlanet.Normalized(), Vector3.Up);
            Matrix world = baseScale * rot * pos3d;

            var glow = new Vector3(type.GlowColor.X, type.GlowColor.Y, type.GlowColor.Z);

            if (type.Fresnel > 0f)
            {
                FxFresnel.World = world;
                FxFresnel.DiffuseColor = glow;
                FxFresnel.Alpha = type.GlowColor.W * type.Fresnel;
                StaticMesh.Draw(MeshGlowFresnel, FxFresnel, TexFresnel);
            }

            {
                FxGlow.World = world;
                FxGlow.DiffuseColor = glow;
                FxGlow.Alpha = type.GlowColor.W;
                //FxGlow.EmissiveColor = glow;
                StaticMesh.Draw(MeshGlowRing, FxGlow, TexGlow);
            }
        }
    }
}
