using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace HexFall.UI
{
    // Shows a floating number for every damage reported to DamageFeed: a HUD label kept over the hit
    // point each frame and animated by DamagePopupMotion. Hidden while the point is behind the camera.
    [RequireComponent(typeof(UIDocument))]
    public class DamagePopupLayer : MonoBehaviour
    {
        [SerializeField] private float lifetimeSeconds = 0.9f;
        [SerializeField] private float riseHeight = 0.8f;
        [SerializeField] private float popScale = 1.6f;
        [SerializeField] private float popSeconds = 0.15f;
        [SerializeField] private float fadeSeconds = 0.3f;
        [Tooltip("Sideways spacing (metres) between consecutive numbers.")]
        [SerializeField] private float driftSpacing = 0.35f;

        private sealed class Popup
        {
            public Label Label;
            public Vector3 At;
            public float Drift;
            public float Age;
        }

        private readonly List<Popup> _popups = new List<Popup>();
        private DamagePopupMotion _motion;
        private VisualElement _root;
        private int _sequence;

        private void OnEnable()
        {
            _motion = new DamagePopupMotion(lifetimeSeconds, riseHeight, popScale, popSeconds, fadeSeconds, driftSpacing);
            _root = GetComponent<UIDocument>().rootVisualElement;
            DamageFeed.Dealt += Spawn;
        }

        private void OnDisable()
        {
            DamageFeed.Dealt -= Spawn;
            foreach (var popup in _popups) popup.Label.RemoveFromHierarchy();
            _popups.Clear();
        }

        private void Spawn(Vector3 at, int amount)
        {
            if (_root == null) return;
            var label = new Label(amount.ToString()) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("damage-popup");
            label.style.display = DisplayStyle.None;
            _root.Add(label);
            _popups.Add(new Popup { Label = label, At = at, Drift = _motion.Drift(_sequence++) });
        }

        private void LateUpdate()
        {
            var cam = Camera.main;
            for (var i = _popups.Count - 1; i >= 0; i--)
            {
                var popup = _popups[i];
                popup.Age += Time.deltaTime;
                if (_motion.IsDone(popup.Age))
                {
                    popup.Label.RemoveFromHierarchy();
                    _popups.RemoveAt(i);
                    continue;
                }
                Place(popup, cam);
            }
        }

        private void Place(Popup popup, Camera cam)
        {
            var style = popup.Label.style;
            if (cam == null || _root.panel == null)
            {
                style.display = DisplayStyle.None;
                return;
            }

            var world = popup.At + Vector3.up * _motion.Rise(popup.Age) + cam.transform.right * popup.Drift;
            var screen = cam.WorldToScreenPoint(world);
            if (screen.z <= 0f)
            {
                style.display = DisplayStyle.None;
                return;
            }

            var panelPos = RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(screen.x, Screen.height - screen.y));
            style.display = DisplayStyle.Flex;
            style.left = panelPos.x;
            style.top = panelPos.y;
            style.scale = new Scale(Vector2.one * _motion.Scale(popup.Age));
            style.opacity = _motion.Alpha(popup.Age);
        }
    }
}
