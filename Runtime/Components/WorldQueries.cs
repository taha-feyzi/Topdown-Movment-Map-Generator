using UnityEngine;

namespace TopDownWorldGen
{
    /// <summary>
    /// Game-agnostic helpers so ANY top-down game (any movement/controller) can talk to the generated world:
    /// spawn the player at the start, find exits, ask if a position is walkable, read darkness.
    /// All positions are world space. Requires that a world was generated or loaded (<see cref="WorldGenerator.Map"/>).
    /// </summary>
    public static class WorldQueries
    {
        /// <summary>World position of the centre of the start cell.</summary>
        public static Vector3 GetStartWorldPosition(this WorldGenerator g)
        {
            var m = g.Map;
            return m == null ? g.transform.position : g.tilemaps.CellToWorld(new Vector2(m.start.x + 0.5f, m.start.y + 0.5f));
        }

        /// <summary>World positions of all exits.</summary>
        public static Vector3[] GetExitWorldPositions(this WorldGenerator g)
        {
            var m = g.Map;
            if (m == null) return new Vector3[0];
            var result = new Vector3[m.exits.Count];
            for (int i = 0; i < result.Length; i++)
                result[i] = g.tilemaps.CellToWorld(new Vector2(m.exits[i].x + 0.5f, m.exits[i].y + 0.5f));
            return result;
        }

        /// <summary>Converts a world position to a map cell. Returns false if outside the map or no map exists.</summary>
        public static bool TryWorldToCell(this WorldGenerator g, Vector3 world, out Vector2Int cell)
        {
            cell = default(Vector2Int);
            var m = g.Map;
            if (m == null || g.tilemaps == null || g.tilemaps.grid == null) return false;
            Vector3 local = g.tilemaps.grid.transform.InverseTransformPoint(world);
            Vector3 cs = g.tilemaps.grid.cellSize;
            cell = new Vector2Int(Mathf.FloorToInt(local.x / cs.x), Mathf.FloorToInt(local.y / cs.y));
            return m.InBounds(cell.x, cell.y);
        }

        public static CellType GetCellType(this WorldGenerator g, Vector3 world)
        {
            Vector2Int c;
            if (!g.TryWorldToCell(world, out c)) return CellType.Unknown;
            return (CellType)g.Map.cell[g.Map.Index(c.x, c.y)];
        }

        /// <summary>True for Walkable, Dangerous and Special cells (i.e. not Blocked / Unknown).</summary>
        public static bool IsWalkable(this WorldGenerator g, Vector3 world)
        {
            Vector2Int c;
            if (!g.TryWorldToCell(world, out c)) return false;
            return WalkabilityManager.IsPassable(g.Map.cell[g.Map.Index(c.x, c.y)]);
        }

        /// <summary>Darkness at a world position, 0 = lit .. 1 = darkest. Returns 0 outside the map.</summary>
        public static float GetDarkness(this WorldGenerator g, Vector3 world)
        {
            Vector2Int c;
            if (!g.TryWorldToCell(world, out c)) return 0f;
            return g.Map.darkness[g.Map.Index(c.x, c.y)];
        }
    }
}
