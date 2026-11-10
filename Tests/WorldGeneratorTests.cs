using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace TopDownWorldGen.Tests
{
    public class WorldGeneratorTests
    {
        private readonly List<Object> _created = new List<Object>();

        private T Make<T>() where T : ScriptableObject { var o = ScriptableObject.CreateInstance<T>(); _created.Add(o); return o; }

        private WorldGenerator Setup(int seed, out WorldGenerationSettings s)
        {
            var tree = new GameObject("TestTree"); _created.Add(tree);
            var veg = Make<VegetationData>();
            veg.category = VegetationCategory.Tree; veg.prefab = tree; veg.density = 0.15f; veg.footprint = new Vector2Int(2, 2); veg.minDistance = 2f;
            var biomeA = Make<BiomeData>(); biomeA.vegetation = new[] { veg };
            var biomeB = Make<BiomeData>(); biomeB.vegetation = new[] { veg };
            s = Make<WorldGenerationSettings>();
            s.width = 96; s.height = 96; s.seed = seed; s.biomes = new[] { biomeA, biomeB };
            var go = new GameObject("Gen"); _created.Add(go);
            var gen = go.AddComponent<WorldGenerator>();
            gen.settings = s;
            return gen;
        }

        [TearDown]
        public void Cleanup() { foreach (var o in _created) if (o != null) Object.DestroyImmediate(o); _created.Clear(); }

        [Test]
        public void SameSeed_ProducesIdenticalWorld()
        {
            WorldGenerationSettings s;
            var gen = Setup(777, out s);
            var a = gen.BuildMap(); var b = gen.BuildMap();
            CollectionAssert.AreEqual(a.terrain, b.terrain);
            CollectionAssert.AreEqual(a.biome, b.biome);
            CollectionAssert.AreEqual(a.cell, b.cell);
            Assert.AreEqual(a.objects.Count, b.objects.Count);
            for (int i = 0; i < a.objects.Count; i++) Assert.AreEqual(a.objects[i].center, b.objects[i].center);
        }

        [Test]
        public void DifferentSeed_ProducesDifferentWorld()
        {
            WorldGenerationSettings s;
            var gen = Setup(1, out s);
            var a = gen.BuildMap();
            s.seed = 2;
            var b = gen.BuildMap();
            CollectionAssert.AreNotEqual(a.terrain, b.terrain);
        }

        [Test]
        public void ExitIsAlwaysReachable_AcrossManySeeds()
        {
            WorldGenerationSettings s;
            var gen = Setup(1, out s);
            s.groundDensity = 0.5f; // lots of water
            for (int seed = 1; seed <= 12; seed++)
            {
                s.seed = seed * 101;
                var map = gen.BuildMap();
                Assert.IsTrue(WalkabilityManager.Validate(map).Success, "Seed " + s.seed + " has unreachable exit");
            }
        }

        [Test]
        public void SolidObjects_NeverOverlap_NorTouchPath()
        {
            WorldGenerationSettings s;
            var gen = Setup(4242, out s);
            var map = gen.BuildMap();
            var used = new HashSet<int>();
            foreach (var o in map.objects)
            {
                for (int fy = 0; fy < o.size.y; fy++)
                    for (int fx = 0; fx < o.size.x; fx++)
                    {
                        int i = (o.cell.y + fy) * map.width + o.cell.x + fx;
                        Assert.IsTrue(used.Add(i), "Overlapping footprint at " + (o.cell + new Vector2Int(fx, fy)));
                        Assert.AreNotEqual((byte)TerrainType.Path, map.terrain[i], "Object on path");
                    }
            }
        }

        [Test]
        public void InvalidSettings_ReturnsNullWithoutThrowing()
        {
            WorldGenerationSettings s;
            var gen = Setup(1, out s);
            s.biomes = new BiomeData[0];
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*"));
            Assert.IsNull(gen.BuildMap());
        }
    }
}
