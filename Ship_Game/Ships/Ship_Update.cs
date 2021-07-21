﻿using Microsoft.Xna.Framework;
using Ship_Game.AI;
using SynapseGaming.LightingSystem.Core;
using System;
using Microsoft.Xna.Framework.Graphics;
using Ship_Game.AI.ExpansionAI;
using Ship_Game.AI.Tasks;
using Ship_Game.Audio;
using SynapseGaming.LightingSystem.Rendering;

namespace Ship_Game.Ships
{
    public partial class Ship
    {
        // > 0 if ship is outside frustum
        float NotVisibleToPlayerTimer;

        // after X seconds of ships being invisible, we remove their scene objects
        const float RemoveInvisibleSceneObjectsAfterTime = 15f;

        public void ShowSceneObjectAt(Vector3 position)
        {
            if (ShipSO == null)
            {
                Log.Info("Showing SceneObject");
                CreateSceneObject();
            }
            ShipSO.World = Matrix.CreateTranslation(position);
            ShipSO.Visibility = GlobalStats.ShipVisibility;
        }

        public bool IsVisibleToPlayer => InFrustum && InSensorRange
                                      && (Empire.Universe?.IsSystemViewOrCloser == true);

        // NOTE: This is called on the main UI Thread by UniverseScreen
        // check UniverseScreen.QueueShipSceneObject()
        public void CreateSceneObject()
        {
            if (StarDriveGame.Instance == null || ShipSO != null)
                return; // allow creating invisible ships in Unit Tests

            //Log.Info($"CreateSO {Id} {Name}");
            shipData.LoadModel(out ShipSO, Empire.Universe.ContentManager);
            ShipSO.World = Matrix.CreateTranslation(new Vector3(Position, 0f));

            NotVisibleToPlayerTimer = 0;
            UpdateVisibilityToPlayer(FixedSimTime.Zero, forceVisible: true);
            ScreenManager.Instance.AddObject(ShipSO);
        }

        public void RemoveSceneObject()
        {
            SceneObject so = ShipSO;
            ShipSO = null;
            if (so != null)
            {
                ScreenManager.Instance.RemoveObject(so);
            }
        }

        void UpdateVisibilityToPlayer(FixedSimTime timeStep, bool forceVisible)
        {
            bool visibleToPlayer = forceVisible || IsVisibleToPlayer;
            if (visibleToPlayer) NotVisibleToPlayerTimer = 0f;
            else                 NotVisibleToPlayerTimer += timeStep.FixedTime;

            if (ShipSO != null) // allow null SceneObject to support ship.Update in UnitTests
            {
                if (!visibleToPlayer && NotVisibleToPlayerTimer > RemoveInvisibleSceneObjectsAfterTime)
                {
                    RemoveSceneObject();
                }
                else
                {
                    ShipSO.Visibility = visibleToPlayer ? GlobalStats.ShipVisibility : ObjectVisibility.None;
                }
            }
        }

        public override void Update(FixedSimTime timeStep)
        {
            if (Active && Health <= 0)
            {
                Die(null, cleanupOnly:true);
            }
            
            UpdateVisibilityToPlayer(timeStep, forceVisible: false);

            if (!Active)
                return;

            if (RandomEventManager.ActiveEvent?.InhibitWarp == true)
            {
                Inhibited = true;
                InhibitedTimer = 10f;
            }

            if (ScuttleTimer > -1f || ScuttleTimer < -1f)
            {
                ScuttleTimer -= timeStep.FixedTime;
                if (ScuttleTimer <= 0f) 
                    Die(null, cleanupOnly:true);
            }

            ShieldRechargeTimer += timeStep.FixedTime;

            if (TetheredTo != null)
            {
                Position = TetheredTo.Center + TetherOffset;
                VelocityMaximum = 0;
            }

            if (IsHangarShip && !Mothership.Active) //Problematic for drones...
                Mothership = null;

            if (!dying) UpdateAlive(timeStep);
            else        UpdateDying(timeStep);
        }

