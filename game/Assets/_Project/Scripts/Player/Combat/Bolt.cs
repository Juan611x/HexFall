using UnityEngine;

namespace HexFall.Player
{
    // A spell projectile. Moves in a straight line and sweeps a sphere along each frame's step so it
    // cannot tunnel through thin targets; the first collider that does not belong to its owner ends it,
    // dealing damage when that collider has an IDamageable.
    public class Bolt : MonoBehaviour
    {
        [SerializeField] private float radius = 0.15f;
        [SerializeField] private float lifetimeSeconds = 3f;

        private GameObject _owner;
        private Vector3 _velocity;
        private int _damage;
        private float _dieAt;

        public void Launch(GameObject owner, Vector3 direction, float speed, int damage)
        {
            _owner = owner;
            _velocity = direction.normalized * speed;
            _damage = damage;
            _dieAt = Time.time + lifetimeSeconds;
            transform.rotation = Quaternion.LookRotation(_velocity);
        }

        private void Update()
        {
            if (Time.time >= _dieAt)
            {
                Destroy(gameObject);
                return;
            }

            var step = _velocity * Time.deltaTime;
            var hits = Physics.SphereCastAll(transform.position, radius, step.normalized, step.magnitude, ~0, QueryTriggerInteraction.Ignore);
            RaycastHit? nearest = null;
            foreach (var hit in hits)
            {
                if (_owner != null && hit.collider.transform.root == _owner.transform.root) continue;
                if (nearest == null || hit.distance < nearest.Value.distance) nearest = hit;
            }

            if (nearest != null)
            {
                var target = nearest.Value.collider.GetComponentInParent<IDamageable>();
                target?.TakeHit(_damage, _owner);
                Destroy(gameObject);
                return;
            }

            transform.position += step;
        }
    }
}
