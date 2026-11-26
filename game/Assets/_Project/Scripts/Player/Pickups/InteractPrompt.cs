using System;
using HexFall.Player.Spells;

namespace HexFall.Player
{
    // What the HUD needs to know about Interact: whether to show the "E" prompt, and for which spell
    // (the one lying in the pickup the player could take).
    public readonly struct InteractPrompt : IEquatable<InteractPrompt>
    {
        public static readonly InteractPrompt Hidden = default;

        public InteractPrompt(SpellDefinition spell)
        {
            Spell = spell;
        }

        public bool Visible => Spell != null;

        public SpellDefinition Spell { get; }

        public bool Equals(InteractPrompt other) => ReferenceEquals(Spell, other.Spell);
        public override bool Equals(object obj) => obj is InteractPrompt other && Equals(other);
        public override int GetHashCode() => Spell != null ? Spell.GetHashCode() : 0;
    }
}
