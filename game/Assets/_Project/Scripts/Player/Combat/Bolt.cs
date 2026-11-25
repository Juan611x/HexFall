using HexFall.Player.Spells;
using UnityEngine;

namespace HexFall.Player
{
    // A spell projectile. Flies a ProjectileFlight (speed, weight x world gravity, drag, curve, lifetime from the spell) and
    // sweeps a sphere along each frame's segment so it cannot tunnel through thin targets; the first
    // collider that does not belong to its owner ends it, hitting it with a HitInfo when that collider
    // has an IDamageable. Pooled: it goes back to BoltPool on a hit or at the end of its life.
    public class Bolt : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private float radius = 0.15f;
        [SerializeField] private float lifetimeSeconds = 3f;
        [SerializeField] private AudioClip impactClip;
        [SerializeField, Range(0f, 1f)] private float impactVolume = 0.9f;

        // Speed used when a spell gives none that is usable (zero, negative or not a number).
        private const float FallbackSpeed = 30f;

        private GameObject _owner;
        private SpellDefinition _spell;
        private ProjectileFlight _flight;
        private int _damage;
        private float _sweepRadius;

        private bool _cached;
        private Vector3 _baseScale;
        private Renderer[] _renderers;
        private TrailRenderer[] _trails;
        private float[] _trailWidths;
        private Gradient[] _trailGradients;
        private MaterialPropertyBlock _block;

        public AudioClip ImpactClip => impactClip;

        // Radius of the sphere the bolt sweeps for hits: base radius x spell size scale x charge scale.
        internal float SweepRadius => _sweepRadius;

        // The prefab this bolt was pooled from; null for a bolt created outside the pool.
        internal Bolt PoolKey { get; set; }

        // A flat projectile with the prefab look and lifetime (what the DummyShooter fires).
        public void Launch(GameObject owner, Vector3 direction, float speed, int damage)
        {
            Launch(owner, direction, new SpellDefinition.ProjectileSettings
            {
                speed = speed, weight = 0f, drag = 0f, curveDegreesPerSecond = 0f, sizeScale = 1f, lifetimeSeconds = lifetimeSeconds,
            }, damage, null);
        }

        // Launches with the spell's ballistics, size and colour. A null spell keeps the prefab look. A charged
        // spell's charge (0..1) scales the bolt, shifts its colour and speeds it up per the spell's feedback settings.
        public void Launch(GameObject owner, Vector3 direction, SpellDefinition.ProjectileSettings projectile, int damage,
            SpellDefinition spell, float charge = 0f)
        {
            Cache();
            _owner = owner;
            _damage = damage;
            _spell = spell;
            var look = spell != null ? spell.LookAtCharge(charge) : new ChargeLook(1f, default, 1f);
            var scale = (projectile.sizeScale > 0f ? projectile.sizeScale : 1f) * look.Scale;
            _sweepRadius = radius * scale;
            var velocity = direction.normalized * (Usable(projectile.speed, FallbackSpeed) * look.SpeedScale);
            // The domain takes an acceleration: the spell's weight times the world gravity (read here, not in the domain).
            _flight = new ProjectileFlight(transform.position, velocity, projectile.weight * Physics.gravity.magnitude,
                projectile.curveDegreesPerSecond, Usable(projectile.lifetimeSeconds, lifetimeSeconds), projectile.drag);
            if (velocity.sqrMagnitude > 0f) transform.rotation = Quaternion.LookRotation(velocity);
            ApplyLook(scale, spell, new Color(look.Color.R, look.Color.G, look.Color.B, spell != null ? spell.Color.a : 1f));
        }

        // A positive finite value, else the fallback: a spell with no speed or lifetime must not fire nothing or hover.
        private static float Usable(float value, float fallback) => value > 0f && !float.IsInfinity(value) ? value : fallback;

        private void Cache()
        {
            if (_cached) return;
            _cached = true;
            _baseScale = transform.localScale;
            _renderers = GetComponentsInChildren<Renderer>(true);
            _trails = GetComponentsInChildren<TrailRenderer>(true);
            _trailWidths = new float[_trails.Length];
            _trailGradients = new Gradient[_trails.Length];
            for (var i = 0; i < _trails.Length; i++)
            {
                _trailWidths[i] = _trails[i].widthMultiplier;
                var g = _trails[i].colorGradient;
                _trailGradients[i] = new Gradient { colorKeys = g.colorKeys, alphaKeys = g.alphaKeys, mode = g.mode };
            }
            _block = new MaterialPropertyBlock();
        }

