namespace HexFall.Player.Locomotion
{
    // Heights are metres above the character root. The foot must rise above LiftHeight before a descent
    // through ContactHeight counts as a step (hysteresis); contacts closer than MinInterval are dropped.
    public readonly struct FootContactSettings
    {
        public readonly float LiftHeight;
        public readonly float ContactHeight;
        public readonly float MinInterval;

        public FootContactSettings(float liftHeight, float contactHeight, float minInterval)
        {
            LiftHeight = liftHeight;
            ContactHeight = contactHeight;
            MinInterval = minInterval;
        }
    }

    // Detects the instant one foot touches down, from its animated height. One instance per foot.
    public sealed class FootContactDetector
    {
        private readonly FootContactSettings _settings;
        private bool _lifted;
        private float _sinceContact;

        public FootContactDetector(FootContactSettings settings)
        {
            _settings = settings;
            _sinceContact = settings.MinInterval;
        }

        // True on the frame the foot touches down. While steps are not allowed (airborne or standing)
        // nothing fires and the foot must be lifted again before the next contact.
        public bool Tick(float height, bool allowed, float deltaTime)
        {
            _sinceContact += deltaTime;
            if (!allowed)
            {
                _lifted = false;
                return false;
            }

            if (height >= _settings.LiftHeight)
            {
                _lifted = true;
                return false;
            }

            if (!_lifted || !(height <= _settings.ContactHeight)) return false;

            _lifted = false;
            if (_sinceContact < _settings.MinInterval) return false;
            _sinceContact = 0f;
            return true;
        }

        public static bool StepsAllowed(bool grounded, float planarSpeed, float minSpeed)
        {
            return grounded && planarSpeed > minSpeed;
        }
    }
}
