using UnityEngine.UIElements;

namespace HexFall.UI
{
    // UI Toolkit has no :focus-visible. Dropping focus after a pointer press keeps the focus ring
    // for keyboard and gamepad navigation only, while text fields keep focus so the player can type.
    public static class PointerFocusRelease
    {
        public static bool ShouldRelease(Focusable focused) =>
            focused is Button || focused is Toggle || focused is BaseSlider<float> || focused is BaseSlider<int>;
    }
}
