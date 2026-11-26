using System;

namespace HexFall.Player.Spells
{
    public enum OfferOutcome
    {
        // Nothing to do: no spell offered, or the one already held.
        None,
        // Holding Base: the offered spell was taken at once and nothing was dropped.
        Swapped,
        // Holding another spell: taking it is the player's choice (Interact) and drops the held one.
        NeedsInteract,
    }

    // Which spell the wizard holds. The Base spell is the default and is never dropped; any other spell is
    // left behind when another one replaces it. Generic over the spell reference so the rule stays pure
    // (the game uses SpellDefinition).
    public sealed class SpellLoadout<T> where T : class
    {
        public SpellLoadout(T baseSpell)
        {
            Base = baseSpell ?? throw new ArgumentNullException(nameof(baseSpell));
            Current = baseSpell;
        }

        public T Base { get; }

        public T Current { get; private set; }

        public bool HoldsBase => ReferenceEquals(Current, Base);

        // A pickup holding this spell was reached. Holding Base takes it automatically.
        public OfferOutcome Offer(T spell)
        {
            if (spell == null || ReferenceEquals(spell, Current)) return OfferOutcome.None;
            if (!HoldsBase) return OfferOutcome.NeedsInteract;
            Current = spell;
            return OfferOutcome.Swapped;
        }

        // The held spell was changed by something else (PlayerCombat.SetSpell): follow it. Base stays the
        // original Base, and nothing means Base.
        public void Sync(T spell) => Current = spell ?? Base;

        // Interact on a pickup: holds the spell and returns the one that was held, to leave on the ground.
        // Null when nothing is dropped (Base is never dropped, and a null or already-held spell changes nothing).
        public T Swap(T spell)
        {
            if (spell == null || ReferenceEquals(spell, Current)) return null;
            var dropped = HoldsBase ? null : Current;
            Current = spell;
            return dropped;
        }
    }
}
