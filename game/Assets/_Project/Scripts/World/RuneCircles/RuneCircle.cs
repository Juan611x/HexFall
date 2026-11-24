using UnityEngine;

namespace HexFall.World.RuneCircles
{
    // A floor circle with a trigger volume. Tracks the player inside it, runs the gate and applies
    // every IRuneCircleEffect on this object, in component order, when the gate fires.
    // Ticks in Update, like the player motor, so its timers share the player's frame clock.
    // OnTriggerEnter only discovers the player's collider; whether it is inside is then decided each
    // frame from the bounds, because trigger callbacks run on the physics step and lag the frame (a
    // launched player landing back on the circle would be seen outside for a frame and re-arm it) and
    // are skipped altogether when the player's controller is toggled to teleport it.
    public sealed class RuneCircle : MonoBehaviour
    {
        [SerializeField] private RuneCircleGateSettings gate = new RuneCircleGateSettings();

        private RuneCircleGate _gate;
        private Collider _trigger;
        private Collider _playerCollider;
        private IRuneCircleTarget _player;
        private IRuneCircleEffect[] _effects;

        public RuneCircleGateSettings Gate => gate;

        // Suppresses firing until the player has been inside and left (used by teleport arrivals).
        public void Lock() => _gate.Lock();

        private void Awake()
        {
            _gate = new RuneCircleGate(gate);
            _trigger = GetComponent<Collider>();
            _effects = GetComponents<IRuneCircleEffect>();
        }

        private void OnTriggerEnter(Collider other)
        {
            // Keep the player already being tracked while it is still inside.
            if (_playerCollider != null && !ReferenceEquals(other, _playerCollider) && IsInside()) return;
            var target = other.GetComponentInParent<IRuneCircleTarget>();
            if (target == null) return;
            _player = target;
            _playerCollider = other;
        }

        private bool IsInside() =>
            _playerCollider != null && _playerCollider.enabled && _playerCollider.gameObject.activeInHierarchy
            && _trigger.bounds.Intersects(_playerCollider.bounds);

        private void Update()
        {
            var inside = IsInside();
            var grounded = _playerCollider != null && _player.IsGrounded;
            if (!_gate.Tick(inside, grounded, Time.deltaTime)) return;

            var activation = new RuneCircleActivation(transform, _player);
            foreach (var effect in _effects) effect.Apply(activation);
        }
    }
}
