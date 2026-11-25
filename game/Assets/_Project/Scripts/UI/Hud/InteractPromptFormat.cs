namespace HexFall.UI
{
    // Text of the Interact prompt. The key is the keyboard one; a gamepad glyph is out of scope for now.
    public static class InteractPromptFormat
    {
        public const string Key = "E";

        public static string Label(string spellName) =>
            string.IsNullOrWhiteSpace(spellName) ? "Swap spell" : $"Swap to {spellName.Trim()}";
    }
}
