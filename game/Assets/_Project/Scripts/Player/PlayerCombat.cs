using HexFall.Player.Actions;
using HexFall.Player.Spells;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HexFall.Player
{
    // Receives the Attack and Parry actions from PlayerInput (Send Messages), applies the attack cooldown
    // and the shield rules, and drives the shield VFX. Incoming damage goes through
    // ResolveHit, which says how much of it the shield absorbed.
    public class PlayerCombat : MonoBehaviour
    {
        [Header("Spell")]
        [Tooltip("The spell the attack casts; the Base spell by default. Cooldown, damage and projectile speed come from it.")]
        [SerializeField] private SpellDefinition activeSpell;
        [Header("Bolt")]
        [SerializeField] private Bolt boltPrefab;
        [SerializeField] private Transform muzzle;
        [SerializeField] private float boltDelaySeconds = 0.2f;
        [SerializeField] private float aimDistance = 100f;
        [Tooltip("How long after the bolt leaves the wizard keeps facing the crosshair.")]
        [SerializeField] private float faceAimHoldSeconds = 0.3f;
        [Tooltip("How fast the wizard turns to the crosshair when attacking (a fast turn, not a snap).")]
        [SerializeField] private float faceAimDegreesPerSecond = 1800f;
        [Header("Shield")]
        [SerializeField] private int shieldCharges = 3;
        [SerializeField] private float chargeRechargeSeconds = 6f;
        [SerializeField] private float perfectParrySeconds = 0.15f;
        [SerializeField] private float guardSeconds = 0.4f;
        [SerializeField, Range(0f, 1f)] private float partialBlock = 0.5f;
        [SerializeField] private MagicShield shield;

        // Counts cast starts (Attack, Charging, Channelling), so Attack() also reports an attack chained on the tick the last one ended.
        private int _attackStarts;

        // The Attack input action, to notice a hold whose release was lost. Null without PlayerInput.
        private InputAction _attackAction;

        // The action state machine; the IK, animator and audio adapters read it. Null before Awake.
        public ActionMachine Machine { get; private set; }

        public int MaxShieldCharges => shieldCharges;
        public int ShieldCharges => Machine?.ShieldCharges(Time.time) ?? shieldCharges;

        // True from the attack until the wizard stops facing the crosshair, and while the shield guard
        // is up. Used by the IK to aim the head and chest harder.
        public bool IsActing => Machine != null && Machine.IsActing;

        private void Awake()
        {
            var playerInput = GetComponent<PlayerInput>();
            _attackAction = playerInput != null && playerInput.actions != null ? playerInput.actions.FindAction("Attack") : null;
            Machine =new ActionMachine(new ActionSettings
            {
                boltDelaySeconds = boltDelaySeconds,
                faceAimHoldSeconds = faceAimHoldSeconds,
                shieldCharges = shieldCharges,
                chargeRechargeSeconds = chargeRechargeSeconds,
                perfectParrySeconds = perfectParrySeconds,
                guardSeconds = guardSeconds,
                partialBlock = partialBlock,
            }, CasterFor(activeSpell));
            Machine.StateChanged += OnActionChanged;
            Machine.ShotFired += FireShot;
            Machine.HitResolved += OnHitResolved;
        }

        private void OnDestroy()
        {
            if (Machine == null) return;
            Machine.StateChanged -= OnActionChanged;
            Machine.ShotFired -= FireShot;
            Machine.HitResolved -= OnHitResolved;
        }

        // The shield flash follows the action state; the animator follows it in ActionAnimatorBridge.
        private void OnActionChanged(ActionStateId from, ActionStateId to)
        {
            if (ActionStateRules.StartsCast(from, to))
            {
                _attackStarts++;
            }
            else if (to == ActionStateId.Parry)
            {
                if (shield != null) shield.Flash(guardSeconds);
            }
        }

        private void OnHitResolved(HitResult hit)
        {
            if (hit.Grade != ParryGrade.None && shield != null) shield.Deflect();
        }

        // The Attack action is Press-and-Release: it reports the press and the release, which a charge
        // and a channel need.
        public void OnAttack(InputValue value)
        {
            if (value.isPressed) Attack();
            else ReleaseAttack();
        }

        public void ReleaseAttack()
        {
            Machine.Tick(new ActionIntent(false, false, true), Time.time);
        }

        // The spell the attack casts. Null means the Base default of the action machine.
        public SpellDefinition ActiveSpell => activeSpell;

        // Swaps the spell; a cast in progress is cancelled.
        public void SetSpell(SpellDefinition spell)
        {
            activeSpell = spell;
            if (Machine != null) Machine.SetCaster(CasterFor(spell) ?? new SpellCaster(BaseConfig), Time.time);
        }

        private static readonly CastConfig BaseConfig = new CastConfig
        {
            Pattern = CastPattern.Single, Damage = 18, CooldownSeconds = 0.5f,
        };

        private static SpellCaster CasterFor(SpellDefinition spell) =>
            spell != null ? new SpellCaster(spell.ToCastConfig()) : null;

        public void OnParry(InputValue value)
        {
            if (value.isPressed) Parry();
        }

        public bool Attack()
        {
            var before = _attackStarts;
            Machine.Tick(new ActionIntent(true, false), Time.time);
            return _attackStarts != before;
        }

        private void Update()
        {
            Step(Time.time, AttackHeld());
        }

        // One frame of the machine. A hold (charge, channel) whose attack input is no longer held lost its
        // release (focus lost, input disabled): it is dropped, not fired.
        internal void Step(float now, bool attackHeld)
        {
            var lost = !attackHeld && Machine.IsHolding;
            Machine.Tick(new ActionIntent(false, false, false, lost), now);
        }

        // False only when the Attack action exists and is not pressed; without PlayerInput nothing can
        // say the hold ended, so it keeps going.
        private bool AttackHeld()
        {
            if (_attackAction == null) return true;
            return _attackAction.IsPressed();
        }

        // Runs after the movement controller, which only turns the wizard toward where it walks, so an
        // attack always faces the crosshair until the bolt is gone.
        private void LateUpdate()
        {
            if (Machine.FacesAim) FaceAim();
        }

        private void FaceAim()
        {
            var current = transform.eulerAngles.y;
            var target = AimFacing.YawToward(transform.position, AimPoint(), current);
            var yaw = AimFacing.Turn(current, target, faceAimDegreesPerSecond * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        // Speed comes from the active spell, damage and spread from the shot.
        private void FireShot(ProjectileShot shot)
        {
            if (boltPrefab == null) return;
            var origin = muzzle != null ? muzzle.position : transform.position + Vector3.up * 1.3f;
            var bolt = BoltPool.Get(boltPrefab, origin, Quaternion.identity);
            var projectile = activeSpell != null ? activeSpell.Projectile : BaseProjectile;
            var direction = ShotAim.Offset(AimPoint() - origin, shot.SpreadDegrees);
            bolt.Launch(gameObject, direction, projectile, shot.Damage, activeSpell);
            if (activeSpell != null && activeSpell.Effects.castEffect != null)
                Instantiate(activeSpell.Effects.castEffect, origin, Quaternion.LookRotation(direction));
        }

        // Ballistics of the default Base spell: 30 m/s, straight.
        private static readonly SpellDefinition.ProjectileSettings BaseProjectile = new SpellDefinition.ProjectileSettings
        {
            speed = 30f, gravity = 0f, curveDegreesPerSecond = 0f, sizeScale = 1f, lifetimeSeconds = 3f,
        };

        // The point under the crosshair: the first thing the camera's centre ray hits that is not the
        // wizard, or a far point along that ray.
        private Vector3 AimPoint()
        {
            var cam = Camera.main;
            if (cam == null) return transform.position + transform.forward * aimDistance;
            var ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            var best = ray.GetPoint(aimDistance);
            var bestDistance = aimDistance;
            foreach (var hit in Physics.RaycastAll(ray, aimDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.root == transform.root || hit.distance >= bestDistance) continue;
                best = hit.point;
                bestDistance = hit.distance;
            }
            return best;
        }

        public void Parry()
        {
            Machine.Tick(new ActionIntent(false, true), Time.time);
        }

        // Restores every shield charge. False when the shield was already full.
        public bool RefillShield() => Machine.RefillShield(Time.time);

        public HitResult ResolveHit(int damage) => Machine.ResolveHit(damage, Time.time);
    }
}
