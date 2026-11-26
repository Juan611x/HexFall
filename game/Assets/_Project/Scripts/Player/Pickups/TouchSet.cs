using System.Collections.Generic;

namespace HexFall.Player
{
    // The things (colliders) of one wizard currently touching a pickup. A pickup is in reach while the set
    // is not empty, so one collider leaving a trigger that others still touch does not end the reach.
    public sealed class TouchSet<T>
    {
        private readonly HashSet<T> _keys = new HashSet<T>();

        public int Count => _keys.Count;
        public bool IsEmpty => _keys.Count == 0;

        // True when this was the first touch (the pickup came into reach).
        public bool Add(T key) => _keys.Add(key) && _keys.Count == 1;

        // True when this was the last touch (the pickup left reach).
        public bool Remove(T key) => _keys.Remove(key) && _keys.Count == 0;

        public void Clear() => _keys.Clear();
    }
}
