using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TopDownWorldGen.Editor
{
    /// <summary>Right-click a sliced sprite sheet (Sprite Mode: Multiple) > Top-Down World Generator > Create Tiles From Sprite Sheet.</summary>
    public static class SpriteSheetTileBuilder
    {
        [MenuItem("Assets/Top-Down World Generator/Create Tiles From Sprite Sheet", true)]
        private static bool Validate() { return Selection.activeObject is Texture2D; }

        [MenuItem("Assets/Top-Down World Generator/Create Tiles From Sprite Sheet")]
        private static void Create()
        {
            var tex = Selection.activeObject as Texture2D;
            string texPath = AssetDatabase.GetAssetPath(tex);
            Object[] subs = AssetDatabase.LoadAllAssetRepresentationsAtPath(texPath);
            string folder = Path.GetDirectoryName(texPath).Replace('\\', '/') + "/" + tex.name + "_Tiles";
            Directory.CreateDirectory(folder);
            int count = 0;
            foreach (var o in subs)
            {
                var sprite = o as Sprite;
                if (sprite == null) continue;
                var tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprite;
                AssetDatabase.CreateAsset(tile, AssetDatabase.GenerateUniqueAssetPath(folder + "/" + sprite.name + ".asset"));
                count++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[WorldGenerator] Created " + count + " tiles in " + folder +
                      (count == 0 ? " - slice the texture first (Sprite Mode: Multiple > Sprite Editor > Slice)." : ""));
        }
    }
}
