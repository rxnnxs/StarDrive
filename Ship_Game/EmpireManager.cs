using System;
using System.Collections.Generic;
using Ship_Game.Gameplay;

namespace Ship_Game
{
	public class EmpireManager
	{
		public  static readonly List<Empire> EmpireList = new List<Empire>();
        private static readonly Dictionary<string, Empire> EmpireDict = new Dictionary<string, Empire>(); 
        private static Empire RemnantsEmpire;
        private static Empire PlayerEmpire;

        /// @todo These should be initialized ONCE during loading, leaving like this for future refactor
        public static Empire Remnants => RemnantsEmpire ?? (RemnantsEmpire = GetEmpireByName("The Remnant"));
        public static Empire Player   => PlayerEmpire   ?? (PlayerEmpire   = FindPlayerEmpire());
        public static void Clear()
        {
            EmpireList.Clear();
            EmpireDict.Clear();
            RemnantsEmpire = null;
            PlayerEmpire   = null;
        }
        public static Empire GetEmpireByName(string name)
        {
            if (name == null)
                return null;
            if (EmpireDict.TryGetValue(name, out Empire e))
                return e;                        
            foreach (Empire empire in EmpireList)
            {
                if (empire.data.Traits.Name != name) continue;
                EmpireDict.Add(name, empire);
                Log.Info("Added Empire: " + empire.PortraitName);
                return empire;
            }
            return null;
        }
        private static Empire FindPlayerEmpire()
        {
            foreach (Empire empire in EmpireList)
                if (empire.isPlayer)
                    return empire;
            return null;
        }
        public static List<Empire> GetAllies(Empire e)
        {
            var allies = new List<Empire>();
            if (e.isFaction || e.MinorRace)
                return allies;

            foreach (Empire empire in EmpireList)
                if (!empire.isPlayer && e.TryGetRelations(empire, out Relationship r) && r.Known && r.Treaty_Alliance)
                    allies.Add(empire);
            return allies;
        }
        public static List<Empire> GetTradePartners(Empire e)
        {
            var allies = new List<Empire>();
            if (e.isFaction || e.MinorRace)
                return allies;

            foreach (Empire empire in EmpireList)
                if (!empire.isPlayer && e.TryGetRelations(empire, out Relationship r) && r.Known && r.Treaty_Trade)
                    allies.Add(empire);
            return allies;
        }
	}
}