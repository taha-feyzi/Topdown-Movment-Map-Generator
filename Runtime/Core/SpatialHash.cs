using System.Collections.Generic;
using UnityEngine;

namespace TopDownWorldGen
{
    /// <summary>Bucketed point set used for minimum-distance checks between placed objects.</summary>
    public sealed class SpatialHash
    {
        private struct Item { public float x, y, minDist; }

        private readonly Dictionary<int, List<Item>> _buckets = new Dictionary<int, List<Item>>();
        private readonly float _size;
        private float _maxMin;

        public SpatialHash(float bucketSize = 8f) { _size = Mathf.Max(1f, bucketSize); }

        private static int Key(int bx, int by) { unchecked { return bx * 100003 + by; } }

        /// <summary>True if no stored item is closer than max(minDist, item.minDist).</summary>
        public bool IsFree(float x, float y, float minDist)
        {
            float r = Mathf.Max(minDist, _maxMin);
            if (r <= 0f) return true;
            int range = Mathf.CeilToInt(r / _size);
            int bx = Mathf.FloorToInt(x / _size), by = Mathf.FloorToInt(y / _size);
            for (int dy = -range; dy <= range; dy++)
                for (int dx = -range; dx <= range; dx++)
                {
                    List<Item> list;
                    if (!_buckets.TryGetValue(Key(bx + dx, by + dy), out list)) continue;
                    for (int i = 0; i < list.Count; i++)
                    {
                        Item it = list[i];
                        float need = Mathf.Max(minDist, it.minDist);
                        float ddx = it.x - x, ddy = it.y - y;
                        if (ddx * ddx + ddy * ddy < need * need) return false;
                    }
                }
            return true;
        }

        public void Add(float x, float y, float minDist)
        {
            int k = Key(Mathf.FloorToInt(x / _size), Mathf.FloorToInt(y / _size));
            List<Item> list;
            if (!_buckets.TryGetValue(k, out list)) { list = new List<Item>(4); _buckets[k] = list; }
            list.Add(new Item { x = x, y = y, minDist = minDist });
            if (minDist > _maxMin) _maxMin = minDist;
        }
    }
}
