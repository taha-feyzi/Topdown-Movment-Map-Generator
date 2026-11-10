using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TopDownWorldGen
{
    /// <summary>One object decided by the generators; instantiated later by <see cref="WorldGenerator"/>.</summary>
    public sealed class PlacedObject
    {
        public ScriptableObject data;
        public GeneratedLayer layer;
        public GameObject prefab;
        public TileBase tile;
        public bool tileOnObstacleLayer;
        public Vector2Int cell;
        public Vector2Int size = Vector2Int.one;
        /// <summary>Footprint centre in cell units.</summary>
        public Vector2 center;
        public Vector3 scale = Vector3.one;
        public float rotationZ;
        public int sortingOrder;
        public string sortingLayer;
        public bool blocksMovement;
        public VegetationCategory category;
        public float minDistance;
        public bool addCollider;
        public float colliderRadius = 0.4f;
    }

    /// <summary>Pure-data result of a generation run. No Unity scene objects are touched while building it.</summary>
    public sealed class WorldMap
    {
        public const int PathDistanceCap = 64;

        public readonly int width, height, seed;
        public readonly BiomeData[] biomes;
        public readonly byte[] biome;
        public readonly byte[] terrain;
        public readonly byte[] cell;
        public readonly float[] darkness;
        public readonly bool[] corruption;
        public readonly bool[] safeZone;
        public readonly bool[] solidOcc;
        public readonly bool[] decorOcc;
        public readonly byte[] occCat;
        public int[] pathDistance;

        public Vector2Int start;
        public readonly List<Vector2Int> exits = new List<Vector2Int>();
        public readonly List<Vector2Int> mainPath = new List<Vector2Int>();
        public readonly List<PlacedObject> objects = new List<PlacedObject>();
        public readonly List<string> log = new List<string>();

        public WorldMap(int width, int height, int seed, BiomeData[] biomes)
        {
            this.width = width; this.height = height; this.seed = seed;
            this.biomes = biomes ?? new BiomeData[0];
            int n = width * height;
            biome = new byte[n]; terrain = new byte[n]; cell = new byte[n];
            darkness = new float[n]; corruption = new bool[n]; safeZone = new bool[n];
            solidOcc = new bool[n]; decorOcc = new bool[n]; occCat = new byte[n];
            pathDistance = new int[n];
        }

        public int Index(int x, int y) { return y * width + x; }
        public bool InBounds(int x, int y) { return (uint)x < (uint)width && (uint)y < (uint)height; }

        /// <summary>Multi-source BFS (4-neighbour) distance to the nearest path cell, capped at <see cref="PathDistanceCap"/>.</summary>
        public void ComputePathDistance()
        {
            int n = width * height;
            if (pathDistance == null || pathDistance.Length != n) pathDistance = new int[n];
            var queue = new int[n];
            int head = 0, tail = 0;
            for (int i = 0; i < n; i++)
            {
                if (terrain[i] == (byte)TerrainType.Path) { pathDistance[i] = 0; queue[tail++] = i; }
                else pathDistance[i] = PathDistanceCap;
            }
            while (head < tail)
            {
                int i = queue[head++];
                int x = i % width, y = i / width;
                int d = pathDistance[i] + 1;
                if (d >= PathDistanceCap) continue;
                if (x > 0 && pathDistance[i - 1] > d) { pathDistance[i - 1] = d; queue[tail++] = i - 1; }
                if (x < width - 1 && pathDistance[i + 1] > d) { pathDistance[i + 1] = d; queue[tail++] = i + 1; }
                if (y > 0 && pathDistance[i - width] > d) { pathDistance[i - width] = d; queue[tail++] = i - width; }
                if (y < height - 1 && pathDistance[i + width] > d) { pathDistance[i + width] = d; queue[tail++] = i + width; }
            }
        }
    }
}
