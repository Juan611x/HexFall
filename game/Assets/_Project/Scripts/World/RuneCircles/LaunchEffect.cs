using UnityEngine;

namespace HexFall.World.RuneCircles
{
    // Throws the player straight up so the apex is launchHeight metres above the launch point.
    // The target may refuse (the player cannot be launched while climbing); the circle's firing is
    // consumed anyway, so the player has to leave the circle and return to try again.
    // Each circle instance can override launchHeight in the inspector.
    public sealed class LaunchEffect : MonoBehaviour, IRuneCircleEffect
    {
        [Tooltip("Height of the apex above the launch point, in metres. Each circle can set its own.")]
        [Min(0f)]
        [SerializeField] private float launchHeight = 10f;

        public float LaunchHeight => launchHeight;

        public void Apply(RuneCircleActivation activation)
        {
            var speed = LaunchMath.SpeedForHeight(launchHeight, activation.Target.GravityMagnitude);
            if (speed > 0f) activation.Target.Launch(speed);
        }
    }
}
