using UnityEngine;

namespace TopDownWorldGen
{
    /// <summary>Assigns a biome index to every cell using warped fractal noise and exact weight quantiles.</summary>
    public static class BiomeGenerator
    {
        public static void Generate(WorldMap map, WorldGenerationSettings s, int seed)
        {
            int w = map.width, h = map.height, n = w * h;
            int nb = map.biomes.Length;
            if (nb <= 1) return; // array is zero-initialised -> biome 0

            var noise = new SeededNoise(seed);
            var warpX = new SeededNoise(seed + 7919);
            var warpY = new SeededNoise(seed + 104729);
            float scale = Mathf.Max(4f, s.biomeScale);
            var values = new float[n];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float wx = x + (warpX.Sample(x * 0.05f + 31.7f, y * 0.05f + 12.3f) - 0.5f) * 2f * s.biomeWarp;
                    float wy = y + (warpY.Sample(x * 0.05f + 77.1f, y * 0.05f + 45.9f) - 0.5f) * 2f * s.biomeWarp;
                    values[y * w + x] = noise.Fractal(wx / scale, wy / scale, 3, 0.5f, 2f);
                }

            var sorted = (float[])values.Clone();
            System.Array.Sort(sorted);

            float totalWeight = 0f;
            for (int b = 0; b < nb; b++) totalWeight += Mathf.Max(0f, map.biomes[b].distributionWeight);
            var thresholds = new float[nb];
            float cum = 0f;
            for (int b = 0; b < nb; b++)
            {
                cum += totalWeight > 0f ? Mathf.Max(0f, map.biomes[b].distributionWeight) / totalWeight : 1f / nb;
                int idx = Mathf.Clamp(Mathf.RoundToInt(cum * (n - 1)), 0, n - 1);
                thresholds[b] = sorted[idx];
            }
            thresholds[nb - 1] = float.MaxValue;

            for (int i = 0; i < n; i++)
            {
                int b = 0;
                while (b < nb - 1 && values[i] > thresholds[b]) b++;
                map.biome[i] = (byte)b;
            }
        }
    }
}
