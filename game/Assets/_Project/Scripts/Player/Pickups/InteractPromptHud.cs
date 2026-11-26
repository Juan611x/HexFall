using HexFall.UI;
using UnityEngine;

namespace HexFall.Player
{
    // Shows the HUD's Interact prompt from SpellInteractor.PromptChanged (event driven, no polling): the
    // "E" key and "Swap to <spell>" while a pickup needing Interact is in reach, hidden otherwise.
    // The key is the keyboard one; a gamepad glyph is out of scope for now.
    [RequireComponent(typeof(SpellInteractor))]
    public class InteractPromptHud : MonoBehaviour
    {
        private SpellInteractor _interactor;
        private IInteractPromptView _view;

        private void Start()
        {
            if (_view == null) Bind(FindAnyObjectByType<PlayerHud>());
        }

        private void OnDestroy() => Unsubscribe();

        // Points the adapter at a HUD (null for none) and shows the current prompt on it at once.
        internal void Bind(IInteractPromptView view)
        {
            Unsubscribe();
            _view = view;
            if (_view == null) return;
            if (_interactor == null) _interactor = GetComponent<SpellInteractor>();
            _interactor.PromptChanged += Apply;
            Apply(_interactor.Prompt);
        }

        private void Unsubscribe()
        {
            if (_interactor != null) _interactor.PromptChanged -= Apply;
        }

        private void Apply(InteractPrompt prompt)
        {
            if (prompt.Visible)
                _view.ShowPrompt(InteractPromptFormat.Key, InteractPromptFormat.Label(prompt.Spell.DisplayName));
            else
                _view.HidePrompt();
        }
    }
}
