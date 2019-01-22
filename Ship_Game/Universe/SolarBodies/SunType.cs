﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Ship_Game.Data;
using Ship_Game;

namespace Ship_Game.Universe.SolarBodies
{
    public class SunType
    {
        [StarDataKey] public string Id;
        #pragma warning disable 649 // they are serialized
        [StarData] string LoResPath;
        [StarData] string HiResPath;
        #pragma warning restore 649
        [StarData] public readonly float LightIntensity = 1.5f;
        [StarData] public readonly float Radius = 150000f;
        [StarData] public readonly Color Color = Color.White;
        [StarData] public readonly bool Habitable = true; // is this star habitable, or is this a dangerous type?
        [StarData] public readonly bool DoubleLayered = true; // don't render hi-res twice
        [StarData] public readonly float RadiationDamage = 0f; // is this star dangerous and damages nearby ships??
        [StarData] public readonly float RadiationRadius = 0f;
        [StarData] public readonly float RotationSpeed = 0.03f;
        [StarData] public readonly float PulsePeriod = 5f; // period of animated pulse
        [StarData] public readonly Range PulseScale = new Range(0.95f, 1.05f); 
        [StarData] public readonly Range PulseColor = new Range(0.95f, 1.05f); 


        public SubTexture LoRes {get;private set;} // lo-res icon used in background star fields
        public SubTexture HiRes {get;private set;} // hi-res texture applied on a 3D object


        public float DamageMultiplier(float distFromSun)
        {
            // this is a custom non-linear falloff
            // enter this into https://www.desmos.com/calculator
            // with y-axis [0,1] and x-axis [0,100000]
            // formula: 1 - (sqrt(x/d) - 0.01*(d/x)^2)
            // 
            // about linear 18% we have full power thanks to 0.01*(d/x)^2
            // then we have a nice smooth falloff thanks to sqrt(x/d)
            float linear  = distFromSun/RadiationRadius;
            float inverse = RadiationRadius/distFromSun;
            float damage = 1.0f - ((float)Math.Sqrt(linear) - 0.01f*inverse*inverse);
            damage = damage.Clamped(0f, 1f);
            return damage;
        }

        public void DrawIcon(SpriteBatch batch, Rectangle rect)
        {
            batch.Draw(LoRes, rect, Color.White);
        }

        static readonly Map<string, SunType> Map = new Map<string, SunType>();
        public static SunType FindSun(string id) => Map[id];

        static SunType[] HabitableSuns;
        static SunType[] BarrenSuns;

        public static SunType RandomHabitableSun(Predicate<SunType> filter)
        {
            return RandomMath.RandItem(HabitableSuns.Filter(filter));
        }

        public static SunType RandomBarrenSun()
        {
            return RandomMath.RandItem(BarrenSuns);
        }

        public static SubTexture[] GetLoResTextures()
            => Map.FilterValues(s => s.LoRes != null).Select(s => s.LoRes);

        public static void LoadAll(GameContentManager content)
        {
            Array<SunType> all;
            using (var parser = new StarDataParser("Suns.yaml"))
                all = parser.DeserializeArray<SunType>();
            
            Map.Clear();
            foreach (SunType sun in all)
            {
                if (sun.LoResPath.NotEmpty())
                {
                    var loRes = content.Load<Texture2D>("Textures/"+sun.LoResPath);
                    sun.LoRes = new SubTexture(sun.LoResPath, loRes);
                }
                if (sun.HiResPath.NotEmpty())
                {
                    var hiRes = content.Load<Texture2D>("Textures/"+sun.HiResPath);
                    sun.HiRes = new SubTexture(sun.HiResPath, hiRes);
                }
                Map[sun.Id] = sun;
            }

            HabitableSuns = all.Filter(s => s.Habitable);
            BarrenSuns    = all.Filter(s => !s.Habitable);
        }
    }
}
