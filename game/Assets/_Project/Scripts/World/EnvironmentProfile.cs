using UnityEngine;

namespace HexFall.World
{
    // Everything that sets the mood of a map: the sky, where its moon is, the moonlight, ambient light
    // and fog. Shared by maps through the Environment prefab so they all start with the same look.
    [CreateAssetMenu(menuName = "HexFall/Environment Profile", fileName = "EP_New")]
    public class EnvironmentProfile : ScriptableObject
    {
        [Header("Sky")]
        [Tooltip("Skybox/Panoramic material. Used only while its texture is present.")]
        public Material sky;
        [Tooltip("Plain sky used when the sky texture is missing (e.g. a local import not done yet).")]
        public Material fallbackSky;
        [Tooltip("Applied to the sky material's Rotation; the moonlight follows it.")]
        public float skyRotation;
        [Tooltip("Where the moon sits in the sky image (u from the left, v from the bottom).")]
        [Range(0f, 1f)] public float moonU = 0.5f;
        [Range(0f, 1f)] public float moonV = 0.75f;

        [Header("Moonlight")]
        public Color moonColor = new Color(0.62f, 0.7f, 1f);
        public float moonIntensity = 0.35f;
        [Range(0f, 1f)] public float moonShadowStrength = 0.8f;

        [Header("Ambient (trilight)")]
        public Color ambientSky = new Color(0.06f, 0.08f, 0.14f);
        public Color ambientEquator = new Color(0.035f, 0.045f, 0.08f);
        public Color ambientGround = new Color(0.015f, 0.015f, 0.025f);
        [Range(0f, 1f)] public float reflectionIntensity = 0.3f;

        [Header("Fog (exponential squared)")]
        public bool fog = true;
        public Color fogColor = new Color(0.025f, 0.035f, 0.07f);
        public float fogDensity = 0.035f;
    }
}
