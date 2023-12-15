﻿using SDUtils;

namespace Ship_Game.Ships
{
    public class MiningBays
    {
        Ship Owner;
        readonly ShipModule[] AllMiningBays;
        readonly ParticleEmitter[] FireEmitters;
        readonly ParticleEmitter[] SmokeEmitters;
        bool EmittersStarted;
        public byte RefiningOutput { get; private set; } // 0-100

        public MiningBays(Ship ship, ShipModule[] slots)
        {
            Owner = ship;
            AllMiningBays = slots.Filter(module => module.IsMiningBay);
            FireEmitters = new ParticleEmitter[AllMiningBays.Length];
            SmokeEmitters = new ParticleEmitter[AllMiningBays.Length];
        }

        public void Dispose()
        {
            Owner = null;
        }

        public void ProcessMiningBays(float rawResourcesStored)
        {
            if (Owner == null
                || rawResourcesStored / (Owner.CargoSpaceMax*0.5) > 0.5f
                || !HasOrdnanceToLaunch())
            {
                return;
            }

            foreach (ShipModule miningBay in AllMiningBays)
            {
                if (miningBay.Active 
                    && miningBay.HangarTimer <= 0
                    && !miningBay.TryGetHangarShipActive(out _)
                    && CreateMiningShip(miningBay, out Ship miningShip))
                {
                    miningBay.HangarTimer = miningBay.HangarTimerConstant;
                    miningShip.AI.OrderMinePlanet(Owner.GetTether());
                    return;
                }
            }
        }

        bool HasOrdnanceToLaunch()
        {
            if (AllMiningBays.Length == 0)
                return false;

            ShipModule miningBay = AllMiningBays[0];
            miningBay.HangarShipUID = Owner.Loyalty.GetMiningShipName();
            Ship miningShipTemplate = ResourceManager.GetShipTemplate(miningBay.HangarShipUID);
            return miningShipTemplate.ShipOrdLaunchCost < Owner.Ordinance;
        }

        bool CreateMiningShip(ShipModule hangar, out Ship miningShip)
        {
            miningShip = Ship.CreateShipFromHangar(Owner.Universe, hangar, Owner.Loyalty, Owner.Position, Owner);
            if (miningShip != null)
            {
                Owner.OnShipLaunched(miningShip);
                Owner.ChangeOrdnance(-miningShip.ShipOrdLaunchCost);
                hangar.SetHangarShip(miningShip);
            }

            return miningShip != null;
        }

        public void UpdateMiningVisuals(FixedSimTime timeStep)
        {
            if (RefiningOutput == 0)
                return;
            
            for (int i = 0; i < AllMiningBays.Length; i++)
            {
                EmittersStarted = true;
                ShipModule miningBay = AllMiningBays[i];
                if (miningBay.Active && miningBay.Powered)
                {
                    if (RefiningOutput > 50)
                    {
                        if (FireEmitters[i] == null)
                            FireEmitters[i] = Owner.Universe.Screen.Particles.PhotonExplosion.NewEmitter(0.2f, miningBay.Position, 0.5f);
                        else
                            FireEmitters[i].Update(timeStep.FixedTime, miningBay.Position.ToVec3(-100));
                    }

                    if (SmokeEmitters[i] == null)
                        SmokeEmitters[i] = Owner.Universe.Screen.Particles.SmokePlume.NewEmitter(0.3f, miningBay.Position, 0.75f);
                    else
                        SmokeEmitters[i].Update(timeStep.FixedTime, miningBay.Position.ToVec3(-50));
                }
            }
        }

        public void DestroyEmmiters()
        {
            if (!EmittersStarted)
                return;

            for (int i = 0; i < AllMiningBays.Length; i++)
            {
                FireEmitters[i] = null;
                SmokeEmitters[i] = null;
            }

            EmittersStarted = false;
        }

        public void UpdateIsRefining(float ratio)
        {
            RefiningOutput = (byte)(ratio * 100);
        }
    }
}
