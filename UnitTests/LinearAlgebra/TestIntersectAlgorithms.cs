﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using Ship_Game;

namespace UnitTests.LinearAlgebra
{
    [TestClass]
    public class TestIntersectAlgorithms
    {
        [TestMethod]
        public void RayCircleIntersects()
        {
            var center = new Vector2(0, 0);

            // Intersect through the circle from OUTSIDE, DIAGONALLY
            // a \
            //   ( )
            //     \ b
            var start = new Vector2(-30, -20);
            var end   = new Vector2(+30, +20);
            Assert.IsTrue(center.RayCircleIntersect(30f, start, end, out float intersect));
            Assert.AreEqual(6.05f, intersect, 0.01f);

            // Intersect through the circle from OUTSIDE, HORIZONTALLY
            // a ---|  |--> b
            start = new Vector2(-20, 0);
            end   = new Vector2(+20, 0);
            Assert.IsTrue(center.RayCircleIntersect(20f, start, end, out intersect));
            Assert.AreEqual(0f, intersect); // we are perfectly touching the edge
            Assert.IsTrue(center.RayCircleIntersect(10f, start, end, out intersect));
            Assert.AreEqual(10f, intersect);

            // Intersect while inside the circe, horizontally
            // | --> |
            start = new Vector2(-20, 0);
            end   = new Vector2(+20, 0);
            Assert.IsTrue(center.RayCircleIntersect(40f, start, end, out intersect));
            Assert.AreEqual(20f, intersect); // from the edge of the circle

            // Intersect STARTS from inside the circle, horizontally
            // ---|-->o   |
            start = new Vector2(-30, 0);
            end   = new Vector2(0, 0);
            Assert.IsTrue(center.RayCircleIntersect(20f, start, end, out intersect));
            Assert.AreEqual(10f, intersect);

            // Intersect STARTS from inside the circle, horizontally
            // |   o  -|--->
            start = new Vector2(10, 0);
            end   = new Vector2(40, 0);
            Assert.IsTrue(center.RayCircleIntersect(20f, start, end, out intersect));
            Assert.AreEqual(10f, intersect);
        }

        [TestMethod]
        public void RayCircleNoIntersection()
        {
            var center = new Vector2(0, 0);

            // NO intersect, we are outside of the circle, horizontally
            // |    |  ---->
            var start = new Vector2(21, 0);
            var end   = new Vector2(50, 0);
            Assert.IsFalse(center.RayCircleIntersect(20f, start, end, out float intersect));
            Assert.AreEqual(float.NaN, intersect); // distance is 20 from the edge of the circle

            // NO intersect, we are outside of the circle, horizontally
            // ---->  |  o  |
            start = new Vector2(-80, 0);
            end   = new Vector2(-40, 0);
            Assert.IsFalse(center.RayCircleIntersect(20f, start, end, out intersect));
            Assert.AreEqual(float.NaN, intersect); // distance is 20 from the edge of the circle

            // From edge to center
            // |-->o   |
            start = new Vector2(-20, 0);
            end   = new Vector2(0, 0);
            Assert.IsTrue(center.RayCircleIntersect(20f, start, end, out intersect));
            Assert.AreEqual(0f, intersect); // distance is 20 from the edge of the circle

            // From edge outwards
            // |  *  |---->
            start = new Vector2(20, 0);
            end   = new Vector2(40, 0);
            Assert.IsTrue(center.RayCircleIntersect(20f, start, end, out intersect));
            Assert.AreEqual(0f, intersect); // distance is 20 from the edge of the circle

            // Trying to get determinant to 0
            // | -*>  |
            start = new Vector2(-4.0f, 0);
            end   = new Vector2(+4.0f, 0);
            Assert.IsTrue(center.RayCircleIntersect(4f, start, end, out intersect));
            Assert.AreEqual(0f, intersect); // distance is 20 from the edge of the circle
        }

        [TestMethod]
        public void ClosestPointOnLine()
        {
            // Horizontal line, perfectly balanced for an easy picking
            //   o
            // --x->
            var start = new Vector2(-10, 10);
            var end   = new Vector2(+10, 10);
            Vector2 point = Vector2.Zero.FindClosestPointOnLine(start, end);
            Assert.That.Equal(0.001f, new Vector2(0, 10), point);

            // Horizontal line with start point as the closest
            //   o
            //   x--->
            start = new Vector2(0, 10);
            end   = new Vector2(10, 10);
            point = Vector2.Zero.FindClosestPointOnLine(start, end);
            Assert.That.Equal(0.001f, start, point);

            // Horizontal line with start point as the closest
            //   o  x--->
            start = new Vector2(10, 0);
            end   = new Vector2(20, 0);
            point = Vector2.Zero.FindClosestPointOnLine(start, end);
            Assert.That.Equal(0.001f, start, point);
        }
    }
}
