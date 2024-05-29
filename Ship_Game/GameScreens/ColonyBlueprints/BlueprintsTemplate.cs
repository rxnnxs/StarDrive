﻿using SDGraphics;
using SDUtils;
using Ship_Game.AI;
using Ship_Game.Data.Serialization;
using Ship_Game.Ships;
using System.Collections.Generic;
using static Ship_Game.Planet;
using Vector2 = SDGraphics.Vector2;

namespace Ship_Game;

// Data used for saved Blueprints
[StarDataType]
public sealed class BlueprintsTemplate
{
    [StarData] public string Name;
    //[StarData] public string Description;
    //[StarData] public subTexture Icon;
    //[StarData] public Color Color;
    [StarData] public string ModName;
    [StarData] public bool Exclusive;
    [StarData] public string LinkTo;
    [StarData] public HashSet<string> PlannedBuildings;
    [StarData] public ColonyType ColonyType;

    public BlueprintsTemplate() { }
    public BlueprintsTemplate(string name, bool exclusive, string linkTo, HashSet<string>plannedBuildings, ColonyType cType) 
    {
        Name = name;
        ModName = GlobalStats.ModName;
        Exclusive = exclusive;
        LinkTo = linkTo;
        PlannedBuildings = plannedBuildings;
        ColonyType = cType;
    }
}
