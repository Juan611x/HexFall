namespace HexFall.UI
{
    // What the player side needs from the HUD to show the Interact prompt (PlayerHud implements it).
    public interface IInteractPromptView
    {
        void ShowPrompt(string key, string text);
        void HidePrompt();
    }
}
