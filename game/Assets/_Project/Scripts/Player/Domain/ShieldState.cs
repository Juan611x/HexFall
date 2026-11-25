using System;

namespace HexFall.Player
{
    public enum ParryGrade { None, Partial, Perfect }

    public readonly struct HitResult
    {
        public readonly ParryGrade Grade;
        public readonly int Taken;
        public readonly int Absorbed;

        public HitResult(ParryGrade grade, int taken, int absorbed)
        {
            Grade = grade;
            Taken = taken;
            Absorbed = absorbed;
        }
    }

    // The magic shield: a few charges; raising it spends one and opens a guard. A hit in the first
    // part of the guard is a perfect parry (no damage); later in the guard it is a partial parry that
    // blocks the shield's percentage; outside the guard the full damage goes through.
    // Spent charges come back one at a time.
    public sealed class ShieldState
    {
        private readonly int _maxCharges;
        private readonly float _rechargeSeconds;
        private readonly float _perfectSeconds;
        private readonly float _guardSeconds;
        private readonly float _partialBlock;

        private int _charges;
        private float _rechargeFrom;
        private float _raisedAt = float.NegativeInfinity;

        public ShieldState(int maxCharges, float rechargeSeconds, float perfectSeconds, float guardSeconds, float partialBlock)
        {
            _maxCharges = maxCharges;
            _rechargeSeconds = rechargeSeconds;
            _perfectSeconds = perfectSeconds;
            _guardSeconds = guardSeconds;
            _partialBlock = partialBlock;
            _charges = maxCharges;
        }

        public int MaxCharges => _maxCharges;

        public int Charges(float now)
        {
            while (_charges < _maxCharges && now >= _rechargeFrom + _rechargeSeconds)
            {
                _charges++;
                _rechargeFrom += _rechargeSeconds;
            }
            return _charges;
        }

        public bool TryRaise(float now)
        {
            if (now < _raisedAt + _guardSeconds) return false;
            var before = Charges(now);
            if (before == 0) return false;
            if (before == _maxCharges) _rechargeFrom = now;
            _charges--;
            _raisedAt = now;
            return true;
        }

        // Restores every charge at once (pickups, respawn). False when nothing was missing.
        public bool Refill(float now)
        {
            if (Charges(now) == _maxCharges) return false;
            _charges = _maxCharges;
            return true;
        }

        // True while at least one charge is available, whether or not a guard is still running.
        public bool HasCharge(float now) => Charges(now) > 0;

        public bool IsGuarding(float now) => now >= _raisedAt && now < _raisedAt + _guardSeconds;

        public ParryGrade GradeAt(float hitTime)
        {
            if (!IsGuarding(hitTime)) return ParryGrade.None;
            return hitTime < _raisedAt + _perfectSeconds ? ParryGrade.Perfect : ParryGrade.Partial;
        }

        public HitResult Resolve(int damage, float hitTime)
        {
            var grade = GradeAt(hitTime);
            int taken;
            switch (grade)
            {
                case ParryGrade.Perfect: taken = 0; break;
                case ParryGrade.Partial: taken = (int)Math.Floor(damage * (1f - _partialBlock)); break;
                default: taken = damage; break;
            }
            return new HitResult(grade, taken, damage - taken);
        }
    }
}
