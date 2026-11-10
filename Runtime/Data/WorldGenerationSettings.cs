using UnityEngine;

namespace TopDownWorldGen
{
    /// <summary>All global generation parameters. Biome-specific data lives in <see cref="BiomeData"/>.</summary>
    [CreateAssetMenu(menuName = "Top-Down World Generator/World Generation Settings", fileName = "WorldSettings")]
    public sealed class WorldGenerationSettings : ScriptableObject
    {
        [Header("World")]
        [Min(16)] public int width = 128;
        [Min(16)] public int height = 128;
        public int seed = 12345;
        [Tooltip("Pick a new random seed on every full Generate.")]
        public bool randomSeed = false;
        [Tooltip("Incremental spreads applying tiles/objects over several frames when using GenerateRoutine at runtime.")]
        public GenerationMode generationMode = GenerationMode.Instant;
        [Range(8, 128)] public int chunkSize = 32;
        [Min(1)] public int chunksPerFrame = 2;
        [Min(1)] public int objectsPerFrame = 250;

        [Header("Biomes")]
        public BiomeData[] biomes = new BiomeData[0];
        [Tooltip("Typical biome patch size in cells.")]
        [Min(4f)] public float biomeScale = 48f;
        [Tooltip("Domain warp in cells - makes biome borders organic.")]
        [Range(0f, 30f)] public float biomeWarp = 8f;
        [Range(0, 8)] public int transitionWidth = 3;
        [Range(0f, 1f)] public float transitionBlend = 0.6f;

        [Header("Terrain")]
        [Tooltip("Fraction of the map that is NOT water (before biome bias).")]
        [Range(0.3f, 1f)] public float groundDensity = 0.85f;
        [Min(1f)] public float noiseScale = 20f;
        [Range(1, 6)] public int noiseOctaves = 3;
        [Range(0.1f, 0.9f)] public float persistence = 0.5f;
        [Range(1.2f, 3f)] public float lacunarity = 2f;
        [Range(0, 8)] public int caIterations = 3;
        [Tooltip("Multiplier for each biome's ground-variation chance.")]
        [Range(0f, 3f)] public float terrainVariation = 1f;
        [Min(1f)] public float dirtScale = 12f;
        [Range(0f, 0.8f)] public float dirtDensity = 0.2f;

        [Header("Vegetation")]
        [Range(0f, 3f)] public float treeDensity = 1f;
        [Range(0f, 3f)] public float bushDensity = 1f;
        [Range(0f, 3f)] public float rockDensity = 1f;
        [Range(0f, 3f)] public float decorationDensity = 1f;
        [Tooltip("Multiplies every object's minimum distance.")]
        [Range(0.25f, 3f)] public float objectSpacing = 1f;
        [Min(100)] public int maxObjects = 100000;

        [Header("Paths")]
        [Range(1, 6)] public int pathWidth = 2;
        [Range(0, 12)] public int branchCount = 2;
        [Min(4f)] public float branchLength = 40f;
        [Tooltip("Higher = more winding paths.")]
        [Range(0f, 8f)] public float pathCurviness = 2.5f;
        [Range(0, 6)] public int waypointCount = 2;
        [Range(0, 4)] public int pathSmoothing = 1;
        [Min(1f)] public float pathWaterCost = 20f;
        [Tooltip("Normalised (0..1) start position.")]
        public Vector2 startPosition = new Vector2(0.5f, 0.08f);
        [Tooltip("Normalised (0..1) exit positions. Every exit is connected to the start.")]
        public Vector2[] exitPositions = { new Vector2(0.5f, 0.92f) };

        [Header("Darkness & light")]
        [Range(0f, 2f)] public float darknessMultiplier = 1f;
        [Range(0f, 1f)] public float darknessVariation = 0.3f;
        [Min(1f)] public float darknessScale = 30f;
        [Tooltip("Light sources per cell (0.01 = 1 per 100 cells).")]
        [Range(0f, 0.1f)] public float lightDensity = 0.012f;
        [Min(0f)] public float lightMinDistance = 6f;
        [Tooltip("Radius around start/exits kept free of solid objects and corruption; darkness fades to zero inside.")]
        [Min(0f)] public float safeZoneRadius = 7f;
        [Tooltip("Place a light beside the main path every N cells. 0 = off.")]
        [Min(0)] public int pathLightSpacing = 18;
        [Range(0f, 1f)] public float corruptionDensity = 0.15f;
        [Min(1f)] public float corruptionScale = 14f;

        [Header("Points of interest")]
        public POIData[] pois = new POIData[0];
        [Range(0f, 3f)] public float poiDensity = 1f;

        /// <summary>Checks the settings for errors that would make generation impossible.</summary>
        public bool Validate(out string error)
        {
            if (biomes == null || biomes.Length == 0) { error = "Assign at least one BiomeData in 'Biomes'."; return false; }
            for (int i = 0; i < biomes.Length; i++)
                if (biomes[i] == null) { error = "Biomes[" + i + "] is empty."; return false; }
            if (biomes.Length > 255) { error = "At most 255 biomes are supported."; return false; }
            if (exitPositions == null || exitPositions.Length == 0) { error = "Add at least one exit position."; return false; }
            if (width < 16 || height < 16) { error = "World must be at least 16x16."; return false; }
            error = null;
            return true;
        }
    }
}
