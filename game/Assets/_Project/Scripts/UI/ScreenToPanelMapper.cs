using UnityEngine;

namespace HexFall.UI
{
    public static class ScreenToPanelMapper
    {
        public static readonly Vector2 Invalid = new Vector2(float.NaN, float.NaN);

        // UI Toolkit hands this function pointer positions already flipped to a top-left origin.
        public static Vector2 Map(Vector2 screenPosition, Vector2 screenSize, Vector2 targetSize)
        {
            if (screenSize.x <= 0f || screenSize.y <= 0f) return Invalid;
            if (screenPosition.x < 0f || screenPosition.y < 0f ||
                screenPosition.x > screenSize.x || screenPosition.y > screenSize.y) return Invalid;

            return new Vector2(
                screenPosition.x / screenSize.x * targetSize.x,
                screenPosition.y / screenSize.y * targetSize.y);
        }
    }
}
