using System;
using HexFall.Player.Locomotion;
using HexFall.Surfaces;
using UnityEngine;

namespace HexFall.Player
{
    // Tracks the surface under the wizard; phase 4 queries SurfaceAt for each foot.
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class PlayerSurfaceSensor : MonoBehaviour
    {
        [SerializeField] private SurfaceRegistry registry;
        [SerializeField] private float probeStartHeight = 0.3f;
        [SerializeField] private float probeLength = 0.6f;

        private PlayerMotor _motor;
        private GroundSurfaceProbe _probe;
        private readonly SurfaceTracker _tracker = new SurfaceTracker();

        public SurfaceDefinition CurrentSurface => _tracker.Current;

        // Raised with (from, to) when the surface under the wizard changes.
        public event Action<SurfaceDefinition, SurfaceDefinition> SurfaceChanged;

        private void Awake()
        {
            if (registry == null)
            {
                Debug.LogWarning("PlayerSurfaceSensor has no SurfaceRegistry assigned; disabling.", this);
                enabled = false;
                return;
            }

            _motor = GetComponent<PlayerMotor>();
            _probe = new GroundSurfaceProbe(new MeshSurfaceResolver(registry), registry, _motor.GroundLayers,
                probeStartHeight, probeLength, transform);
        }

        public SurfaceDefinition SurfaceAt(Vector3 point)
        {
            return _probe != null ? _probe.SurfaceAt(point) : null;
        }

        private void Update()
        {
            if (!_motor.IsGrounded) return;
            var previous = _tracker.Current;
            if (_tracker.Update(_probe.SurfaceAt(transform.position)))
            {
                SurfaceChanged?.Invoke(previous, _tracker.Current);
            }
        }
    }
}