        void UpdateAlive(FixedSimTime timeStep)
        {
            ExploreCurrentSystem(timeStep);

            if (EMPdisabled)
            {
                float third = Radius / 3f;
                for (int i = 5 - 1; i >= 0; --i)
                {
                    Vector3 randPos = UniverseRandom.Vector32D(third);
                    Empire.Universe.Particles.Lightning.AddParticle(Position.ToVec3() + randPos);
                }
            }

            if (!Active)
                return;

            if (timeStep.FixedTime > 0f)
            {
                UpdateShipStatus(timeStep);
                UpdateEnginesAndVelocity(timeStep);
            }

            if (IsVisibleToPlayer)
            {
                if (ShipSO != null)
                {
                    ShipSO.World = Matrix.CreateRotationY(yRotation)
                                 * Matrix.CreateRotationZ(Rotation)
                                 * Matrix.CreateTranslation(new Vector3(Position, 0.0f));
                    ShipSO.UpdateAnimation(timeStep.FixedTime);
                    UpdateThrusters(timeStep);
                }
                else // auto-create scene objects if possible
                {
                    Empire.Universe?.QueueSceneObjectCreation(this);
                }
            }

            SoundEmitter.Position = new Vector3(Position, 0);

            ResetFrameThrustState();
        }

        void ExploreCurrentSystem(FixedSimTime timeStep)
        {
            if (System != null && timeStep.FixedTime > 0f && loyalty?.isFaction == false
                && !System.IsFullyExploredBy(loyalty)
                && System.PlanetList != null) // Added easy out for fully explored systems
            {
                foreach (Planet p in System.PlanetList)
                {
                    if (p.IsExploredBy(loyalty)) // already explored
                        continue;
                    if (p.Center.OutsideRadius(Position, 3000f))
                        continue;

                    if (p.EventsOnTiles())
                    {
                        if (loyalty == EmpireManager.Player)
                        {
                            Empire.Universe.NotificationManager.AddFoundSomethingInteresting(p);
                        }
                        else if (p.Owner == null)
                        {
                            loyalty.GetEmpireAI().SendExplorationFleet(p);
                            if (CurrentGame.Difficulty > UniverseData.GameDifficulty.Normal 
                                && PlanetRanker.IsGoodValueForUs(p, loyalty)
                                && p.ParentSystem.GetKnownStrengthHostileTo(loyalty).AlmostZero())
                            {
                                var task = MilitaryTask.CreateGuardTask(loyalty, p);
                                loyalty.GetEmpireAI().AddPendingTask(task);
                            }
                        }
                    }

                    p.SetExploredBy(loyalty);
                    System.UpdateFullyExploredBy(loyalty);
                }
            }
        }

        void UpdateThrusters(FixedSimTime timeStep)
        {
            Color thrust0 = loyalty.ThrustColor0;
            Color thrust1 = loyalty.ThrustColor1;
            Color thrust2 = loyalty.EmpireColor;
            float velocity = Velocity.Length();
            float velocityPercent = velocity / VelocityMaximum;
            bool notPaused = timeStep.FixedTime > 0f;

            for (int i = 0; i < ThrusterList.Length; ++i)
            {
                Thruster thruster = ThrusterList[i];
                thruster.UpdatePosition();

                bool enginesOn = ThrustThisFrame == Ships.Thrust.Forward || ThrustThisFrame == Ships.Thrust.Reverse;
                if (enginesOn)
                {
                    if (notPaused && thruster.heat < velocityPercent)
                        thruster.heat += 0.06f;

                    if (engineState == MoveState.Warp)
                    {
                        thruster.Update(Direction3D, thruster.heat, 0.004f, Empire.Universe.CamPos, thrust0, thrust1);
                    }
                    else
                    {
                        if (thruster.heat > 0.6f)
                            thruster.heat = 0.6f;
                        thruster.Update(Direction3D, thruster.heat, 0.002f, Empire.Universe.CamPos, thrust0, thrust1);
                    }
                }
                else
                {
                    if (notPaused)
                        thruster.heat = 0.01f;
                    thruster.Update(Direction3D, 0.1f, 1.0f / 500.0f, Empire.Universe.CamPos, thrust0, thrust1);
                }

                if (GlobalStats.EnableEngineTrails && velocityPercent > 0.1f && notPaused)
                {
                    // tscale is in world units, engine-trail effect width at scale=1 is 32 units
                    float thrustScale = thruster.tscale / 32f;
                    float thrustPower = (thruster.heat * (Thrust / 64f)).Clamped(32f, 320f) * thrustScale;
                    Vector3 thrustDirection = -Direction3D;
                    Vector3 thrustVelocity = thrustDirection * thrustPower;
                    
                    var thrustFx = Empire.Universe.Particles.ThrustEffect;
                    
                    for (int x = 0; x < 3; ++x)
                    {
                        thrustFx.AddParticle(thruster.WorldPos + thrustDirection*(x*3f), thrustVelocity,
                                             thrustScale, thrust2);
                    }

                    var trailFx = Empire.Universe.Particles.EngineTrail;
                    Vector3 trailOffset = thrustDirection*16f;
                    trailFx.AddParticle(thruster.WorldPos + trailOffset, thrustVelocity*0.5f,
                                        thrustScale, thrust1);
                }
            }
        }

