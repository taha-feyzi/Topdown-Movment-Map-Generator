using System.Collections.Generic;
using UnityEngine;

namespace TopDownWorldGen
{
    /// <summary>
    /// Cluster-based vegetation placement. Per category (trees first) every cell is visited in a seeded shuffled order,
    /// an entry is picked by weight, then accepted by density x cluster x proximity rules, footprint/terrain/path checks
    /// and minimum distance.
    /// </summary>
    public static class VegetationGenerator
    {
        private sealed class Item { public VegetationData data; public bool special; }

        private static readonly VegetationCategory[] Order =
        {
            VegetationCategory.Tree, VegetationCategory.Rock, VegetationCategory.FallenTree, VegetationCategory.Bush,
            VegetationCategory.DeadVegetation, VegetationCategory.Grass, VegetationCategory.Flower,
            VegetationCategory.Mushroom, VegetationCategory.Decoration
        };

        public static bool IsSolid(VegetationCategory c)
        {
            return c == VegetationCategory.Tree || c == VegetationCategory.Rock || c == VegetationCategory.FallenTree
                || c == VegetationCategory.Bush || c == VegetationCategory.DeadVegetation;
        }

        private static float CategoryMultiplier(WorldGenerationSettings s, VegetationCategory c)
        {
            switch (c)
            {
                case VegetationCategory.Tree:
                case VegetationCategory.FallenTree: return s.treeDensity;
                case VegetationCategory.Bush:
                case VegetationCategory.DeadVegetation: return s.bushDensity;
                case VegetationCategory.Rock: return s.rockDensity;
                default: return s.decorationDensity;
            }
        }

        public static void Generate(WorldMap map, WorldGenerationSettings s, int seed)
        {
            int w = map.width, h = map.height, n = w * h, nb = map.biomes.Length;
            int catCount = System.Enum.GetValues(typeof(VegetationCategory)).Length;
            var rng = new System.Random(seed);

            var lists = new List<Item>[nb][];
            for (int b = 0; b < nb; b++)
            {
                lists[b] = new List<Item>[catCount];
                AddRange(map, lists, b, map.biomes[b].vegetation, false);
                AddRange(map, lists, b, map.biomes[b].specialObjects, true);
            }

            var order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;
            for (int i = n - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int t = order[i]; order[i] = order[j]; order[j] = t;
            }

            var anchors = new List<Vector2Int> { map.start };
            anchors.AddRange(map.exits);
            float safeSq = s.safeZoneRadius * s.safeZoneRadius;
            var hash = new SpatialHash(8f);
            int placedTotal = 0;

            foreach (var cat in Order)
            {
                int c = (int)cat;
                bool any = false;
                for (int b = 0; b < nb; b++) if (lists[b][c] != null && lists[b][c].Count > 0) any = true;
                if (!any) continue;

                var noise = new SeededNoise(seed + 101 * (c + 1));
                bool solid = IsSolid(cat);
                float catMult = CategoryMultiplier(s, cat);

                for (int k = 0; k < n; k++)
                {
                    if (map.objects.Count >= s.maxObjects) { map.log.Add("Vegetation: maxObjects reached."); return; }
                    int idx = order[k];
                    var list = lists[map.biome[idx]][c];
                    if (list == null || list.Count == 0) continue;

                    int x = idx % w, y = idx / w;
                    Item item = PickWeighted(list, rng);
                    VegetationData v = item.data;
                    var biome = map.biomes[map.biome[idx]];

                    float p = v.density * catMult * biome.vegetationDensity;
                    if (p <= 0f) continue;
                    if (v.clusterStrength > 0f)
                    {
                        float cn = noise.Fractal(x / v.clusterScale, y / v.clusterScale, 2, 0.5f, 2f);
                        float factor = Mathf.Clamp01((cn - 0.42f) / 0.22f);
                        p *= Mathf.Lerp(1f, factor, v.clusterStrength);
                    }
                    if (v.preferNearCategory)
                        p *= HasNear(map, x, y, (byte)((int)v.nearCategory + 1), 3) ? v.nearBonus : 1f / Mathf.Max(1f, v.nearBonus);
                    if ((float)rng.NextDouble() >= p) continue;

                    Vector2Int fp = new Vector2Int(Mathf.Max(1, v.footprint.x), Mathf.Max(1, v.footprint.y));
                    if (!FootprintFree(map, x, y, fp, v, solid)) continue;

                    Vector2 centre = new Vector2(x + fp.x * 0.5f, y + fp.y * 0.5f);
                    if (solid)
                    {
                        bool protectedZone = false;
                        for (int a = 0; a < anchors.Count; a++)
                        {
                            float dx = centre.x - anchors[a].x, dy = centre.y - anchors[a].y;
                            if (dx * dx + dy * dy < safeSq) { protectedZone = true; break; }
                        }
                        if (protectedZone) continue;
                    }
                    float md = v.minDistance * s.objectSpacing;
                    if (md > 0f && !hash.IsFree(centre.x, centre.y, md)) continue;

                    float sc = Mathf.Lerp(v.minScale, Mathf.Max(v.minScale, v.maxScale), (float)rng.NextDouble());
                    Vector3 scale = new Vector3(sc, sc, 1f);
                    float rot = 0f;
                    switch (v.rotation)
                    {
                        case RotationMode.RandomZ: rot = (float)rng.NextDouble() * 360f; break;
                        case RotationMode.Random90: rot = rng.Next(4) * 90f; break;
                        case RotationMode.FlipX: if (rng.NextDouble() < 0.5) scale.x = -scale.x; break;
                    }

                    for (int fy = 0; fy < fp.y; fy++)
                        for (int fx = 0; fx < fp.x; fx++)
                        {
                            int ci = (y + fy) * w + x + fx;
                            if (solid || v.blocksMovement) map.solidOcc[ci] = true; else map.decorOcc[ci] = true;
                            if (!solid) map.decorOcc[ci] = true;
                            map.occCat[ci] = (byte)(c + 1);
                            if (v.blocksMovement) map.cell[ci] = (byte)CellType.Blocked;
                        }
                    if (md > 0f) hash.Add(centre.x, centre.y, md);

                    map.objects.Add(new PlacedObject
                    {
                        data = v,
                        layer = item.special ? GeneratedLayer.Special : GeneratedLayer.Vegetation,
                        prefab = v.prefab,
                        tile = v.prefab == null ? v.tile : null,
                        tileOnObstacleLayer = v.blocksMovement,
                        cell = new Vector2Int(x, y),
                        size = fp,
                        center = centre,
                        scale = scale,
                        rotationZ = rot,
                        sortingOrder = (v.ySort ? -Mathf.RoundToInt(centre.y * 10f) : 0) + v.sortingOrder,
                        sortingLayer = v.sortingLayer,
                        blocksMovement = v.blocksMovement,
                        category = cat,
                        minDistance = md,
                        addCollider = v.addColliderIfMissing && v.blocksMovement,
                        colliderRadius = v.colliderRadius
                    });
                    placedTotal++;
                }
            }
            map.log.Add("Vegetation: " + placedTotal + " objects.");
        }

