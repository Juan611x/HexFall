namespace HexFall.Player.Locomotion
{
    // The animator bridge's memory of the fade shown during the over phase of a climb over: the fade
    // happens once when the over phase starts, and the Climb to Fall change that follows must not
    // restart it. Kept free of Unity so the bridge's decisions are testable.
    public sealed class ClimbOverFade
    {
        private bool _shown;

        // True once, on the first frame of a climb's over phase: the bridge fades to the hop now.
        public bool ShouldFadeNow(LocomotionStateId current, bool overPhase)
        {
            if (current != LocomotionStateId.Climb || !overPhase || _shown) return false;
            _shown = true;
            return true;
        }

        // Call on every locomotion change. True only for Climb to Fall right after the over fade was
        // shown (that change needs no fade of its own); the latch is always reset.
        public bool SkipsStateFade(LocomotionStateId from, LocomotionStateId to)
        {
            var shown = _shown;
            _shown = false;
            return AnimatorStateSelector.FadeAlreadyShown(from, to, shown);
        }
    }
}
