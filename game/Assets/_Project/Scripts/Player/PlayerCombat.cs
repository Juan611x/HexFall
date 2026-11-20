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
        private AttackCooldown _attack;
        private ShieldState _shield;
        private float _fireAt = float.PositiveInfinity;

        public int MaxShieldCharges => shieldCharges;
        public int ShieldCharges => _shield?.Charges(Time.time) ?? shieldCharges;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _attack = new AttackCooldown(attackCooldownSeconds);
            _shield = new ShieldState(shieldCharges, chargeRechargeSeconds, perfectParrySeconds, guardSeconds, partialBlock);
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
            if (!_attack.TryAttack(Time.time)) return false;
            _animator.SetTrigger(AttackTrigger);
            _fireAt = Time.time + boltDelaySeconds;
            return true;
        }

        private void Update()
        {
            if (Time.time < _fireAt) return;
            _fireAt = float.PositiveInfinity;
            FireBolt();
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
            if (!_shield.TryRaise(Time.time)) return;
            _animator.SetTrigger(ParryTrigger);
            if (shield != null) shield.Flash(guardSeconds);
        }

        public HitResult ResolveHit(int damage)
        {
            var hit = _shield.Resolve(damage, Time.time);
            if (hit.Grade != ParryGrade.None && shield != null) shield.Deflect();
            return hit;
        }
    }
}
