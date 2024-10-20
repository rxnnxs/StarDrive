﻿using SDUtils;
using Ship_Game.Ships;
using Ship_Game.Universe;
using Vector2 = SDGraphics.Vector2;

// ReSharper disable once CheckNamespace
namespace Ship_Game.GameScreens.ShipDesign
{
    public class DesignShip : Ship
    {
        Array<ShipModule> PrevModules;

        public Ships.ShipDesign Design;
        public ShipDesignStats DesignStats;

        public DesignShip(UniverseState us, Ships.ShipDesign designHull)
            : base(us, us.CreateId(), us.Player, designHull, isTemplate:true, shipyardDesign:true)
        {
            Design = designHull;
            DesignStats = new(this, us.Player);
            Position = Vector2.Zero;
        }

        public void UpdateDesign(Array<ShipModule> placedModules, bool forceUpdate = false)
        {
            if (!forceUpdate && PrevModules != null && AreEqual(PrevModules, placedModules))
                return;

            PrevModules = placedModules;
            CreateModuleSlotsFromShipyardModules(placedModules, Design);
            InitializeShip();
            DesignStats.Update(Universe.Player);
        }

        static bool AreEqual(Array<ShipModule> a, Array<ShipModule> b)
        {
            if (a.Count != b.Count)
                return false;
            for (int i = 0; i < a.Count; ++i)
                if (!a[i].Equals(b[i]))
                    return false;
            return true;
        }
    }
}
