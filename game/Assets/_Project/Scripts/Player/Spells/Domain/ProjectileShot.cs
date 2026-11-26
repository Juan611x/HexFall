namespace HexFall.Player.Spells
{
    public readonly struct ProjectileShot
    {
        public readonly int Index;
        public readonly int Damage;
        public readonly float SpreadDegrees;
        public readonly float Charge;

        public ProjectileShot(int index, int damage, float spreadDegrees, float charge)
        {
            Index = index;
            Damage = damage;
            SpreadDegrees = spreadDegrees;
            Charge = charge;
        }
    }
}