        AudioHandle DeathSfx;

        void UpdateDying(FixedSimTime timeStep)
        {
            DestroyThrusters();

            dietimer -= timeStep.FixedTime;
            if (dietimer <= 1.9f && IsVisibleToPlayer && (DeathSfx == null || DeathSfx.IsStopped))
            {
                string cueName;
                if (SurfaceArea < 80) cueName = "sd_explosion_ship_warpdet_small";
                else if (SurfaceArea < 250) cueName = "sd_explosion_ship_warpdet_medium";
                else cueName = "sd_explosion_ship_warpdet_large";

                if (DeathSfx == null)
                    DeathSfx = new AudioHandle();
                DeathSfx.PlaySfxAsync(cueName, SoundEmitter);
            }
            if (dietimer <= 0.0f)
            {
                reallyDie = true;
                Die(LastDamagedBy, true);
                return;
            }

            if (ShipSO == null)
                return;

            // for a cool death effect, make the ship accelerate out of control:
            ApplyThrust(100f, Ships.Thrust.Forward);
            UpdateVelocityAndPosition(timeStep);
            PlanetCrash?.Update(timeStep);

            if (!IsMeteor && IsVisibleToPlayer)
            {
                int num1 = UniverseRandom.IntBetween(0, 60);
                if (num1 >= 57 && InFrustum)
                {
                    Vector3 position = UniverseRandom.Vector3D(0f, Radius);
                    ExplosionManager.AddExplosion(position, Velocity, ShipSO.WorldBoundingSphere.Radius, 2.5f, ExplosionType.Ship);
                    Empire.Universe.Particles.Flash.AddParticle(position);
                }
                if (num1 >= 40)
                {
                    Vector3 position = UniverseRandom.Vector3D(0f, Radius);
                    Empire.Universe.Particles.Sparks.AddParticle(position);
                }
            }

            yRotation += DieRotation.X * timeStep.FixedTime;
            xRotation += DieRotation.Y * timeStep.FixedTime;
            Rotation  += DieRotation.Z * timeStep.FixedTime;
            Rotation = Rotation.AsNormalizedRadians(); // [0; +2PI]

            if (InSensorRange && Empire.Universe.IsShipViewOrCloser)
            {
                float scale  = PlanetCrash?.Scale ?? 1;
                ShipSO.World = Matrix.CreateScale(scale) 
                             * Matrix.CreateRotationY(yRotation)
                             * Matrix.CreateRotationX(xRotation)
                             * Matrix.CreateRotationZ(Rotation)
                             * Matrix.CreateTranslation(new Vector3(Position, 0.0f));


                if (RandomMath.RollDice(10) && !IsMeteor) // Spawn some junk when tumbling
                {
                    float radSqrt = (float)Math.Sqrt(Radius);
                    float junkScale = (radSqrt * 0.02f).UpperBound(0.2f) * scale;
                    SpaceJunk.SpawnJunk(1, Position.GenerateRandomPointOnCircle(Radius / 20),
                        Velocity * scale, this, Radius, junkScale, true);
                }

                ShipSO.UpdateAnimation(timeStep.FixedTime);
            }

            SoundEmitter.Position = new Vector3(Position, 0);

            for (int i = 0; i < ModuleSlotList.Length; i++)
            {
                ModuleSlotList[i].UpdateWhileDying(timeStep);
            }
        }

