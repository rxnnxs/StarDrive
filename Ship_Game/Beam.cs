using System;
using System.Xml.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Newtonsoft.Json;
using Ship_Game.Gameplay;


namespace Ship_Game
{
    public sealed class Beam : Projectile
    {
        public float PowerCost;
        public Vector2 Source;
        public Vector2 Destination;
        public Vector2 ActualHitDestination; // actual location where beam hits another ship
        public int Thickness { get; private set; }
        public static Effect BeamEffect;
        public bool FollowMouse;
        public VertexPositionNormalTexture[] Vertices = new VertexPositionNormalTexture[4];
        public int[] Indexes = new int[6];
        private readonly float BeamZ = RandomMath2.RandomBetween(-1f, 1f);
        public bool Infinite;
        private VertexDeclaration QuadVertexDecl;
        private float Displacement = 1f;

        private AudioHandle DamageToggleSound = default(AudioHandle);

        [XmlIgnore][JsonIgnore]
        public GameplayObject Target { get; }

        // Create a targeted beam that follows GameplayObject [target]
        public Beam(Weapon weapon, GameplayObject target) : this(weapon, target.Center)
        {
            Target = target;
        }

        // Create an untargeted beam with an initial destination position
        public Beam(Weapon weapon, Vector2 destination)
        {
            Weapon           = weapon;
            ModuleAttachedTo = weapon.moduleAttachedTo;
            DamageAmount = weapon.DamageAmount;
            PowerCost    = weapon.BeamPowerCostPerSecond;
            Range        = weapon.Range;
            Duration     = weapon.BeamDuration > 0f ? weapon.BeamDuration : 2f;
            Thickness    = weapon.BeamThickness;

            Owner  = ModuleAttachedTo.GetParent();
            Source = ModuleAttachedTo.Center;
            SetDestination(destination);
            ActualHitDestination = Destination;

            SetSystem(Owner.System);
            InitBeamMeshIndices();
            UpdateBeamMesh();
        }

        // Create a spatially fixed beam spawned from a ship center
        public Beam(Ship ship, Vector2 destination, int thickness)
        {
            Owner       = ship;
            Source      = ship.Center;
            Destination = destination;
            Thickness   = thickness;

            SetSystem(Owner.System);
            InitBeamMeshIndices();
            UpdateBeamMesh();
        }

        private void SetDestination(Vector2 destination)
        {
            Vector2 deltaVec = destination - Source;
            Destination = Source + deltaVec.Normalized()*Math.Min(Range, deltaVec.Length());
        }

        public override void Die(GameplayObject source, bool cleanupOnly)
        {
            DamageToggleSound.Stop();

            if (Owner != null)
            {
                Owner.RemoveBeam(this);
            }
            else if (Weapon.drowner != null)
            {
                (Weapon.drowner as Projectile)?.GetDroneAI().Beams.QueuePendingRemoval(this);
                SetSystem(Weapon.drowner.System);
            }
            Weapon.ResetToggleSound();
            base.Die(source, cleanupOnly);
        }

