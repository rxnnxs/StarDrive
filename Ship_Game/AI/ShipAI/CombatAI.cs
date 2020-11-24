using Microsoft.Xna.Framework;
using Ship_Game.Gameplay;
using Ship_Game.Ships;
using Ship_Game.AI.ShipMovement;
using System;
using Microsoft.Xna.Framework.Graphics;
using static Ship_Game.AI.ShipAI;
using static Ship_Game.AI.ShipAI.TargetParameterTotals;
using SynapseGaming.LightingSystem.Shadows;

namespace Ship_Game.AI
{
    public sealed class CombatAI
    {
        public float VultureWeight = 0.5f;
        public float SelfDefenseWeight = 0.5f;
        public float SmallAttackWeight;
        public float MediumAttackWeight;
        public float LargeAttackWeight;
        public float PirateWeight;
        private float AssistWeight = 0.5f;
        public Ship Owner;
        ShipAIPlan CombatTactic;
        CombatState CurrentCombatStance;

        public CombatAI()
        {
        }

        public CombatAI(Ship ship)
        {
            Owner = ship;
            CurrentCombatStance = ship.AI.CombatState;
            SetCombatTactics(ship.AI.CombatState);
        }

        public void ClearTargets()
        {
            Owner.AI.Target = null;
            Owner.AI.PotentialTargets.Clear();
        }

        public ShipWeight ShipCommandTargeting(ShipWeight weight, TargetParameterTotals targetPrefs)
        {
            // standard ship targeting:
            // within max weapons range
            // within desired range
            // pirate scavenging
            // Size desire / hit chance
            // speed / turnrate difference
            // damaged by

            // Target of opportunity
            // target is threat. 
            // target is objective

            Vector2 friendlyCenter = Owner.fleet != null ? Owner.FleetOffset : Owner.AI.FriendliesSwarmCenter;
            Ship target = weight.Ship;
            float theirDps = target.TotalDps;
            float distanceToTarget = Owner.Center.Distance(weight.Ship.Center).LowerBound(1);
            float distanceToMass = friendlyCenter.Distance(targetPrefs.Center);
            float enemyMassDistance = Owner.Center.Distance(targetPrefs.Center);
            float errorRatio = 1 - (target.Radius - Owner.MaxWeaponError) / target.Radius;
            bool inTheirRange = distanceToTarget < target.WeaponsMaxRange;
            bool inOurRange = distanceToTarget < Owner.WeaponsMaxRange;

            // more agile than us the less they are valued. 
            float turnRatio = (Owner.RotationRadiansPerSecond - target.RotationRadiansPerSecond).Clamped(-1, 1);
            float stlRatio = (Owner.MaxSTLSpeed - target.MaxSTLSpeed).Clamped(-1,0);
            float errorValue = ((Owner.MaxWeaponError * 2) - target.Radius / 8).Clamped(-1, 1);
            float massDPSValue = (target.TotalDps - targetPrefs.DPS).Clamped(-1, 1);
            float targetDPSValue = (Owner.TotalDps - target.TotalDps) > 0 ? -1 : 0;
            float massTargetValue = (distanceToMass - distanceToTarget) > 0 ? 1 : -1;
            float ownerTargetValue = (Owner.WeaponsMaxRange - distanceToTarget) > 0 ? 1 : 0;

            float targetValue = 0;
            Ship motherShip = Owner.Mothership ?? Owner.AI.EscortTarget;
            if (motherShip != null)
            {
                bool targetingMothership = target.AI.Target == motherShip;
                bool targetOfMothership = target == motherShip.AI.Target;
                bool damagingMotherShip = motherShip.LastDamagedBy == target;
                float motherShipDistanceValue = (motherShip.Center.Distance(target.Center) - distanceToTarget).Clamped(-1, 1);

                targetValue += motherShipDistanceValue;
                switch (Owner.shipData.HangarDesignation)
                {
                    case ShipData.HangarOptions.General:
                        break;
                    case ShipData.HangarOptions.AntiShip:
                        {
                            targetValue += targetOfMothership ? 1 : 0;
                            break;
                        }
                    case ShipData.HangarOptions.Interceptor:
                        {
                            targetValue += targetingMothership ? 1 : 0;
                            targetValue += damagingMotherShip ? 1 : 0;
                            targetValue += target.Mothership != null ? 1 : 0;
                            targetValue += target.DesignRoleType == ShipData.RoleType.Troop ? 1 : 0;
                            break;
                        }
                    default:
                        break;
                }
            }
            targetValue += turnRatio;
            targetValue += stlRatio;
            targetValue += errorValue;
            targetValue += massDPSValue;
            targetValue += targetDPSValue;
            targetValue += massTargetValue;
            targetValue += ownerTargetValue;
            targetValue += inTheirRange ? 1 : 0;
            targetValue += inOurRange ? 1 : 0;
            targetValue += target == Owner.AI.Target ? 0.5f : 0;
            targetValue += Owner.loyalty.WeArePirates && target.shipData.ShipCategory == ShipData.Category.Civilian ? 1 : 0;
            targetValue += target.AI.State == AIState.Resupply ? -1 : 0;
            targetValue += target.Mothership != null ? -1 : 0;
            targetValue += target.HomePlanet != null ? -1 : 0;
            targetValue += target.MaxSTLSpeed == 0 ? -1 : 0;

            weight.SetWeight(targetValue);

            if (float.IsNaN(weight.Weight))
                Log.Error($"ship weight NaN for {weight.Ship}");
            if (float.IsInfinity(weight.Weight))
                Log.Error($"ship weight infinite for {weight.Ship}");
            Vector2 debugOffset = new Vector2(target.Radius + 50);
            if (Empire.Universe.SelectedShip == Owner)
                Empire.Universe.DebugWin?.DrawText(target.Center + debugOffset, $"TargetValue : {targetValue.ToString()}", Color.Yellow, 0.1f);
            return weight;
        }

