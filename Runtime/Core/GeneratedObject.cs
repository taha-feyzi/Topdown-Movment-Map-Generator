using UnityEngine;

namespace TopDownWorldGen
{
    /// <summary>
    /// Marker added to every object spawned by the generator. Clear/Regenerate only removes objects that carry it,
    /// so objects you place manually are never deleted. Add this component to a manual object (and parent it under
    /// the Generated roots) if you want it included in Save.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GeneratedObject : MonoBehaviour
    {
        public GameObject sourcePrefab;
        public GeneratedLayer layer;
        public ScriptableObject data;
        public Vector2Int cell;
    }
}
