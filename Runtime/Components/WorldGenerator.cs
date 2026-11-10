using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TopDownWorldGen
{
    /// <summary>
    /// Orchestrates the whole pipeline.
    /// 1) <see cref="BuildMap"/> computes a pure-data <see cref="WorldMap"/> (deterministic per seed).
    /// 2) <see cref="ApplyRoutine"/> writes tiles (chunked) and instantiates objects.
    /// Every stage uses its own derived seed, so regenerating one stage does not change the others.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WorldTilemaps))]
    public sealed class WorldGenerator : MonoBehaviour
    {
        private const string PreviewName = "__WorldPreview";

        public WorldGenerationSettings settings;
        public WorldTilemaps tilemaps;

        [Header("Preview / gizmos")]
        public bool showBounds = true;
        public PreviewMode previewMode = PreviewMode.Terrain;

        /// <summary>Last generated or loaded map data (use it from gameplay scripts: walkability, darkness, start/exits).</summary>
        public WorldMap Map { get; private set; }
        public WalkabilityReport LastReport { get; private set; }

        private void Reset() { tilemaps = GetComponent<WorldTilemaps>(); }
        private void Awake() { if (tilemaps == null) tilemaps = GetComponent<WorldTilemaps>(); }

        public void SetMap(WorldMap map) { Map = map; }

        public static int NewRandomSeed()
        {
            return new System.Random(unchecked(System.Environment.TickCount * 31 + System.Guid.NewGuid().GetHashCode())).Next(1, int.MaxValue);
        }

        // ------------------------------------------------------------------------------------------------
        // Data generation
        // ------------------------------------------------------------------------------------------------

        /// <summary>Builds the complete world data without touching the scene. Returns null on invalid settings.</summary>
        public WorldMap BuildMap()
        {
            string err;
            if (settings == null) { Debug.LogError("[WorldGenerator] No WorldGenerationSettings assigned.", this); return null; }
            if (!settings.Validate(out err)) { Debug.LogError("[WorldGenerator] " + err, settings); return null; }

            int seed = settings.seed;
            var map = new WorldMap(settings.width, settings.height, seed, settings.biomes);
            BiomeGenerator.Generate(map, settings, WorldHash.StageSeed(seed, 0));
            TerrainGenerator.Generate(map, settings, WorldHash.StageSeed(seed, 1));
            PathGenerator.Generate(map, settings, WorldHash.StageSeed(seed, 2));
            map.ComputePathDistance();
            POIGenerator.Generate(map, settings, WorldHash.StageSeed(seed, 3));
            VegetationGenerator.Generate(map, settings, WorldHash.StageSeed(seed, 4));
            DarknessGenerator.Generate(map, settings, WorldHash.StageSeed(seed, 5));

            var report = WalkabilityManager.Validate(map);
            if (!report.Success)
            {
                int carved = WalkabilityManager.EnsureConnectivity(map);
                map.log.Add("Connectivity repaired (" + carved + " cells carved).");
                report = WalkabilityManager.Validate(map);
            }
            LastReport = report;
            map.log.Add(report.ToString());
            Map = map;
            return map;
        }

        // ------------------------------------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------------------------------------

        /// <summary>Synchronous full generation (editor or runtime).</summary>
        public void Generate() { Generate(GenerationStage.All); }

        public void Generate(GenerationStage stages) { Run(GenerateRoutine(stages, null)); }

        /// <summary>
        /// Generation as an enumerator. Run it with StartCoroutine for incremental (multi-frame) generation, or
        /// drain it synchronously. Progress callback receives (0..1, message).
        /// </summary>
        public IEnumerator GenerateRoutine(GenerationStage stages, System.Action<float, string> progress)
        {
            if (settings == null) { Debug.LogError("[WorldGenerator] No settings assigned.", this); yield break; }
            if (stages == GenerationStage.All && settings.randomSeed) settings.seed = NewRandomSeed();

            if (progress != null) progress(0f, "Building world data");
            var map = BuildMap();
            if (map == null) yield break;
            yield return null;

            var apply = ApplyRoutine(map, stages, progress);
            while (apply.MoveNext()) yield return null;

            foreach (var line in map.log) Debug.Log("[WorldGenerator] " + line, this);
            if (LastReport != null && !LastReport.Success)
                Debug.LogWarning("[WorldGenerator] Exit is NOT reachable from the start: " + LastReport, this);
            if (progress != null) progress(1f, "Done");
        }

        /// <summary>Writes the requested stages of <paramref name="map"/> into the scene.</summary>
        public IEnumerator ApplyRoutine(WorldMap map, GenerationStage stages, System.Action<float, string> progress)
        {
            if (tilemaps == null) tilemaps = GetComponent<WorldTilemaps>();
            tilemaps.EnsureLayers();
            Map = map;

            if ((stages & GenerationStage.Terrain) != 0)
            {
                var e = ApplyTerrain(map, 31, progress);
                while (e.MoveNext()) yield return null;
            }
            else
            {
                int mask = 0;
                if ((stages & GenerationStage.Paths) != 0) mask |= 4 | 8 | 16;   // dirt, water, path
                if ((stages & GenerationStage.Darkness) != 0) mask |= 2;          // variation (corruption)
                if (mask != 0)
                {
                    var e = ApplyTerrain(map, mask, progress);
                    while (e.MoveNext()) yield return null;
                }
            }

            int layers = 0;
            bool tiles = false;
            if ((stages & GenerationStage.POI) != 0) layers |= 1 << (int)GeneratedLayer.POI;
            if ((stages & GenerationStage.Vegetation) != 0)
            {
                layers |= (1 << (int)GeneratedLayer.Vegetation) | (1 << (int)GeneratedLayer.Special);
                tiles = true;
            }
            if ((stages & GenerationStage.Darkness) != 0) layers |= 1 << (int)GeneratedLayer.Light;
            if (layers != 0)
            {
                var e = ApplyObjects(map, layers, tiles, progress);
                while (e.MoveNext()) yield return null;
            }
        }

        /// <summary>Removes tiles and generated objects of the given stages. Manual objects without GeneratedObject stay.</summary>
        public void Clear(GenerationStage stages)
        {
            if (tilemaps == null) tilemaps = GetComponent<WorldTilemaps>();
            if (tilemaps.grid == null) return;
            tilemaps.EnsureLayers();
            if ((stages & GenerationStage.Terrain) != 0)
            {
                Wipe(tilemaps.ground); Wipe(tilemaps.groundVariation); Wipe(tilemaps.dirt); Wipe(tilemaps.water); Wipe(tilemaps.path);
            }
            if ((stages & GenerationStage.Paths) != 0) { Wipe(tilemaps.dirt); Wipe(tilemaps.water); Wipe(tilemaps.path); }
            if ((stages & GenerationStage.POI) != 0) ClearObjects(GeneratedLayer.POI);
            if ((stages & GenerationStage.Vegetation) != 0)
            {
                ClearObjects(GeneratedLayer.Vegetation); ClearObjects(GeneratedLayer.Special);
                Wipe(tilemaps.decoration); Wipe(tilemaps.obstacle);
            }
            if ((stages & GenerationStage.Darkness) != 0) { ClearObjects(GeneratedLayer.Light); Wipe(tilemaps.groundVariation); }
            if (stages == GenerationStage.All) { Map = null; HidePreview(); }
        }

        public bool HasGeneratedContent()
        {
            return tilemaps != null && tilemaps.ground != null && tilemaps.ground.GetUsedTilesCount() > 0;
        }

        /// <summary>Re-validates reachability. Optionally also blocks cells that physically contain colliders.</summary>
        public WalkabilityReport ValidateScene(bool useColliders)
        {
            if (Map == null) { Debug.LogWarning("[WorldGenerator] Nothing to validate - generate or preview first."); return null; }
            if (useColliders && tilemaps != null && tilemaps.grid != null)
            {
                int n = WalkabilityManager.ScanColliders(Map, tilemaps.grid);
                Debug.Log("[WorldGenerator] Collider scan marked " + n + " additional blocked cells.", this);
            }
            LastReport = WalkabilityManager.Validate(Map);
            Debug.Log("[WorldGenerator] " + LastReport, this);
            return LastReport;
        }

        // ------------------------------------------------------------------------------------------------
        // Preview
        // ------------------------------------------------------------------------------------------------

        public void ShowPreview(PreviewMode mode, bool rebuild)
        {
            if (tilemaps == null) tilemaps = GetComponent<WorldTilemaps>();
            tilemaps.EnsureLayers();
            if (rebuild || Map == null) { if (BuildMap() == null) return; }
            previewMode = mode;

            Transform t = tilemaps.grid.transform.Find(PreviewName);
            GameObject go;
            if (t == null)
            {
                go = new GameObject(PreviewName);
                go.transform.SetParent(tilemaps.grid.transform, false);
                go.hideFlags = HideFlags.DontSave;
            }
            else go = t.gameObject;

            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) sr = go.AddComponent<SpriteRenderer>();
            DestroyPreviewAssets(sr);

            Texture2D tex = WorldPreview.BuildTexture(Map, mode);
            tex.hideFlags = HideFlags.DontSave;
            Vector3 cs = tilemaps.grid.cellSize;
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.zero, 1f / Mathf.Max(0.0001f, cs.x));
            sprite.hideFlags = HideFlags.DontSave;
            sr.sprite = sprite;
            sr.sortingOrder = 32000;
            sr.color = new Color(1f, 1f, 1f, 0.7f);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = new Vector3(1f, cs.y / Mathf.Max(0.0001f, cs.x), 1f);
        }

        public void HidePreview()
        {
            if (tilemaps == null || tilemaps.grid == null) return;
            Transform t = tilemaps.grid.transform.Find(PreviewName);
            if (t == null) return;
            var sr = t.GetComponent<SpriteRenderer>();
            if (sr != null) DestroyPreviewAssets(sr);
            DestroyAny(t.gameObject);
        }

        private static void DestroyPreviewAssets(SpriteRenderer sr)
        {
            if (sr.sprite == null) return;
            Texture2D tex = sr.sprite.texture;
            Sprite sp = sr.sprite;
            sr.sprite = null;
            DestroyAny(sp);
            if (tex != null) DestroyAny(tex);
        }

        private void OnDrawGizmos()
        {
            if (!showBounds || settings == null || tilemaps == null || tilemaps.grid == null) return;
            Gizmos.matrix = tilemaps.grid.transform.localToWorldMatrix;
            Vector3 cs = tilemaps.grid.cellSize;
            Vector3 size = new Vector3(settings.width * cs.x, settings.height * cs.y, 0.1f);
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            Gizmos.DrawWireCube(size * 0.5f, size);
            if (Map != null)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(new Vector3((Map.start.x + 0.5f) * cs.x, (Map.start.y + 0.5f) * cs.y, 0f), cs.x * 1.5f);
                Gizmos.color = Color.magenta;
                foreach (var e in Map.exits)
                    Gizmos.DrawWireSphere(new Vector3((e.x + 0.5f) * cs.x, (e.y + 0.5f) * cs.y, 0f), cs.x * 1.5f);
            }
        }

        // ------------------------------------------------------------------------------------------------
        // Applying: terrain tiles (chunked)
        // ------------------------------------------------------------------------------------------------

        // layerMask bits: 1 ground, 2 variation, 4 dirt, 8 water, 16 path
        private IEnumerator ApplyTerrain(WorldMap map, int layerMask, System.Action<float, string> progress)
        {
            var s = settings;
            int w = map.width, h = map.height, cs = Mathf.Max(8, s.chunkSize), seed = map.seed;
            var tm = tilemaps;

            if ((layerMask & 1) != 0) Wipe(tm.ground);
            if ((layerMask & 2) != 0) Wipe(tm.groundVariation);
            if ((layerMask & 4) != 0) Wipe(tm.dirt);
            if ((layerMask & 8) != 0) Wipe(tm.water);
            if ((layerMask & 16) != 0) Wipe(tm.path);

            var lG = new List<TileChangeData>(cs * cs);
            var lV = new List<TileChangeData>();
            var lD = new List<TileChangeData>();
            var lW = new List<TileChangeData>();
            var lP = new List<TileChangeData>();

            int chunksX = (w + cs - 1) / cs, chunksY = (h + cs - 1) / cs, total = chunksX * chunksY, done = 0;
            int perFrame = Mathf.Max(1, s.chunksPerFrame);
            bool incremental = s.generationMode == GenerationMode.Incremental;

            for (int cy = 0; cy < chunksY; cy++)
                for (int cx = 0; cx < chunksX; cx++)
                {
                    int x0 = cx * cs, y0 = cy * cs, x1 = Mathf.Min(w, x0 + cs), y1 = Mathf.Min(h, y0 + cs);
                    for (int y = y0; y < y1; y++)
                        for (int x = x0; x < x1; x++)
                        {
                            int i = y * w + x;
                            var t = (TerrainType)map.terrain[i];
                            var pos = new Vector3Int(x, y, 0);
                            var own = map.biomes[map.biome[i]];

                            if ((layerMask & 1) != 0)
                            {
                                var vb = VisualBiome(map, x, y, i, seed);
                                TileBase tile = vb.ground.PickFill(WorldHash.Rand01(seed, x, y, 1));
                                if (tile != null)
                                {
                                    float k = 1f + (WorldHash.Rand01(seed, x, y, 2) - 0.5f) * 2f * vb.colorVariation;
                                    Color c = new Color(Mathf.Clamp01(vb.tint.r * k), Mathf.Clamp01(vb.tint.g * k), Mathf.Clamp01(vb.tint.b * k), 1f);
                                    lG.Add(new TileChangeData(pos, tile, c, Matrix4x4.identity));
                                }
                            }
                            if ((layerMask & 2) != 0)
                            {
                                TileBase tile = null;
                                if (map.corruption[i] && own.corruptionTile != null) tile = own.corruptionTile;
                                else if (t == TerrainType.Ground || t == TerrainType.Dirt)
                                {
                                    float chance = own.variationChance * s.terrainVariation;
                                    if (chance > 0f && WorldHash.Rand01(seed, x, y, 3) < chance)
                                        tile = TerrainTileSet.PickWeighted(own.groundVariation, WorldHash.Rand01(seed, x, y, 4));
                                }
                                if (tile != null) lV.Add(new TileChangeData(pos, tile, Color.white, Matrix4x4.identity));
                            }
                            if ((layerMask & 4) != 0 && t == TerrainType.Dirt)
                            {
                                TileBase tile = own.dirt.Get(Mask(map, x, y, TerrainType.Dirt), WorldHash.Rand01(seed, x, y, 5));
                                if (tile != null) lD.Add(new TileChangeData(pos, tile, Color.white, Matrix4x4.identity));
                            }
                            if ((layerMask & 8) != 0 && t == TerrainType.Water)
                            {
                                TileBase tile = own.water.Get(Mask(map, x, y, TerrainType.Water), WorldHash.Rand01(seed, x, y, 6));
                                if (tile != null) lW.Add(new TileChangeData(pos, tile, Color.white, Matrix4x4.identity));
                            }
                            if ((layerMask & 16) != 0 && t == TerrainType.Path)
                            {
                                TileBase tile = own.path.Get(Mask(map, x, y, TerrainType.Path), WorldHash.Rand01(seed, x, y, 7));
                                if (tile != null) lP.Add(new TileChangeData(pos, tile, Color.white, Matrix4x4.identity));
                            }
                        }

                    Flush(tm.ground, lG); Flush(tm.groundVariation, lV); Flush(tm.dirt, lD); Flush(tm.water, lW); Flush(tm.path, lP);
                    done++;
                    if (progress != null) progress(done / (float)total, "Applying terrain");
                    if (incremental && done % perFrame == 0) yield return null;
                }
        }

        private BiomeData VisualBiome(WorldMap map, int x, int y, int i, int seed)
        {
            var own = map.biomes[map.biome[i]];
            int tw = settings.transitionWidth;
            if (tw <= 0 || map.biomes.Length < 2 || settings.transitionBlend <= 0f) return own;
            int ob = map.biome[i];
            for (int d = 1; d <= tw; d++)
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + (k == 0 ? d : k == 1 ? -d : 0);
                    int ny = y + (k == 2 ? d : k == 3 ? -d : 0);
                    if (!map.InBounds(nx, ny)) continue;
                    int nb = map.biome[ny * map.width + nx];
                    if (nb == ob) continue;
                    float p = settings.transitionBlend * 0.5f * (1f - (d - 1) / (float)tw);
                    return WorldHash.Rand01(seed, x, y, 9) < p ? map.biomes[nb] : own;
                }
            return own;
        }

        private static int Mask(WorldMap map, int x, int y, TerrainType type)
        {
            int m = 0;
            if (Same(map, x, y + 1, type)) m |= 1;
            if (Same(map, x + 1, y, type)) m |= 2;
            if (Same(map, x, y - 1, type)) m |= 4;
            if (Same(map, x - 1, y, type)) m |= 8;
            return m;
        }

        private static bool Same(WorldMap map, int x, int y, TerrainType type)
        {
            if (!map.InBounds(x, y)) return true;
            return map.terrain[y * map.width + x] == (byte)type;
        }

        private static void Flush(Tilemap tm, List<TileChangeData> list)
        {
            if (tm != null && list.Count > 0) tm.SetTiles(list.ToArray(), true);
            list.Clear();
        }

        // ------------------------------------------------------------------------------------------------
        // Applying: objects
        // ------------------------------------------------------------------------------------------------

        private IEnumerator ApplyObjects(WorldMap map, int layerBits, bool includeTiles, System.Action<float, string> progress)
        {
            for (int l = 0; l < 4; l++) if ((layerBits & (1 << l)) != 0) ClearObjects((GeneratedLayer)l);
            if (includeTiles) { Wipe(tilemaps.decoration); Wipe(tilemaps.obstacle); }

            var decor = new List<TileChangeData>();
            var obst = new List<TileChangeData>();
            int perFrame = Mathf.Max(1, settings.objectsPerFrame);
            bool incremental = settings.generationMode == GenerationMode.Incremental;
            int count = 0, total = map.objects.Count;

            for (int k = 0; k < total; k++)
            {
                var o = map.objects[k];
                if ((layerBits & (1 << (int)o.layer)) == 0) continue;
                if (o.prefab != null) SpawnPlaced(o);
                else if (o.tile != null && includeTiles)
                {
                    var list = o.tileOnObstacleLayer ? obst : decor;
                    list.Add(new TileChangeData(new Vector3Int(o.cell.x, o.cell.y, 0), o.tile, Color.white, Matrix4x4.identity));
                }
                if (++count % perFrame == 0)
                {
                    if (progress != null) progress(k / (float)Mathf.Max(1, total), "Spawning objects");
                    if (incremental) yield return null;
                }
            }
            FlushLarge(tilemaps.decoration, decor);
            FlushLarge(tilemaps.obstacle, obst);
        }

        private static void FlushLarge(Tilemap tm, List<TileChangeData> list)
        {
            if (tm == null || list.Count == 0) return;
            const int batch = 8192;
            for (int i = 0; i < list.Count; i += batch)
            {
                int n = Mathf.Min(batch, list.Count - i);
                var arr = new TileChangeData[n];
                list.CopyTo(i, arr, 0, n);
                tm.SetTiles(arr, true);
            }
            list.Clear();
        }

        private void SpawnPlaced(PlacedObject o)
        {
            GameObject go = SpawnPrefab(o.prefab, o.layer);
            if (go == null) return;
            go.transform.position = tilemaps.CellToWorld(o.center);
            go.transform.localScale = Vector3.Scale(go.transform.localScale, o.scale);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, o.rotationZ) * go.transform.localRotation;
            ApplySorting(go, o.sortingOrder, o.sortingLayer);
            if (o.addCollider && go.GetComponentInChildren<Collider2D>() == null)
            {
                var col = go.AddComponent<CircleCollider2D>();
                col.radius = o.colliderRadius;
            }
            var tag = go.GetComponent<GeneratedObject>();
            tag.layer = o.layer;
            tag.data = o.data;
            tag.cell = o.cell;
        }

        /// <summary>Instantiates a prefab under the root of the layer and tags it as generated (editor: undo-aware, keeps prefab link).</summary>
        public GameObject SpawnPrefab(GameObject prefab, GeneratedLayer layer)
        {
            if (prefab == null) return null;
            Transform parent = tilemaps.GetRoot(layer);
            GameObject go;
#if UNITY_EDITOR
            if (!Application.isPlaying && UnityEditor.PrefabUtility.IsPartOfPrefabAsset(prefab))
            {
                go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, parent);
                UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Generate World");
            }
            else go = Instantiate(prefab, parent);
