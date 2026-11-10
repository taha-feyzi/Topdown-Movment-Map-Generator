using UnityEngine;
using UnityEngine.Tilemaps;

namespace TopDownWorldGen
{
    /// <summary>One placeable vegetation / decoration type. Use a prefab, or a tile for flat decorations.</summary>
    [CreateAssetMenu(menuName = "Top-Down World Generator/Vegetation Data", fileName = "NewVegetation")]
    public sealed class VegetationData : ScriptableObject
    {
        [Header("What")]
        public VegetationCategory category = VegetationCategory.Tree;
        [Tooltip("Prefab to instantiate. Leave empty to use 'tile' instead.")]
        public GameObject prefab;
        [Tooltip("Used only when no prefab is set. Painted on the Decoration (or Obstacle if blocksMovement) tilemap.")]
        public TileBase tile;

        [Header("Placement")]
        [Tooltip("Relative chance to be chosen among entries of the same category in a biome.")]
        [Min(0f)] public float weight = 1f;
        [Tooltip("Per-cell spawn probability once chosen (before biome / global density multipliers).")]
        [Min(0f)] public float density = 0.05f;
        [Tooltip("Footprint in cells. The cell is the bottom-left of the footprint; object is centred on it.")]
        public Vector2Int footprint = Vector2Int.one;
        [Tooltip("Minimum distance in cells to other objects (scaled by global Object Spacing).")]
        [Min(0f)] public float minDistance = 1f;
        [Tooltip("Empty cells required between this object and any path.")]
        [Min(0)] public int minPathDistance = 1;
        public bool allowOnGround = true, allowOnDirt = true, allowOnWater = false;
        [Tooltip("Empty = allowed in every biome that lists this asset.")]
        public BiomeData[] allowedBiomes;

        [Header("Clusters")]
        [Range(0f, 1f)] public float clusterStrength = 0f;
        [Min(1f)] public float clusterScale = 12f;
        public bool preferNearCategory;
        public VegetationCategory nearCategory = VegetationCategory.Tree;
        [Min(1f)] public float nearBonus = 2f;

        [Header("Look")]
        [Min(0.01f)] public float minScale = 1f;
        [Min(0.01f)] public float maxScale = 1f;
        public RotationMode rotation = RotationMode.None;
        public string sortingLayer = "";
        public int sortingOrder = 0;
        [Tooltip("Top-down depth sorting: lower objects (smaller Y) draw in front.")]
        public bool ySort = true;

        [Header("Collision")]
        public bool blocksMovement = true;
        [Tooltip("Adds a CircleCollider2D when the prefab has no Collider2D and blocksMovement is on.")]
        public bool addColliderIfMissing = false;
        [Min(0.01f)] public float colliderRadius = 0.4f;
    }
}
