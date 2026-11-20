namespace HexFall.Player
{
    public sealed class Health
    {
        public Health(int max)
        {
            Max = max;
            Current = max;
        }

        public int Max { get; }
        public int Current { get; private set; }
        public bool IsDead => Current <= 0;

        // Returns the damage actually applied (never more than the health left).
        public int TakeDamage(int amount)
        {
            if (amount <= 0) return 0;
            var applied = amount > Current ? Current : amount;
            Current -= applied;
            return applied;
        }

        public void Reset() => Current = Max;
    }
}
