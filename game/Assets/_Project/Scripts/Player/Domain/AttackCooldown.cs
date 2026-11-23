namespace HexFall.Player
{
    public sealed class AttackCooldown
    {
        private readonly float _seconds;
        private float _lastAttack = float.NegativeInfinity;

        public AttackCooldown(float seconds) => _seconds = seconds;

        public bool TryAttack(float now)
        {
            if (now < _lastAttack + _seconds) return false;
            _lastAttack = now;
            return true;
        }
    }
}
