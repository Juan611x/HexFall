using HexFall.Player.Spells;
using HexFall.UI;
using UnityEngine;

namespace HexFall.Player
{
    // Pushes the spell ring (cooldown, charge, channel) to the HUD every frame. The ring is a per-frame visual
    // that drains and fills continuously, so it polls the action machine instead of following events.
    [RequireComponent(typeof(PlayerCombat))]
    public class SpellRingHud : MonoBehaviour
    {
        // Ring colour of the default Base spell (it has no SpellDefinition of its own).
        internal static readonly Color BaseColor = new Color(0.369f, 0.922f, 0.831f, 1f);

        private PlayerCombat _combat;
        private ISpellRingView _view;

        private void Start()
        {
            if (_view == null) Bind(FindAnyObjectByType<PlayerHud>());
        }

        private void Update() => Refresh();

        // Points the adapter at a HUD (null for none) and shows the current ring on it at once.
        internal void Bind(ISpellRingView view)
        {
            _view = view;
            Refresh();
        }

        internal void Refresh()
        {
            if (_view == null) return;
            if (_combat == null) _combat = GetComponent<PlayerCombat>();
            if (_combat == null || _combat.Machine == null) return;
            var ring = _combat.Machine.SpellRing(_combat.Now);
            _view.SetSpellRing(ring, ColorFor(_combat.ActiveSpell, ring));
        }

        // The spell colour; while charging, the colour the charge has blended to (the same as the bolt and the orb).
        internal static Color ColorFor(SpellDefinition spell, SpellRingState ring)
        {
            if (spell == null) return BaseColor;
            if (ring.Mode != SpellRingMode.Charging) return spell.Color;
            var look = spell.LookAtCharge(ring.Fraction).Color;
            return new Color(look.R, look.G, look.B, spell.Color.a);
        }
    }
}
