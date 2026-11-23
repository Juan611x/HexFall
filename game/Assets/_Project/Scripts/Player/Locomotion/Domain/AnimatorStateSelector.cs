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

        private const float StandingPlanarSpeed = 0.5f;

        // Animator state shown while climbing: the climb clip, or the in-air pose once a climb over
        // reaches its over phase (the clip has ended there and would otherwise hold its last frame).
        public static string ForClimb(bool overPhase) => overPhase ? InAir : Climb;

        // True when the Climb to Fall change needs no cross-fade because the animator already shows
        // the in-air pose from the over phase; restarting it would replay the fade.
        public static bool FadeAlreadyShown(LocomotionStateId from, LocomotionStateId to, bool inAirShown) =>
            inAirShown && from == LocomotionStateId.Climb && to == LocomotionStateId.Fall;

        // A standing hard landing plays the land clip; moving landings blend straight into locomotion
        // so the run is not interrupted.
        public static string For(LocomotionStateId to, LocomotionStateId from, float landingSpeed, float planarSpeed, float hardLandingSpeed)
        {
            switch (to)
            {
                case LocomotionStateId.Jump:
                    return JumpStart;
                case LocomotionStateId.Fall:
                    return InAir;
                case LocomotionStateId.Crouch:
                    return Crouch;
                case LocomotionStateId.Slide:
                    return Slide;
                case LocomotionStateId.Climb:
                    return Climb;
                default:
                    var hardStandingLanding = from == LocomotionStateId.Fall
                        && landingSpeed >= hardLandingSpeed
                        && planarSpeed < StandingPlanarSpeed;
                    return hardStandingLanding ? JumpLand : Locomotion;
            }
        }
    }
}
