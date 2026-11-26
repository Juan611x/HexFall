using System;
using UnityEngine;

namespace HexFall.Player.Spells
{
    // Data only: runtime state (cooldowns, charge) lives in SpellCaster.
    [CreateAssetMenu(fileName = "Spell", menuName = "HexFall/Spell Definition")]
    public sealed class SpellDefinition : ScriptableObject
    {
        [Serializable]
        public struct ProjectileSettings
        {
            public float speed;
            [Tooltip("Downward acceleration in m/s^2. 0 flies straight.")] public float gravity;
            [Tooltip("Horizontal yaw curve in degrees per second. 0 = none.")] public float curveDegreesPerSecond;
            public float sizeScale;
            public float lifetimeSeconds;
        }

        [Serializable]
        public struct ChargeSettings
        {
            public float maxChargeSeconds;
            public int minDamage;
            public int maxDamage;
        }

        [Serializable]
        public struct BurstSettings
        {
            public int count;
            public float intervalSeconds;
            [Tooltip("Angle step between consecutive shots, centred on the aim.")] public float spreadDegrees;
        }

        [Serializable]
        public struct ChannelSettings
        {
            public float durationSeconds;
            public float ticksPerSecond;
            public int damagePerTick;
            public float rangeMeters;
            [Tooltip("Extra cooldown per second actually channelled.")] public float cooldownPerChannelSecond;
        }

        // Optional hooks, all unused for now; consumers must null-check.
        [Serializable]
        public struct SpellEffects
        {
            public GameObject castEffect;
            public GameObject impactEffect;
            public GameObject enemyHitEffect;
            public GameObject surfaceImpactParticles;
            public GameObject surfaceDecal;
            public AudioClip castClip;
            public AudioClip impactClip;
            public AudioClip chargeOrLoopClip;
        }

        [SerializeField] private string displayName = "Spell";
        [SerializeField] private Color color = Color.white;
        [SerializeField] private CastPattern pattern = CastPattern.Single;
        [SerializeField] private int damage = 18;
        [SerializeField] private float cooldownSeconds = 0.5f;
        [SerializeField] private ProjectileSettings projectile = new ProjectileSettings
        {
            speed = 30f, gravity = 0f, curveDegreesPerSecond = 0f, sizeScale = 1f, lifetimeSeconds = 3f,
        };
        [SerializeField] private ChargeSettings charge = new ChargeSettings
        {
            maxChargeSeconds = 1.2f, minDamage = 18, maxDamage = 45,
        };
        [SerializeField] private BurstSettings burst = new BurstSettings
        {
            count = 3, intervalSeconds = 0.08f, spreadDegrees = 3f,
        };
        [SerializeField] private ChannelSettings channel = new ChannelSettings
        {
            durationSeconds = 1.5f, ticksPerSecond = 10f, damagePerTick = 4,
            rangeMeters = 20f, cooldownPerChannelSecond = 0.6f,
        };
        [SerializeField] private SpellEffects effects;

        public string DisplayName => displayName;
        public Color Color => color;
        public CastPattern Pattern => pattern;
        public int Damage => damage;
        public float CooldownSeconds => cooldownSeconds;
        public ProjectileSettings Projectile => projectile;
        public ChargeSettings Charge => charge;
        public BurstSettings Burst => burst;
        public ChannelSettings Channel => channel;
        public SpellEffects Effects => effects;

        public CastConfig ToCastConfig() => new CastConfig
        {
            Pattern = pattern,
            Damage = damage,
            CooldownSeconds = cooldownSeconds,
            MaxChargeSeconds = charge.maxChargeSeconds,
            MinDamage = charge.minDamage,
            MaxDamage = charge.maxDamage,
            BurstCount = burst.count,
            BurstIntervalSeconds = burst.intervalSeconds,
            BurstSpreadDegrees = burst.spreadDegrees,
            ChannelDurationSeconds = channel.durationSeconds,
            ChannelTicksPerSecond = channel.ticksPerSecond,
            ChannelDamagePerTick = channel.damagePerTick,
            ChannelCooldownPerSecond = channel.cooldownPerChannelSecond,
        };

        private void OnValidate() => Sanitize();

        // Clamps obviously invalid authoring values; public so tests can run it without the editor callback.
        public void Sanitize()
        {
            damage = Mathf.Max(0, damage);
            cooldownSeconds = Mathf.Max(0f, cooldownSeconds);

            projectile.speed = Mathf.Max(0f, projectile.speed);
            projectile.gravity = Mathf.Max(0f, projectile.gravity);
            projectile.sizeScale = Mathf.Max(0.01f, projectile.sizeScale);
            projectile.lifetimeSeconds = Mathf.Max(0.01f, projectile.lifetimeSeconds);

            charge.maxChargeSeconds = Mathf.Max(0f, charge.maxChargeSeconds);
            charge.minDamage = Mathf.Max(0, charge.minDamage);
            charge.maxDamage = Mathf.Max(charge.minDamage, charge.maxDamage);

            burst.count = Mathf.Max(1, burst.count);
            burst.intervalSeconds = Mathf.Max(0f, burst.intervalSeconds);
            burst.spreadDegrees = Mathf.Max(0f, burst.spreadDegrees);

            channel.durationSeconds = Mathf.Max(0f, channel.durationSeconds);
            channel.ticksPerSecond = Mathf.Max(0.1f, channel.ticksPerSecond);
            channel.damagePerTick = Mathf.Max(0, channel.damagePerTick);
            channel.rangeMeters = Mathf.Max(0f, channel.rangeMeters);
            channel.cooldownPerChannelSecond = Mathf.Max(0f, channel.cooldownPerChannelSecond);
        }
    }
}
