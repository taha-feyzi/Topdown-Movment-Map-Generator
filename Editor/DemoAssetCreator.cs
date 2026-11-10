using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TopDownWorldGen.Editor
{
    /// <summary>Generates placeholder pixel art, tiles, prefabs, 3 biomes, 2 POIs and a settings asset so the tool works out of the box.</summary>
    public static class DemoAssetCreator
    {
        private const string Root = "Assets/TopDownWorldGenerator/Demo";

        [MenuItem("Tools/Top-Down World Generator/Create Demo Assets")]
        public static WorldGenerationSettings CreateDemoAssets()
        {
            if (AssetDatabase.IsValidFolder(Root)) AssetDatabase.DeleteAsset(Root);
            Directory.CreateDirectory(Root + "/Textures");
            Directory.CreateDirectory(Root + "/Tiles");
            Directory.CreateDirectory(Root + "/Prefabs");
            Directory.CreateDirectory(Root + "/Data");
            AssetDatabase.Refresh();

            // ---- tiles
            Tile grassN = MakeTile("Grass_Normal", Noisy(new Color(0.22f, 0.46f, 0.20f), 0.12f, 1), Tile.ColliderType.None);
            Tile grassN2 = MakeTile("Grass_Normal2", Noisy(new Color(0.25f, 0.50f, 0.22f), 0.12f, 2), Tile.ColliderType.None);
            Tile grassD = MakeTile("Grass_Dark", Noisy(new Color(0.10f, 0.25f, 0.14f), 0.12f, 3), Tile.ColliderType.None);
            Tile grassS = MakeTile("Grass_Swamp", Noisy(new Color(0.20f, 0.30f, 0.16f), 0.14f, 4), Tile.ColliderType.None);
            Tile dirtFill = MakeTile("Dirt_Fill", Noisy(new Color(0.40f, 0.28f, 0.16f), 0.10f, 5), Tile.ColliderType.None);
            TileBase[] dirtEdges = MakeEdgeSet("Dirt", new Color(0.40f, 0.28f, 0.16f), new Color(0.30f, 0.20f, 0.10f), Tile.ColliderType.None);
            Tile waterFill = MakeTile("Water_Fill", Noisy(new Color(0.12f, 0.30f, 0.62f), 0.08f, 6), Tile.ColliderType.Grid);
            TileBase[] waterEdges = MakeEdgeSet("Water", new Color(0.12f, 0.30f, 0.62f), new Color(0.35f, 0.55f, 0.85f), Tile.ColliderType.Grid);
            Tile pathFill = MakeTile("Path_Fill", Noisy(new Color(0.62f, 0.52f, 0.34f), 0.08f, 7), Tile.ColliderType.None);
            TileBase[] pathEdges = MakeEdgeSet("Path", new Color(0.62f, 0.52f, 0.34f), new Color(0.45f, 0.36f, 0.22f), Tile.ColliderType.None);
            Tile varA = MakeTile("Var_Leaves", Dots(new Color(0, 0, 0, 0), new Color(0.35f, 0.28f, 0.10f, 0.9f), 8), Tile.ColliderType.None);
            Tile corrupt = MakeTile("Corruption", Noisy(new Color(0.35f, 0.10f, 0.45f, 0.75f), 0.3f, 9), Tile.ColliderType.None);
            Tile grassTuft = MakeTile("Decor_Grass", Dots(new Color(0, 0, 0, 0), new Color(0.35f, 0.70f, 0.30f, 1f), 10), Tile.ColliderType.None);
            Tile flower = MakeTile("Decor_Flower", Dots(new Color(0, 0, 0, 0), new Color(0.95f, 0.85f, 0.2f, 1f), 11), Tile.ColliderType.None);
            Tile mushroom = MakeTile("Decor_Mushroom", Dots(new Color(0, 0, 0, 0), new Color(0.4f, 0.9f, 0.95f, 1f), 12), Tile.ColliderType.None);

            // ---- prefabs
            GameObject tree = MakePrefab("Tree", MakeSprite("Tree", 32, 32, TreePix), 0, go =>
            { var c = go.AddComponent<CircleCollider2D>(); c.radius = 0.45f; c.offset = new Vector2(0f, -0.4f); });
            GameObject bush = MakePrefab("Bush", MakeSprite("Bush", 16, 16, (x, y) => Circle(x, y, 8, 8, 6.5f, new Color(0.15f, 0.40f, 0.15f))), 0, go =>
            { var c = go.AddComponent<CircleCollider2D>(); c.radius = 0.35f; });
            GameObject rock = MakePrefab("Rock", MakeSprite("Rock", 16, 16, (x, y) => Circle(x, y, 8, 7, 6f, new Color(0.45f, 0.45f, 0.48f))), 0, go =>
            { var c = go.AddComponent<CircleCollider2D>(); c.radius = 0.35f; });
            GameObject lamp = MakePrefab("LightSource", MakeSprite("Lamp", 16, 16, (x, y) => Circle(x, y, 8, 8, 4f, new Color(1f, 0.85f, 0.35f))), 5, null);
            GameObject ruins = MakePrefab("Ruins", MakeSprite("Ruins", 48, 48, (x, y) => BrickPix(x, y, 48)), 0, go =>
            { var c = go.AddComponent<BoxCollider2D>(); c.size = new Vector2(2.6f, 2.6f); });
            GameObject camp = MakePrefab("Camp", MakeSprite("Camp", 48, 48, (x, y) => Circle(x, y, 24, 24, 18f, new Color(0.5f, 0.32f, 0.18f))), 0, null);

            // ---- vegetation data
            var vTree = MakeVeg("Veg_Tree", VegetationCategory.Tree, tree, null, 0.10f, new Vector2Int(2, 2), 2.5f, 1, 0.8f, 14f, true);
            var vTreeDense = MakeVeg("Veg_TreeDense", VegetationCategory.Tree, tree, null, 0.20f, new Vector2Int(2, 2), 2.0f, 1, 0.8f, 10f, true);
            var vBush = MakeVeg("Veg_Bush", VegetationCategory.Bush, bush, null, 0.10f, Vector2Int.one, 1.5f, 1, 0f, 12f, true);
            vBush.preferNearCategory = true; vBush.nearCategory = VegetationCategory.Tree; vBush.nearBonus = 3f;
            var vRock = MakeVeg("Veg_Rock", VegetationCategory.Rock, rock, null, 0.015f, Vector2Int.one, 4f, 1, 0f, 12f, true);
            var vGrass = MakeVeg("Veg_Grass", VegetationCategory.Grass, null, grassTuft, 0.25f, Vector2Int.one, 0f, 0, 0f, 12f, false);
            var vFlower = MakeVeg("Veg_Flower", VegetationCategory.Flower, null, flower, 0.03f, Vector2Int.one, 0f, 0, 0.7f, 8f, false);
            var vMush = MakeVeg("Veg_Mushroom", VegetationCategory.Mushroom, null, mushroom, 0.05f, Vector2Int.one, 0f, 0, 0.8f, 8f, false);

            // ---- biomes
            var normal = MakeBiome("Biome_NormalForest", "Normal Forest", new Color(0.3f, 0.7f, 0.3f), new[] { grassN, grassN2 }, dirtFill, dirtEdges, waterFill, waterEdges, pathFill, pathEdges, varA,
                new[] { vTree, vBush, vRock, vGrass, vFlower }, 0.35f, 1f, 0f, null);
            var dark = MakeBiome("Biome_DarkForest", "Dark Forest", new Color(0.1f, 0.35f, 0.25f), new[] { grassD }, dirtFill, dirtEdges, waterFill, waterEdges, pathFill, pathEdges, varA,
                new[] { vTreeDense, vBush, vRock, vGrass, vMush }, 0.65f, 1f, 0.6f, corrupt);
            var swamp = MakeBiome("Biome_Swamp", "Swamp", new Color(0.45f, 0.5f, 0.2f), new[] { grassS }, dirtFill, dirtEdges, waterFill, waterEdges, pathFill, pathEdges, varA,
                new[] { vTree, vBush, vMush }, 0.55f, 0.8f, 0.3f, corrupt);
            swamp.waterBias = 0.05f; swamp.vegetationDensity = 0.6f;
            foreach (var b in new[] { normal, dark, swamp })
                b.lightSources = new[] { new LightSourceEntry { prefab = lamp, weight = 1f, lightRadius = 4f } };
            EditorUtility.SetDirty(swamp);

            // ---- POIs
            var pRuins = MakePoi("POI_Ruins", "Ancient Ruins", POIType.Ruins, ruins, new Vector2Int(3, 3), 1, 2, true, 0f);
            var pCamp = MakePoi("POI_Camp", "Abandoned Camp", POIType.Camp, camp, new Vector2Int(3, 3), 1, 2, false, 6f);
            pCamp.type = POIType.SafePoint;

            // ---- settings
            var s = ScriptableObject.CreateInstance<WorldGenerationSettings>();
            s.biomes = new[] { normal, dark, swamp };
            s.pois = new[] { pRuins, pCamp };
            AssetDatabase.CreateAsset(s, Root + "/Data/DemoWorldSettings.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = s;
            Debug.Log("[WorldGenerator] Demo assets created in " + Root + ". They use placeholder art - replace with your own tiles/prefabs.");
            return s;
        }

        // ---------------------------------------------------------------- pixel helpers
        private static float H(int x, int y, int salt) { return WorldHash.Rand01(salt, x, y, 3); }

        private static Func<int, int, Color> Noisy(Color c, float amt, int salt)
        {
            return (x, y) => { float k = 1f + (H(x, y, salt) - 0.5f) * amt * 2f; return new Color(c.r * k, c.g * k, c.b * k, c.a); };
        }

        private static Func<int, int, Color> Dots(Color bg, Color dot, int salt)
        {
            return (x, y) => H(x, y, salt) > 0.93f ? dot : bg;
        }

        private static Color Circle(int x, int y, float cx, float cy, float r, Color c)
        {
            float dx = x - cx, dy = y - cy;
            if (dx * dx + dy * dy > r * r) return new Color(0, 0, 0, 0);
            float k = 1f - (dy / (r * 2f)) * 0.5f;
            return new Color(c.r * k, c.g * k, c.b * k, 1f);
        }

        private static Color TreePix(int x, int y)
        {
            if (x >= 14 && x <= 17 && y < 12) return new Color(0.35f, 0.22f, 0.1f);
            return Circle(x, y, 16, 20, 11.5f, new Color(0.12f, 0.38f, 0.14f));
        }

        private static Color BrickPix(int x, int y, int size)
        {
            if (x < 2 || y < 2 || x >= size - 2 || y >= size - 2) return new Color(0, 0, 0, 0);
            bool mortar = (y % 8 == 0) || (((x + ((y / 8) % 2) * 6) % 12) == 0);
            float k = 0.9f + (H(x, y, 21) - 0.5f) * 0.2f;
            Color c = mortar ? new Color(0.25f, 0.25f, 0.27f) : new Color(0.5f * k, 0.48f * k, 0.46f * k);
            return c;
        }

        // ---------------------------------------------------------------- asset helpers
        private static Sprite MakeSprite(string name, int w, int h, Func<int, int, Color> px)
        {
            string path = Root + "/Textures/" + name + ".png";
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) tex.SetPixel(x, y, px(x, y));
            tex.Apply();
            File.WriteAllBytes(Path.GetFullPath(path), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = 16;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Tile MakeTile(string name, Func<int, int, Color> px, Tile.ColliderType collider)
        {
            Sprite sp = MakeSprite("T_" + name, 16, 16, px);
            var t = ScriptableObject.CreateInstance<Tile>();
            t.sprite = sp;
            t.colliderType = collider;
            AssetDatabase.CreateAsset(t, Root + "/Tiles/" + name + ".asset");
            return t;
        }

        /// <summary>16 tiles for the 4-bit neighbour mask (bit0 N, bit1 E, bit2 S, bit3 W): border drawn where the neighbour is missing.</summary>
        private static TileBase[] MakeEdgeSet(string name, Color fill, Color edge, Tile.ColliderType collider)
        {
            var set = new TileBase[16];
            for (int m = 0; m < 16; m++)
            {
                int mask = m;
                Func<int, int, Color> px = (x, y) =>
                {
                    bool e = ((mask & 1) == 0 && y >= 14) || ((mask & 2) == 0 && x >= 14) || ((mask & 4) == 0 && y <= 1) || ((mask & 8) == 0 && x <= 1);
                    Color c = e ? edge : fill;
                    float k = 1f + (H(x, y, 30 + mask) - 0.5f) * 0.12f;
                    return new Color(c.r * k, c.g * k, c.b * k, 1f);
                };
                set[m] = MakeTile(name + "_Edge" + m, px, collider);
            }
            return set;
        }

        private static GameObject MakePrefab(string name, Sprite sprite, int order, Action<GameObject> extra)
        {
            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            if (extra != null) extra(go);
            string path = Root + "/Prefabs/" + name + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            return prefab;
        }

        private static VegetationData MakeVeg(string name, VegetationCategory cat, GameObject prefab, TileBase tile, float density,
            Vector2Int footprint, float minDist, int minPathDist, float cluster, float clusterScale, bool blocks)
        {
            var v = ScriptableObject.CreateInstance<VegetationData>();
            v.category = cat; v.prefab = prefab; v.tile = tile; v.density = density; v.footprint = footprint;
            v.minDistance = minDist; v.minPathDistance = minPathDist; v.clusterStrength = cluster; v.clusterScale = clusterScale;
            v.blocksMovement = blocks;
            v.minScale = 0.9f; v.maxScale = 1.1f;
            v.rotation = RotationMode.FlipX;
            AssetDatabase.CreateAsset(v, Root + "/Data/" + name + ".asset");
            return v;
        }

        private static BiomeData MakeBiome(string assetName, string display, Color preview, Tile[] grounds,
            Tile dirtFill, TileBase[] dirtEdges, Tile waterFill, TileBase[] waterEdges, Tile pathFill, TileBase[] pathEdges, Tile variation,
            VegetationData[] veg, float darkness, float vegDensity, float corruption, TileBase corruptionTile)
        {
            var b = ScriptableObject.CreateInstance<BiomeData>();
            b.displayName = display; b.previewColor = preview;
            var gw = new WeightedTile[grounds.Length];
            for (int i = 0; i < grounds.Length; i++) gw[i] = new WeightedTile { tile = grounds[i], weight = 1f };
            b.ground = new TerrainTileSet { fill = gw };
            b.dirt = new TerrainTileSet { fill = new[] { new WeightedTile { tile = dirtFill } }, edgeTiles = dirtEdges };
            b.water = new TerrainTileSet { fill = new[] { new WeightedTile { tile = waterFill } }, edgeTiles = waterEdges };
            b.path = new TerrainTileSet { fill = new[] { new WeightedTile { tile = pathFill } }, edgeTiles = pathEdges };
            b.groundVariation = new[] { new WeightedTile { tile = variation } };
            b.vegetation = veg;
            b.vegetationDensity = vegDensity;
            b.darknessIntensity = darkness;
            b.corruptionDensity = corruption;
            b.corruptionTile = corruptionTile;
            AssetDatabase.CreateAsset(b, Root + "/Data/" + assetName + ".asset");
            return b;
        }

        private static POIData MakePoi(string assetName, string display, POIType type, GameObject prefab, Vector2Int fp, int min, int max, bool blocks, float safeRadius)
        {
            var p = ScriptableObject.CreateInstance<POIData>();
            p.displayName = display; p.type = type; p.prefabs = new[] { prefab };
            p.footprint = fp; p.minCount = min; p.maxCount = max; p.blocksMovement = blocks; p.safeZoneRadius = safeRadius;
            AssetDatabase.CreateAsset(p, Root + "/Data/" + assetName + ".asset");
            return p;
        }
    }
}
