﻿using System;
using Microsoft.Xna.Framework;

namespace Ship_Game.Ships.Legacy
{
    public struct LegacyShipGridInfo
    {
        public Point Size; // slot dimensions of the grid, for example 4x4 for Vulcan Scout
        public Point GridOrigin; // origin of the grid from grid center
        public Vector2 VirtualOrigin; // where is the TopLeft of the grid? in the virtual coordinate space
        public Vector2 Span; // actual size of the grid in world coordinate space (64.0 x 64.0 for vulcan scout)
        public int SurfaceArea;
        public Vector2 MeshOffset; // offset of the mesh from Mesh object center, for grid to match model

        public override string ToString() => $"surface={SurfaceArea} size={Size} Vorigin={VirtualOrigin} span={Span}";

        public LegacyShipGridInfo(string name, LegacyModuleSlotData[] templateSlots, bool isHull, LegacyShipData baseHull)
        {
            SurfaceArea = 0;
            var min = new Vector2(+4096, +4096);
            var max = new Vector2(-4096, -4096);

            if (isHull || LegacyShipData.IsAllDummySlots(templateSlots))
            {
                // hulls are simple
                for (int i = 0; i < templateSlots.Length; ++i)
                {
                    LegacyModuleSlotData slot = templateSlots[i];
                    if (slot.ModuleUID != null)
                        throw new Exception($"A ShipHull cannot have ModuleUID! uid={slot.ModuleUID}");

                    var topLeft = slot.Position - new Vector2(ShipModule.ModuleSlotOffset);
                    var botRight = new Vector2(topLeft.X + 16f, topLeft.Y + 16f);
                    if (topLeft.X  < min.X) min.X = topLeft.X;
                    if (topLeft.Y  < min.Y) min.Y = topLeft.Y;
                    if (botRight.X > max.X) max.X = botRight.X;
                    if (botRight.Y > max.Y) max.Y = botRight.Y;
                    ++SurfaceArea;
                }
            }
            else
            {
                // This is the worst case, we need to support any crazy designs out there
                // including designs which don't even match BaseHull and have a mix of Dummy and Placed modules
                // Only way is to create a Map of unique coordinates

                var slotsMap = new Map<Point, LegacyModuleSlotData>();

                // insert BaseHull slots, this is required for some broken designs
                // where BaseHull has added Slots to the Top, but design has not been updated
                // leading to a mismatched ModuleGrid
                for (int i = 0; i < baseHull.ModuleSlots.Length; ++i)
                {
                    LegacyModuleSlotData designSlot = baseHull.ModuleSlots[i];
                    slotsMap[designSlot.PosAsPoint] = designSlot;
                }

                // insert dummy modules first
                for (int i = 0; i < templateSlots.Length; ++i)
                {
                    LegacyModuleSlotData designSlot = templateSlots[i];
                    if (designSlot.IsDummy)
                    {
                        slotsMap[designSlot.PosAsPoint] = designSlot;
                    }
                }

                // now place non-dummy modules as XSIZE*YSIZE grids
                for (int i = 0; i < templateSlots.Length; ++i)
                {
                    LegacyModuleSlotData designSlot = templateSlots[i];
                    if (!designSlot.IsDummy)
                    {
                        Point position = designSlot.PosAsPoint;
                        slotsMap[position] = designSlot;

                        ShipModule m = designSlot.ModuleOrNull;
                        if (m == null)
                            throw new Exception($"Module {designSlot.ModuleUID} does not exist! This design is invalid.");

                        Point size = m.GetOrientedSize(designSlot.Orientation);
                        for (int x = 0; x < size.X; ++x)
                        for (int y = 0; y < size.Y; ++y)
                        {
                            if (x == 0 && y == 0) continue;

                            var pos = new Point(position.X + x*16, position.Y + y*16);
                            if (!slotsMap.ContainsKey(pos))
                            {
                                slotsMap[pos] = new LegacyModuleSlotData(new Vector2(pos.X, pos.Y), designSlot.Restrictions);
                            }
                        }
                    }
                }

                // Now we should have a list of unique slots, normalized to 1x1
                foreach (LegacyModuleSlotData slot in slotsMap.Values)
                {
                    var topLeft = slot.Position - new Vector2(ShipModule.ModuleSlotOffset);
                    var botRight = new Vector2(topLeft.X + 16f, topLeft.Y + 16f);
                    if (topLeft.X  < min.X) min.X = topLeft.X;
                    if (topLeft.Y  < min.Y) min.Y = topLeft.Y;
                    if (botRight.X > max.X) max.X = botRight.X;
                    if (botRight.Y > max.Y) max.Y = botRight.Y;
                    ++SurfaceArea;
                }
            }

            VirtualOrigin = new Vector2(min.X, min.Y);
            Span = new Vector2(max.X - min.X, max.Y - min.Y);
            Size = new Point((int)Span.X / 16, (int)Span.Y / 16);
            GridOrigin = new Point(-Size.X / 2, -Size.Y / 2);

            Vector2 offset = -(VirtualOrigin + Span*0.5f);
            if (offset != Vector2.Zero)
                Log.Info($"MeshOffset {offset}  {name}");
            MeshOffset = offset;
        }
    }
}