        void CheckAndPowerConduit(ShipModule module)
        {
            if (!module.Active)
                return;
            module.Powered = true;
            module.CheckedConduits = true;
            Vector2 center = module.LocalCenter;
            for (int x = 0; x < ModuleSlotList.Length; x++)
            {
                ShipModule slot = ModuleSlotList[x];
                if (slot == module || slot.ModuleType != ShipModuleType.PowerConduit || slot.CheckedConduits)
                    continue;
                var distanceX = (int) Math.Abs(center.X - slot.LocalCenter.X) ;
                var distanceY = (int) Math.Abs(center.Y - slot.LocalCenter.Y) ;
                if (distanceX + distanceY > 16)
                {
                    if (distanceX + distanceY > 33)
                        continue;
                    if (distanceX + distanceY < 33)
                        continue;
                }

                CheckAndPowerConduit(slot);
            }
        }

        public void RecalculatePower()
        {
            ShouldRecalculatePower = false;

            for (int i = 0; i < ModuleSlotList.Length; ++i)
            {
                ShipModule slot      = ModuleSlotList[i];
                slot.Powered         = false;
                slot.CheckedConduits = false;
            }

            for (int i = 0; i < ModuleSlotList.Length; ++i)
            {
                ShipModule module = ModuleSlotList[i];
                //better fix for modules that dont use power.
                if (module.PowerRadius < 1 && (module.PowerDraw <= 0 || module.AlwaysPowered))
                {
                    module.Powered = true;
                    continue;
                }
                //Filter by powerplants.
                if (!module.Is(ShipModuleType.PowerPlant) || !module.Active)
                    continue;

                module.Powered = true;
                foreach (ShipModule slot2 in ModuleSlotList)
                {
                    if (slot2.ModuleType != ShipModuleType.PowerConduit || slot2.Powered)
                        continue;

                    if (IsAnyPartOfModuleInRadius(module, slot2.LocalCenter, 16))
                        CheckAndPowerConduit(slot2);
                }
            }
            for (int i = 0; i < ModuleSlotList.Length; ++i)
            {
                ShipModule module = ModuleSlotList[i];
                if (!module.Active || module.PowerRadius < 1 || !module.Powered )
                    continue;

                float cx = module.LocalCenter.X;
                float cy = module.LocalCenter.Y;
                int powerRadius = module.PowerRadius * 16 + (int)module.Radius;

                foreach (ShipModule slot2 in ModuleSlotList)
                {
                    if (!slot2.Active || slot2.Powered  || slot2 == module || slot2.ModuleType == ShipModuleType.PowerConduit)
                        continue;

                    int distanceFromPowerX = (int)Math.Abs(cx - (slot2.Position.X + 8)) ;
                    int distanceFromPowerY = (int)Math.Abs(cy - (slot2.Position.Y + 8));
                    if (distanceFromPowerX + distanceFromPowerY <= powerRadius)
                    {
                        slot2.Powered = true;
                        continue;
                    }
                    //if its really far away dont bother.
                    if (distanceFromPowerX + distanceFromPowerY > slot2.Radius * 2 + powerRadius)
                        continue;
                    slot2.Powered = IsAnyPartOfModuleInRadius(slot2, new Vector2(cx, cy), powerRadius);
                }
            }
        }
        //not sure where to put this. I guess shipModule but its huge. Maybe an extension?
        private static bool IsAnyPartOfModuleInRadius(ShipModule moduleAreaToCheck, Vector2 pos, int radius)
        {
            float cx = pos.X;
            float cy = pos.Y;
            for (int y = 0; y < moduleAreaToCheck.YSIZE; ++y)
            {
                float sy = moduleAreaToCheck.Position.Y + (y * 16) + 8;
                for (int x = 0; x < moduleAreaToCheck.XSIZE; ++x)
                {
                    if (y == moduleAreaToCheck.YSIZE * 16 && x == moduleAreaToCheck.XSIZE *16)
                        continue;

                    float sx = moduleAreaToCheck.Position.X + (x * 16) + 8;
                    if ((int) Math.Abs(cx - sx) + (int) Math.Abs(cy - sy) <= radius + 8)
                        return true;
                }
            }
            return false;
        }
    }
}