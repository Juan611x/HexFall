namespace HexFall.World.RuneCircles
{
    public enum RuneCircleActivationMode
    {
        // Fires as soon as the player is inside (and grounded, when required).
        OnEnter,
        // Fires after the player has stayed inside (and grounded, when required) for standSeconds.
        AfterStanding,
    }

    [System.Serializable]
    public sealed class RuneCircleGateSettings
    {
        public RuneCircleActivationMode mode = RuneCircleActivationMode.OnEnter;
        public float standSeconds = 1f;
        // Minimum time between two firings.
        public float cooldownSeconds = 0.5f;
        public bool requireGrounded = true;
    }

    // Decides when a rune circle fires. Rules:
    //  - at most once per stay: after firing, the player must leave before it can fire again; with
    //    requireGrounded the exit counts only once the player has touched the ground outside, so a
    //    launched player landing back on the circle does not relaunch;
    //  - never within cooldownSeconds of the previous firing (the cooldown runs even while outside, and
    //    a stay that began inside the cooldown fires once when it elapses);
    //  - with requireGrounded, an airborne player is not eligible (AfterStanding restarts its timer);
    //  - Lock() suppresses firing until the player has been seen inside and then outside again, so a
    //    teleport arrival does not trigger the destination circle.
    public sealed class RuneCircleGate
    {
        private readonly RuneCircleGateSettings _settings;
        private float _cooldownRemaining;
        private float _standTime;
        private bool _firedThisStay;
        private bool _locked;
        private bool _lockSawInside;

        public RuneCircleGate(RuneCircleGateSettings settings)
        {
            _settings = settings;
        }

        public void Lock()
        {
            _locked = true;
            _lockSawInside = false;
        }

        // Returns true on the tick the circle fires.
        public bool Tick(bool inside, bool grounded, float deltaTime)
        {
            if (_cooldownRemaining > 0f) _cooldownRemaining -= deltaTime;

            if (!inside)
            {
                _standTime = 0f;
                // A launch carries the player out of the volume; only touching the ground outside
                // (or any exit, when grounding is not required) ends the stay.
                if (grounded || !_settings.requireGrounded) _firedThisStay = false;
                if (_locked && _lockSawInside) _locked = false;
                return false;
            }

            if (_locked) _lockSawInside = true;
            var eligible = !_settings.requireGrounded || grounded;
            if (!eligible)
            {
                _standTime = 0f;
                return false;
            }
            _standTime += deltaTime;
            if (_locked || _firedThisStay || _cooldownRemaining > 0.0001f) return false;

            var ready = _settings.mode == RuneCircleActivationMode.OnEnter || _standTime >= _settings.standSeconds;
            if (!ready) return false;

            _firedThisStay = true;
            _cooldownRemaining = _settings.cooldownSeconds;
            return true;
        }
    }
}
