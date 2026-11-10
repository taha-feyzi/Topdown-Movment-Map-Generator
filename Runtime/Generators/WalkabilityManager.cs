using System.Collections.Generic;
using UnityEngine;

namespace TopDownWorldGen
{
    public sealed class WalkabilityReport
    {
        public bool startWalkable;
        public int regionCount;
        public int largestRegion;
        public int walkable, blocked, dangerous, special, unknown;
        public readonly List<Vector2Int> unreachableExits = new List<Vector2Int>();

        public bool Success { get { return startWalkable && unreachableExits.Count == 0; } }

        public override string ToString()
        {
            return "Walkability: start " + (startWalkable ? "OK" : "BLOCKED") +
                   ", unreachable exits " + unreachableExits.Count +
                   ", regions " + regionCount + " (largest " + largestRegion + ")" +
                   ", cells W/B/D/S/U = " + walkable + "/" + blocked + "/" + dangerous + "/" + special + "/" + unknown;
        }
    }

    /// <summary>Cell classification, connected regions, reachability validation and repair.</summary>
    public static class WalkabilityManager
    {
        public static bool IsPassable(byte c)
        {
            return c == (byte)CellType.Walkable || c == (byte)CellType.Dangerous || c == (byte)CellType.Special;
        }

        /// <summary>Flood fills (4-neighbour) the passable cells, then checks start and exits.</summary>
        public static WalkabilityReport Validate(WorldMap map)
        {
            int w = map.width, h = map.height, n = w * h;
            var report = new WalkabilityReport();
            var region = new int[n];
            var queue = new int[n];
            int regions = 0, largest = 0;
            int startIdx = map.start.y * w + map.start.x;

            for (int i = 0; i < n; i++)
            {
                switch ((CellType)map.cell[i])
                {
                    case CellType.Walkable: report.walkable++; break;
                    case CellType.Blocked: report.blocked++; break;
                    case CellType.Dangerous: report.dangerous++; break;
                    case CellType.Special: report.special++; break;
                    default: report.unknown++; break;
                }
                if (region[i] != 0 || !IsPassable(map.cell[i])) continue;
                regions++;
                int head = 0, tail = 0, size = 0;
                queue[tail++] = i; region[i] = regions;
                while (head < tail)
                {
                    int c = queue[head++]; size++;
                    int x = c % w, y = c / w;
                    if (x > 0) Try(map, region, queue, ref tail, c - 1, regions);
                    if (x < w - 1) Try(map, region, queue, ref tail, c + 1, regions);
                    if (y > 0) Try(map, region, queue, ref tail, c - w, regions);
                    if (y < h - 1) Try(map, region, queue, ref tail, c + w, regions);
                }
                if (size > largest) largest = size;
            }

            report.regionCount = regions;
            report.largestRegion = largest;
            report.startWalkable = IsPassable(map.cell[startIdx]);
            int startRegion = report.startWalkable ? region[startIdx] : -1;
            foreach (var e in map.exits)
            {
                int ei = e.y * w + e.x;
                if (startRegion <= 0 || region[ei] != startRegion) report.unreachableExits.Add(e);
            }
            return report;
        }

        private static void Try(WorldMap map, int[] region, int[] queue, ref int tail, int idx, int id)
        {
            if (region[idx] != 0 || !IsPassable(map.cell[idx])) return;
            region[idx] = id;
            queue[tail++] = idx;
        }

        /// <summary>
        /// Safety net: if an exit is unreachable, carves an L-shaped 1-cell path from the start and removes objects
        /// whose footprint it crosses. Returns carved cell count.
        /// </summary>
        public static int EnsureConnectivity(WorldMap map)
        {
            var report = Validate(map);
            if (report.Success) return 0;
            var carved = new HashSet<int>();
            var targets = report.startWalkable ? report.unreachableExits : new List<Vector2Int>(map.exits);
            foreach (var e in targets)
            {
                int x = map.start.x, y = map.start.y;
                Carve(map, x, y, carved);
                while (x != e.x) { x += x < e.x ? 1 : -1; Carve(map, x, y, carved); }
                while (y != e.y) { y += y < e.y ? 1 : -1; Carve(map, x, y, carved); }
            }
            for (int i = map.objects.Count - 1; i >= 0; i--)
            {
                var o = map.objects[i];
                bool hit = false;
                for (int fy = 0; fy < o.size.y && !hit; fy++)
                    for (int fx = 0; fx < o.size.x && !hit; fx++)
                        if (carved.Contains((o.cell.y + fy) * map.width + o.cell.x + fx)) hit = true;
                if (hit) map.objects.RemoveAt(i);
            }
            return carved.Count;
        }

        private static void Carve(WorldMap map, int x, int y, HashSet<int> carved)
        {
            int i = y * map.width + x;
            map.terrain[i] = (byte)TerrainType.Path;
            map.cell[i] = (byte)CellType.Walkable;
            map.solidOcc[i] = false;
            carved.Add(i);
        }

        /// <summary>
        /// Marks cells containing a non-trigger Collider2D as Blocked. Requires colliders to exist in the scene.
        /// Returns the number of cells newly marked.
        /// </summary>
        public static int ScanColliders(WorldMap map, Grid grid)
        {
            Physics2D.SyncTransforms();
            Vector3 cs = grid.cellSize;
            int count = 0;
            for (int y = 0; y < map.height; y++)
                for (int x = 0; x < map.width; x++)
                {
                    int i = y * map.width + x;
                    if (!IsPassable(map.cell[i])) continue;
                    Vector3 wp = grid.transform.TransformPoint(new Vector3((x + 0.5f) * cs.x, (y + 0.5f) * cs.y, 0f));
                    Collider2D col = Physics2D.OverlapPoint(wp);
                    if (col != null && !col.isTrigger) { map.cell[i] = (byte)CellType.Blocked; count++; }
                }
            return count;
        }
    }
}
