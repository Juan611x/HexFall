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

        [Tooltip("Played at the departure circle and again at the arrival circle when the player is teleported.")]
        [SerializeField] private AudioClip sound;
        [Range(0f, 1f)] [SerializeField] private float soundVolume = 1f;

        private bool _warned;

        public AudioClip Sound => sound;
        public float SoundVolume => soundVolume;

        internal void ConfigureSound(AudioClip clip, float volume)
        {
            sound = clip;
            soundVolume = volume;
        }

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
            // Lock only when the player really moved, so a refused teleport does not silence the destination.
            if (!activation.Target.Teleport(arrival.position + Vector3.up * arrivalLift, yaw)) return;
            destination.Lock(activation.Target);
            // Out at the source, in at the destination: the listener follows the player, so the arrival is
            // the one that is always heard and the departure is heard while the source is within range.
            SpatialSfx.Play(sound, activation.Circle.position, soundVolume);
            SpatialSfx.Play(sound, arrival.position, soundVolume);
        }
    }
}
