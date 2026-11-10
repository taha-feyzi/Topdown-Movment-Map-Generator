# Top-Down World Generator for Unity

Generate complete top-down 2D environments from data you define: **biomes, autotiled terrain, vegetation clusters,
guaranteed-walkable paths, points of interest, optional darkness/light data** – with a live preview, undo, and save/load.

* Works with **any** top-down game and **any** movement/controller (it only produces tiles, objects and queryable data).
* No game-specific code. All content (tiles, prefabs, biomes) is yours, configured with ScriptableObjects.
* Seed-deterministic: same seed + settings = same world.
* No render-pipeline dependency (Built-in / URP / HDRP 2D). Light sources are your own prefabs.
* Unity **2022.3+** (developed for Unity 6). Uses only built-in Tilemap + Physics2D modules.



## Install
**Package Manager → + → Add package from git URL…**
```
https://github.com/taha-feyzi/Topdown-Movment-Map-Generator.git
```
(or download the repo and use *Add package from disk…* → `package.json`).

## 2-minute start
1. `Tools > Top-Down World Generator > World Generator`
2. **Create Generator In Scene**
3. **Create Demo Assets** (placeholder art, 3 biomes, 2 POIs, settings – created in `Assets/TopDownWorldGenerator/Demo`)
4. **Generate World**. Try **Preview** modes: Biomes / Terrain / Walkability / Darkness / Objects.

## Use your own art
1. Slice your sprite sheet (Sprite Mode *Multiple*), right-click it → **Top-Down World Generator > Create Tiles From Sprite Sheet**.
2. `Create > Top-Down World Generator > Biome Data` – fill the tile sets, vegetation lists, lights.
3. `Create > Top-Down World Generator > Vegetation Data / POI Data / World Generation Settings`.
4. Assign biomes (and POIs) in the settings, assign the settings in the window, press Generate.

Prefab pivot: objects are centred on their footprint (a 2×2 tree sits centred on its 2×2 cells).

## Use it in your game (any controller)
```csharp
using TopDownWorldGen;

[SerializeField] WorldGenerator world;
[SerializeField] Transform player;

void Start()
{
    world.Generate();                                   // or load a saved world
    player.position = world.GetStartWorldPosition();    // spawn at the path start
    foreach (var exit in world.GetExitWorldPositions()) { /* place level exits */ }
}

bool CanStep(Vector3 pos) => world.IsWalkable(pos);     // Walkable / Dangerous / Special
float dark = world.GetDarkness(player.position);        // 0 lit .. 1 dark (optional feature)
```
Raw data: `world.Map` → `cell`, `terrain`, `biome`, `darkness`, `start`, `exits`, `objects`.
Colliders: Water/Obstacle tilemaps get `TilemapCollider2D + CompositeCollider2D`; tile/prefab colliders are yours.

## Autotiling
Each terrain layer of a biome (`ground`, `dirt`, `water`, `path`) is a `TerrainTileSet`:
* **fill** – weighted random variants (can be a RuleTile → Unity handles neighbours itself).
* **edgeTiles** (optional, exactly 16) – built-in neighbour-aware tiles. Index = mask of neighbours of the same layer:
  `bit0 North, bit1 East, bit2 South, bit3 West`; index 15 (all present) uses fill; missing entries fall back to fill.
Biome borders are blended (`transitionWidth`, `transitionBlend`).

## Features
| Area | What you get |
|---|---|
| Biomes | Any number per map, weighted distribution, warped organic borders, border blending, per-biome tiles/vegetation/noise/tint/darkness |
| Terrain | Noise + cellular automata water, dirt patches, ground variation, neighbour-aware autotiling, 7 tilemap layers |
| Vegetation | Weighted entries, clusters, near-category bonus (bushes near trees), min distance, multi-cell footprints, scale/rotation/flip, y-sorting, colliders |
| Paths | A* winding main path(s) to one or more exits, branches, width, smoothing, never blocked by trees/POIs |
| POIs | Per-POI count/probability, footprint, biome filter, path-distance range, spacing, blocks or marks special cells |
| Darkness / light (optional) | Per-cell darkness map, safe zones, corruption cells, light prefabs along the path and in the dark |
| Walkability | Walkable / Blocked / Dangerous / Special / Unknown, connected regions, start→exit validation + automatic repair, optional collider scan |
| Editor | Sectioned window, progress bar, per-layer regenerate, preview overlay, undo, save/load |
| Performance | Chunked `SetTiles`, hash-based per-cell randomness, spatial hash, cached references, incremental runtime generation |

## Architecture
```
Runtime/Core        enums, SeededNoise + hashing, SpatialHash, WorldMap (pure data), GeneratedObject marker
Runtime/Data        BiomeData, VegetationData, POIData, WorldGenerationSettings, WorldSaveData, TerrainTileSet
Runtime/Generators  Biome, Terrain, Path, POI, Vegetation, Darkness, WalkabilityManager
Runtime/Components  WorldGenerator (orchestrator), WorldTilemaps (layers), WorldPreview, WorldSaveManager, WorldQueries
Editor              World Generator window, Demo asset creator, sprite-sheet → tiles tool
Tests               EditMode tests
```
`WorldGenerator.BuildMap()` computes a pure-data `WorldMap` (deterministic, no scene access) →
`ApplyRoutine()` writes chunked tiles and instantiates prefabs. Every stage has its own derived seed and per-cell hashing,
so **regenerating one layer doesn't change the others**.

## Editing, regeneration, saving
* Paint or move things freely. **Regenerate X** only clears layer X (tiles + objects carrying `GeneratedObject`).
* Objects you add yourself (no `GeneratedObject`) are never deleted.
* **Save Generated World** snapshots the real scene (manual edits included) to a `WorldSaveData` asset; **Load** restores it.
* Runtime incremental generation:
```csharp
settings.generationMode = GenerationMode.Incremental;
StartCoroutine(world.GenerateRoutine(GenerationStage.All, (p, msg) => Debug.Log($"{p:P0} {msg}")));
```

## Testing checklist
- [ ] Compiles, menu `Tools > Top-Down World Generator` visible
- [ ] Test Runner (EditMode): determinism, reachability on 12 water-heavy seeds, no overlap, invalid settings
- [ ] Demo Assets → Generate: terrain, autotiled water/path edges, trees, POIs, lights, no Console errors
- [ ] Same seed twice → identical; Randomize Seed → different
- [ ] All preview modes work; Hide removes the overlay
- [ ] Validate Walkability → reachable (also with *Include colliders*)
- [ ] Walk start → exit with your player; trees never block the path
- [ ] Regenerate Vegetation keeps terrain and manual tile edits, no duplicates; Clear removes all generated content
- [ ] Save → Clear → Load restores everything; Ctrl+Z undoes a Generate
- [ ] 400×400 map generates without freezing

## Known limitations
* Data generation runs on the main thread (ScriptableObject data isn't safe off-thread); applying can be incremental.
* No object pooling; tile-based decoration is batched instead. Very prefab-heavy maps (>20k objects) instantiate slowly.
* Built-in autotiling = 16-tile 4-neighbour mask (no 47-tile corner blobs). Use a RuleTile as `fill` for corners.
* Collider scan needs baked colliders in the scene.
* Saves are ScriptableObject assets (editor-time); runtime saving in builds is not included.
* Loading restores scene + walkability/darkness maps, not the in-memory `PlacedObject` list.
* Tile-based vegetation paints 1×1 tiles only. Maps up to ~512×512 are comfortable.
