using HexFall.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace HexFall.UI.Tests
{
    public class PanoramicSkyTests
    {
        private static void AssertDirection(Vector3 expected, Vector3 actual) =>
            Assert.Less(Vector3.Distance(expected, actual), 0.001f, $"expected {expected}, got {actual}");

        [Test]
        public void CentreOfTheHorizonLooksAlongX() =>
            AssertDirection(Vector3.right, PanoramicSky.Direction(0.5f, 0.5f, 0f));

        [Test]
        public void QuarterAcrossTheHorizonLooksAlongZ() =>
            AssertDirection(Vector3.forward, PanoramicSky.Direction(0.25f, 0.5f, 0f));

        [Test]
        public void TopOfTheImageLooksUp() =>
            AssertDirection(Vector3.up, PanoramicSky.Direction(0.3f, 1f, 0f));

        [Test]
        public void HalfwayUpIsFortyFiveDegrees() =>
            AssertDirection(new Vector3(0.7071f, 0.7071f, 0f), PanoramicSky.Direction(0.5f, 0.75f, 0f));

        [Test]
        public void SkyRotationTurnsTheDirectionAroundY() =>
            AssertDirection(Vector3.forward, PanoramicSky.Direction(0.5f, 0.5f, 90f));
    }

    public class GroundMistTests
    {
        [Test]
        public void EmitterSitsUnderTheTargetAtMistHeight()
        {
            Assert.AreEqual(new Vector3(12f, 0.2f, -7f), GroundMist.Anchor(new Vector3(12f, 1.7f, -7f), 0.2f));
        }

        [Test]
        public void TargetHeightDoesNotMoveTheMist()
        {
            Assert.AreEqual(GroundMist.Anchor(new Vector3(3f, 0f, 4f), 0.5f), GroundMist.Anchor(new Vector3(3f, 30f, 4f), 0.5f));
        }
    }

    public class EnvironmentApplierTests
    {
        private Material _savedSkybox;
        private bool _savedFog;
        private Color _savedFogColor;
        private float _savedFogDensity;
        private FogMode _savedFogMode;
        private float _savedFogStart, _savedFogEnd;
        private AmbientMode _savedAmbientMode;
        private Color _savedSky, _savedEquator, _savedGround;
        private float _savedReflection;
        private Light _savedSun;
        private GameObject _lightObject;
        private EnvironmentProfile _profile;

        [SetUp]
        public void Save()
        {
            _savedSkybox = RenderSettings.skybox;
            _savedFog = RenderSettings.fog;
            _savedFogColor = RenderSettings.fogColor;
            _savedFogDensity = RenderSettings.fogDensity;
            _savedFogMode = RenderSettings.fogMode;
            _savedFogStart = RenderSettings.fogStartDistance;
            _savedFogEnd = RenderSettings.fogEndDistance;
            _savedAmbientMode = RenderSettings.ambientMode;
            _savedSky = RenderSettings.ambientSkyColor;
            _savedEquator = RenderSettings.ambientEquatorColor;
            _savedGround = RenderSettings.ambientGroundColor;
            _savedReflection = RenderSettings.reflectionIntensity;
            _savedSun = RenderSettings.sun;

            _lightObject = new GameObject("TestMoon", typeof(Light));
            _profile = ScriptableObject.CreateInstance<EnvironmentProfile>();
            _profile.sky = new Material(Shader.Find("Skybox/Panoramic"));
            _profile.sky.SetTexture("_MainTex", new Texture2D(4, 2));
            _profile.fallbackSky = new Material(Shader.Find("Skybox/Procedural"));
            _profile.moonU = 0.5f;
            _profile.moonV = 0.75f;
            _profile.skyRotation = 0f;
            _profile.moonColor = new Color(0.6f, 0.7f, 1f);
            _profile.moonIntensity = 0.3f;
            _profile.moonShadowStrength = 0.8f;
            _profile.ambientSky = new Color(0.05f, 0.06f, 0.1f);
            _profile.ambientEquator = new Color(0.03f, 0.04f, 0.07f);
            _profile.ambientGround = new Color(0.01f, 0.01f, 0.02f);
            _profile.fog = true;
            _profile.fogColor = new Color(0.02f, 0.03f, 0.06f);
            _profile.fogMode = FogMode.ExponentialSquared;
            _profile.fogDensity = 0.04f;
            _profile.fogStart = 8f;
            _profile.fogEnd = 45f;
            _profile.reflectionIntensity = 0.25f;
        }

        [TearDown]
        public void Restore()
        {
            RenderSettings.skybox = _savedSkybox;
            RenderSettings.fog = _savedFog;
            RenderSettings.fogColor = _savedFogColor;
            RenderSettings.fogDensity = _savedFogDensity;
            RenderSettings.fogMode = _savedFogMode;
            RenderSettings.fogStartDistance = _savedFogStart;
            RenderSettings.fogEndDistance = _savedFogEnd;
            RenderSettings.ambientMode = _savedAmbientMode;
            RenderSettings.ambientSkyColor = _savedSky;
            RenderSettings.ambientEquatorColor = _savedEquator;
            RenderSettings.ambientGroundColor = _savedGround;
            RenderSettings.reflectionIntensity = _savedReflection;
            RenderSettings.sun = _savedSun;
            Object.DestroyImmediate(_lightObject);
            Object.DestroyImmediate(_profile);
        }

        private Light Moon => _lightObject.GetComponent<Light>();

        [Test]
        public void UsesTheSkyWhenItsTextureIsPresent()
        {
            EnvironmentApplier.Apply(_profile, Moon);
            Assert.AreSame(_profile.sky, RenderSettings.skybox);
        }

        [Test]
        public void FallsBackWhenTheSkyTextureIsMissing()
        {
            _profile.sky.SetTexture("_MainTex", null);
            EnvironmentApplier.Apply(_profile, Moon);
            Assert.AreSame(_profile.fallbackSky, RenderSettings.skybox);
        }

        [Test]
        public void AppliesAmbientFogAndReflections()
        {
            EnvironmentApplier.Apply(_profile, Moon);

            Assert.AreEqual(AmbientMode.Trilight, RenderSettings.ambientMode);
            Assert.AreEqual(_profile.ambientSky, RenderSettings.ambientSkyColor);
            Assert.AreEqual(_profile.ambientEquator, RenderSettings.ambientEquatorColor);
            Assert.AreEqual(_profile.ambientGround, RenderSettings.ambientGroundColor);
            Assert.IsTrue(RenderSettings.fog);
            Assert.AreEqual(FogMode.ExponentialSquared, RenderSettings.fogMode);
            Assert.AreEqual(_profile.fogColor, RenderSettings.fogColor);
            Assert.AreEqual(0.04f, RenderSettings.fogDensity, 0.0001f);
            Assert.AreEqual(0.25f, RenderSettings.reflectionIntensity, 0.0001f);
        }

        [Test]
        public void LinearFogIsClearUpCloseAndOpaqueFarAway()
        {
            _profile.fogMode = FogMode.Linear;
            EnvironmentApplier.Apply(_profile, Moon);

            Assert.AreEqual(FogMode.Linear, RenderSettings.fogMode);
            Assert.AreEqual(8f, RenderSettings.fogStartDistance, 0.0001f);
            Assert.AreEqual(45f, RenderSettings.fogEndDistance, 0.0001f);
        }

        [Test]
        public void MoonLightShinesFromTheMoonInTheSky()
        {
            EnvironmentApplier.Apply(_profile, Moon);

            var towardMoon = new Vector3(0.7071f, 0.7071f, 0f);
            Assert.Less(Vector3.Distance(-towardMoon, Moon.transform.forward), 0.001f);
            Assert.AreEqual(LightType.Directional, Moon.type);
            Assert.AreEqual(_profile.moonColor, Moon.color);
            Assert.AreEqual(0.3f, Moon.intensity, 0.0001f);
            Assert.AreEqual(0.8f, Moon.shadowStrength, 0.0001f);
        }

        [Test]
        public void MissingProfileChangesNothing()
        {
            RenderSettings.fogDensity = 0.123f;
            Assert.DoesNotThrow(() => EnvironmentApplier.Apply(null, Moon));
            Assert.AreEqual(0.123f, RenderSettings.fogDensity, 0.0001f);
        }
    }
}
