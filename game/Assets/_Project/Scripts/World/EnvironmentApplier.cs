using UnityEngine;
using UnityEngine.Rendering;

namespace HexFall.World
{
    // Puts a map's EnvironmentProfile into the scene: sky (or its fallback), ambient light, fog and the
    // moon light, aimed from where the moon is drawn in the sky. Runs in the editor too, so the scene
    // looks right while it is being built.
    [ExecuteAlways]
    public class EnvironmentApplier : MonoBehaviour
    {
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");
        private static readonly int Rotation = Shader.PropertyToID("_Rotation");

        [SerializeField] private EnvironmentProfile profile;
        [SerializeField] private Light moonLight;

        private void OnEnable() => Apply(profile, moonLight);

        private void OnValidate() => Apply(profile, moonLight);

        public static void Apply(EnvironmentProfile profile, Light moon)
        {
            if (profile == null) return;

            var hasSky = profile.sky != null && profile.sky.HasProperty(MainTex) && profile.sky.GetTexture(MainTex) != null;
            if (hasSky && profile.sky.HasProperty(Rotation)) profile.sky.SetFloat(Rotation, profile.skyRotation);
            RenderSettings.skybox = hasSky ? profile.sky : profile.fallbackSky;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = profile.ambientSky;
            RenderSettings.ambientEquatorColor = profile.ambientEquator;
            RenderSettings.ambientGroundColor = profile.ambientGround;
            RenderSettings.reflectionIntensity = profile.reflectionIntensity;

            RenderSettings.fog = profile.fog;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = profile.fogColor;
            RenderSettings.fogDensity = profile.fogDensity;

            if (moon == null) return;
            moon.type = LightType.Directional;
            moon.color = profile.moonColor;
            moon.intensity = profile.moonIntensity;
            moon.shadowStrength = profile.moonShadowStrength;
            var towardMoon = PanoramicSky.Direction(profile.moonU, profile.moonV, profile.skyRotation);
            moon.transform.rotation = Quaternion.LookRotation(-towardMoon);
            RenderSettings.sun = moon;
        }
    }
}
