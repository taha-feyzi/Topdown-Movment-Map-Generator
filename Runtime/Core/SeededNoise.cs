using UnityEngine;

namespace TopDownWorldGen
{
    /// <summary>
    /// Deterministic, platform-independent Perlin noise (own permutation table, no UnityEngine.Mathf.PerlinNoise).
    /// Output range is 0..1.
    /// </summary>
    public sealed class SeededNoise
    {
        private readonly int[] _p = new int[512];

        public SeededNoise(int seed)
        {
            var rng = new System.Random(seed);
            var perm = new int[256];
            for (int i = 0; i < 256; i++) perm[i] = i;
            for (int i = 255; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int t = perm[i]; perm[i] = perm[j]; perm[j] = t;
            }
            for (int i = 0; i < 512; i++) _p[i] = perm[i & 255];
        }

        private static float Fade(float t) { return t * t * t * (t * (t * 6f - 15f) + 10f); }
        private static float Lerp(float a, float b, float t) { return a + (b - a) * t; }

        private static float Grad(int h, float x, float y)
        {
            switch (h & 7)
            {
                case 0: return x + y;
                case 1: return -x + y;
                case 2: return x - y;
                case 3: return -x - y;
                case 4: return x;
                case 5: return -x;
                case 6: return y;
                default: return -y;
            }
        }

        /// <summary>Single octave Perlin noise, 0..1.</summary>
        public float Sample(float x, float y)
        {
            int fx = Mathf.FloorToInt(x), fy = Mathf.FloorToInt(y);
            int xi = fx & 255, yi = fy & 255;
            float xf = x - fx, yf = y - fy;
            float u = Fade(xf), v = Fade(yf);
            int aa = _p[_p[xi] + yi], ab = _p[_p[xi] + yi + 1];
            int ba = _p[_p[xi + 1] + yi], bb = _p[_p[xi + 1] + yi + 1];
            float x1 = Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1f, yf), u);
            float x2 = Lerp(Grad(ab, xf, yf - 1f), Grad(bb, xf - 1f, yf - 1f), u);
            return Mathf.Clamp01(Lerp(x1, x2, v) * 0.5f + 0.5f);
        }

        /// <summary>Fractal (fBm) noise normalised to 0..1.</summary>
        public float Fractal(float x, float y, int octaves, float persistence, float lacunarity)
        {
            float amp = 1f, freq = 1f, sum = 0f, norm = 0f;
            octaves = Mathf.Max(1, octaves);
            for (int o = 0; o < octaves; o++)
            {
                sum += Sample(x * freq + o * 37.31f, y * freq + o * 91.17f) * amp;
                norm += amp;
                amp *= persistence;
                freq *= lacunarity;
            }
            return sum / norm;
        }
    }

    /// <summary>Stateless integer hashing so per-cell randomness does not depend on iteration order.</summary>
    public static class WorldHash
    {
        public static uint Hash(int seed, int x, int y, int salt)
        {
            unchecked
            {
                uint h = (uint)seed * 747796405u + 2891336453u;
                h ^= (uint)x * 0x9E3779B1u;
                h = (h << 13) | (h >> 19);
                h *= 0x85EBCA6Bu;
                h ^= (uint)y * 0xC2B2AE35u;
                h = (h << 15) | (h >> 17);
                h *= 0x27D4EB2Fu;
                h ^= (uint)salt * 0x165667B1u;
                h ^= h >> 15; h *= 0x2C1B3C6Du;
                h ^= h >> 12; h *= 0x297A2D39u;
                h ^= h >> 15;
                return h;
            }
        }

        /// <summary>Deterministic float in [0,1).</summary>
        public static float Rand01(int seed, int x, int y, int salt)
        {
            return (Hash(seed, x, y, salt) >> 8) * (1f / 16777216f);
        }

        /// <summary>Derives an independent seed for each generation stage.</summary>
        public static int StageSeed(int seed, int stage)
        {
            unchecked { return seed * 73856093 ^ (stage + 1) * 19349663; }
        }
    }
}
