using System;

namespace HexFall.Player.Actions
{
    public enum ActionStateId { Ready, Attack, Parry, ParryEmpty }

    // What the player asked for this tick.
    public readonly struct ActionIntent
    {
        public readonly bool AttackPressed;
        public readonly bool ParryPressed;

        public ActionIntent(bool attackPressed, bool parryPressed)
        {
            AttackPressed = attackPressed;
            ParryPressed = parryPressed;
        }
    }

    // Timings and shield rules of the actions, with the values the wizard ships with.
    [Serializable]
    public class ActionSettings
    {
        public float attackCooldownSeconds = 0.5f;
        public float boltDelaySeconds = 0.2f;
        public float faceAimHoldSeconds = 0.3f;
        public int shieldCharges = 3;
        public float chargeRechargeSeconds = 6f;
        public float perfectParrySeconds = 0.15f;
        public float guardSeconds = 0.4f;
        public float partialBlock = 0.5f;
    }
}
