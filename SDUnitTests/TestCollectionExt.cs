﻿using System.Collections.Generic;
using NUnit.Framework;
using Ship_Game;

namespace SDUnitTests
{
    [TestFixture]
    public class TestCollectionExt
    {
        [Test]
        public void TestIndexOf()
        {
            IReadOnlyList<string> ilist = new Array<string>
            {
                "hello", "world", "ilist", "indexof", "etc"
            };

            Assert.AreEqual(0, ilist.IndexOf("hello"));
            Assert.AreEqual(1, ilist.IndexOf("world"));
            Assert.AreEqual(2, ilist.IndexOf("ilist"));
            Assert.AreEqual(3, ilist.IndexOf("indexof"));
            Assert.AreEqual(4, ilist.IndexOf("etc"));
        }

        [Test]
        public void TestFindMinMax()
        {
            var list = new Array<string> { "a", "bb", "ccc", "dddd", "55555", "666666"};

            Assert.AreEqual("666666", list.FindMax(s => s.Length));
            Assert.AreEqual("a", list.FindMin(s => s.Length));

            Assert.AreEqual("dddd", list.FindMaxFiltered(s => s.Length < 5, s => s.Length));
            Assert.AreEqual("ccc", list.FindMinFiltered(s => s.Length > 2, s => s.Length));
        }

        [Test]
        public void TestAny()
        {
            var list = new Array<string> { "a", "bb", "ccc", "dddd", "55555", "666666" };

            Assert.IsTrue(list.Any(s => s == "55555"));
            Assert.IsTrue(list.Any(s => s.Length > 5));
            Assert.IsFalse(list.Any(s => s == "not in list"));
            Assert.IsFalse(list.Any(s => s.Length > 10));
        }

        [Test]
        public void TestCount()
        {
            var list = new Array<string> { "a", "bb", "ccc", "dddd", "55555", "666666" };

            Assert.AreEqual(3, list.Count(s => s.Length % 2 == 0));
            Assert.AreEqual(6, list.Count(s => true));
            Assert.AreEqual(1, list.Count(s => s == "ccc"));
        }



        [Test]
        public void TestUniqueExclude()
        {
            string[] setA = { "E", "F", "G", "H", "A", "B", "C", "D" };
            string[] setB = { "G", "D", "A", "H" };
            // unique exclude is unstable, so the resulting order will be scrambled
            string[] excluded = setA.UniqueExclude(setB);

            Assert.AreEqual(4, excluded.Length, "Expected exclusion length doesn't match expected");
            Assert.Contains("B", excluded); // unstable ordering
            Assert.Contains("C", excluded);
            Assert.Contains("E", excluded);
            Assert.Contains("F", excluded);
            //Assert.AreEqual(new[] { "E", "F", "B", "C"}, excluded, "Invalid exclusion result");

            string[] empty1 = { };
            string[] excludedEmpty = empty1.UniqueExclude(setB);
            Assert.AreEqual(0, excludedEmpty.Length, "Empty exclusion should be an empty array");

            string[] excludeNothing = setA.UniqueExclude(empty1);
            Assert.AreEqual(setA, excludeNothing, "Excluding with empty should be equal to original");
        }
    }
}
