using HexFall.Player.Spells;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HexFall.Player
{
    // A spell lying on the ground. It does not extend Pickup: that base collects on touch, hides and
    // respawns, while this one keeps its place, holds whichever spell is on it (swapped by Interact) and
    // only reports who is in reach. What a touch means is the wizard's SpellInteractor's call (take it at
    // once while holding Base, else wait for Interact). Spell pickups never respawn.
    // The motion is the shared PickupMotion; the visual is tinted with the spell colour.
    [RequireComponent(typeof(Collider))]
    public class SpellPickup : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [Tooltip("The spell lying here. Empty offers nothing.")]
        [SerializeField] private SpellDefinition spell;
        [Tooltip("The floating model. Moved and turned; the root stays put.")]
        [SerializeField] private Transform visual;
        [Tooltip("The renderer tinted with the spell colour (through a property block).")]
        [SerializeField] private Renderer tint;
        [SerializeField] private float bobHeight = 0.15f;
        [SerializeField] private float bobPeriodSeconds = 2f;
        [SerializeField] private float spinDegreesPerSecond = 45f;

        private MaterialPropertyBlock _block;
        private Vector3 _restPosition;
        private SpellInteractor _reachedBy;
        // The colliders of the wizard inside the trigger: the reach lasts until the last one leaves.
        private readonly TouchSet<Object> _touching = new TouchSet<Object>();

        public SpellDefinition Spell => spell;

        // Taken by a wizard that held Base: nothing is left to pick up.
        public bool Consumed { get; private set; }

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            if (visual != null) _restPosition = visual.localPosition;
            ApplyTint();
        }

        private void OnDisable()
        {
            _touching.Clear();
            if (_reachedBy != null) _reachedBy.Leave(this);
            _reachedBy = null;
        }

        private void Update()
        {
            if (visual == null) return;
            visual.localPosition = _restPosition + Vector3.up * PickupMotion.Bob(Time.time, bobHeight, bobPeriodSeconds);
            visual.localRotation = Quaternion.Euler(0f, PickupMotion.Spin(Time.time, spinDegreesPerSecond), 0f);
        }

        private void OnTriggerEnter(Collider other) => Reach(other);
        private void OnTriggerExit(Collider other) => Leave(other);

        internal void Reach(Collider collider) => Reach(collider.gameObject, collider);
        internal void Leave(Collider collider) => Leave(collider.gameObject, collider);
        internal void Reach(GameObject collector) => Reach(collector, collector);
        internal void Leave(GameObject collector) => Leave(collector, collector);

        // Something touched the trigger: a wizard with a SpellInteractor takes it from here.
        private void Reach(GameObject collector, Object key)
        {
            if (Consumed) return;
            var interactor = collector.GetComponentInParent<SpellInteractor>();
            if (interactor == null) return;
            _touching.Add(key);
            _reachedBy = interactor;
            interactor.Reach(this);
        }

        private void Leave(GameObject collector, Object key)
        {
            var interactor = collector.GetComponentInParent<SpellInteractor>();
            if (interactor == null) return;
            if (!_touching.Remove(key) && !_touching.IsEmpty) return; // others of the wizard are still inside
            interactor.Leave(this);
            if (_reachedBy == interactor) _reachedBy = null;
        }

        // Interact swapped: this pickup now holds the spell the wizard put down.
        internal void Hold(SpellDefinition newSpell)
        {
            spell = newSpell;
            ApplyTint();
        }

        // The wizard took it and left nothing behind.
        internal void Consume()
        {
            Consumed = true;
            gameObject.SetActive(false);
        }

        private void ApplyTint()
        {
            if (tint == null) return;
            _block ??= new MaterialPropertyBlock();
            _block.Clear();
            if (spell != null)
            {
                _block.SetColor(BaseColorId, spell.Color);
                _block.SetColor(ColorId, spell.Color);
            }
            tint.SetPropertyBlock(_block);
        }
    }
}
