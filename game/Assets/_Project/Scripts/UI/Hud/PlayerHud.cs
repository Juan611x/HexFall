using UnityEngine;
using UnityEngine.UIElements;

namespace HexFall.UI
{
    // Fills the HUD's player cluster. Until health and shield systems exist, it shows the serialized
    // values; gameplay code will call Show with the live status.
    [RequireComponent(typeof(UIDocument))]
    public class PlayerHud : MonoBehaviour, IInteractPromptView
    {
        [SerializeField] private string playerName = "Wizard";
        [SerializeField] private int health = 100;
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private int shieldCharges = 3;
        [SerializeField] private int maxShieldCharges = 3;

        private InkPlayerHudElement _cluster;
        private Label _name;
        private Label _health;
        private Label _shield;
        private InkPromptElement _prompt;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            _cluster = root.Q<InkPlayerHudElement>("player-cluster");
            _name = root.Q<Label>("player-name");
            _health = root.Q<Label>("player-health");
            _shield = root.Q<Label>("player-shield");
            _prompt = root.Q<InkPromptElement>("interact-prompt");
            Show(new PlayerStatus(playerName, health, maxHealth, shieldCharges, maxShieldCharges));
        }

        // The Interact prompt: a key badge and a label, shown only while something needs Interact.
        public void ShowPrompt(string key, string text) => _prompt?.Show(key, text);

        public void HidePrompt() => _prompt?.Hide();

        public void Show(PlayerStatus status)
        {
            if (_cluster == null) return;
            _cluster.SetStatus(PlayerHudFormat.HealthFraction(status.Health, status.MaxHealth),
                PlayerHudFormat.Seals(status.ShieldCharges, status.MaxShieldCharges));
            _name.text = status.Name;
            _health.text = PlayerHudFormat.HealthLabel(status.Health, status.MaxHealth);
            if (_shield != null) _shield.text = PlayerHudFormat.ShieldLabel(status.ShieldCharges, status.MaxShieldCharges);
        }
    }
}
