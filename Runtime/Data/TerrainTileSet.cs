using UnityEngine;
using UnityEngine.Tilemaps;

namespace TopDownWorldGen
{
    [System.Serializable]
    public class WeightedTile
    {
        public TileBase tile;
        [Min(0f)] public float weight = 1f;
    }

    /// <summary>
    /// Tiles for one terrain layer of a biome.
    /// <b>fill</b>: weighted random variants used for interior cells. Any TileBase works, including a Unity RuleTile
    /// (then Unity itself handles neighbour-aware autotiling and you can leave edgeTiles empty).
    /// <b>edgeTiles</b>: optional built-in autotiling with exactly 16 entries, indexed by a 4-bit mask of
    /// neighbours that belong to the same layer: bit0 = North (+Y), bit1 = East (+X), bit2 = South, bit3 = West.
    /// Index 15 (all neighbours present) falls back to <b>fill</b>. Missing entries also fall back to fill.
    /// </summary>
    [System.Serializable]
    public class TerrainTileSet
    {
        public WeightedTile[] fill = new WeightedTile[0];
        public TileBase[] edgeTiles = new TileBase[0];

        public bool HasTiles
        {
            get
            {
                if (fill == null) return false;
                for (int i = 0; i < fill.Length; i++) if (fill[i] != null && fill[i].tile != null) return true;
                return false;
            }
        }

        public static TileBase PickWeighted(WeightedTile[] arr, float r01)
        {
            if (arr == null || arr.Length == 0) return null;
            float total = 0f;
            TileBase first = null, last = null;
            for (int i = 0; i < arr.Length; i++)
            {
                var w = arr[i];
                if (w == null || w.tile == null) continue;
                if (first == null) first = w.tile;
                last = w.tile;
                total += Mathf.Max(0f, w.weight);
            }
            if (first == null) return null;
            if (total <= 0f) return first;
            float t = r01 * total;
            for (int i = 0; i < arr.Length; i++)
            {
                var w = arr[i];
                if (w == null || w.tile == null) continue;
                t -= Mathf.Max(0f, w.weight);
                if (t <= 0f) return w.tile;
            }
            return last;
        }

        public TileBase PickFill(float r01) { return PickWeighted(fill, r01); }

        public TileBase Get(int mask, float r01)
        {
            if (mask != 15 && edgeTiles != null && edgeTiles.Length == 16 && edgeTiles[mask] != null)
                return edgeTiles[mask];
            return PickFill(r01);
        }
    }
}
