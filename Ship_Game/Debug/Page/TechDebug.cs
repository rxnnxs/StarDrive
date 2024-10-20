﻿using Microsoft.Xna.Framework.Graphics;
using SDUtils;
using Ship_Game.Ships;
using System.Linq;

namespace Ship_Game.Debug.Page;

public class TechDebug : DebugPage
{
    public TechDebug(DebugInfoScreen parent) : base(parent, DebugModes.Tech)
    {
    }

    public override void Draw(SpriteBatch batch, DrawTimes elapsed)
    {
        if (!Visible)
            return;

        Text.Cursor.Y -= (float)(Fonts.Arial20Bold.LineSpacing + 2) * 4;
        int column = 0;
        foreach (Empire e in Universe.Empires)
        {
            if (!e.IsFaction && !e.IsDefeated)
            {
                DrawEmpireTech(e, column);
                ++column;
            }
        }

        base.Draw(batch, elapsed);
    }
    void DrawEmpireTech(Empire e, int column)
    {
        Text.SetCursor(Parent.Win.X + 10 + 255 * column, Parent.Win.Y + 10, e.EmpireColor);
        Text.String(e.data.Traits.Name);

        if (e.data.DiplomaticPersonality != null)
        {
            Text.String(e.data.DiplomaticPersonality.Name);
            Text.String(e.data.EconomicPersonality.Name);
        }

        Text.String($"Corvettes: {e.CanBuildCorvettes}");
        Text.String($"Frigates: {e.CanBuildFrigates}");
        Text.String($"Cruisers: {e.CanBuildCruisers}");
        Text.String($"Battleships: {e.CanBuildBattleships}");
        Text.String($"Capitals: {e.CanBuildCapitals}");
        Text.String($"Bombers: {e.CanBuildBombers}");
        Text.String($"Carriers: {e.CanBuildCarriers}");
        Text.String($"Troopships: {e.CanBuildTroopShips}");
        Text.NewLine();

        if (e.Research.HasTopic)
        {
            Text.String($"Research: {e.Research.Current.Progress:0}/{e.Research.Current.TechCost:0} ({e.Research.NetResearch.String()} / {e.Research.MaxResearchPotential.String()})");
            Text.String("   --" + e.Research.Topic);
            IShipDesign bestShip = e.AI.TechChooser.LineFocus.BestCombatShip;
            if (bestShip != null)
            {
                var neededTechs = bestShip.TechsNeeded.Except(e.ShipTechs);
                float techCost = 0;
                foreach(var tech in neededTechs)
                    techCost += e.TechCost(tech);

                Text.String($"Ship : {bestShip.Name}");
                Text.String($"Hull : {bestShip.BaseHull.Role}");
                Text.String($"Role : {bestShip.Role}");
                Text.String($"Str : {(int)bestShip.BaseStrength} - Tech : {techCost}");
            }
        }

        if (Parent.GetResearchLog(e, out var empireLog))
        {
            Text.NewLine();
            for (int x = 0; x < empireLog.Count - 1; x++)
            {
                var text = empireLog[x];
                Text.String(text ?? "Error");
            }
        }
    }

    public override bool HandleInput(InputState input)
    {
        return base.HandleInput(input);
    }

    public override void Update(float fixedDeltaTime)
    {
        base.Update(fixedDeltaTime);
    }
}