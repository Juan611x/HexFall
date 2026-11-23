namespace HexFall.Player.Actions
{
    public enum WizardSfxCue { None, Cast, ShieldRaise, ParryDeflect, ParryPerfect, BoltImpact, Snap, Huff }

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
