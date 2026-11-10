using UnityEngine;

namespace TopDownWorldGen
{
    /// <summary>A point of interest (temple, camp, boss arena, chest, ...).</summary>
    [CreateAssetMenu(menuName = "Top-Down World Generator/POI Data", fileName = "NewPOI")]
    public sealed class POIData : ScriptableObject
    {
        public string displayName = "POI";
        public POIType type = POIType.Generic;
        [Tooltip("One of these is chosen at random.")]
        public GameObject[] prefabs = new GameObject[0];

        [Header("Amount")]
        [Min(0)] public int minCount = 0;
        [Min(0)] public int maxCount = 1;
        [Tooltip("Chance for each instance above minCount to be attempted.")]
        [Range(0f, 1f)] public float spawnProbability = 0.5f;

        [Header("Placement rules")]
        [Tooltip("Free space in cells (ground/dirt only, no path, no other object).")]
        public Vector2Int footprint = new Vector2Int(3, 3);
        [Min(0f)] public float minDistanceFromOtherPOIs = 15f;
        [Min(0f)] public float minDistanceFromStartAndExit = 8f;
        [Tooltip("Minimum distance (cells) from the nearest path to the footprint centre.")]
        [Min(0)] public int minPathDistance = 2;
        [Tooltip("Maximum distance from a path. 0 = unlimited.")]
        [Min(0)] public int maxPathDistance = 0;
        public BiomeData[] allowedBiomes;

        [Header("Gameplay")]
        public bool blocksMovement = true;
        [Tooltip("Cell type of the footprint when it does not block movement.")]
        public CellType specialCellType = CellType.Special;
        [Tooltip("If > 0, darkness is cleared in this radius (e.g. checkpoints or rest areas).")]
        [Min(0f)] public float safeZoneRadius = 0f;

        [Header("Look")]
        public string sortingLayer = "";
        public int sortingOrder = 0;
        public bool ySort = true;
    }
}
