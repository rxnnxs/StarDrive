﻿using System;
using System.Diagnostics.CodeAnalysis;

namespace Ship_Game.Spatial
{
    ///////////////////////////////////////////////////////////////////////////////////////////

    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    public sealed partial class Qtree : ISpatial
    {
        public static readonly SpatialObj[] NoObjects = new SpatialObj[0];

        int Levels { get; }
        public float FullSize { get; }

        readonly float QuadToLinearSearchThreshold;

        /// <summary>
        /// How many objects to store per cell before subdividing
        /// </summary>
        public const int CellThreshold = 64;

        /// <summary>
        /// Ratio of search radius where we switch to Linear search
        /// because Quad search would traverse entire tree
        /// </summary>
        const float QuadToLinearRatio = 0.75f;

        QtreeNode Root;

        readonly Array<GameplayObject> Objects = new Array<GameplayObject>();
        SpatialObj[] SpatialObjects = new SpatialObj[0];
        QtreeRecycleBuffer FrontBuffer = new QtreeRecycleBuffer(10000);
        QtreeRecycleBuffer BackBuffer  = new QtreeRecycleBuffer(20000);

        public float WorldSize { get; }
        public int Count { get; private set; }

        /// <summary>
        /// Current number of active QtreeNodes in the tree
        /// </summary>
        int NumActiveNodes;

        public string Name => "C#-Qtree";

        // Create a quadtree to fit the universe
        public Qtree(float universeSize, float smallestCell = 512f)
        {
            WorldSize = universeSize;
            Levels = 1;
            FullSize = smallestCell;
            while (FullSize < universeSize)
            {
                ++Levels;
                FullSize *= 2;
            }
            QuadToLinearSearchThreshold = FullSize * QuadToLinearRatio;
            Clear();
        }

        public void Clear()
        {
            // universe is centered at [0,0], so Root node goes from [-half, +half)
            float half = FullSize / 2;
            Root = FrontBuffer.Create(-half, -half, +half, +half);
            lock (Objects)
            {
                Objects.Clear();
            }
        }

        struct OverlapsRect
        {
            public readonly byte NW, NE, SE, SW;
            public OverlapsRect(in AABoundingBox2D quad, in AABoundingBox2D rect)
            {
                float midX = (quad.X1 + quad.X2) * 0.5f;
                float midY = (quad.Y1 + quad.Y2) * 0.5f;
                // +---------+   The target rectangle overlaps Left quadrants (NW, SW)
                // | x--|    |
                // |-|--+----|
                // | x--|    |
                // +---------+
                byte overlaps_Left = (rect.X1 < midX)?(byte)1:(byte)0;
                // +---------+   The target rectangle overlaps Right quadrants (NE, SE)
                // |    |--x |
                // |----+--|-|
                // |    |--x |
                // +---------+
                byte overlaps_Right = (rect.X2 >= midX)?(byte)1:(byte)0;
                // +---------+   The target rectangle overlaps Top quadrants (NW, NE)
                // | x--|-x  |
                // |----+----|
                // |    |    |
                // +---------+
                byte overlaps_Top = (rect.Y1 < midY)?(byte)1:(byte)0;
                // +---------+   The target rectangle overlaps Bottom quadrants (SW, SE)
                // |    |    |
                // |----+----|
                // | x--|-x  |
                // +---------+
                byte overlaps_Bottom = (rect.Y2 >= midY)?(byte)1:(byte)0;

                // bitwise combine to get which quadrants we overlap: NW, NE, SE, SW
                NW = (byte)(overlaps_Top & overlaps_Left);
                NE = (byte)(overlaps_Top & overlaps_Right);
                SE = (byte)(overlaps_Bottom & overlaps_Right);
                SW = (byte)(overlaps_Bottom & overlaps_Left);
            }
        }

        void InsertAt(QtreeNode node, int level, SpatialObj[] spatialObjects, int objectId)
        {
            AABoundingBox2D objectRect = spatialObjects[objectId].AABB;
            for (;;)
            {
                if (level <= 1) // no more subdivisions possible
                {
                    node.Add(objectId);
                    return;
                }

                if (node.NW != null) // isBranch
                {
                    var over = new OverlapsRect(node.AABB, objectRect);
                    int overlaps = over.NW + over.NE + over.SE + over.SW;

                    // this is an optimal case, we only overlap 1 sub-quadrant, so we go deeper
                    if (overlaps == 1)
                    {
                        if      (over.NW != 0) { node = node.NW; --level; }
                        else if (over.NE != 0) { node = node.NE; --level; }
                        else if (over.SE != 0) { node = node.SE; --level; }
                        else if (over.SW != 0) { node = node.SW; --level; }
                    }
                    else // target overlaps multiple quadrants, so it has to be inserted into several of them:
                    {
                        if (over.NW != 0) { InsertAt(node.NW, level-1, spatialObjects, objectId); }
                        if (over.NE != 0) { InsertAt(node.NE, level-1, spatialObjects, objectId); }
                        if (over.SE != 0) { InsertAt(node.SE, level-1, spatialObjects, objectId); }
                        if (over.SW != 0) { InsertAt(node.SW, level-1, spatialObjects, objectId); }
                        return;
                    }
                }
                else // isLeaf
                {
                    InsertAtLeaf(node, level, spatialObjects, objectId);
                    return;
                }
            }
        }

        void InsertAtLeaf(QtreeNode leaf, int level, SpatialObj[] spatialObjects, int objectId)
        {
            // are we maybe over Threshold and should Subdivide ?
            if (level > 0 && leaf.Count >= CellThreshold)
            {
                float x1 = leaf.AABB.X1;
                float x2 = leaf.AABB.X2;
                float y1 = leaf.AABB.Y1;
                float y2 = leaf.AABB.Y2;
                float midX = (x1 + x2) * 0.5f;
                float midY = (y1 + y2) * 0.5f;

                leaf.NW = FrontBuffer.Create(x1, y1, midX, midY);
                leaf.NE = FrontBuffer.Create(midX, y1, x2, midY);
                leaf.SE = FrontBuffer.Create(midX, midY, x2, y2);
                leaf.SW = FrontBuffer.Create(x1, midY, midX, y2);

                int count = leaf.Count;
                int[] arr = leaf.Items;
                leaf.Items = QtreeNode.NoObjects;
                leaf.Count = 0;

                // and now reinsert all items one by one
                for (int i = 0; i < count; ++i)
                    InsertAt(leaf, level, spatialObjects, arr[i]);

                // and now try to insert our object again
                InsertAt(leaf, level, spatialObjects, objectId);
            }
            else // expand LEAF
            {
                leaf.Add(objectId);
            }
        }

        QtreeNode CreateFullTree(Array<GameplayObject> allObjects, SpatialObj[] spatialObjects)
        {
            // universe is centered at [0,0], so Root node goes from [-half, +half)
            float half = FullSize / 2;
            QtreeNode newRoot = FrontBuffer.Create(-half, -half, +half, +half);

            for (int i = 0; i < allObjects.Count; ++i)
            {
                GameplayObject go = allObjects[i];
                if (go.Active)
                {
                    int objectId = i;
                    spatialObjects[objectId] = new SpatialObj(go, objectId);
                    InsertAt(newRoot, Levels, spatialObjects, objectId);
                }
            }
            Count = allObjects.Count;
            return newRoot;
        }

        public void UpdateAll(Array<GameplayObject> allObjects)
        {
            // prepare our node buffer for allocation
            FrontBuffer.MarkAllNodesInactive();

            // create the new tree from current world state
            var spatialObjects = new SpatialObj[allObjects.Count];
            QtreeNode newRoot = CreateFullTree(allObjects, spatialObjects);
            // Swap recycle lists
            // We move last frame's nodes to front and start overwriting them
            QtreeRecycleBuffer newBackBuffer = FrontBuffer;

            lock (Objects)
            {
                Objects.Assign(allObjects);

                Root = newRoot;
                SpatialObjects = spatialObjects;
                NumActiveNodes = newBackBuffer.NumActiveNodes;
                FrontBuffer = BackBuffer; // move backbuffer to front
                BackBuffer = newBackBuffer;
            }
        }
    }
}
