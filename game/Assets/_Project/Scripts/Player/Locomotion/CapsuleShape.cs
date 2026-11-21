using UnityEngine;

namespace HexFall.Player.Locomotion
{
    // CharacterController height and center for a body of a given height whose feet stay put.
    public static class CapsuleShape
    {
        // feetOffset is the gap between the transform origin and the capsule bottom (skin on the prefab).
        public static (float height, float centerY) For(float height, float feetOffset)
        {
            return (height, feetOffset + height * 0.5f);
        }

        // Moves the height toward the target by at most speed * dt, never past it.
        public static float Step(float current, float target, float speed, float dt)
        {
            return Mathf.MoveTowards(current, target, speed * dt);
        }
    }
}
