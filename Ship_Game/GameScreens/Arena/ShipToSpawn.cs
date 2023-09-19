using Microsoft.Xna.Framework.Graphics;
using SDGraphics;
using SDUtils;
using Ship_Game.AI;
using Ship_Game;
using Ship_Game.GameScreens.MainMenu;
using Ship_Game.GameScreens.NewGame;
using Ship_Game.GameScreens.ShipDesign;
using Ship_Game.Ships;
using Ship_Game.UI;
using Ship_Game.Universe;
using Ship_Game.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ship_Game
{
    public class ShipToSpawn
    {
        public ShipToSpawn(IShipDesign shipDesign, Vector2 pos) 
        {
            Design = shipDesign;
            Pos = pos;
        }
        public IShipDesign Design;
        public Vector2 Pos;
    }
    internal class TeamToSpawn
    {
        public TeamToSpawn(TeamOptions team)
        {
            SpawnList = new List<ShipToSpawn>();
            Team = team;
        }
        public List<ShipToSpawn> SpawnList;
        public TeamOptions Team;
    }
}
