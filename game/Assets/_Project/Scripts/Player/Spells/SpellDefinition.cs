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

            [Header("Damage")]
            [Tooltip("Damage before the charge multiplier: what a tap deals and what the multiplier scales.")]
            [UnityEngine.Serialization.FormerlySerializedAs("minDamage")] public int baseDamage;
            [Tooltip("Constant: damageMultiplier applies at full charge only. Curve: the damage curve sets the multiplier at every charge.")]
            public ChargeDamageMode damageMode;
            [Tooltip("Constant mode: multiplier on the base damage at full charge; any lesser charge deals the base damage.")]
            public float constantMultiplier;
            [Tooltip("Curve mode: x = charge (0 = tap, 1 = full), y = multiplier on the base damage.")]
            public AnimationCurve damageCurve;

            [Header("Feedback")]
            [Tooltip("Projectile and orb size multiplier at no charge.")] public float minSizeScale;
            [Tooltip("Projectile and orb size multiplier at full charge.")] public float maxSizeScale;
            [Tooltip("Colour the spell colour blends toward as the charge grows.")] public Color fullChargeColor;
            [Tooltip("How far toward the full-charge colour a full charge goes: 0 keeps the spell colour, 1 reaches it.")]
            public float colorBlend;
            [Tooltip("Projectile speed multiplier at full charge (1 = no change).")] public float speedScaleAtMax;
            [Tooltip("Show a charging orb at the muzzle while the charge is held.")] public bool showOrb;
            [Tooltip("Orb diameter in metres at size scale 1.")] public float orbSize;

            [Header("Animation")]
            [Tooltip("UpperBody animator state played while charging. An unknown name falls back to the Charging mapping.")]
            public string windUpState;
            [Tooltip("Normalized time of that state where the pose is held while the charge lasts.")]
            public float windUpHoldNormalized;
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
            maxChargeSeconds = 1.2f, baseDamage = 18, damageMode = ChargeDamageMode.Constant,
            constantMultiplier = 1f, damageCurve = FlatCurve(),
            minSizeScale = 1f, maxSizeScale = 1f, fullChargeColor = Color.white, colorBlend = 0f, speedScaleAtMax = 1f,
            showOrb = true, orbSize = 0.25f, windUpState = "Charge", windUpHoldNormalized = 0.5f,
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

        public ChargeFeedbackConfig ToFeedbackConfig() => new ChargeFeedbackConfig
        {
            MinScale = charge.minSizeScale,
            MaxScale = charge.maxSizeScale,
            ColorBlend = charge.colorBlend,
            FullColor = ToRgb(charge.fullChargeColor),
            SpeedScaleAtMax = charge.speedScaleAtMax,
        };

        // Size, colour and speed of the projectile (or orb) at a charge level. Only a Charged spell changes
        // with charge; every other pattern keeps its own colour and size.
        public ChargeLook LookAtCharge(float level) => pattern == CastPattern.Charged
            ? ChargeFeedback.Evaluate(ToFeedbackConfig(), ToRgb(color), level)
            : new ChargeLook(1f, ToRgb(color), 1f);

        private static Rgb ToRgb(Color c) => new Rgb(c.r, c.g, c.b);

        public CastConfig ToCastConfig() => new CastConfig
        {
            Pattern = pattern,
            Damage = damage,
            CooldownSeconds = cooldownSeconds,
            MaxChargeSeconds = charge.maxChargeSeconds,
            BaseDamage = charge.baseDamage,
            ChargeMode = charge.damageMode,
            ChargeMultiplier = charge.constantMultiplier,
            ChargeCurve = BakeDamageCurve(charge.damageCurve),
            BurstCount = burst.count,
            BurstIntervalSeconds = burst.intervalSeconds,
            BurstSpreadDegrees = burst.spreadDegrees,
            ChannelDurationSeconds = channel.durationSeconds,
            ChannelTicksPerSecond = channel.ticksPerSecond,
            ChannelDamagePerTick = channel.damagePerTick,
            ChannelCooldownPerSecond = channel.cooldownPerChannelSecond,
        };

        private static AnimationCurve FlatCurve() => AnimationCurve.Linear(0f, 1f, 1f, 1f);

        // The domain never sees the AnimationCurve: it gets a table of multipliers sampled over charge 0..1.
        private static float[] BakeDamageCurve(AnimationCurve curve) =>
            curve == null || curve.length == 0
                ? ChargeDamage.Bake(x => 1f)
                : ChargeDamage.Bake(curve.Evaluate);

        private static bool IsUsable(AnimationCurve curve)
        {
            if (curve == null || curve.length == 0) return false;
            foreach (var k in curve.keys)
                if (!IsFinite(k.time) || !IsFinite(k.value)) return false;
            return true;
        }

        private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        private void OnValidate() => Sanitize();

        // NaN and +/-Infinity fall back to 0 so the clamps below always see a finite number.
        private static float Finite(float v) => float.IsNaN(v) || float.IsInfinity(v) ? 0f : v;

        // Clamps obviously invalid authoring values; public so tests can run it without the editor callback.
        public void Sanitize()
        {
            damage = Mathf.Max(0, damage);
            cooldownSeconds = Mathf.Max(0f, Finite(cooldownSeconds));

            projectile.speed = Mathf.Max(0f, Finite(projectile.speed));
            projectile.gravity = Mathf.Max(0f, Finite(projectile.gravity));
            projectile.curveDegreesPerSecond = Finite(projectile.curveDegreesPerSecond);
            projectile.sizeScale = Mathf.Max(0.01f, Finite(projectile.sizeScale));
            projectile.lifetimeSeconds = Mathf.Max(0.01f, Finite(projectile.lifetimeSeconds));

            charge.maxChargeSeconds = Mathf.Max(0f, Finite(charge.maxChargeSeconds));
            charge.baseDamage = Mathf.Max(0, charge.baseDamage);
            charge.constantMultiplier = !IsFinite(charge.constantMultiplier) || charge.constantMultiplier < 0f
                ? 1f : charge.constantMultiplier;
            if (!IsUsable(charge.damageCurve)) charge.damageCurve = FlatCurve();
            charge.minSizeScale = Mathf.Max(0.01f, Finite(charge.minSizeScale));
            charge.maxSizeScale = Mathf.Max(0.01f, Finite(charge.maxSizeScale));
            charge.colorBlend = Mathf.Clamp01(Finite(charge.colorBlend));
            charge.speedScaleAtMax = Mathf.Max(0.01f, Finite(charge.speedScaleAtMax));
            charge.orbSize = Mathf.Max(0f, Finite(charge.orbSize));
            charge.windUpHoldNormalized = Mathf.Clamp01(Finite(charge.windUpHoldNormalized));

            burst.count = Mathf.Max(1, burst.count);
            burst.intervalSeconds = Mathf.Max(0f, Finite(burst.intervalSeconds));
            burst.spreadDegrees = Mathf.Max(0f, Finite(burst.spreadDegrees));

            channel.durationSeconds = Mathf.Max(0f, Finite(channel.durationSeconds));
            channel.ticksPerSecond = Mathf.Max(0.1f, Finite(channel.ticksPerSecond));
            channel.damagePerTick = Mathf.Max(0, channel.damagePerTick);
            channel.rangeMeters = Mathf.Max(0f, Finite(channel.rangeMeters));
            channel.cooldownPerChannelSecond = Mathf.Max(0f, Finite(channel.cooldownPerChannelSecond));
        }
    }
}
