using System;

namespace TopDownWorldGen
{
    /// <summary>Walkability classification of a single grid cell.</summary>
    public enum CellType : byte { Unknown = 0, Walkable = 1, Blocked = 2, Dangerous = 3, Special = 4 }

    /// <summary>Base terrain kind of a cell.</summary>
    public enum TerrainType : byte { Ground = 0, Water = 1, Dirt = 2, Path = 3 }

    public enum VegetationCategory
    {
        Tree = 0, Bush = 1, Rock = 2, Grass = 3, Flower = 4, Mushroom = 5,
        FallenTree = 6, DeadVegetation = 7, Decoration = 8
    }

    public enum POIType
    {
        Generic, Temple, Camp, Ruins, Chest, EnemySpawn, BossArena, Secret,
        SafePoint, Statue, DungeonEntrance
    }

    /// <summary>Scene root an instantiated object lives under.</summary>
    public enum GeneratedLayer { Vegetation = 0, POI = 1, Light = 2, Special = 3 }

    /// <summary>Which generation stages to (re)build / clear.</summary>
    [Flags]
    public enum GenerationStage
    {
        None = 0, Terrain = 1, Paths = 2, POI = 4, Vegetation = 8, Darkness = 16,
        All = Terrain | Paths | POI | Vegetation | Darkness
    }

    public enum TilemapLayerKind { Ground, GroundVariation, Dirt, Water, Path, Decoration, Obstacle }

    public enum RotationMode { None, RandomZ, Random90, FlipX }

    public enum GenerationMode { Instant, Incremental }

    public enum PreviewMode { Biomes, Terrain, Walkability, Darkness, Objects }
}