        private static void AddRange(WorldMap map, List<Item>[][] lists, int b, VegetationData[] arr, bool special)
        {
            if (arr == null) return;
            foreach (var v in arr)
            {
                if (v == null) continue;
                if (v.prefab == null && v.tile == null)
                {
                    map.log.Add("Vegetation '" + v.name + "' has neither prefab nor tile - skipped.");
                    continue;
                }
                if (v.allowedBiomes != null && v.allowedBiomes.Length > 0 &&
                    System.Array.IndexOf(v.allowedBiomes, map.biomes[b]) < 0) continue;
                int c = (int)v.category;
                if (lists[b][c] == null) lists[b][c] = new List<Item>();
                lists[b][c].Add(new Item { data = v, special = special });
            }
        }

        private static Item PickWeighted(List<Item> list, System.Random rng)
        {
            if (list.Count == 1) return list[0];
            float total = 0f;
            for (int i = 0; i < list.Count; i++) total += Mathf.Max(0f, list[i].data.weight);
            if (total <= 0f) return list[0];
            float t = (float)rng.NextDouble() * total;
            for (int i = 0; i < list.Count; i++)
            {
                t -= Mathf.Max(0f, list[i].data.weight);
                if (t <= 0f) return list[i];
            }
            return list[list.Count - 1];
        }

        private static bool HasNear(WorldMap map, int x, int y, byte catId, int r)
        {
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (map.InBounds(nx, ny) && map.occCat[ny * map.width + nx] == catId) return true;
                }
            return false;
        }

        private static bool FootprintFree(WorldMap map, int x, int y, Vector2Int fp, VegetationData v, bool solid)
        {
            for (int fy = 0; fy < fp.y; fy++)
                for (int fx = 0; fx < fp.x; fx++)
                {
                    int cx = x + fx, cy = y + fy;
                    if (!map.InBounds(cx, cy)) return false;
                    int i = cy * map.width + cx;
                    var t = (TerrainType)map.terrain[i];
                    if (t == TerrainType.Path) return false;
                    if (t == TerrainType.Water && !v.allowOnWater) return false;
                    if (t == TerrainType.Ground && !v.allowOnGround) return false;
                    if (t == TerrainType.Dirt && !v.allowOnDirt) return false;
                    if (map.pathDistance[i] <= v.minPathDistance) return false;
                    if (map.solidOcc[i]) return false;
                    if (!solid && map.decorOcc[i]) return false;
                }
            return true;
        }
    }
}
