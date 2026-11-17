using System;

namespace HexFall.UI
{
    public static class TimeLabel
    {
        public static string MinSec(int totalSeconds)
        {
            if (totalSeconds < 0) totalSeconds = 0;
            return $"{totalSeconds / 60}:{totalSeconds % 60:00}";
        }
    }

    public sealed class Countdown
    {
        private readonly int _seconds;
        private float _remaining;

        public Countdown(int seconds) => _seconds = seconds;

        public int RemainingSeconds => (int)Math.Ceiling(_remaining);
        public bool IsRunning { get; private set; }
        public bool IsFinished { get; private set; }

        public void Start()
        {
            _remaining = _seconds;
            IsRunning = true;
            IsFinished = false;
        }

        public void Tick(float deltaSeconds)
        {
            if (!IsRunning) return;
            _remaining -= deltaSeconds;
            if (_remaining > 0f) return;
            _remaining = 0f;
            IsRunning = false;
            IsFinished = true;
        }
    }
}
