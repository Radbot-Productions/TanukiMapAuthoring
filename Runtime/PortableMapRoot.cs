using UnityEngine;

namespace Radbot.MapAuthoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Tanuki/Map Root")]
    public sealed class PortableMapRoot : MonoBehaviour
    {
        [Tooltip("Stable identity of this map. Keep it when publishing a revision.")]
        public string mapId = "my-map";

        private void Reset() { mapId=System.Guid.NewGuid().ToString("N"); }
    }
}
