using HexFall.Player.Actions;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HexFall.Player
{
    // Receives the Attack and Parry actions from PlayerInput (Send Messages), applies the attack cooldown
    // and the shield rules, and drives the animator and the shield VFX. Incoming damage goes through
    // ResolveHit, which says how much of it the shield absorbed.
    [RequireComponent(typeof(Animator))]
    public class PlayerCombat : MonoBehaviour
    {
        [SerializeField] private float attackCooldownSeconds = 0.5f;
        [Header("Bolt")]
        [SerializeField] private Bolt boltPrefab;
        [SerializeField] private Transform muzzle;
        [SerializeField] private float boltDelaySeconds = 0.2f;
        [SerializeField] private float boltSpeed = 30f;
        [SerializeField] private int boltDamage = 18;
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

        private static readonly int AttackTrigger = Animator.StringToHash("Attack");
        private static readonly int ParryTrigger = Animator.StringToHash("Parry");

        private Animator _animator;
        // Counts entries into Attack, so Attack() also reports an attack chained on the tick the last one ended.
        private int _attackStarts;

        // The action state machine; the IK, animator and audio adapters read it. Null before Awake.
        public ActionMachine Machine { get; private set; }

        public int MaxShieldCharges => shieldCharges;
        public int ShieldCharges => Machine?.ShieldCharges(Time.time) ?? shieldCharges;

        // True from the attack until the wizard stops facing the crosshair, and while the shield guard
        // is up. Used by the IK to aim the head and chest harder.
        public bool IsActing => Machine != null && Machine.IsActing;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            Machine = new ActionMachine(new ActionSettings
            {
                attackCooldownSeconds = attackCooldownSeconds,
                boltDelaySeconds = boltDelaySeconds,
                faceAimHoldSeconds = faceAimHoldSeconds,
                shieldCharges = shieldCharges,
                chargeRechargeSeconds = chargeRechargeSeconds,
                perfectParrySeconds = perfectParrySeconds,
                guardSeconds = guardSeconds,
                partialBlock = partialBlock,
            });
            Machine.StateChanged += OnActionChanged;
            Machine.BoltFired += FireBolt;
            Machine.HitResolved += OnHitResolved;
        }

        private void OnDestroy()
        {
            if (Machine == null) return;
            Machine.StateChanged -= OnActionChanged;
            Machine.BoltFired -= FireBolt;
            Machine.HitResolved -= OnHitResolved;
        }

        // Animator triggers and the shield flash follow the action state.
        private void OnActionChanged(ActionStateId from, ActionStateId to)
        {
            if (to == ActionStateId.Attack)
            {
                _attackStarts++;
                _animator.SetTrigger(AttackTrigger);
            }
            else if (to == ActionStateId.Parry)
            {
                _animator.SetTrigger(ParryTrigger);
                if (shield != null) shield.Flash(guardSeconds);
            }
        }

        private void OnHitResolved(HitResult hit)
        {
            if (hit.Grade != ParryGrade.None && shield != null) shield.Deflect();
        }

        public void OnAttack(InputValue value)
        {
            if (value.isPressed) Attack();
        }

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
            Machine.Tick(default, Time.time);
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

        private void FireBolt()
        {
            if (boltPrefab == null) return;
            var origin = muzzle != null ? muzzle.position : transform.position + Vector3.up * 1.3f;
            var bolt = Instantiate(boltPrefab, origin, Quaternion.identity);
            bolt.Launch(gameObject, AimPoint() - origin, boltSpeed, boltDamage);
        }

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
