using UnityEngine;
using UnityEngine.Tilemaps;

namespace TopDownWorldGen
{
    [System.Serializable]
    public class LightSourceEntry
    {
        [Tooltip("Prefab that already contains your light (e.g. a Light2D, torch, lamp, glowing mushroom).")]
        public GameObject prefab;
        [Min(0f)] public float weight = 1f;
        [Tooltip("Approx. radius in cells in which the stored darkness map is lowered.")]
        [Min(0f)] public float lightRadius = 4f;
    }

    /// <summary>Independent configuration for one biome.</summary>
    [CreateAssetMenu(menuName = "Top-Down World Generator/Biome Data", fileName = "NewBiome")]
    public sealed class BiomeData : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Biome";
        [Tooltip("Share of the map this biome covers relative to the other biomes.")]
        [Min(0f)] public float distributionWeight = 1f;
        public Color previewColor = Color.green;

        [Header("Terrain tiles")]
        public TerrainTileSet ground = new TerrainTileSet();
        public TerrainTileSet dirt = new TerrainTileSet();
        public TerrainTileSet water = new TerrainTileSet();
        public TerrainTileSet path = new TerrainTileSet();
        public WeightedTile[] groundVariation = new WeightedTile[0];
        [Range(0f, 1f)] public float variationChance = 0.15f;

        [Header("Terrain noise")]
        [Min(0.05f)] public float noiseScaleMultiplier = 1f;
        [Range(-0.3f, 0.3f)] public float waterBias = 0f;
        [Range(-0.3f, 0.3f)] public float dirtBias = 0f;

        [Header("Vegetation")]
        public VegetationData[] vegetation = new VegetationData[0];
        [Tooltip("Special environmental objects (particles, altars...). Same rules as vegetation, spawned under 'Special'.")]
        public VegetationData[] specialObjects = new VegetationData[0];
        [Min(0f)] public float vegetationDensity = 1f;

        [Header("Colour")]
        public Color tint = Color.white;
        [Range(0f, 0.3f)] public float colorVariation = 0.03f;

        [Header("Darkness & light")]
        public Color ambientColor = new Color(0.1f, 0.1f, 0.2f, 1f);
        [Range(0f, 1f)] public float darknessIntensity = 0.5f;
        public LightSourceEntry[] lightSources = new LightSourceEntry[0];
        [Min(0f)] public float lightDensityMultiplier = 1f;
        [Range(0f, 1f)] public float corruptionDensity = 0f;
        [Tooltip("Tile painted on the Ground Variation layer in corrupted cells.")]
        public TileBase corruptionTile;
    }
}
