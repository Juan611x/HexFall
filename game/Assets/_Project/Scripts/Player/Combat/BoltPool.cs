using System.Collections.Generic;
using UnityEngine;

namespace HexFall.Player
{
    // Keeps spent bolts for reuse, one stack per prefab. A pooled bolt that was destroyed meanwhile (a
    // scene unload) is skipped when the stack is popped.
    public static class BoltPool
    {
        private static readonly Dictionary<Bolt, Stack<Bolt>> Free = new Dictionary<Bolt, Stack<Bolt>>();

        public static Bolt Get(Bolt prefab, Vector3 position, Quaternion rotation)
        {
            if (Free.TryGetValue(prefab, out var stack))
            {
                while (stack.Count > 0)
                {
                    var bolt = stack.Pop();
                    if (bolt == null) continue;
                    bolt.transform.SetPositionAndRotation(position, rotation);
                    bolt.gameObject.SetActive(true);
                    return bolt;
                }
            }

            var created = Object.Instantiate(prefab, position, rotation);
            created.PoolKey = prefab;
            created.gameObject.SetActive(true);
            return created;
        }

        // Back to the pool; a bolt that did not come from it (a DummyShooter bolt) is destroyed.
        public static void Release(Bolt bolt)
        {
            if (bolt == null) return;
            var key = bolt.PoolKey;
            if (key == null)
            {
                if (Application.isPlaying) Object.Destroy(bolt.gameObject);
                else Object.DestroyImmediate(bolt.gameObject);
                return;
            }

            bolt.gameObject.SetActive(false);
            if (!Free.TryGetValue(key, out var stack))
            {
                stack = new Stack<Bolt>();
                Free[key] = stack;
            }
            stack.Push(bolt);
        }

        // Forgets every pooled bolt without destroying it; for tests and scene resets.
        public static void Clear() => Free.Clear();
    }
}
