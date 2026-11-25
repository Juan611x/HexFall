using System;
using System.Collections.Generic;
using HexFall.Player.Spells;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HexFall.Player
{
    // The wizard's side of spell pickups. Owns the loadout (the Base spell is the spell PlayerCombat starts
    // with), applies swaps through PlayerCombat.SetSpell and receives the Interact action from PlayerInput
    // (Send Messages). Pickups in reach register here; the nearest one that holds a different spell is the
    // Interact target, and Prompt says so for the HUD.
    public class SpellInteractor : MonoBehaviour
    {
        private PlayerCombat _combat;
        private SpellLoadout<SpellDefinition> _loadout;
        private readonly List<SpellPickup> _inReach = new List<SpellPickup>();
        private readonly List<Vector3> _points = new List<Vector3>();
        private readonly List<SpellPickup> _candidates = new List<SpellPickup>();

        // The "E" prompt: visible while a pickup that needs Interact is in reach, with the spell it holds.
        public InteractPrompt Prompt { get; private set; }

        // Raised only when Prompt changes: a pickup needing Interact enters or leaves reach, the nearest
        // one changes, or a swap changes what the target holds.
        public event Action<InteractPrompt> PromptChanged;

        private void Awake()
        {
            _combat = GetComponent<PlayerCombat>();
        }

        private void Update() => Refresh();

        private void OnDisable()
        {
            _inReach.Clear();
            Refresh();
        }

        // Interact input (Send Messages): only the press counts.
        public void OnInteract(InputValue value)
        {
            if (value.isPressed) Interact();
        }

        // A pickup was touched. Holding Base takes its spell at once; otherwise it waits for Interact.
        internal void Reach(SpellPickup pickup)
        {
            if (!EnsureLoadout() || pickup == null || pickup.Consumed) return;
            if (_loadout.Offer(pickup.Spell) == OfferOutcome.Swapped)
            {
                _combat.SetSpell(_loadout.Current);
                pickup.Consume();
                _inReach.Remove(pickup);
            }
            else if (!_inReach.Contains(pickup))
            {
                _inReach.Add(pickup);
            }
            Refresh();
        }

        internal void Leave(SpellPickup pickup)
        {
            _inReach.Remove(pickup);
            Refresh();
        }

        // Swaps with the nearest pickup in reach: the held spell is left in that pickup. False when there is none.
        public bool Interact()
        {
            Refresh();
            var target = Target();
            if (target == null) return false;
            var dropped = _loadout.Swap(target.Spell);
            _combat.SetSpell(_loadout.Current);
            if (dropped != null) target.Hold(dropped);
            else target.Consume();
            Refresh();
            return true;
        }

        // Recomputes the target and the prompt (the nearest pickup changes as the wizard moves).
        internal void Refresh()
        {
            var target = Target();
            var next = target != null ? new InteractPrompt(target.Spell) : InteractPrompt.Hidden;
            if (next.Equals(Prompt)) return;
            Prompt = next;
            PromptChanged?.Invoke(next);
        }

        // The nearest pickup that is still there and holds a spell other than the held one.
        private SpellPickup Target()
        {
            if (_loadout == null) return null;
            _points.Clear();
            _candidates.Clear();
            foreach (var pickup in _inReach)
            {
                if (pickup == null || pickup.Consumed || !pickup.gameObject.activeInHierarchy) continue;
                if (pickup.Spell == null || ReferenceEquals(pickup.Spell, _loadout.Current)) continue;
                _candidates.Add(pickup);
                _points.Add(pickup.transform.position);
            }
            var index = PickupTargeting.Nearest(transform.position, _points);
            return index < 0 ? null : _candidates[index];
        }

        // The Base spell is whatever PlayerCombat starts with; without one there is no loadout.
        private bool EnsureLoadout()
        {
            if (_loadout != null) return true;
            if (_combat == null) _combat = GetComponent<PlayerCombat>();
            if (_combat == null || _combat.ActiveSpell == null) return false;
            _loadout = new SpellLoadout<SpellDefinition>(_combat.ActiveSpell);
            return true;
        }
    }
}
