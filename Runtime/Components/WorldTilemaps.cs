using UnityEngine;
using UnityEngine.Tilemaps;

namespace TopDownWorldGen
{
    /// <summary>
    /// Owns the Grid, the 7 Tilemap layers and the object roots (Vegetation / POI / Lights / Special).
    /// Call <see cref="EnsureLayers"/> to create anything that is missing; existing objects with matching names are reused.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldTilemaps : MonoBehaviour
    {
        public Grid grid;
        public Tilemap ground, groundVariation, dirt, water, path, decoration, obstacle;
        public Transform generatedRoot;
        [Tooltip("Adds TilemapCollider2D + CompositeCollider2D to the Water tilemap. Water tiles need a collider type (Grid/Sprite).")]
        public bool waterCollider = true;
        [Tooltip("Adds colliders to the Obstacle tilemap (used by tile-based blocking vegetation).")]
        public bool obstacleCollider = true;

        private static readonly string[] RootNames = { "Vegetation", "POI", "Lights", "Special" };

        public void EnsureLayers()
        {
            if (grid == null) grid = GetComponentInChildren<Grid>(true);
            if (grid == null)
            {
                var g = new GameObject("Grid");
                g.transform.SetParent(transform, false);
                grid = g.AddComponent<Grid>();
                grid.cellSize = Vector3.one;
            }

            ground = Ensure(ground, "Ground", -20000);
            groundVariation = Ensure(groundVariation, "Ground Variation", -19999);
            dirt = Ensure(dirt, "Dirt", -19998);
            water = Ensure(water, "Water", -19997);
            path = Ensure(path, "Path", -19996);
            decoration = Ensure(decoration, "Decoration", -19990);
            obstacle = Ensure(obstacle, "Obstacle", -19980);

            if (generatedRoot == null)
            {
                Transform t = grid.transform.Find("Generated");
                if (t == null)
                {
                    var go = new GameObject("Generated");
                    go.transform.SetParent(grid.transform, false);
                    t = go.transform;
                }
                generatedRoot = t;
            }
            foreach (var n in RootNames) GetRootByName(n);

            if (waterCollider) EnsureCollider(water);
            if (obstacleCollider) EnsureCollider(obstacle);
        }

        private Tilemap Ensure(Tilemap existing, string layerName, int order)
        {
            if (existing != null) return existing;
            Transform t = grid.transform.Find(layerName);
            GameObject go;
            if (t != null) go = t.gameObject;
            else
            {
                go = new GameObject(layerName);
                go.transform.SetParent(grid.transform, false);
            }
            var tm = go.GetComponent<Tilemap>();
            if (tm == null) tm = go.AddComponent<Tilemap>();
            var r = go.GetComponent<TilemapRenderer>();
            if (r == null) { r = go.AddComponent<TilemapRenderer>(); r.sortingOrder = order; }
            return tm;
        }

        private static void EnsureCollider(Tilemap tm)
        {
            if (tm == null) return;
            var tc = tm.GetComponent<TilemapCollider2D>();
            if (tc == null) tc = tm.gameObject.AddComponent<TilemapCollider2D>();
            var cc = tm.GetComponent<CompositeCollider2D>();
            if (cc == null) cc = tm.gameObject.AddComponent<CompositeCollider2D>();
            var rb = tm.GetComponent<Rigidbody2D>();
            if (rb != null) rb.bodyType = RigidbodyType2D.Static;
            tc.compositeOperation = Collider2D.CompositeOperation.Merge;
        }

        private Transform GetRootByName(string rootName)
        {
            if (generatedRoot == null) return null;
            Transform t = generatedRoot.Find(rootName);
            if (t == null)
            {
                var go = new GameObject(rootName);
                go.transform.SetParent(generatedRoot, false);
                t = go.transform;
            }
            return t;
        }

        public Transform GetRoot(GeneratedLayer layer) { return GetRootByName(RootNames[(int)layer]); }

        public Tilemap GetTilemap(TilemapLayerKind kind)
        {
            switch (kind)
            {
                case TilemapLayerKind.Ground: return ground;
                case TilemapLayerKind.GroundVariation: return groundVariation;
                case TilemapLayerKind.Dirt: return dirt;
                case TilemapLayerKind.Water: return water;
                case TilemapLayerKind.Path: return path;
                case TilemapLayerKind.Decoration: return decoration;
                default: return obstacle;
            }
        }

        /// <summary>Converts a position in cell units (e.g. footprint centre) to world space.</summary>
        public Vector3 CellToWorld(Vector2 cellPos)
        {
            Vector3 cs = grid != null ? grid.cellSize : Vector3.one;
            Transform t = grid != null ? grid.transform : transform;
            return t.TransformPoint(new Vector3(cellPos.x * cs.x, cellPos.y * cs.y, 0f));
        }
    }
}
