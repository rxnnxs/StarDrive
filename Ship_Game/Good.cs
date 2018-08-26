using System;
using Ship_Game.AI;

namespace Ship_Game
{
    public enum Goods
    {
        None,
        Production,
        Food,
        Colonists
    }

    public sealed class Good
    {
        public string UID;
        public bool IsCargo = true;
        public string Name;
        public string Description;
        public float Cost;
        public float Mass;
        public string IconTexturePath;
    }
    public sealed class GoodToPlan
    {
        public static ShipAI.Plan Pickup(Goods good)
        {
            switch (good)
            {
                case Goods.None:
                    break;
                case Goods.Food:
                case Goods.Production:
                    return ShipAI.Plan.PickupGoods;       
                case Goods.Colonists:
                    return ShipAI.Plan.PickupPassengers;
                default:
                    throw new ArgumentOutOfRangeException(nameof(good), good, null);
            }
            return ShipAI.Plan.Trade;
        }

        public static ShipAI.Plan DropOff(Goods good)
        {
            switch (good)
            {
                case Goods.None:
                    break;
                case Goods.Food:
                case Goods.Production:
                    return ShipAI.Plan.DropOffGoods;
                case Goods.Colonists:
                    return ShipAI.Plan.DropoffPassengers;
                default:
                    throw new ArgumentOutOfRangeException(nameof(good), good, null);
            }
            return ShipAI.Plan.Trade;
        }
    }
}