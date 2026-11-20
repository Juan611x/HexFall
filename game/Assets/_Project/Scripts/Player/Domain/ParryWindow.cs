namespace HexFall.Player
{
    public enum HitOutcome { Parried, Damaged }

    // A parry is a short timing window, not a held block: raising it opens [raisedAt, raisedAt + active),
    // and another raise is refused until the recovery after that window has passed.
    public sealed class ParryWindow
    {
        private readonly float _activeSeconds;
        private readonly float _recoverySeconds;
        private float _raisedAt = float.NegativeInfinity;

        public ParryWindow(float activeSeconds, float recoverySeconds)
        {
            _activeSeconds = activeSeconds;
            _recoverySeconds = recoverySeconds;
        }

        public bool TryRaise(float now)
        {
            if (now < _raisedAt + _activeSeconds + _recoverySeconds) return false;
            _raisedAt = now;
            return true;
        }

        public bool IsActive(float now) => now >= _raisedAt && now < _raisedAt + _activeSeconds;

        public HitOutcome Resolve(float hitTime) => IsActive(hitTime) ? HitOutcome.Parried : HitOutcome.Damaged;
    }
}
