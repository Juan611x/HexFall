using HexFall.UI;
using UnityEngine;

namespace HexFall.Player
{
    // The player's health. Incoming hits go through the shield first (PlayerCombat), the rest is taken
    // from health, and the HUD is kept in sync. Until there is a death flow the wizard is simply
    // restored to full health and shield charges when defeated.
    [RequireComponent(typeof(PlayerCombat))]
    public class PlayerVitals : MonoBehaviour, IDamageable
    {
        [SerializeField] private string displayName = "Wizard";
        [SerializeField] private int maxHealth = 100;

        private PlayerCombat _combat;
        private Health _health;
        private PlayerHud _hud;
        private int _shownCharges = -1;

        private void Awake()
        {
            _combat = GetComponent<PlayerCombat>();
            _health = new Health(maxHealth);
        }

        private void Start()
        {
            _hud = FindAnyObjectByType<PlayerHud>();
            Push();
        }

        public void TakeHit(int damage, GameObject source)
        {
            var hit = _combat.ResolveHit(damage);
            _health.TakeDamage(hit.Taken);
            Debug.Log($"[Player] hit {damage}: {hit.Grade}, took {hit.Taken}, health {_health.Current}/{_health.Max}");
            if (_health.IsDead)
            {
                Debug.Log("[Player] defeated; restoring health and shield for testing");
                _health.Reset();
                _combat.RefillShield();
            }
            Push();
        }

        // Restores up to `amount` health. False when nothing was healed (already full).
        public bool Heal(int amount)
        {
            if (_health.Heal(amount) == 0) return false;
            Push();
            return true;
        }

        private void Update()
        {
            if (_combat.ShieldCharges != _shownCharges) Push();
        }

        private void Push()
        {
            _shownCharges = _combat.ShieldCharges;
            if (_hud != null)
                _hud.Show(new PlayerStatus(displayName, _health.Current, _health.Max, _shownCharges, _combat.MaxShieldCharges));
        }
    }
}
