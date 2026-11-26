using System;
using System.Collections.Generic;
using HexFall.Player.Spells;
using UnityEngine;

namespace HexFall.Player
{
    // The visible and damaging side of a channelled spell (Water). Child of the wizard, driven by
    // PlayerCombat from the channel events: Begin shows a stretched cylinder tinted with the spell colour,
    // Refresh stretches it every frame, Pulse (one per channel tick) deals that tick's damage to whatever
    // the beam meets first, End hides it. Geometry is BeamTrace; this class only asks the physics engine.
    // Runs after the Cinemachine brain (order 100, LateUpdate): the muzzle (animated staff orb), the wizard's
    // facing and position and the camera aim are all final by then, so the beam never lags a frame behind.
    [DefaultExecutionOrder(ExecutionOrder)]
    public class SpellBeam : MonoBehaviour
    {
        public const int ExecutionOrder = 200;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [Tooltip("Placed at the muzzle facing the aim; its Z scale is the beam length, X and Y the width.")]
        [SerializeField] private Transform pivot;
        [Tooltip("The cylinder's renderer, tinted with the spell colour through a property block.")]
        [SerializeField] private Renderer visual;
        [SerializeField] private float width = 0.25f;

        private GameObject _owner;
        private SpellDefinition _spell;
        private MaterialPropertyBlock _block;
        private Func<Vector3> _origin;
        private Func<Vector3> _aim;
        private readonly List<int> _queuedTicks = new List<int>();

        public bool IsActive { get; private set; }

        // Length of the beam as last drawn; 0 while inactive.
        public float Length { get; private set; }

        // The channel started: show the beam in the spell colour. The owner's colliders are never hit.
        public void Begin(GameObject owner, SpellDefinition spell) => Begin(owner, spell, null, null);

        // With sources, the beam draws itself and deals its queued ticks at the end of every frame.
        public void Begin(GameObject owner, SpellDefinition spell, Func<Vector3> origin, Func<Vector3> aim)
        {
            _origin = origin;
            _aim = aim;
            _queuedTicks.Clear();
            _owner = owner;
            _spell = spell;
            IsActive = true;
            Length = 0f;
            Tint();
            if (pivot != null) pivot.gameObject.SetActive(true);
        }

        // The channel ended, was parried or cancelled: hide the beam.
        public void End()
        {
            IsActive = false;
            Length = 0f;
            _queuedTicks.Clear();
            if (pivot != null) pivot.gameObject.SetActive(false);
        }

        // A channel tick fired mid-frame: its damage lands in the end-of-frame pass, along the final pose.
        public void QueuePulse(int damage)
        {
            if (IsActive) _queuedTicks.Add(damage);
        }

        private void LateUpdate() => UpdateToFinalPose();

        // Draws the beam and deals the queued ticks from the sources' current origin and aim.
        public void UpdateToFinalPose()
        {
            if (!IsActive || _origin == null || _aim == null) return;
            var origin = _origin();
            var aim = _aim();
            Refresh(origin, aim);
            var ticks = _queuedTicks.ToArray();
            _queuedTicks.Clear();
            foreach (var damage in ticks) Pulse(origin, aim, damage);
        }

        // Stretches the visual from the muzzle toward the aim point, to the range or the first surface.
        public void Refresh(Vector3 origin, Vector3 aimPoint)
        {
            if (!IsActive) return;
            var direction = BeamTrace.DirectionTo(origin, aimPoint);
            var segment = Trace(origin, direction, out _);
            Length = segment.Length;
            if (pivot == null || direction == Vector3.zero) return;
            pivot.SetPositionAndRotation(origin, Quaternion.LookRotation(direction));
            var parent = pivot.parent != null ? pivot.parent.lossyScale : Vector3.one;
            pivot.localScale = new Vector3(
                width / Mathf.Max(Mathf.Abs(parent.x), 1e-4f),
                width / Mathf.Max(Mathf.Abs(parent.y), 1e-4f),
                segment.Length / Mathf.Max(Mathf.Abs(parent.z), 1e-4f));
        }

        // One channel tick: damages the IDamageable under the beam right now. A surface that is not
        // damageable only spawns the surface effects; nothing in range does nothing.
        public void Pulse(Vector3 origin, Vector3 aimPoint, int damage)
        {
            if (!IsActive) return;
            var direction = BeamTrace.DirectionTo(origin, aimPoint);
            var segment = Trace(origin, direction, out var hit);
            if (!segment.Hit) return;

            var point = hit.distance <= 0f ? origin : hit.point;
            var normal = hit.distance <= 0f ? -direction : hit.normal;
            var target = hit.collider.GetComponentInParent<IDamageable>();
            target?.TakeHit(new HitInfo(damage, _owner, point, normal, _spell));
            SpawnEffects(target != null, point, normal);
        }

        private BeamSegment Trace(Vector3 origin, Vector3 direction, out RaycastHit nearest)
        {
            nearest = default;
            var range = _spell != null ? _spell.Channel.rangeMeters : 0f;
            if (direction == Vector3.zero || !(range > 0f)) return BeamTrace.Resolve(origin, direction, range, null);

            float? distance = null;
            foreach (var hit in Physics.RaycastAll(origin, direction, range, ~0, QueryTriggerInteraction.Ignore))
            {
                if (OwnerFilter.Owns(_owner, hit.collider.transform)) continue;
                if (distance != null && hit.distance >= distance.Value) continue;
                distance = hit.distance;
                nearest = hit;
            }
            return BeamTrace.Resolve(origin, direction, range, distance);
        }

        private void Tint()
        {
            if (visual == null) return;
            _block ??= new MaterialPropertyBlock();
            _block.Clear();
            if (_spell != null)
            {
                _block.SetColor(BaseColorId, _spell.Color);
                _block.SetColor(ColorId, _spell.Color);
            }
            visual.SetPropertyBlock(_block);
        }

        // The spell's optional effect prefabs; they clean themselves up. None are set yet.
        private void SpawnEffects(bool damageable, Vector3 point, Vector3 normal)
        {
            if (_spell == null) return;
            var rotation = normal.sqrMagnitude > 0f ? Quaternion.LookRotation(normal) : Quaternion.identity;
            foreach (var prefab in HitEffectPlan.Select(damageable, _spell.Effects))
                Instantiate(prefab, point, rotation);
        }
    }
}
