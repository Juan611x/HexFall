using HexFall.UI;
using UnityEngine;

namespace HexFall.Player
{
    // Target for testing damage: takes hits, plays hit and death animations, shows its health above
    // the head and stands up again a few seconds after dying.
    public class TrainingDummy : MonoBehaviour, IDamageable
    {
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private float resetSeconds = 3f;
        [SerializeField] private TextMesh label;
        [Tooltip("Height above the feet where damage numbers appear.")]
        [SerializeField] private float damagePopupHeight = 1.6f;

        private static readonly int HitTrigger = Animator.StringToHash("Hit");
        private static readonly int DeadBool = Animator.StringToHash("Dead");

        private Animator _animator;
        private Health _health;
        private float _resetAt = float.PositiveInfinity;
        private int _lastDamage;

        public int CurrentHealth => _health?.Current ?? maxHealth;

        private void Awake()
        {
            _animator = GetComponentInChildren<Animator>();
            _health = new Health(maxHealth);
            Refresh();
        }

        public void TakeHit(int damage, GameObject source)
        {
            if (_health.IsDead) return;
            _lastDamage = _health.TakeDamage(damage);
            DamageFeed.Report(transform.position + Vector3.up * damagePopupHeight, _lastDamage);
            if (_health.IsDead)
            {
                _animator.SetBool(DeadBool, true);
                _resetAt = Time.time + resetSeconds;
            }
            else
            {
                _animator.SetTrigger(HitTrigger);
            }
            Refresh();
        }

        private void Update()
        {
            if (Time.time < _resetAt) return;
            _resetAt = float.PositiveInfinity;
            _health.Reset();
            _lastDamage = 0;
            _animator.SetBool(DeadBool, false);
            Refresh();
        }

        private void LateUpdate()
        {
            if (label == null || Camera.main == null) return;
            label.transform.rotation = Quaternion.LookRotation(label.transform.position - Camera.main.transform.position);
        }

        private void Refresh()
        {
            if (label == null) return;
            label.text = _lastDamage > 0
                ? $"{_health.Current} / {_health.Max}\n-{_lastDamage}"
                : $"{_health.Current} / {_health.Max}";
        }
    }
}
