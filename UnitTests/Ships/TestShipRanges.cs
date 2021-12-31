﻿using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using Ship_Game;
using Ship_Game.AI;
using Ship_Game.Gameplay;
using Ship_Game.Ships;

namespace UnitTests.Ships
{
    [TestClass]
    public class TestShipRanges : StarDriveTest
    {
        public TestShipRanges()
        {
            LoadStarterShips("Heavy Carrier mk5-b");
            CreateUniverseAndPlayerEmpire();
        }

        void UpdateStatus(Ship ship, CombatState state)
        {
            ship.AI.CombatState = state;
            ship.ShipStatusChanged = true;
            ship.Update(new FixedSimTime(1f));
        }

        [TestMethod]
        public void ShipRanges()
        {
            Ship ship = SpawnShip("Heavy Carrier mk5-b", Player, Vector2.Zero);

            UpdateStatus(ship, CombatState.Artillery);
            Assert.That.Equal(11500, ship.WeaponsMaxRange);
            Assert.That.Equal(7500, ship.WeaponsMinRange);
            Assert.That.Equal(10166, ship.WeaponsAvgRange);
            Assert.That.Equal(10350, ship.DesiredCombatRange);
            Assert.That.Equal(ship.OffensiveWeapons.Average(w => w.ProjectileSpeed), ship.InterceptSpeed);

            UpdateStatus(ship, CombatState.Evade);
            Assert.That.Equal(Ship.UnarmedRange, ship.DesiredCombatRange);

            UpdateStatus(ship, CombatState.HoldPosition);
            Assert.That.Equal(ship.WeaponsMaxRange, ship.DesiredCombatRange);

            UpdateStatus(ship, CombatState.ShortRange);
            Assert.That.Equal(ship.WeaponsMinRange*0.9f, ship.DesiredCombatRange);
            
            UpdateStatus(ship, CombatState.Artillery);
            Assert.That.Equal(ship.WeaponsMaxRange*0.9f, ship.DesiredCombatRange);

            UpdateStatus(ship, CombatState.BroadsideLeft);
            Assert.That.Equal(ship.WeaponsAvgRange*0.9f, ship.DesiredCombatRange);
            UpdateStatus(ship, CombatState.BroadsideRight);
            Assert.That.Equal(ship.WeaponsAvgRange*0.9f, ship.DesiredCombatRange);

            UpdateStatus(ship, CombatState.OrbitLeft);
            Assert.That.Equal(ship.WeaponsAvgRange*0.9f, ship.DesiredCombatRange);
            UpdateStatus(ship, CombatState.OrbitRight);
            Assert.That.Equal(ship.WeaponsAvgRange*0.9f, ship.DesiredCombatRange);

            UpdateStatus(ship, CombatState.AssaultShip);
            Assert.That.Equal(ship.WeaponsAvgRange*0.9f, ship.DesiredCombatRange);
            UpdateStatus(ship, CombatState.OrbitalDefense);
            Assert.That.Equal(ship.WeaponsAvgRange*0.9f, ship.DesiredCombatRange);
        }

        [TestMethod]
        public void ShipRangesWithModifiers()
        {
            Ship ship = SpawnShip("Heavy Carrier mk5-b", Player, Vector2.Zero);
            
            WeaponTagModifier kinetic = Player.WeaponBonuses(WeaponTag.Kinetic);
            WeaponTagModifier guided = Player.WeaponBonuses(WeaponTag.Guided);
            kinetic.Range = 1;
            guided.Range = 1;

            UpdateStatus(ship, CombatState.Artillery);
            Assert.That.Equal(23000, ship.WeaponsMaxRange);
            Assert.That.Equal(7500, ship.WeaponsMinRange);
            Assert.That.Equal(17833, ship.WeaponsAvgRange);
            Assert.That.Equal(20700, ship.DesiredCombatRange);
            Assert.That.Equal(ship.OffensiveWeapons.Average(w => w.ProjectileSpeed), ship.InterceptSpeed);
        }
    }
}
