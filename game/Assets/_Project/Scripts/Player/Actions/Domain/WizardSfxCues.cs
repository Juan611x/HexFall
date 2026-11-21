namespace HexFall.Player.Actions
{
    public enum WizardSfxCue { None, Cast, ShieldRaise, ParryDeflect, ParryPerfect, BoltImpact }

    // Which wizard sound, if any, a machine event calls for.
    public static class WizardSfxCues
    {
        public static WizardSfxCue ForStateChange(ActionStateId from, ActionStateId to)
        {
            switch (to)
            {
                case ActionStateId.Attack: return WizardSfxCue.Cast;
                case ActionStateId.Parry: return WizardSfxCue.ShieldRaise;
                default: return WizardSfxCue.None;
            }
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
