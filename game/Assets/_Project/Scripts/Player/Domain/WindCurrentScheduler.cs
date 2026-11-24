using System;
using System.Collections.Generic;

namespace HexFall.Player
{
    public readonly struct WindCurrentState
    {
        public readonly float Progress;     // life progress in [0,1)
        public readonly float Height;       // normalized height inside the band (fraction of the sphere radius)
        public readonly float TiltDegrees;  // small random tilt of the band
        public readonly float Lifetime;     // seconds the current lives

        public WindCurrentState(float progress, float height, float tiltDegrees, float lifetime)
        {
            Progress = progress;
            Height = height;
            TiltDegrees = tiltDegrees;
            Lifetime = lifetime;
        }
    }

    // Keeps a few horizontal wind currents alive around the shield. Lifetimes are staggered so they
    // never appear or vanish together, and a finished current respawns at a new height that keeps a
    // minimum gap from the other active currents (bounded retries, then the farthest candidate).
    public sealed class WindCurrentScheduler
    {
        private const int MaxPlacementTries = 8;

        private readonly Random _random;
        private readonly float _minHeight;
        private readonly float _maxHeight;
        private readonly float _minHeightGap;
        private readonly float _maxTiltDegrees;
        private readonly float _minLifetime;
        private readonly float _maxLifetime;
        private readonly float[] _age;
        private readonly WindCurrentState[] _states;

        public IReadOnlyList<WindCurrentState> Currents => _states;

        public WindCurrentScheduler(Random random, int count = 3, float minHeight = -0.6f, float maxHeight = 0.6f,
            float minHeightGap = 0.3f, float maxTiltDegrees = 12f, float minLifetime = 1.2f, float maxLifetime = 2.2f)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (count < 3 || count > 4) throw new ArgumentOutOfRangeException(nameof(count));
            if (maxHeight < minHeight) throw new ArgumentException("maxHeight must not be below minHeight");
            if (minLifetime <= 0f || maxLifetime < minLifetime) throw new ArgumentException("invalid lifetime range");

            _random = random;
            _minHeight = minHeight;
            _maxHeight = maxHeight;
            _minHeightGap = minHeightGap;
            _maxTiltDegrees = maxTiltDegrees;
            _minLifetime = minLifetime;
            _maxLifetime = maxLifetime;
            _age = new float[count];
            _states = new WindCurrentState[count];

            for (int i = 0; i < count; i++)
            {
                float lifetime = NextLifetime();
                float phase = (i + (float)_random.NextDouble() * 0.5f) / count; // evenly spread life phases
                _age[i] = phase * lifetime;
                _states[i] = new WindCurrentState(phase, PickHeight(i, i), NextTilt(), lifetime);
            }
        }

        public void Tick(float deltaTime)
        {
            for (int i = 0; i < _states.Length; i++)
            {
                _age[i] += deltaTime;
                var s = _states[i];
                if (_age[i] >= s.Lifetime)
                {
                    _age[i] = 0f;
                    _states[i] = new WindCurrentState(0f, PickHeight(i, _states.Length), NextTilt(), NextLifetime());
                }
                else
                {
                    _states[i] = new WindCurrentState(_age[i] / s.Lifetime, s.Height, s.TiltDegrees, s.Lifetime);
                }
            }
        }

        // Heights of the active currents: all others, or only the first `placed` while building.
        private float PickHeight(int self, int placed)
        {
            float best = 0f;
            float bestDistance = -1f;
            for (int attempt = 0; attempt < MaxPlacementTries; attempt++)
            {
                float candidate = _minHeight + (float)_random.NextDouble() * (_maxHeight - _minHeight);
                float distance = float.MaxValue;
                for (int j = 0; j < placed; j++)
                {
                    if (j == self) continue;
                    distance = Math.Min(distance, Math.Abs(candidate - _states[j].Height));
                }

                if (distance >= _minHeightGap) return candidate;
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            return best;
        }

        private float NextTilt() => ((float)_random.NextDouble() * 2f - 1f) * _maxTiltDegrees;

        private float NextLifetime() => _minLifetime + (float)_random.NextDouble() * (_maxLifetime - _minLifetime);
    }
}
