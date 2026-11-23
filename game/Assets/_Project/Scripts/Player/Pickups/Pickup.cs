using UnityEngine;

namespace HexFall.Player
{
    // Base for every collectible. Owns what all pickups share: the visual floats and turns slowly,
    // touching the trigger collects it, the pickup VFX plays, and it comes back in place after a while.
    // A subclass only says what collecting does; when TryApply returns false (nothing to give, e.g. the
    // shield is already full) the pickup stays where it is.
    [RequireComponent(typeof(Collider))]
    public abstract class Pickup : MonoBehaviour
    {
        [Tooltip("The floating model (and its idle VFX). Moved and hidden; the root stays put.")]
        [SerializeField] private Transform visual;
        [SerializeField] private float bobHeight = 0.15f;
        [SerializeField] private float bobPeriodSeconds = 2f;
        [SerializeField] private float spinDegreesPerSecond = 45f;
        [SerializeField] private float respawnSeconds = 15f;
        [Tooltip("Spawned where the pickup was collected; destroyed after its lifetime.")]
        [SerializeField] private GameObject collectVfx;
        [SerializeField] private float collectVfxSeconds = 2f;

        private RespawnTimer _respawn;
        private Vector3 _restPosition;
        private bool _hidden;

        protected virtual void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            _respawn = new RespawnTimer(respawnSeconds);
            if (visual != null) _restPosition = visual.localPosition;
        }

        // Says what collecting does for the object that touched the pickup (any collider of it).
        protected abstract bool TryApply(GameObject collector);

        private void Update()
        {
            if (_hidden && _respawn.IsAvailable(Time.time)) SetHidden(false);
            if (_hidden || visual == null) return;
            visual.localPosition = _restPosition + Vector3.up * PickupMotion.Bob(Time.time, bobHeight, bobPeriodSeconds);
            visual.localRotation = Quaternion.Euler(0f, PickupMotion.Spin(Time.time, spinDegreesPerSecond), 0f);
        }

        // Stay as well as Enter: someone waiting inside a pickup they could not use gets it as soon as
        // it would do something.
        private void OnTriggerEnter(Collider other) => TryCollect(other);
        private void OnTriggerStay(Collider other) => TryCollect(other);

        private void TryCollect(Collider other)
        {
            if (_hidden) return;
            if (!TryApply(other.gameObject)) return;
            _respawn.Consume(Time.time);
            if (collectVfx != null)
            {
                var vfxAt = visual != null ? visual.position : transform.position;
                Destroy(Instantiate(collectVfx, vfxAt, Quaternion.identity), collectVfxSeconds);
            }
            SetHidden(true);
        }

        private void SetHidden(bool hidden)
        {
            _hidden = hidden;
            if (visual != null) visual.gameObject.SetActive(!hidden);
        }
    }
}