        // Size, tint and a clean trail, so a reused bolt carries nothing over from its last flight.
        private void ApplyLook(float scale, SpellDefinition spell, Color color)
        {
            transform.localScale = _baseScale * scale;
            foreach (var r in _renderers) Tint(r, spell, color);

            for (var i = 0; i < _trails.Length; i++)
            {
                _trails[i].widthMultiplier = _trailWidths[i] * scale;
                _trails[i].colorGradient = spell != null ? Tinted(_trailGradients[i], color) : _trailGradients[i];
                _trails[i].Clear();
            }
        }

        // Colour from the spell via a property block, so no material is created or modified.
        private void Tint(Renderer target, SpellDefinition spell, Color color)
        {
            if (spell == null)
            {
                target.SetPropertyBlock(null);
                return;
            }
            _block.Clear();
            _block.SetColor(BaseColorId, color);
            _block.SetColor(ColorId, color);
            target.SetPropertyBlock(_block);
        }

        // The prefab gradient's alpha fade with the spell colour.
        private static Gradient Tinted(Gradient source, Color color)
        {
            var keys = source.colorKeys;
            for (var i = 0; i < keys.Length; i++) keys[i].color = color;
            return new Gradient { colorKeys = keys, alphaKeys = source.alphaKeys, mode = source.mode };
        }

        private void Update() => Advance(Time.deltaTime);

        // One frame of flight and hit detection; the part of Update that tests can drive.
        internal void Advance(float dt)
        {
            if (_flight == null || float.IsNaN(dt) || float.IsInfinity(dt) || dt < 0f) return;
            if (_flight.IsExpired)
            {
                Finish();
                return;
            }

            var (from, to) = _flight.Step(dt);
            var delta = to - from;
            var distance = delta.magnitude;
            if (distance > 1e-6f && TrySweep(from, delta / distance, distance))
            {
                Finish();
                return;
            }

            transform.position = to;
            var velocity = _flight.Velocity;
            if (velocity.sqrMagnitude > 0f) transform.rotation = Quaternion.LookRotation(velocity);
        }

        // Sweeps the segment; true when it hit something that is not the owner.
        private bool TrySweep(Vector3 from, Vector3 direction, float distance)
        {
            var hits = Physics.SphereCastAll(from, _sweepRadius, direction, distance, ~0, QueryTriggerInteraction.Ignore);
            RaycastHit? nearest = null;
            foreach (var hit in hits)
            {
                if (OwnerFilter.Owns(_owner, hit.collider.transform)) continue;
                if (nearest == null || hit.distance < nearest.Value.distance) nearest = hit;
            }

            if (nearest == null) return false;
            var best = nearest.Value;
            // A sweep that starts inside a collider reports no useful point or normal.
            var point = best.distance <= 0f ? from : best.point;
            var normal = best.distance <= 0f ? -direction : best.normal;
            var target = best.collider.GetComponentInParent<IDamageable>();
            target?.TakeHit(new HitInfo(_damage, _owner, point, normal, _spell));
            SpawnEffects(target != null, point, normal);
            // Any impact (target or world) is heard where it lands.
            if (impactClip != null) AudioSource.PlayClipAtPoint(impactClip, point, impactVolume * EffectsVolume.Current());
            return true;
        }

        // The spell's optional effect prefabs; they clean themselves up. None are set yet.
        private void SpawnEffects(bool damageable, Vector3 point, Vector3 normal)
        {
            if (_spell == null) return;
            var rotation = normal.sqrMagnitude > 0f ? Quaternion.LookRotation(normal) : Quaternion.identity;
            foreach (var prefab in HitEffectPlan.Select(damageable, _spell.Effects))
                Instantiate(prefab, point, rotation);
        }

        private void Finish()
        {
            _flight = null;
            BoltPool.Release(this);
        }
    }
}
