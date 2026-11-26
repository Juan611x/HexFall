using System;
using UnityEngine;

namespace HexFall.Player.Spells
{
    // The points on the wizard a spell can start from, by id: the bolt muzzle, the orb on the staff, and any
    // other point a future spell asks for. A spell names its anchor (SpellDefinition.CastAnchor); an empty or
    // unknown id, or an anchor that is missing, falls back to the Muzzle entry.
    public class CastAnchors : MonoBehaviour
    {
        public const string Muzzle = "Muzzle";
        public const string StaffOrb = "StaffOrb";

        [Serializable]
        public struct Entry
        {
            public string id;
            public Transform anchor;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        // Replaces the entries (used by tests and editor setup; the prefab serializes them).
        public void Configure(params (string Id, Transform Anchor)[] map)
        {
            entries = new Entry[map.Length];
            for (var i = 0; i < map.Length; i++) entries[i] = new Entry { id = map[i].Id, anchor = map[i].Anchor };
        }

        // The transform for the id, or the Muzzle's when the id is empty, unknown or its anchor is gone; null
        // only when there is no usable Muzzle either.
        public Transform Resolve(string id)
        {
            var found = Find(string.IsNullOrWhiteSpace(id) ? Muzzle : id);
            return found != null ? found : Find(Muzzle);
        }

        // World position of the anchor, or the given fallback when nothing resolves.
        public Vector3 PositionFor(string id, Vector3 fallback)
        {
            var anchor = Resolve(id);
            return anchor != null ? anchor.position : fallback;
        }

        private Transform Find(string id)
        {
            if (entries == null) return null;
            foreach (var entry in entries)
            {
                if (entry.anchor == null) continue; // Unity null: also covers a destroyed transform
                if (string.Equals(entry.id, id, StringComparison.OrdinalIgnoreCase)) return entry.anchor;
            }
            return null;
        }
    }
}