        public void SetCombatTactics(CombatState combatState)
        {
            if (CurrentCombatStance != combatState)
            {
                CurrentCombatStance = combatState;
                CombatTactic = null;
                Owner.shipStatusChanged = true; // FIX: force DesiredCombatRange update
            }

            if (CombatTactic == null)
            {
                switch (combatState)
                {
                    case CombatState.Artillery:
                        CombatTactic = new CombatTactics.Artillery(Owner.AI);
                        break;
                    case CombatState.BroadsideLeft:
                        CombatTactic = new CombatTactics.BroadSides(Owner.AI, OrbitPlan.OrbitDirection.Left);
                        break;
                    case CombatState.BroadsideRight:
                        CombatTactic = new CombatTactics.BroadSides(Owner.AI, OrbitPlan.OrbitDirection.Right);
                        break;
                    case CombatState.OrbitLeft:
                        CombatTactic = new CombatTactics.OrbitTarget(Owner.AI, OrbitPlan.OrbitDirection.Left);
                        break;
                    case CombatState.OrbitRight:
                        CombatTactic = new CombatTactics.OrbitTarget(Owner.AI, OrbitPlan.OrbitDirection.Right);
                        break;
                    case CombatState.AttackRuns:
                        CombatTactic = new CombatTactics.AttackRun(Owner.AI);
                        break;
                    case CombatState.HoldPosition:
                        CombatTactic = new CombatTactics.HoldPosition(Owner.AI);
                        break;
                    case CombatState.Evade:
                        CombatTactic = new CombatTactics.Evade(Owner.AI);
                        break;
                    case CombatState.AssaultShip:
                        CombatTactic = new CombatTactics.AssaultShipCombat(Owner.AI);
                        break;
                    case CombatState.OrbitalDefense:
                        break;
                    case CombatState.ShortRange:
                        CombatTactic = new CombatTactics.Artillery(Owner.AI);
                        break;
                }

            }
        }

        public void ExecuteCombatTactic(FixedSimTime timeStep) => CombatTactic?.Execute(timeStep, null);

    }
}