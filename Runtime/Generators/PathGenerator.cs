using System.Collections.Generic;
using UnityEngine;

namespace TopDownWorldGen
{
    /// <summary>
    /// Main path(s) start -> exits through jittered waypoints using A* on a noise cost field (winding, 4-connected),
    /// plus branches, brush stamping for width, and smoothing. Water under the path is replaced by path, so the route
    /// is always walkable.
    /// </summary>
    public static class PathGenerator
    {
        public static void Generate(WorldMap map, WorldGenerationSettings s, int seed)
        {
            int w = map.width, h = map.height, n = w * h;
            var rng = new System.Random(seed);
            var noise = new SeededNoise(seed + 17);

            var cost = new float[n];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    cost[i] = 1f + s.pathCurviness * noise.Sample(x * 0.11f, y * 0.11f);
                    if (map.terrain[i] == (byte)TerrainType.Water) cost[i] += s.pathWaterCost;
                }

            map.start = ToCell(s.startPosition, w, h);
            map.exits.Clear();
            foreach (var e in s.exitPositions) map.exits.Add(ToCell(e, w, h));

            var center = new bool[n];
            var astar = new AStar(w, h, cost, center);
            var main = new List<int>();
            var branch = new List<int>();

            foreach (var exit in map.exits)
            {
                var wps = new List<Vector2Int> { map.start };
                float dx = exit.x - map.start.x, dy = exit.y - map.start.y;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                for (int k = 1; k <= s.waypointCount && dist > 1f; k++)
                {
                    float t = k / (float)(s.waypointCount + 1);
                    float off = ((float)rng.NextDouble() * 2f - 1f) * 0.25f * dist;
                    int px = Mathf.Clamp(Mathf.RoundToInt(map.start.x + dx * t - dy / dist * off), 2, w - 3);
                    int py = Mathf.Clamp(Mathf.RoundToInt(map.start.y + dy * t + dx / dist * off), 2, h - 3);
                    wps.Add(new Vector2Int(px, py));
                }
                wps.Add(exit);
                for (int k = 0; k < wps.Count - 1; k++) Route(astar, wps[k], wps[k + 1], w, center, main);
            }

            for (int b = 0; b < s.branchCount && main.Count > 0; b++)
            {
                int origin = main[rng.Next(main.Count)];
                int ox = origin % w, oy = origin / w;
                double ang = rng.NextDouble() * System.Math.PI * 2.0;
                float len = s.branchLength * (0.5f + 0.5f * (float)rng.NextDouble());
                int tx = Mathf.Clamp(Mathf.RoundToInt(ox + (float)System.Math.Cos(ang) * len), 2, w - 3);
                int ty = Mathf.Clamp(Mathf.RoundToInt(oy + (float)System.Math.Sin(ang) * len), 2, h - 3);
                Route(astar, new Vector2Int(ox, oy), new Vector2Int(tx, ty), w, center, branch);
            }

            var mask = new bool[n];
            foreach (int i in main) Stamp(mask, w, h, i % w, i / w, s.pathWidth);
            int bw = Mathf.Max(1, s.pathWidth - 1);
            foreach (int i in branch) Stamp(mask, w, h, i % w, i / w, bw);

            for (int it = 0; it < s.pathSmoothing; it++)
            {
                var next = (bool[])mask.Clone();
                for (int y = 1; y < h - 1; y++)
                    for (int x = 1; x < w - 1; x++)
                    {
                        int i = y * w + x;
                        int c = 0;
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                                if ((dx != 0 || dy != 0) && mask[(y + dy) * w + x + dx]) c++;
                        if (!mask[i] && c >= 5) next[i] = true;
                        else if (mask[i] && !center[i] && c <= 2) next[i] = false;
                    }
                mask = next;
            }

            int count = 0;
            for (int i = 0; i < n; i++)
                if (mask[i])
                {
                    map.terrain[i] = (byte)TerrainType.Path;
                    map.cell[i] = (byte)CellType.Walkable;
                    count++;
                }
            map.mainPath.Clear();
            foreach (int i in main) map.mainPath.Add(new Vector2Int(i % w, i / w));
            map.log.Add("Paths: " + count + " path cells, " + map.exits.Count + " exit(s), " + s.branchCount + " branch(es).");
        }

        private static Vector2Int ToCell(Vector2 norm, int w, int h)
        {
            return new Vector2Int(
                Mathf.Clamp(Mathf.RoundToInt(norm.x * (w - 1)), 2, w - 3),
                Mathf.Clamp(Mathf.RoundToInt(norm.y * (h - 1)), 2, h - 3));
        }

