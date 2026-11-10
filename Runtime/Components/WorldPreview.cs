using UnityEngine;

namespace TopDownWorldGen
{
    /// <summary>Builds a 1-pixel-per-cell debug texture of a <see cref="WorldMap"/>.</summary>
    public static class WorldPreview
    {
        public static Texture2D BuildTexture(WorldMap map, PreviewMode mode)
        {
            int w = map.width, h = map.height;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    px[i] = ColorOf(map, i, mode);
                }

            if (mode == PreviewMode.Objects)
            {
                foreach (var o in map.objects)
                {
                    Color32 c = ObjectColor(o);
                    for (int fy = 0; fy < o.size.y; fy++)
                        for (int fx = 0; fx < o.size.x; fx++)
                        {
                            int x = o.cell.x + fx, y = o.cell.y + fy;
                            if (map.InBounds(x, y)) px[y * w + x] = c;
                        }
                }
            }

            Mark(px, map, map.start.x, map.start.y, new Color32(255, 255, 255, 255));
            foreach (var e in map.exits) Mark(px, map, e.x, e.y, new Color32(255, 0, 255, 255));

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        private static void Mark(Color32[] px, WorldMap map, int cx, int cy, Color32 c)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (map.InBounds(cx + dx, cy + dy)) px[(cy + dy) * map.width + cx + dx] = c;
        }

        private static Color32 ColorOf(WorldMap map, int i, PreviewMode mode)
        {
            switch (mode)
            {
                case PreviewMode.Biomes:
                    return map.biomes[map.biome[i]].previewColor;
                case PreviewMode.Walkability:
                    switch ((CellType)map.cell[i])
                    {
                        case CellType.Walkable: return new Color32(40, 140, 60, 255);
                        case CellType.Blocked: return new Color32(200, 40, 40, 255);
                        case CellType.Dangerous: return new Color32(240, 150, 20, 255);
                        case CellType.Special: return new Color32(180, 60, 220, 255);
                        default: return new Color32(120, 120, 120, 255);
                    }
                case PreviewMode.Darkness:
                    {
                        byte v = (byte)Mathf.RoundToInt((1f - map.darkness[i]) * 255f);
                        if (map.corruption[i]) return new Color32((byte)(v / 2 + 90), 0, (byte)(v / 2 + 120), 255);
                        return new Color32(v, v, v, 255);
                    }
                default:
                    return TerrainColor(map, i, mode == PreviewMode.Objects ? 0.55f : 1f);
            }
        }

        private static Color32 TerrainColor(WorldMap map, int i, float k)
        {
            Color c;
            switch ((TerrainType)map.terrain[i])
            {
                case TerrainType.Water: c = new Color(0.15f, 0.35f, 0.8f); break;
                case TerrainType.Dirt: c = new Color(0.45f, 0.3f, 0.15f); break;
                case TerrainType.Path: c = new Color(0.9f, 0.8f, 0.4f); break;
                default: c = new Color(0.25f, 0.55f, 0.25f); break;
            }
            return new Color(c.r * k, c.g * k, c.b * k, 1f);
        }

        private static Color32 ObjectColor(PlacedObject o)
        {
            switch (o.layer)
            {
                case GeneratedLayer.POI: return new Color32(0, 230, 230, 255);
                case GeneratedLayer.Light: return new Color32(255, 240, 80, 255);
                case GeneratedLayer.Special: return new Color32(255, 120, 200, 255);
            }
            switch (o.category)
            {
                case VegetationCategory.Tree: return new Color32(0, 90, 0, 255);
                case VegetationCategory.Bush: return new Color32(80, 190, 60, 255);
                case VegetationCategory.Rock: return new Color32(150, 150, 150, 255);
                case VegetationCategory.FallenTree: return new Color32(110, 70, 30, 255);
                default: return new Color32(200, 220, 120, 255);
            }
        }
    }
}
