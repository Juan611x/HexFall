using UnityEngine;

namespace HexFall.World.RuneCircles
{
    // A floor circle with a trigger volume. Tracks the player, runs the gate and applies every
    // IRuneCircleEffect on this object, in component order, when the gate fires.
    // Ticks in Update, like the player motor, so its timers share the player's frame clock.
    // OnTriggerEnter only discovers the player; whether it is inside is then decided each frame from the
    // player's feet and the circle's real footprint (a disc plus a height band), because trigger
    // callbacks run on the physics step and lag the frame (a launched player landing back on the circle
    // would be seen outside for a frame and re-arm it) and are skipped altogether when the player's
    // controller is toggled to teleport it.
    // Firing is consumed by the gate, not by the effects: an effect the target refuses (a launch while
    // climbing) is not retried, the player has to leave and come back.
    // Nothing here depends on Awake, so Lock and Tick work on a circle that has not been awakened yet.
    public sealed class RuneCircle : MonoBehaviour
    {
        [SerializeField] private RuneCircleGateSettings gate = new RuneCircleGateSettings();
        [Tooltip("Horizontal radius of the footprint, in metres; matches the visual ring of the 2x2 quad.")]
        [SerializeField] private float radius = 0.9f;
        [Tooltip("Lowest feet height relative to the circle that still counts as inside, in metres.")]
        [SerializeField] private float minHeight = -0.2f;
        [Tooltip("Highest feet height relative to the circle that still counts as inside, in metres.")]
        [SerializeField] private float maxHeight = 1f;

        private RuneCircleGate _gate;
        private IRuneCircleTarget _player;
        private IRuneCircleEffect[] _effects;

        public RuneCircleGateSettings Gate => gate;
        public float Radius => radius;

        private RuneCircleGate TheGate => _gate ??= new RuneCircleGate(gate);

        // Suppresses firing until the player has been inside and left.
        public void Lock() => TheGate.Lock();

        // Locks the circle for a player that arrives inside it without a trigger callback (a teleport):
        // the circle follows that player from now on, so leaving it unlocks the circle.
        public void Lock(IRuneCircleTarget arriving)
        {
            TheGate.Lock();
            Track(arriving);
        }

        internal void Track(IRuneCircleTarget target) => _player = target;

        internal void OnTriggerEnter(Collider other)
        {
            var target = other.GetComponentInParent<IRuneCircleTarget>();
            if (target == null) return;
            // Keep the player already being tracked while it is still inside.
            if (_player != null && !ReferenceEquals(target, _player) && IsInside()) return;
            Track(target);
        }

        private static bool IsAlive(IRuneCircleTarget target)
        {
            if (target == null) return false;
            // A destroyed component keeps a non-null managed reference.
            if (target is Object unityObject && unityObject == null) return false;
            return !(target is Component component) || component.gameObject.activeInHierarchy;
        }

        private bool IsInside() =>
            IsAlive(_player)
            && RuneCircleFootprint.Contains(transform.position, transform.rotation, _player.FeetPosition, radius, minHeight, maxHeight);

        private void Update() => Tick(Time.deltaTime);

        internal void Tick(float deltaTime)
        {
            if (_player is Object destroyed && destroyed == null) _player = null;

            var inside = IsInside();
            var grounded = IsAlive(_player) && _player.IsGrounded;
            if (!TheGate.Tick(inside, grounded, deltaTime)) return;

            _effects ??= GetComponents<IRuneCircleEffect>();
            var activation = new RuneCircleActivation(transform, _player);
            foreach (var effect in _effects) effect.Apply(activation);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            var centre = transform.position;
            var previous = centre + transform.rotation * new Vector3(radius, 0f, 0f);
            for (var i = 1; i <= 32; i++)
            {
                var angle = i * Mathf.PI * 2f / 32f;
                var next = centre + transform.rotation * new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
