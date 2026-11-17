using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace HexFall.UI
{
    // Renders the UI panel into a texture the size of the URP render target so
    // HexFall/PSX Post can composite it before quantization and dithering.
    [RequireComponent(typeof(UIDocument))]
    [DefaultExecutionOrder(-100)]
    public sealed class PsxUiTarget : MonoBehaviour
    {
        private static readonly int UiTexId = Shader.PropertyToID("_PSXUITex");
        private static readonly int UiEnabledId = Shader.PropertyToID("_PSXUIEnabled");

        [SerializeField] private PanelSettings panelSettings;

        private PanelSettings _runtimeSettings;
        private RenderTexture _target;
        private Vector2Int _screenSize;
        private float _renderScale;

        public RenderTexture Target => _target;
        public PanelSettings RuntimeSettings => _runtimeSettings;

        private void Awake()
        {
            // A copy keeps the runtime render texture out of the shared asset.
            _runtimeSettings = Instantiate(panelSettings);
            // The panel does not clear a target texture by default, which leaves stale frames behind.
            _runtimeSettings.clearColor = true;
            _runtimeSettings.colorClearValue = Color.clear;
            GetComponent<UIDocument>().panelSettings = _runtimeSettings;
            _runtimeSettings.SetScreenToPanelSpaceFunction(MapScreenToPanel);
        }

        private void OnEnable() => Rebuild();

        private void OnDisable()
        {
            Shader.SetGlobalFloat(UiEnabledId, 0f);
            Shader.SetGlobalTexture(UiTexId, null);
            if (_runtimeSettings != null) _runtimeSettings.targetTexture = null;
            Release();
        }

        private void OnDestroy()
        {
            if (_runtimeSettings != null) Destroy(_runtimeSettings);
        }

        private void Update()
        {
            if (Screen.width != _screenSize.x || Screen.height != _screenSize.y || CurrentScale() != _renderScale)
                Rebuild();
        }

        private static float CurrentScale() =>
            GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp ? urp.renderScale : 1f;

        private Vector2 MapScreenToPanel(Vector2 screenPosition)
        {
            if (_target == null) return ScreenToPanelMapper.Invalid;
            return ScreenToPanelMapper.Map(
                screenPosition,
                new Vector2(Screen.width, Screen.height),
                new Vector2(_target.width, _target.height));
        }

        private void Rebuild()
        {
            if (_runtimeSettings == null) return;

            _screenSize = new Vector2Int(Screen.width, Screen.height);
            _renderScale = CurrentScale();
            var size = PsxUiTargetSize.Compute(_screenSize.x, _screenSize.y, _renderScale);

            // Unbind before releasing the old texture the panel still points at.
            _runtimeSettings.targetTexture = null;
            Release();

            _target = new RenderTexture(size.x, size.y, 0, RenderTextureFormat.ARGB32)
            {
                name = "PSX UI Target",
                filterMode = FilterMode.Point,
                useMipMap = false,
                autoGenerateMips = false,
            };
            _target.Create();

            _runtimeSettings.targetTexture = _target;
            Shader.SetGlobalTexture(UiTexId, _target);
            Shader.SetGlobalFloat(UiEnabledId, 1f);
        }

        private void Release()
        {
            if (_target == null) return;
            _target.Release();
            Destroy(_target);
            _target = null;
        }
    }
}
