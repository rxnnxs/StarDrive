using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SDGraphics;
using Ship_Game.Ships;
using Rectangle = SDGraphics.Rectangle;


namespace Ship_Game
{
    class TeamDropDown : DropOptions<TeamOptions>
    {
        public TeamDropDown(in Rectangle TeamRect) : base(TeamRect)
        {
            
        }
    }
    public enum TeamOptions
    {
        Team1,
        Team2,
    }
}
