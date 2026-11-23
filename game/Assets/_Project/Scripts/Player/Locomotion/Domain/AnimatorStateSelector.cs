namespace HexFall.Player.Locomotion
{
    // Chooses which Animator state a locomotion transition cross-fades to. Keys are looked up in the
    // animator bridge, which maps them to state names.
    public static class AnimatorStateSelector
    {
        public const string Locomotion = "Locomotion";
        public const string JumpStart = "JumpStart";
        public const string InAir = "InAir";
        public const string JumpLand = "JumpLand";
        public const string Crouch = "Crouch";
        public const string Slide = "Slide";
        public const string Climb = "Climb";
        // The ninja hop of a climb-over drop (start into loop) and its landing.
        public const string Hop = "Hop";
        public const string HopLand = "HopLand";

        private const float StandingPlanarSpeed = 0.5f;
        // Below this speed a hop landing still plays its land clip: the over climb throws the body forward,
        // so it is still sliding to a stop on landing even with the stick released. Kept under walk speed.
        private const float HopLandMaxPlanarSpeed = 1.5f;

        // Animator state shown while climbing: the climb clip, or the hop once a climb over reaches its
        // over phase (the clip has ended there and would otherwise hold its last frame).
        public static string ForClimb(bool overPhase) => overPhase ? Hop : Climb;

        // True when the Climb to Fall change needs no cross-fade because the animator already shows
        // the in-air pose from the over phase; restarting it would replay the fade.
        public static bool FadeAlreadyShown(LocomotionStateId from, LocomotionStateId to, bool inAirShown) =>
            inAirShown && from == LocomotionStateId.Climb && to == LocomotionStateId.Fall;

        // A standing hard landing plays the land clip; moving landings blend straight into locomotion
        // so the run is not interrupted. fromClimbOver marks the fall (or its landing) of a climb-over
        // drop: it shows the hop and, unless running, lands with the hop land clip whatever the speed.
        public static string For(LocomotionStateId to, LocomotionStateId from, float landingSpeed, float planarSpeed, float hardLandingSpeed, bool fromClimbOver = false)
        {
            switch (to)
            {
                case LocomotionStateId.Jump:
                    return JumpStart;
                case LocomotionStateId.Fall:
                    return fromClimbOver ? Hop : InAir;
                case LocomotionStateId.Crouch:
                    return Crouch;
                case LocomotionStateId.Slide:
                    return Slide;
                case LocomotionStateId.Climb:
                    return Climb;
                default:
                    var standing = planarSpeed < StandingPlanarSpeed;
                    if (fromClimbOver && from == LocomotionStateId.Fall && planarSpeed < HopLandMaxPlanarSpeed) return HopLand;
                    var hardStandingLanding = from == LocomotionStateId.Fall
                        && landingSpeed >= hardLandingSpeed
                        && standing;
                    return hardStandingLanding ? JumpLand : Locomotion;
            }
        }
    }
}