#else
            go = Instantiate(prefab, parent);
#endif
            var tag = go.GetComponent<GeneratedObject>();
            if (tag == null) tag = go.AddComponent<GeneratedObject>();
            tag.sourcePrefab = prefab;
            tag.layer = layer;
            return go;
        }

        private static void ApplySorting(GameObject go, int order, string layerName)
        {
            var renderers = go.GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers.Length == 0) return;
            int min = int.MaxValue;
            foreach (var r in renderers) if (r.sortingOrder < min) min = r.sortingOrder;
            int layerId = 0;
            bool useLayer = false;
            if (!string.IsNullOrEmpty(layerName))
            {
                layerId = SortingLayer.NameToID(layerName);
                useLayer = SortingLayer.IsValid(layerId);
            }
            foreach (var r in renderers)
            {
                r.sortingOrder = order + (r.sortingOrder - min);
                if (useLayer) r.sortingLayerID = layerId;
            }
        }

        // ------------------------------------------------------------------------------------------------
        // Clearing helpers
        // ------------------------------------------------------------------------------------------------

        private void ClearObjects(GeneratedLayer layer)
        {
            Transform root = tilemaps.GetRoot(layer);
            if (root == null) return;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform c = root.GetChild(i);
                if (c.GetComponent<GeneratedObject>() == null) continue;
#if UNITY_EDITOR
                if (!Application.isPlaying) { UnityEditor.Undo.DestroyObjectImmediate(c.gameObject); continue; }
#endif
                DestroyAny(c.gameObject);
            }
        }

        private static void Wipe(Tilemap tm)
        {
            if (tm == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.Undo.RegisterCompleteObjectUndo(tm, "Generate World");
                UnityEditor.EditorUtility.SetDirty(tm);
            }
#endif
            tm.ClearAllTiles();
        }

        private static void DestroyAny(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        private static void Run(IEnumerator routine) { while (routine.MoveNext()) { } }
    }
}
