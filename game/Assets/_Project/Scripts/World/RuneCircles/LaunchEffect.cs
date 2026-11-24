using UnityEngine;

namespace HexFall.World.RuneCircles
{
    // Throws the player straight up so the apex is launchHeight metres above the launch point.
    public sealed class LaunchEffect : MonoBehaviour, IRuneCircleEffect
    {
        [Tooltip("Height of the apex above the launch point, in metres.")]
        [SerializeField] private float launchHeight = 6f;

        public float LaunchHeight => launchHeight;

        public void Apply(RuneCircleActivation activation)
        {
            var speed = LaunchMath.SpeedForHeight(launchHeight, activation.Target.GravityMagnitude);
            if (speed > 0f) activation.Target.Launch(speed);
        }
    }
}
