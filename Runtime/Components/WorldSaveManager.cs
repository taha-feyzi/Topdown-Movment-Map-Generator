using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TopDownWorldGen
{
    /// <summary>
    /// Captures the CURRENT scene state (tilemaps + generated objects, including manual edits) into a
    /// <see cref="WorldSaveData"/> and restores it. Writing the asset to disk is done by the editor window.
    /// </summary>
    public static class WorldSaveManager
    {
        private static readonly TilemapLayerKind[] Kinds =
        {
            TilemapLayerKind.Ground, TilemapLayerKind.GroundVariation, TilemapLayerKind.Dirt, TilemapLayerKind.Water,
            TilemapLayerKind.Path, TilemapLayerKind.Decoration, TilemapLayerKind.Obstacle
        };

        public static WorldSaveData Capture(WorldGenerator gen)
        {
            if (gen == null || gen.tilemaps == null || gen.tilemaps.grid == null) return null;
            var tm = gen.tilemaps;
            var data = ScriptableObject.CreateInstance<WorldSaveData>();
            data.settings = gen.settings;
            if (gen.settings != null) { data.seed = gen.settings.seed; data.width = gen.settings.width; data.height = gen.settings.height; }

            if (gen.Map != null)
            {
                var m = gen.Map;
                data.seed = m.seed; data.width = m.width; data.height = m.height;
                data.biomeMap = (byte[])m.biome.Clone();
                data.terrainMap = (byte[])m.terrain.Clone();
                data.cellMap = (byte[])m.cell.Clone();
                data.darknessMap = (float[])m.darkness.Clone();
                data.start = m.start;
                data.exits = new List<Vector2Int>(m.exits);
            }

            foreach (var kind in Kinds)
            {
                Tilemap map = tm.GetTilemap(kind);
                if (map == null) continue;
                var layer = new WorldSaveData.TileLayerData { kind = kind };
                BoundsInt b = map.cellBounds;
                TileBase[] tiles = map.GetTilesBlock(b);
                int sx = b.size.x, sy = b.size.y;
                for (int i = 0; i < tiles.Length; i++)
                {
                    if (tiles[i] == null) continue;
                    int x = b.xMin + i % sx;
                    int y = b.yMin + (i / sx) % sy;
                    int z = b.zMin + i / (sx * sy);
                    var pos = new Vector3Int(x, y, z);
                    layer.tiles.Add(new WorldSaveData.TileEntry { position = pos, tile = tiles[i], color = map.GetColor(pos) });
                }
                data.layers.Add(layer);
            }

            Transform genRoot = tm.generatedRoot;
            for (int l = 0; l < 4; l++)
            {
                Transform root = tm.GetRoot((GeneratedLayer)l);
                if (root == null) continue;
                for (int c = 0; c < root.childCount; c++)
                {
                    Transform child = root.GetChild(c);
                    var tag = child.GetComponent<GeneratedObject>();
                    GameObject prefab = tag != null ? tag.sourcePrefab : null;
#if UNITY_EDITOR
                    if (prefab == null) prefab = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
#endif
                    if (prefab == null) continue;
                    var sr = child.GetComponentInChildren<SpriteRenderer>(true);
                    data.objects.Add(new WorldSaveData.ObjectEntry
                    {
                        prefab = prefab,
                        data = tag != null ? tag.data : null,
                        layer = (GeneratedLayer)l,
                        localPosition = genRoot.InverseTransformPoint(child.position),
                        localRotation = Quaternion.Inverse(genRoot.rotation) * child.rotation,
                        localScale = child.localScale,
                        cell = tag != null ? tag.cell : Vector2Int.zero,
                        sortingOrder = sr != null ? sr.sortingOrder : 0,
                        sortingLayer = sr != null ? SortingLayer.IDToName(sr.sortingLayerID) : ""
                    });
                }
            }
            return data;
        }

        public static void Load(WorldGenerator gen, WorldSaveData data)
        {
            if (gen == null || data == null) return;
            if (gen.tilemaps == null) gen.tilemaps = gen.GetComponent<WorldTilemaps>();
            if (gen.settings == null) gen.settings = data.settings;
            gen.Clear(GenerationStage.All);
            var tm = gen.tilemaps;
            tm.EnsureLayers();

            foreach (var layer in data.layers)
            {
                Tilemap map = tm.GetTilemap(layer.kind);
                if (map == null || layer.tiles.Count == 0) continue;
                const int batch = 8192;
                for (int i = 0; i < layer.tiles.Count; i += batch)
                {
                    int n = Mathf.Min(batch, layer.tiles.Count - i);
                    var arr = new TileChangeData[n];
                    for (int k = 0; k < n; k++)
                    {
                        var e = layer.tiles[i + k];
                        arr[k] = new TileChangeData(e.position, e.tile, e.color, Matrix4x4.identity);
                    }
                    map.SetTiles(arr, true);
                }
            }

            foreach (var e in data.objects)
            {
                GameObject go = gen.SpawnPrefab(e.prefab, e.layer);
                if (go == null) continue;
                go.transform.position = tm.generatedRoot.TransformPoint(e.localPosition);
                go.transform.rotation = tm.generatedRoot.rotation * e.localRotation;
                go.transform.localScale = e.localScale;
                var tag = go.GetComponent<GeneratedObject>();
                tag.data = e.data; tag.cell = e.cell;
                var renderers = go.GetComponentsInChildren<SpriteRenderer>(true);
                if (renderers.Length > 0)
                {
                    int min = int.MaxValue;
                    foreach (var r in renderers) if (r.sortingOrder < min) min = r.sortingOrder;
                    int id = string.IsNullOrEmpty(e.sortingLayer) ? 0 : SortingLayer.NameToID(e.sortingLayer);
                    foreach (var r in renderers)
                    {
                        r.sortingOrder = e.sortingOrder + (r.sortingOrder - min);
                        if (id != 0 && SortingLayer.IsValid(id)) r.sortingLayerID = id;
                    }
                }
            }

            if (data.terrainMap != null && data.width > 0 && data.settings != null)
            {
                var map = new WorldMap(data.width, data.height, data.seed, data.settings.biomes);
                int n = data.width * data.height;
                if (data.biomeMap != null && data.biomeMap.Length == n) System.Array.Copy(data.biomeMap, map.biome, n);
                if (data.terrainMap.Length == n) System.Array.Copy(data.terrainMap, map.terrain, n);
                if (data.cellMap != null && data.cellMap.Length == n) System.Array.Copy(data.cellMap, map.cell, n);
                if (data.darknessMap != null && data.darknessMap.Length == n) System.Array.Copy(data.darknessMap, map.darkness, n);
                map.start = data.start;
                map.exits.AddRange(data.exits);
                gen.SetMap(map);
            }
        }
    }
}
