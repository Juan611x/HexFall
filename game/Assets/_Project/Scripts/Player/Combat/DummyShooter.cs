using UnityEngine;

namespace HexFall.Player
{
    // Makes a training dummy fire a slow bolt straight ahead at a fixed rhythm, so the player can test
    // taking damage and parrying. Stops while the dummy is dead.
    [RequireComponent(typeof(TrainingDummy))]
    public class DummyShooter : MonoBehaviour
    {
        [SerializeField] private Bolt boltPrefab;
        [SerializeField] private Transform muzzle;
        [SerializeField] private float intervalSeconds = 2.5f;
        [SerializeField] private float windUpSeconds = 0.25f;
        [SerializeField] private float boltSpeed = 12f;
        [SerializeField] private int damage = 10;

        private static readonly int ShootTrigger = Animator.StringToHash("Shoot");

        private TrainingDummy _dummy;
        private Animator _animator;
        private float _nextShotAt;
        private float _fireAt = float.PositiveInfinity;

        private void Awake()
        {
            _dummy = GetComponent<TrainingDummy>();
            _animator = GetComponentInChildren<Animator>();
            _nextShotAt = Time.time + intervalSeconds;
        }

        private void Update()
        {
            if (_dummy.CurrentHealth <= 0)
            {
                _fireAt = float.PositiveInfinity;
                _nextShotAt = Time.time + intervalSeconds;
                return;
            }

            if (Time.time >= _nextShotAt)
            {
                _nextShotAt = Time.time + intervalSeconds;
                _fireAt = Time.time + windUpSeconds;
                _animator.SetTrigger(ShootTrigger);
            }

            if (Time.time < _fireAt) return;
            _fireAt = float.PositiveInfinity;
            var origin = muzzle != null ? muzzle : transform;
            var bolt = Instantiate(boltPrefab, origin.position, Quaternion.identity);
            bolt.Launch(gameObject, transform.forward, boltSpeed, damage);
        }
    }
}
