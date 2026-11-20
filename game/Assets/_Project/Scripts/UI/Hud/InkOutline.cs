using UnityEngine;

namespace HexFall.UI
{
    // Organic "ink blot" outline: an ellipse inscribed in an area whose radius wobbles by up to
    // `roughness` pixels. The wobble is seeded noise smoothed over neighbours, so the edge reads as
    // a brush blot rather than jagged spikes, and the same seed always gives the same shape.
    public static class InkOutline
    {
        private const int SmoothingPasses = 2;

        public static Vector2[] Blob(Rect area, int pointCount, float roughness, int seed)
        {
            var count = Mathf.Max(3, pointCount);
            var noise = SmoothNoise(count, seed);
            var centre = area.center;
            var rx = area.width / 2f;
            var ry = area.height / 2f;
            var points = new Vector2[count];
            for (var i = 0; i < count; i++)
            {
                var angle = 2f * Mathf.PI * i / count;
                var offset = noise[i] * roughness;
                points[i] = centre + new Vector2(Mathf.Cos(angle) * (rx + offset), Mathf.Sin(angle) * (ry + offset));
            }
            return points;
        }

        private static float[] SmoothNoise(int count, int seed)
        {
            var random = new System.Random(seed);
            var values = new float[count];
            for (var i = 0; i < count; i++) values[i] = (float)(random.NextDouble() * 2.0 - 1.0);
            for (var pass = 0; pass < SmoothingPasses; pass++)
            {
                var next = new float[count];
                for (var i = 0; i < count; i++)
                    next[i] = (values[(i + count - 1) % count] + values[i] + values[(i + 1) % count]) / 3f;
                values = next;
            }

            var peak = 0f;
            foreach (var v in values) peak = Mathf.Max(peak, Mathf.Abs(v));
            if (peak > 0f)
                for (var i = 0; i < count; i++) values[i] /= peak;
            return values;
        }
    }
}