        public void Draw(ScreenManager screenMgr)
        {
            lock (GlobalStats.BeamEffectLocker)
            {
                Empire.Universe.beamflashes.AddParticleThreadA(new Vector3(Source, BeamZ), Vector3.Zero);
                screenMgr.GraphicsDevice.VertexDeclaration = QuadVertexDecl;
                BeamEffect.CurrentTechnique = BeamEffect.Techniques["Technique1"];
                BeamEffect.Parameters["World"].SetValue(Matrix.Identity);
                string beamTexPath = "Beams/" + ResourceManager.WeaponsDict[Weapon.UID].BeamTexture;
                BeamEffect.Parameters["tex"].SetValue(ResourceManager.Texture(beamTexPath));
                Displacement -= 0.05f;
                if (Displacement < 0f)
                {
                    Displacement = 1f;
                }
                BeamEffect.Parameters["displacement"].SetValue(new Vector2(0f, Displacement));
                BeamEffect.Begin();
                var rs = screenMgr.GraphicsDevice.RenderState;
                rs.AlphaTestEnable = true;
                rs.AlphaFunction   = CompareFunction.GreaterEqual;
                rs.ReferenceAlpha  = 200;
                foreach (EffectPass pass in BeamEffect.CurrentTechnique.Passes)
                {
                    pass.Begin();
                    screenMgr.GraphicsDevice.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, Vertices, 0, 4, Indexes, 0, 2);
                    pass.End();
                }
                rs.DepthBufferWriteEnable = false;
                rs.AlphaBlendEnable       = true;
                rs.SourceBlend            = Blend.SourceAlpha;
                rs.DestinationBlend       = Blend.InverseSourceAlpha;
                rs.AlphaTestEnable        = true;
                rs.AlphaFunction          = CompareFunction.Less;
                rs.ReferenceAlpha         = 200;
                foreach (EffectPass pass in BeamEffect.CurrentTechnique.Passes)
                {
                    pass.Begin();
                    screenMgr.GraphicsDevice.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, Vertices, 0, 4, Indexes, 0, 2);
                    pass.End();
                }
                rs.AlphaBlendEnable = false;
                rs.DepthBufferWriteEnable = true;
                rs.AlphaTestEnable = false;
                BeamEffect.End();
            }
        }
        
        private void InitBeamMeshIndices()
        {
            Vertices[0].TextureCoordinate = new Vector2(0f, 1f);
            Vertices[1].TextureCoordinate = new Vector2(0f, 0f);
            Vertices[2].TextureCoordinate = new Vector2(1f, 1f);
            Vertices[3].TextureCoordinate = new Vector2(1f, 0f);
            Indexes[0] = 0;
            Indexes[1] = 1;
            Indexes[2] = 2;
            Indexes[3] = 2;
            Indexes[4] = 1;
            Indexes[5] = 3;
        }

        private void UpdateBeamMesh()
        {
            Vector2 src = Source;
            Vector2 dst = ActualHitDestination;
            Vector2 deltaVec = dst - src;
            Vector2 right = new Vector2(deltaVec.Y, -deltaVec.X).Normalized();

            // typical zigzag pattern:  |\|
            Vertices[0].Position = new Vector3(dst - (right * Thickness), BeamZ); // botleft
            Vertices[1].Position = new Vector3(src - (right * Thickness), BeamZ); // topleft
            Vertices[2].Position = new Vector3(dst + (right * Thickness), BeamZ); // botright
            Vertices[3].Position = new Vector3(src + (right * Thickness), BeamZ); // topright

            // @todo Why are we always doing this extra work??
            Vertices[0].TextureCoordinate = new Vector2(0f, 1f);
            Vertices[1].TextureCoordinate = new Vector2(0f, 0f);
            Vertices[2].TextureCoordinate = new Vector2(1f, 1f);
            Vertices[3].TextureCoordinate = new Vector2(1f, 0f);
            Indexes[0] = 0;
            Indexes[1] = 1;
            Indexes[2] = 2;
            Indexes[3] = 2;
            Indexes[4] = 1;
            Indexes[5] = 3;
        }

        public bool LoadContent(ScreenManager screenMgr, Matrix view, Matrix projection)
        {
            QuadVertexDecl = new VertexDeclaration(screenMgr.GraphicsDevice, VertexPositionNormalTexture.VertexElements);
            return true;
        }

        public override bool Touch(GameplayObject target)
        {
            if (target == null || target == Owner && !Weapon.HitsFriendlies || target is Ship)
                return false;
            if (target is Projectile && WeaponType != "Missile")
                return false;

            var targetModule = target as ShipModule;
            if (DamageAmount < 0f && targetModule?.ShieldPower > 0f) // @todo Repair beam??
                return false;

            targetModule?.Damage(this, DamageAmount);
            return true;
        }

        public void Update(Vector2 srcCenter, int thickness, float elapsedTime)
        {
            Owner.PowerCurrent -= PowerCost * elapsedTime;
            if (Owner.PowerCurrent < 0f)
            {
                Owner.PowerCurrent = 0f;
                Die(null, false);
                Duration = 0f;
                return;
            }
            var ship = Target as Ship;
            if (Owner.engineState == Ship.MoveState.Warp || ship != null && ship.engineState == Ship.MoveState.Warp )
            {
                Die(null, false);
                Duration = 0f;
                return;
            }
            Duration -= elapsedTime;
            Source = srcCenter;

            // always update Destination to ensure beam stays in range
            SetDestination(FollowMouse
                        ? Empire.Universe.mouseWorldPos
                        : Target?.Center ?? Destination);

            if (!CollidedThisFrame)
                ActualHitDestination = Destination;

            if (!Owner.PlayerShip)
            {
                if (Destination.OutsideRadius(Source, Range + Owner.Radius)) // +Radius So beams at the back of a ship can hit too!
                {
                    Log.Info($"Beam killed because of distance: Dist = {Destination.Distance(Source)}  Beam Range = {Range}");
                    Die(null, true);
                    return;
                }
                if (!Owner.CheckIfInsideFireArc(Weapon, Destination, Owner.Rotation))
                {
                    Log.Info("Beam killed because of angle");
                    Die(null, true);
                    return;
                }
            }

            UpdateBeamMesh();
            if (Duration < 0f && !Infinite)
                Die(null, true);
        }

        public void UpdateDroneBeam(Vector2 srcCenter, Vector2 dstCenter, int thickness, float elapsedTime)
        {
            Duration -= elapsedTime;
            Source = srcCenter;
            Destination = dstCenter;
            Thickness = thickness;

            UpdateBeamMesh();
            if (Duration < 0f && !Infinite)
                Die(null, true);
        }

        protected override void Dispose(bool disposing)
        {
            QuadVertexDecl?.Dispose(ref QuadVertexDecl);
            base.Dispose(disposing);
        }
    }
}