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

    // Keeps three to eight horizontal wind currents alive around the shield. Lifetimes are staggered so they
    // never appear or vanish together, and a finished current respawns at a new height that keeps a
    // minimum gap from the other active currents (several candidates, then the farthest one). A finished
    // current also carries its leftover time into the next one and picks a lifetime whose expiry stays
    // apart from the other currents', so a big or hitched delta cannot make them all respawn together.
    public sealed class WindCurrentScheduler
    {
        private const int Candidates = 8;
        private const int MaxCarryRespawns = 16;

        private readonly Random _random;
        private readonly float _minHeight;
        private readonly float _maxHeight;
        private readonly float _minHeightGap;
        private readonly float _maxTiltDegrees;
        private readonly float _minLifetime;
        private readonly float _maxLifetime;
        private readonly float _minExpiryGap;
        private readonly float[] _age;
        private readonly WindCurrentState[] _states;
        private readonly bool[] _placed;
        private readonly bool[] _expired;
        private readonly float[] _candidates = new float[Candidates];
        private readonly float[] _lifetimes = new float[Candidates];
        private readonly float[] _others;

        public IReadOnlyList<WindCurrentState> Currents => _states;

        // Draining: a finished current is not respawned (it stays at progress 1, invisible), so the live ones fade out.
        public bool Draining { get; private set; }

        // Draining and every current has finished.
        public bool IsIdle
        {
            get
            {
                if (!Draining) return false;
                for (int i = 0; i < _states.Length; i++)
                    if (_age[i] < _states[i].Lifetime) return false;
                return true;
            }
        }

        // No new currents from now on; the ones alive finish their life.
        public void StopSpawning() => Draining = true;

        public WindCurrentScheduler(Random random, int count = 6, float minHeight = -0.85f, float maxHeight = 0.85f,
            float minHeightGap = 0.22f, float maxTiltDegrees = 12f, float minLifetime = 0.6f, float maxLifetime = 0.8f,
            float minExpiryGap = 0.1f)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (count < 3 || count > 8) throw new ArgumentOutOfRangeException(nameof(count));
            if (maxHeight < minHeight) throw new ArgumentException("maxHeight must not be below minHeight");
            if (minLifetime <= 0f || maxLifetime < minLifetime) throw new ArgumentException("invalid lifetime range");

            _random = random;
            _minHeight = minHeight;
            _maxHeight = maxHeight;
            _minHeightGap = minHeightGap;
            _maxTiltDegrees = maxTiltDegrees;
            _minLifetime = minLifetime;
            _maxLifetime = maxLifetime;
            _minExpiryGap = minExpiryGap;
            _age = new float[count];
            _states = new WindCurrentState[count];
            _placed = new bool[count];
            _expired = new bool[count];
            _others = new float[count];

            for (int i = 0; i < count; i++)
            {
                float phase = (i + (float)_random.NextDouble() * 0.5f) / count; // evenly spread life phases
                float height = PickHeight(i);
                float lifetime = PickLifetime(i, 0f, phase, NextLifetime());
                _age[i] = phase * lifetime;
                _states[i] = new WindCurrentState(phase, height, NextTilt(), lifetime);
                _placed[i] = true;
            }
        }

        // Negative, NaN and infinite deltas are ignored.
        public void Tick(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0f) return;

            for (int i = 0; i < _states.Length; i++)
            {
                _age[i] += deltaTime;
                var s = _states[i];
                _expired[i] = _age[i] >= s.Lifetime;
                if (_expired[i])
                {
                    _placed[i] = false; // not one of the "others" until it has respawned
                    if (Draining) _states[i] = new WindCurrentState(1f, s.Height, s.TiltDegrees, s.Lifetime);
                }
                else
                    _states[i] = new WindCurrentState(_age[i] / s.Lifetime, s.Height, s.TiltDegrees, s.Lifetime);
            }

            for (int i = 0; i < _states.Length; i++)
            {
                if (!_expired[i] || Draining) continue;
                Respawn(i);
                _placed[i] = true;
            }
        }

        // Index of the first candidate at least `gap` away from each of the first `othersCount` values, otherwise the
        // candidate farthest from them (ties keep the earliest). With no others the first candidate wins.
        public static int PickIndex(float[] candidates, float[] others, int othersCount, float gap)
        {
            int best = 0;
            float bestDistance = -1f;
            for (int c = 0; c < candidates.Length; c++)
            {
                float distance = float.MaxValue;
                for (int j = 0; j < othersCount; j++)
                    distance = Math.Min(distance, Math.Abs(candidates[c] - others[j]));

                if (distance >= gap) return c;
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    best = c;
                }
            }

            return best;
        }

        public static int PickIndex(float[] candidates, float[] others, float gap) =>
            PickIndex(candidates, others, others.Length, gap);

        private void Respawn(int self)
        {
            // Carry the time past the expiry into the new current, consuming whole lifetimes a hitch skipped over.
            float leftover = _age[self] - _states[self].Lifetime;
            float lifetime = NextLifetime();
            for (int guard = 0; leftover >= lifetime; guard++)
            {
                if (guard >= MaxCarryRespawns)
                {
                    leftover %= lifetime;
                    break;
                }

                leftover -= lifetime;
                lifetime = NextLifetime();
            }

            float height = PickHeight(self);
            lifetime = PickLifetime(self, leftover, 0f, lifetime);
            _age[self] = leftover;
            _states[self] = new WindCurrentState(leftover / lifetime, height, NextTilt(), lifetime);
        }

        private float PickHeight(int self)
        {
            for (int k = 0; k < Candidates; k++)
                _candidates[k] = _minHeight + (float)_random.NextDouble() * (_maxHeight - _minHeight);

            int n = CollectOthers(self, remaining: false);
            return _candidates[PickIndex(_candidates, _others, n, _minHeightGap)];
        }

        // Lifetime whose remaining time stays apart from the other currents' remaining time. The current has already
        // lived `age` seconds (respawn carry) or sits at life `phase` (initial spread), so it has
        // lifetime * (1 - phase) - age left. `first` is already known to outlast `age`; redraws that do not are replaced by it.
        private float PickLifetime(int self, float age, float phase, float first)
        {
            _lifetimes[0] = first;
            for (int k = 1; k < Candidates; k++)
            {
                float candidate = NextLifetime();
                _lifetimes[k] = candidate * (1f - phase) > age ? candidate : first;
            }

            for (int k = 0; k < Candidates; k++) _candidates[k] = _lifetimes[k] * (1f - phase) - age;
            int n = CollectOthers(self, remaining: true);
            return _lifetimes[PickIndex(_candidates, _others, n, _minExpiryGap)];
        }

        private int CollectOthers(int self, bool remaining)
        {
            int n = 0;
            for (int j = 0; j < _states.Length; j++)
            {
                if (j == self || !_placed[j]) continue;
                _others[n++] = remaining ? _states[j].Lifetime - _age[j] : _states[j].Height;
            }

            return n;
        }

        private float NextTilt() => ((float)_random.NextDouble() * 2f - 1f) * _maxTiltDegrees;

        private float NextLifetime() => _minLifetime + (float)_random.NextDouble() * (_maxLifetime - _minLifetime);
    }
}
