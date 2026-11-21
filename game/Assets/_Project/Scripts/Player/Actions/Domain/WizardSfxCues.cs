using HexFall.Player.Locomotion;

namespace HexFall.Player.Actions
{
    public enum WizardSfxCue { None, Cast, ShieldRaise, ParryDeflect, ParryPerfect, BoltImpact, Snap, Huff, Jump }

    // Which wizard sound, if any, a machine event calls for.
    public static class WizardSfxCues
    {
        public static WizardSfxCue ForStateChange(ActionStateId from, ActionStateId to)
        {
            switch (to)
            {
                case ActionStateId.Parry: return WizardSfxCue.ShieldRaise;
                default: return WizardSfxCue.None;
            }
        }

        // Body sounds: the wizard reacts to every press, accepted or not.
        public static WizardSfxCue ForAttackPress() => WizardSfxCue.Huff;

        public static WizardSfxCue ForParryPress() => WizardSfxCue.Snap;

        // Effect sound: the bolt leaves the staff.
        public static WizardSfxCue ForBoltFired() => WizardSfxCue.Cast;

        // Body sound: the wizard hops off the ground.
        public static WizardSfxCue ForLocomotionChange(LocomotionStateId from, LocomotionStateId to)
        {
            return to == LocomotionStateId.Jump ? WizardSfxCue.Jump : WizardSfxCue.None;
        }

        public static WizardSfxCue ForHit(HitResult hit)
        {
            switch (hit.Grade)
            {
                case ParryGrade.Perfect: return WizardSfxCue.ParryPerfect;
                case ParryGrade.Partial: return WizardSfxCue.ParryDeflect;
                default: return WizardSfxCue.None;
            }
        }
    }
}
