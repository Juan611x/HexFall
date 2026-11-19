using System;

namespace HexFall.UI
{
    public sealed class GuestNameGenerator
    {
        private static readonly string[] First = { "Ashen", "Hex", "Storm", "Gloom", "Ember", "Frost", "Void", "Rune" };
        private static readonly string[] Second = { "Mage", "Weaver", "Caller", "Warden", "Sage", "Binder" };

        private readonly Random _random;

        public GuestNameGenerator(Random random) => _random = random;

        public string Generate() =>
            $"{First[_random.Next(First.Length)]}_{Second[_random.Next(Second.Length)]}_{_random.Next(1000):000}";
    }
}
