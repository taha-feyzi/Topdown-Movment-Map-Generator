using UnityEngine;

namespace TopDownWorldGen
{
    /// <summary>Water (noise + cellular automata smoothing) and dirt patches.</summary>
    public static class TerrainGenerator
    {
        public static void Generate(WorldMap map, WorldGenerationSettings s, int seed)
        {
            int w = map.width, h = map.height, n = w * h;
            var hn = new SeededNoise(seed);
            var dn = new SeededNoise(seed + 4242);
            var heights = new float[n];
            var dirtVals = new float[n];
            float dScale = Mathf.Max(1f, s.dirtScale);

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    var b = map.biomes[map.biome[i]];
                    float sc = Mathf.Max(1f, s.noiseScale * Mathf.Max(0.05f, b.noiseScaleMultiplier));
                    heights[i] = hn.Fractal(x / sc, y / sc, s.noiseOctaves, s.persistence, s.lacunarity);
                    dirtVals[i] = dn.Fractal(x / dScale, y / dScale, 2, 0.5f, 2f);
                }

            float waterThr = Quantile(heights, 1f - s.groundDensity);
            var water = new bool[n];
            for (int i = 0; i < n; i++)
                water[i] = heights[i] < waterThr + map.biomes[map.biome[i]].waterBias;

            // Cellular automata smoothing -> organic shorelines. Out-of-bounds counts as land.
            for (int it = 0; it < s.caIterations; it++)
            {
                var next = new bool[n];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int c = 0;
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                if (dx == 0 && dy == 0) continue;
                                int nx = x + dx, ny = y + dy;
                                if (nx >= 0 && ny >= 0 && nx < w && ny < h && water[ny * w + nx]) c++;
                            }
                        int i = y * w + x;
                        next[i] = c >= 5 ? true : (c <= 3 ? false : water[i]);
                    }
                water = next;
            }

            float dirtThr = Quantile(dirtVals, 1f - s.dirtDensity);
            for (int i = 0; i < n; i++)
            {
                if (water[i]) { map.terrain[i] = (byte)TerrainType.Water; map.cell[i] = (byte)CellType.Blocked; }
                else
                {
                    bool dirt = s.dirtDensity > 0f && dirtVals[i] > dirtThr - map.biomes[map.biome[i]].dirtBias;
                    map.terrain[i] = (byte)(dirt ? TerrainType.Dirt : TerrainType.Ground);
                    map.cell[i] = (byte)CellType.Walkable;
                }
            }
        }

        private static float Quantile(float[] values, float q)
        {
            var copy = (float[])values.Clone();
            System.Array.Sort(copy);
            int idx = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(q) * (copy.Length - 1)), 0, copy.Length - 1);
            return copy[idx];
        }
    }
}
