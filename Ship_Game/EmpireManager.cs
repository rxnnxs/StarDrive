using System;
using System.Collections.Generic;
using Ship_Game.Gameplay;

namespace Ship_Game
{
    public class EmpireManager
    {
        private static readonly Array<Empire>       EmpireList = new Array<Empire>();
        private static readonly Map<string, Empire> EmpireDict = new Map<string, Empire>(); 

        private static Empire PlayerEmpire;
        private static Empire CordrazineEmpire;

        private static Empire RemnantsFaction;
        private static Empire UnknownFaction;
        private static Empire CorsairsFaction;

        public static IReadOnlyList<Empire> Empires => EmpireList;
        public static int NumEmpires => EmpireList.Count;

        /// @todo These should be initialized ONCE during loading, leaving like this for future refactor
        public static Empire Player     => PlayerEmpire     ?? (PlayerEmpire     = FindPlayerEmpire());
        public static Empire Cordrazine => CordrazineEmpire ?? (CordrazineEmpire = GetEmpireByName("Cordrazine Collective"));

        // Special factions
        public static Empire Remnants => RemnantsFaction ?? (RemnantsFaction = GetEmpireByName("The Remnant"));
        public static Empire Unknown  => UnknownFaction  ?? (UnknownFaction  = GetEmpireByName("Unknown"));
        public static Empire Corsairs => CorsairsFaction ?? (CorsairsFaction = GetEmpireByName("Corsairs"));

        public static void Add(Empire e)
        {
            // avoid duplicate entries, due to some bad design code structuring...
            if (!EmpireList.Contains(e))
            {
                EmpireList.Add(e);
                e.Id = EmpireList.Count;
            }
        }

        public static void Clear()
        {
            EmpireList.Clear();
            EmpireDict.Clear();
            PlayerEmpire     = null;
            CordrazineEmpire = null;
            RemnantsFaction  = null;
            UnknownFaction   = null;
            CorsairsFaction  = null;
        }

        
        public static Empire GetEmpireById(int empireId)
        {
            return empireId == 0 ? null : EmpireList[empireId-1];
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
        public static Array<Empire> GetAllies(Empire e)
        {
            var allies = new Array<Empire>();
            if (e.isFaction || e.MinorRace)
                return allies;

            foreach (Empire empire in EmpireList)
                if (!empire.isPlayer && e.TryGetRelations(empire, out Relationship r) && r.Known && r.Treaty_Alliance)
                    allies.Add(empire);
            return allies;
        }
        public static Array<Empire> GetTradePartners(Empire e)
        {
            var allies = new Array<Empire>();
            if (e.isFaction || e.MinorRace)
                return allies;

            foreach (Empire empire in EmpireList)
                if (!empire.isPlayer && e.TryGetRelations(empire, out Relationship r) && r.Known && r.Treaty_Trade)
                    allies.Add(empire);
            return allies;
        }
    }
}