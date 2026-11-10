using UnityEngine;

namespace TopDownWorldGen
{
    /// <summary>
    /// Places points of interest before vegetation. A POI never overlaps water, the path or another object,
    /// respects biome restrictions, path-distance limits, start/exit distance and spacing to other POIs.
    /// </summary>
    public static class POIGenerator
    {
        public static void Generate(WorldMap map, WorldGenerationSettings s, int seed)
        {
            if (s.pois == null || s.pois.Length == 0) return;
            int w = map.width, h = map.height;
            var rng = new System.Random(seed);
            var hash = new SpatialHash(8f);
            int total = 0;

            foreach (var poi in s.pois)
            {
                if (poi == null) continue;
                if (poi.prefabs == null || poi.prefabs.Length == 0 || HasNullPrefab(poi))
                {
                    map.log.Add("POI '" + poi.name + "' has no (valid) prefabs - skipped.");
                    continue;
                }
                Vector2Int fp = new Vector2Int(Mathf.Max(1, poi.footprint.x), Mathf.Max(1, poi.footprint.y));
                int baseCount = rng.Next(poi.minCount, Mathf.Max(poi.minCount, poi.maxCount) + 1);
                int count = Mathf.RoundToInt(baseCount * s.poiDensity);
                int placed = 0;

                for (int n = 0; n < count; n++)
                {
                    if (n >= poi.minCount && rng.NextDouble() > poi.spawnProbability) continue;
                    bool ok = false;
                    for (int attempt = 0; attempt < 300 && !ok; attempt++)
                    {
                        int x = rng.Next(1, Mathf.Max(2, w - fp.x - 1));
                        int y = rng.Next(1, Mathf.Max(2, h - fp.y - 1));
                        Vector2 centre = new Vector2(x + fp.x * 0.5f, y + fp.y * 0.5f);
                        if (!Fits(map, poi, x, y, fp, centre, hash)) continue;

                        for (int fy = 0; fy < fp.y; fy++)
                            for (int fx = 0; fx < fp.x; fx++)
                            {
                                int i = (y + fy) * w + x + fx;
                                map.solidOcc[i] = true;
                                map.cell[i] = (byte)(poi.blocksMovement ? CellType.Blocked : poi.specialCellType);
                            }
                        hash.Add(centre.x, centre.y, poi.minDistanceFromOtherPOIs);
                        map.objects.Add(new PlacedObject
                        {
                            data = poi,
                            layer = GeneratedLayer.POI,
                            prefab = poi.prefabs[rng.Next(poi.prefabs.Length)],
                            cell = new Vector2Int(x, y),
                            size = fp,
                            center = centre,
                            sortingOrder = (poi.ySort ? -Mathf.RoundToInt(centre.y * 10f) : 0) + poi.sortingOrder,
                            sortingLayer = poi.sortingLayer,
                            blocksMovement = poi.blocksMovement,
                            minDistance = poi.minDistanceFromOtherPOIs
                        });
                        ok = true; placed++; total++;
                    }
                }
                if (placed < poi.minCount)
                    map.log.Add("POI '" + poi.name + "': only " + placed + "/" + poi.minCount + " required placed (map too small/crowded?).");
            }
            map.log.Add("POIs: " + total + " placed.");
        }

        private static bool HasNullPrefab(POIData poi)
        {
            foreach (var p in poi.prefabs) if (p == null) return true;
            return false;
        }

        private static bool Fits(WorldMap map, POIData poi, int x, int y, Vector2Int fp, Vector2 centre, SpatialHash hash)
        {
            int w = map.width;
            for (int fy = 0; fy < fp.y; fy++)
                for (int fx = 0; fx < fp.x; fx++)
                {
                    int i = (y + fy) * w + x + fx;
                    var t = (TerrainType)map.terrain[i];
                    if (t != TerrainType.Ground && t != TerrainType.Dirt) return false;
                    if (map.solidOcc[i]) return false;
                    if (map.pathDistance[i] < 1) return false;
                }
            int ci = Mathf.Clamp(Mathf.FloorToInt(centre.y), 0, map.height - 1) * w + Mathf.Clamp(Mathf.FloorToInt(centre.x), 0, w - 1);
            if (poi.allowedBiomes != null && poi.allowedBiomes.Length > 0 &&
                System.Array.IndexOf(poi.allowedBiomes, map.biomes[map.biome[ci]]) < 0) return false;
            int pd = map.pathDistance[ci];
            if (pd < poi.minPathDistance) return false;
            if (poi.maxPathDistance > 0 && pd > poi.maxPathDistance) return false;

            float minSq = poi.minDistanceFromStartAndExit * poi.minDistanceFromStartAndExit;
            if ((centre - (Vector2)map.start).sqrMagnitude < minSq) return false;
            foreach (var e in map.exits) if ((centre - (Vector2)e).sqrMagnitude < minSq) return false;

            return hash.IsFree(centre.x, centre.y, poi.minDistanceFromOtherPOIs);
        }
    }
}
