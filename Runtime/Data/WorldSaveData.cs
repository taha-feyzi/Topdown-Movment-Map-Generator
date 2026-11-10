using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TopDownWorldGen
{
    /// <summary>Persistent snapshot of a generated world, including manual edits. Stored as a ScriptableObject asset.</summary>
    public sealed class WorldSaveData : ScriptableObject
    {
        [System.Serializable] public struct TileEntry { public Vector3Int position; public TileBase tile; public Color color; }

        [System.Serializable]
        public class TileLayerData
        {
            public TilemapLayerKind kind;
            public List<TileEntry> tiles = new List<TileEntry>();
        }

        [System.Serializable]
        public class ObjectEntry
        {
            public GameObject prefab;
            public ScriptableObject data;
            public GeneratedLayer layer;
            public Vector3 localPosition;
            public Quaternion localRotation = Quaternion.identity;
            public Vector3 localScale = Vector3.one;
            public Vector2Int cell;
            public int sortingOrder;
            public string sortingLayer;
        }

        public int seed;
        public int width;
        public int height;
        public WorldGenerationSettings settings;
        public byte[] biomeMap;
        public byte[] terrainMap;
        public byte[] cellMap;
        public float[] darknessMap;
        public Vector2Int start;
        public List<Vector2Int> exits = new List<Vector2Int>();
        public List<TileLayerData> layers = new List<TileLayerData>();
        public List<ObjectEntry> objects = new List<ObjectEntry>();
    }
}
