using UnityEngine;
using UnityEngine.InputSystem;

namespace HexFall.Player
{
    // Receives the Attack and Parry actions from PlayerInput (Send Messages), applies the timing rules
    // and drives the animator and the shield. Damage sources call ReceiveHit to learn whether it was parried.
    [RequireComponent(typeof(Animator))]
    public class PlayerCombat : MonoBehaviour
    {
        [SerializeField] private float attackCooldownSeconds = 0.5f;
        [SerializeField] private float parryWindowSeconds = 0.25f;
        [SerializeField] private float parryRecoverySeconds = 0.6f;
        [SerializeField] private MagicShield shield;

        private static readonly int AttackTrigger = Animator.StringToHash("Attack");
        private static readonly int ParryTrigger = Animator.StringToHash("Parry");

        private Animator _animator;
        private AttackCooldown _attack;
        private ParryWindow _parry;

        public float ParryWindowSeconds => parryWindowSeconds;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _attack = new AttackCooldown(attackCooldownSeconds);
            _parry = new ParryWindow(parryWindowSeconds, parryRecoverySeconds);
        }

        public void OnAttack(InputValue value)
        {
            if (value.isPressed) Attack();
        }

        public void OnParry(InputValue value)
        {
            if (value.isPressed) Parry();
        }

        public void Attack()
        {
            if (!_attack.TryAttack(Time.time)) return;
            _animator.SetTrigger(AttackTrigger);
        }

        public void Parry()
        {
            if (!_parry.TryRaise(Time.time)) return;
            _animator.SetTrigger(ParryTrigger);
            if (shield != null) shield.Flash(parryWindowSeconds);
        }

        public HitOutcome ReceiveHit()
        {
            var outcome = _parry.Resolve(Time.time);
            if (outcome == HitOutcome.Parried && shield != null) shield.Deflect();
            return outcome;
        }
    }
}
