using UnityEngine;

namespace HexFall.UI
{
    // What the player side needs from the HUD to show the spell ring around the crosshair (PlayerHud implements it).
    public interface ISpellRingView
    {
        void SetSpellRing(SpellRingState state, Color color);
    }
}
