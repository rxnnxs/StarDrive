﻿using Microsoft.Xna.Framework;
using Ship_Game.Ships;

// ReSharper disable once CheckNamespace
namespace Ship_Game
{
    public sealed partial class ShipDesignScreen
    {
        private class CategoryDropDown : DropOptions<ShipData.Category>
        {            
            public CategoryDropDown(UIElementV2 parent, Rectangle dropdownRect) : base(parent, dropdownRect)
            {                
            }
            public override bool HandleInput(InputState input)
            {
                if (Rect.HitTest(input.CursorPosition)) //fbedard: add tooltip for CategoryList
                {
                    switch (ActiveValue)
                    {
                        default:
                        case ShipData.Category.Unclassified: ToolTip.CreateTooltip("Repair when internal damage is above 65%"); break;
                        case ShipData.Category.Civilian: ToolTip.CreateTooltip("Can be used as Freighter. Evade when enemies are near. Repair when internal damage is above 15%"); break;
                        case ShipData.Category.Recon:    ToolTip.CreateTooltip("Repair when internal damage is above 35%"); break;
                        case ShipData.Category.Combat: ToolTip.CreateTooltip("Repair when internal damage is above 75%"); break;
                        case ShipData.Category.Bomber: ToolTip.CreateTooltip("Repair when internal damage is above 60%"); break;
                        case ShipData.Category.Fighter:  ToolTip.CreateTooltip("Repair when internal damage is above 80%"); break;
                        case ShipData.Category.Kamikaze: ToolTip.CreateTooltip("Never Repair! Never Rearm!\n(unless ordered)"); break;
                    }
                }
                return base.HandleInput(input);
            }
        }
    }
}