using System.Collections.Generic;
using UnityEngine;

namespace TopDownWorldGen
{
    /// <summary>
    /// Produces a per-cell darkness map (0 = fully lit, 1 = darkest), safe zones, corruption cells and light-source
    /// objects. It does NOT implement lighting: light sources are your own prefabs (e.g. with URP Light2D), and the
    /// darkness map is data your own lighting or gameplay scripts can query through <see cref="WorldGenerator.Map"/>.
    /// </summary>
    public static class DarknessGenerator
    {
        public static void Generate(WorldMap map, WorldGenerationSettings s, int seed)
        {
            int w = map.width, h = map.height, n = w * h;
            var rng = new System.Random(seed);
            var noise = new SeededNoise(seed);
            var corrNoise = new SeededNoise(seed + 1);

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    var b = map.biomes[map.biome[i]];
                    float d = b.darknessIntensity * s.darknessMultiplier;
                    float nv = noise.Fractal(x / Mathf.Max(1f, s.darknessScale), y / Mathf.Max(1f, s.darknessScale), 3, 0.5f, 2f);
                    d += (nv - 0.5f) * 2f * s.darknessVariation;
                    map.darkness[i] = Mathf.Clamp01(d);
                }

            // Safe zones: start, exits and POIs with a safe radius.
            Safe(map, map.start.x, map.start.y, s.safeZoneRadius);
            foreach (var e in map.exits) Safe(map, e.x, e.y, s.safeZoneRadius);
            foreach (var o in map.objects)
            {
                var poi = o.data as POIData;
                if (poi != null && poi.safeZoneRadius > 0f) Safe(map, Mathf.RoundToInt(o.center.x), Mathf.RoundToInt(o.center.y), poi.safeZoneRadius);
            }

            // Corruption.
            int corrupted = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    float cd = s.corruptionDensity * map.biomes[map.biome[i]].corruptionDensity;
                    if (cd <= 0f || map.safeZone[i]) continue;
                    var t = (TerrainType)map.terrain[i];
                    if (t == TerrainType.Water || t == TerrainType.Path) continue;
                    float cn = corrNoise.Fractal(x / Mathf.Max(1f, s.corruptionScale), y / Mathf.Max(1f, s.corruptionScale), 2, 0.5f, 2f);
                    if (cn > Mathf.Lerp(0.8f, 0.45f, Mathf.Clamp01(cd)))
                    {
                        map.corruption[i] = true;
                        map.darkness[i] = Mathf.Clamp01(map.darkness[i] + 0.2f);
                        if (map.cell[i] == (byte)CellType.Walkable) map.cell[i] = (byte)CellType.Dangerous;
                        corrupted++;
                    }
                }

            // Lights.
            var hash = new SpatialHash(8f);
            int lights = 0;

            var anchors = new List<Vector2Int> { map.start };
            anchors.AddRange(map.exits);
            foreach (var a in anchors)
            {
                int x, y;
                if (FindFree(map, a.x, a.y, 2, 4, out x, out y)) { if (PlaceLight(map, rng, x, y, hash, 0f)) lights++; }
            }

            if (s.pathLightSpacing > 0)
                for (int k = s.pathLightSpacing; k < map.mainPath.Count; k += s.pathLightSpacing)
                {
                    int x, y;
                    if (FindFree(map, map.mainPath[k].x, map.mainPath[k].y, 1, 3, out x, out y))
                        if (PlaceLight(map, rng, x, y, hash, s.lightMinDistance * 0.5f)) lights++;
                }

            int target = Mathf.RoundToInt(n * s.lightDensity);
            int attempts = target * 20;
            int placed = 0;
            for (int a = 0; a < attempts && placed < target; a++)
            {
                int x = rng.Next(1, w - 1), y = rng.Next(1, h - 1);
                int i = y * w + x;
                var b = map.biomes[map.biome[i]];
                if ((float)rng.NextDouble() > Mathf.Clamp01(b.lightDensityMultiplier * (0.4f + map.darkness[i]))) continue;
                var t = (TerrainType)map.terrain[i];
                if (t == TerrainType.Water || t == TerrainType.Path) continue;
                if (map.solidOcc[i] || map.pathDistance[i] < 1) continue;
                if (PlaceLight(map, rng, x, y, hash, s.lightMinDistance)) { placed++; lights++; }
            }
            map.log.Add("Darkness: " + lights + " light sources, " + corrupted + " corrupted cells.");
        }

        private static void Safe(WorldMap map, int cx, int cy, float radius)
        {
            if (radius <= 0f) return;
            int r = Mathf.CeilToInt(radius);
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int x = cx + dx, y = cy + dy;
                    if (!map.InBounds(x, y)) continue;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / radius;
                    if (d >= 1f) continue;
                    int i = y * map.width + x;
                    map.safeZone[i] = true;
                    map.darkness[i] *= Mathf.SmoothStep(0f, 1f, d);
                }
        }

        private static void Lighten(WorldMap map, int cx, int cy, float radius)
        {
            if (radius <= 0f) return;
            int r = Mathf.CeilToInt(radius);
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int x = cx + dx, y = cy + dy;
                    if (!map.InBounds(x, y)) continue;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / radius;
                    if (d >= 1f) continue;
                    map.darkness[y * map.width + x] *= Mathf.SmoothStep(0f, 1f, d);
                }
        }

        private static bool FindFree(WorldMap map, int cx, int cy, int minR, int maxR, out int ox, out int oy)
        {
            for (int r = minR; r <= maxR; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                        int x = cx + dx, y = cy + dy;
                        if (!map.InBounds(x, y)) continue;
                        int i = y * map.width + x;
                        var t = (TerrainType)map.terrain[i];
                        if (t == TerrainType.Water || t == TerrainType.Path) continue;
                        if (map.solidOcc[i] || map.pathDistance[i] < 1) continue;
                        ox = x; oy = y; return true;
                    }
            ox = oy = 0;
            return false;
        }

        private static bool PlaceLight(WorldMap map, System.Random rng, int x, int y, SpatialHash hash, float minDist)
        {
            int i = y * map.width + x;
            var biome = map.biomes[map.biome[i]];
            var entries = biome.lightSources;
            if (entries == null || entries.Length == 0) return false;
            float total = 0f;
            foreach (var e in entries) if (e != null && e.prefab != null) total += Mathf.Max(0f, e.weight);
            if (total <= 0f) return false;
            if (!hash.IsFree(x + 0.5f, y + 0.5f, minDist)) return false;

            float t = (float)rng.NextDouble() * total;
            LightSourceEntry pick = null;
            foreach (var e in entries)
            {
                if (e == null || e.prefab == null) continue;
                pick = e;
                t -= Mathf.Max(0f, e.weight);
                if (t <= 0f) break;
            }
            if (pick == null) return false;

            hash.Add(x + 0.5f, y + 0.5f, minDist);
            map.solidOcc[i] = true;
            map.objects.Add(new PlacedObject
            {
                layer = GeneratedLayer.Light,
                prefab = pick.prefab,
                cell = new Vector2Int(x, y),
                center = new Vector2(x + 0.5f, y + 0.5f),
                sortingOrder = -Mathf.RoundToInt((y + 0.5f) * 10f),
                blocksMovement = false
            });
            Lighten(map, x, y, pick.lightRadius);
            return true;
        }
    }
}
