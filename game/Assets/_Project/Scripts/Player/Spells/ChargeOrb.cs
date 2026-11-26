using UnityEngine;

namespace HexFall.Player.Spells
{
    // The charging orb at the muzzle: a sphere (the bolt's mesh and material) that PlayerCombat shows while a
    // charged spell is held. It grows from the spell's min to max size with the charge, shifts toward the
    // full-charge colour through a property block, and throbs once fully charged. Everything it needs comes
    // from the spell's charge settings, so any charged spell gets it by data.
    public class ChargeOrb : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [Tooltip("The sphere that is shown; hidden by default and moved to the muzzle while charging.")]
        [SerializeField] private Transform visual;
        [Tooltip("The sphere's renderer, tinted with the charge colour through a property block.")]
        [SerializeField] private Renderer visualRenderer;

        private MaterialPropertyBlock _block;
        private float _fullSince = float.NaN;

        public bool IsVisible { get; private set; }

        // The charge level last shown; 0 while hidden.
        public float Level { get; private set; }

        // Shows the orb at the position for the spell at the charge level. A spell that does not charge or has
        // the orb turned off hides it instead.
        public void Show(SpellDefinition spell, float level, float now, Vector3 position)
        {
            if (spell == null || spell.Pattern != CastPattern.Charged || !spell.Charge.showOrb || visual == null)
            {
                Hide();
                return;
            }

            var look = spell.LookAtCharge(level);
            if (level >= 1f && float.IsNaN(_fullSince)) _fullSince = now;
            if (level < 1f) _fullSince = float.NaN;
            var pulse = ChargeFeedback.Pulse(level, float.IsNaN(_fullSince) ? 0f : now - _fullSince);

            IsVisible = true;
            Level = level;
            visual.gameObject.SetActive(true);
            visual.position = position;
            visual.localScale = LocalScale(spell.Charge.orbSize * look.Scale * pulse);
            Tint(new Color(look.Color.R, look.Color.G, look.Color.B, spell.Color.a));
        }

        public void Hide()
        {
            IsVisible = false;
            Level = 0f;
            _fullSince = float.NaN;
            if (visual != null) visual.gameObject.SetActive(false);
        }

        private void OnDisable() => Hide();

        // A world-space diameter as a local scale, whatever the parents' scale.
        private Vector3 LocalScale(float diameter)
        {
            var parent = visual.parent != null ? visual.parent.lossyScale : Vector3.one;
            return new Vector3(
                diameter / Mathf.Max(Mathf.Abs(parent.x), 1e-4f),
                diameter / Mathf.Max(Mathf.Abs(parent.y), 1e-4f),
                diameter / Mathf.Max(Mathf.Abs(parent.z), 1e-4f));
        }

        private void Tint(Color color)
        {
            if (visualRenderer == null) return;
            _block ??= new MaterialPropertyBlock();
            _block.Clear();
            _block.SetColor(BaseColorId, color);
            _block.SetColor(ColorId, color);
            visualRenderer.SetPropertyBlock(_block);
        }
    }
}
