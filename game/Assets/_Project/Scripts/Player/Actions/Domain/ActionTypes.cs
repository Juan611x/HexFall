using System;

namespace HexFall.Player.Actions
{
    // Charging and Channelling follow the spell's hold patterns: both face the aim until the cast ends.
    public enum ActionStateId { Ready, Attack, Parry, ParryEmpty, Charging, Channelling }

    // What the player asked for this tick.
    public readonly struct ActionIntent
    {
        public readonly bool AttackPressed;
        public readonly bool ParryPressed;
        public readonly bool AttackReleased;
        // The attack input ended without a release (focus lost, input disabled): a charge or channel
        // in progress is dropped instead of fired. An idle tick leaves it false, so it never cancels.
        public readonly bool AttackCancelled;

        public ActionIntent(bool attackPressed, bool parryPressed, bool attackReleased = false, bool attackCancelled = false)
        {
            AttackPressed = attackPressed;
            ParryPressed = parryPressed;
            AttackReleased = attackReleased;
            AttackCancelled = attackCancelled;
        }
    }

    public static class ActionStateRules
    {
        // True when the change begins a cast: entering Attack (unless it is the end of a charge),
        // Charging or Channelling.
        public static bool StartsCast(ActionStateId from, ActionStateId to) =>
            (to == ActionStateId.Attack && from != ActionStateId.Charging)
            || to == ActionStateId.Charging
            || to == ActionStateId.Channelling;
    }

    // Wind-up, aim hold and shield rules of the actions, with the values the wizard ships with.
    // The attack cooldown, charge and channel timing belong to the active spell.
    [Serializable]
    public class ActionSettings
    {
        public float boltDelaySeconds = 0.2f;
        public float faceAimHoldSeconds = 0.3f;
        public int shieldCharges = 3;
        public float chargeRechargeSeconds = 6f;
        public float perfectParrySeconds = 0.15f;
        public float guardSeconds = 0.4f;
        public float partialBlock = 0.5f;
    }
}
