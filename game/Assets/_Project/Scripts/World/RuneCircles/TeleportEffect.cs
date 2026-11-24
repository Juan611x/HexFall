using UnityEngine;

namespace HexFall.World.RuneCircles
{
    // Moves the player to the destination circle's centre and locks that circle, so the arrival does
    // not fire it and bounce the player back; it fires again once the player has stepped off and on.
    public sealed class TeleportEffect : MonoBehaviour, IRuneCircleEffect
    {
        [Tooltip("The circle the player arrives at.")]
        [SerializeField] private RuneCircle destination;
        [Tooltip("Keep the player's current facing; otherwise face the destination circle's forward.")]
        [SerializeField] private bool keepFacing = true;
        [Tooltip("Metres above the destination circle's centre the feet are placed at, so the body never starts inside the floor.")]
        [SerializeField] private float arrivalLift = 0.05f;

        private bool _warned;

        public RuneCircle Destination => destination;
        public bool KeepFacing => keepFacing;

        internal void Configure(RuneCircle newDestination, bool newKeepFacing)
        {
            destination = newDestination;
            keepFacing = newKeepFacing;
        }

        public void Apply(RuneCircleActivation activation)
        {
            if (destination == null)
            {
                if (!_warned) Debug.LogWarning($"{name}: TeleportEffect has no destination circle; nothing to teleport to.", this);
                _warned = true;
                return;
            }

            var arrival = destination.transform;
            var yaw = keepFacing ? activation.Target.FacingYaw : arrival.eulerAngles.y;
            activation.Target.Teleport(arrival.position + Vector3.up * arrivalLift, yaw);
            destination.Lock(activation.Target);
        }
    }
}
