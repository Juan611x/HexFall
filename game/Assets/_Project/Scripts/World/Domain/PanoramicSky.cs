using UnityEngine;

namespace HexFall.World
{
    // Maps a spot in a latitude-longitude sky image to the world direction it is drawn at, matching
    // Unity's Skybox/Panoramic shader (u across from the left, v up from the bottom, rotation around Y
    // as the material's Rotation property applies it).
    public static class PanoramicSky
    {
        public static Vector3 Direction(float u, float v, float rotationDegrees)
        {
            var longitude = (0.5f - u) * 2f * Mathf.PI;
            var polar = (1f - v) * Mathf.PI;
            var x = Mathf.Sin(polar) * Mathf.Cos(longitude);
            var y = Mathf.Cos(polar);
            var z = Mathf.Sin(polar) * Mathf.Sin(longitude);

            var a = rotationDegrees * Mathf.Deg2Rad;
            var cos = Mathf.Cos(a);
            var sin = Mathf.Sin(a);
            return new Vector3(cos * x - sin * z, y, sin * x + cos * z).normalized;
        }
    }
}
