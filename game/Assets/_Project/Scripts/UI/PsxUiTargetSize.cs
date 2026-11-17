using UnityEngine;

namespace HexFall.UI
{
    public static class PsxUiTargetSize
    {
        // Mirrors URP: scale clamped to [0.1, 2], size truncated.
        public static Vector2Int Compute(int screenWidth, int screenHeight, float renderScale)
        {
            var scale = Mathf.Clamp(renderScale, 0.1f, 2f);
            return new Vector2Int(
                Mathf.Max(1, (int)(screenWidth * scale)),
                Mathf.Max(1, (int)(screenHeight * scale)));
        }
    }
}
