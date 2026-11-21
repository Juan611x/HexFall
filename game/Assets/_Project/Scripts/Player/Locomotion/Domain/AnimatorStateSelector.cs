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

        private const float StandingPlanarSpeed = 0.5f;

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
                default:
                    var hardStandingLanding = from == LocomotionStateId.Fall
                        && landingSpeed >= hardLandingSpeed
                        && planarSpeed < StandingPlanarSpeed;
                    return hardStandingLanding ? JumpLand : Locomotion;
            }
        }
    }
}