        private static void Route(AStar astar, Vector2Int a, Vector2Int b, int w, bool[] center, List<int> output)
        {
            List<int> seg = astar.Find(a.y * w + a.x, b.y * w + b.x);
            if (seg == null) seg = Straight(a, b, w);
            foreach (int i in seg) { center[i] = true; output.Add(i); }
        }

        private static List<int> Straight(Vector2Int a, Vector2Int b, int w)
        {
            var list = new List<int>();
            int x = a.x, y = a.y;
            list.Add(y * w + x);
            while (x != b.x) { x += x < b.x ? 1 : -1; list.Add(y * w + x); }
            while (y != b.y) { y += y < b.y ? 1 : -1; list.Add(y * w + x); }
            return list;
        }

        private static void Stamp(bool[] mask, int w, int h, int cx, int cy, int width)
        {
            float r = width * 0.5f;
            int ri = Mathf.CeilToInt(r);
            for (int dy = -ri; dy <= ri; dy++)
                for (int dx = -ri; dx <= ri; dx++)
                {
                    if (dx * dx + dy * dy > r * r + 0.25f) continue;
                    int x = cx + dx, y = cy + dy;
                    if (x < 1 || y < 1 || x >= w - 1 || y >= h - 1) continue;
                    mask[y * w + x] = true;
                }
        }

        // ---- A* -------------------------------------------------------------------------------------------
        private sealed class AStar
        {
            private readonly int _w, _h;
            private readonly float[] _cost, _g;
            private readonly bool[] _center;
            private readonly int[] _parent, _seen, _closed;
            private int _stamp;

            public AStar(int w, int h, float[] cost, bool[] centerMask)
            {
                _w = w; _h = h; _cost = cost; _center = centerMask;
                int n = w * h;
                _g = new float[n]; _parent = new int[n]; _seen = new int[n]; _closed = new int[n];
            }

            private float H(int i, int gx, int gy)
            {
                return (Mathf.Abs(i % _w - gx) + Mathf.Abs(i / _w - gy)) * 0.9f;
            }

            public List<int> Find(int start, int goal)
            {
                _stamp++;
                int gx = goal % _w, gy = goal / _w;
                var heap = new MinHeap(1024);
                _g[start] = 0f; _parent[start] = -1; _seen[start] = _stamp;
                heap.Push(H(start, gx, gy), start);
                while (heap.Count > 0)
                {
                    int cur = heap.Pop();
                    if (_closed[cur] == _stamp) continue;
                    _closed[cur] = _stamp;
                    if (cur == goal) break;
                    int x = cur % _w, y = cur / _w;
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0);
                        int ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                        if (nx < 0 || ny < 0 || nx >= _w || ny >= _h) continue;
                        int nb = ny * _w + nx;
                        if (_closed[nb] == _stamp) continue;
                        float step = _cost[nb] * (_center[nb] ? 0.35f : 1f);
                        float ng = _g[cur] + step;
                        if (_seen[nb] != _stamp || ng < _g[nb])
                        {
                            _seen[nb] = _stamp; _g[nb] = ng; _parent[nb] = cur;
                            heap.Push(ng + H(nb, gx, gy), nb);
                        }
                    }
                }
                if (_seen[goal] != _stamp || _closed[goal] != _stamp) return null;
                var path = new List<int>();
                for (int c = goal; c != -1; c = _parent[c]) path.Add(c);
                path.Reverse();
                return path;
            }
        }

        private sealed class MinHeap
        {
            private float[] _keys;
            private int[] _vals;
            public int Count { get; private set; }

            public MinHeap(int capacity) { _keys = new float[capacity]; _vals = new int[capacity]; }

            public void Push(float key, int val)
            {
                if (Count == _keys.Length)
                {
                    System.Array.Resize(ref _keys, Count * 2);
                    System.Array.Resize(ref _vals, Count * 2);
                }
                int i = Count++;
                while (i > 0)
                {
                    int p = (i - 1) >> 1;
                    if (_keys[p] <= key) break;
                    _keys[i] = _keys[p]; _vals[i] = _vals[p];
                    i = p;
                }
                _keys[i] = key; _vals[i] = val;
            }

            public int Pop()
            {
                int result = _vals[0];
                Count--;
                if (Count > 0)
                {
                    float key = _keys[Count];
                    int val = _vals[Count];
                    int i = 0;
                    while (true)
                    {
                        int l = i * 2 + 1;
                        if (l >= Count) break;
                        int r = l + 1;
                        int m = (r < Count && _keys[r] < _keys[l]) ? r : l;
                        if (_keys[m] >= key) break;
                        _keys[i] = _keys[m]; _vals[i] = _vals[m];
                        i = m;
                    }
                    _keys[i] = key; _vals[i] = val;
                }
                return result;
            }
        }
    }
}
