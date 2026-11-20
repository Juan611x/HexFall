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
            return true;
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
