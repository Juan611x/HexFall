namespace HexFall.Player
{
    // Tracks when a consumed pickup comes back.
    public sealed class RespawnTimer
    {
        private readonly float _seconds;
        private float _availableAt = float.NegativeInfinity;

        public RespawnTimer(float seconds) => _seconds = seconds;

        public bool IsAvailable(float now) => now >= _availableAt;

        public bool Consume(float now)
        {
            if (!IsAvailable(now)) return false;
            _availableAt = now + _seconds;
            return true;
        }
    }
}
