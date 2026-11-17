using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace HexFall.UI
{
    // Optionally renders the UI panel into a texture the size of the URP render target
    // so HexFall/PSX Post can composite it before quantization and dithering.
    // Off by default: the panel is then a normal, crisp screen overlay.
    [RequireComponent(typeof(UIDocument))]
    [DefaultExecutionOrder(-100)]
    public sealed class PsxUiTarget : MonoBehaviour
    {
        private static readonly int UiTexId = Shader.PropertyToID("_PSXUITex");
        private static readonly int UiEnabledId = Shader.PropertyToID("_PSXUIEnabled");

        [SerializeField] private PanelSettings panelSettings;
        [SerializeField] private bool psxUiEnabled;

        private PanelSettings _runtimeSettings;
        private bool _originalClearColor;
        private Color _originalClearValue;
        private RenderTexture _target;
        private Vector2Int _screenSize;
        private float _renderScale;
        private PsxUiPresentation _presentation;
        private bool _mapperInstalled;

        public RenderTexture Target => _target;
        public PanelSettings RuntimeSettings => _runtimeSettings;

        public bool PsxUiEnabled
        {
            get => psxUiEnabled;
            set => SetPsxUiEnabled(value);
        }

        public void SetPsxUiEnabled(bool enabled)
        {
            psxUiEnabled = enabled;
            if (isActiveAndEnabled && _runtimeSettings != null) Apply();
        }

        private void Awake()
        {
            // A copy keeps the runtime render texture out of the shared asset.
            _runtimeSettings = Instantiate(panelSettings);
            _originalClearColor = _runtimeSettings.clearColor;
            _originalClearValue = _runtimeSettings.colorClearValue;
            GetComponent<UIDocument>().panelSettings = _runtimeSettings;
        }

        private void OnEnable() => Apply();

        private void OnDisable()
        {
            Shader.SetGlobalFloat(UiEnabledId, 0f);
            Shader.SetGlobalTexture(UiTexId, null);
            if (_runtimeSettings != null)
            {
                _runtimeSettings.targetTexture = null;
                ClearMapper();
            }
            Release();
        }

        private void OnDestroy()
        {
            if (_runtimeSettings != null) Destroy(_runtimeSettings);
        }

        private void OnValidate()
        {
            if (Application.isPlaying && isActiveAndEnabled && _runtimeSettings != null) Apply();
        }

        private void Update()
        {
            if (_presentation.RenderToTexture &&
                (Screen.width != _screenSize.x || Screen.height != _screenSize.y || CurrentScale() != _renderScale))
                Apply();
            ApplyRootClass();
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

        private void Apply()
        {
            if (_runtimeSettings == null) return;

            _presentation = PsxUiPresentation.Resolve(psxUiEnabled);

            // Unbind before releasing the old texture the panel still points at.
            _runtimeSettings.targetTexture = null;
            Release();

            if (_presentation.RenderToTexture)
            {
                _screenSize = new Vector2Int(Screen.width, Screen.height);
                _renderScale = CurrentScale();
                var size = PsxUiTargetSize.Compute(_screenSize.x, _screenSize.y, _renderScale);

                _target = new RenderTexture(size.x, size.y, 0, RenderTextureFormat.ARGB32)
                {
                    name = "PSX UI Target",
                    filterMode = FilterMode.Point,
                    useMipMap = false,
                    autoGenerateMips = false,
                };
                _target.Create();

                // The panel does not clear a target texture by default, which leaves stale frames behind.
                _runtimeSettings.clearColor = true;
                _runtimeSettings.colorClearValue = Color.clear;
                _runtimeSettings.SetScreenToPanelSpaceFunction(MapScreenToPanel);
                _mapperInstalled = true;
                _runtimeSettings.targetTexture = _target;
                Shader.SetGlobalTexture(UiTexId, _target);
            }
            else
            {
                _runtimeSettings.clearColor = _originalClearColor;
                _runtimeSettings.colorClearValue = _originalClearValue;
                ClearMapper();
                Shader.SetGlobalTexture(UiTexId, null);
            }

            Shader.SetGlobalFloat(UiEnabledId, _presentation.CompositeFlag);
            ApplyRootClass();
        }

        private void ClearMapper()
        {
            if (!_mapperInstalled) return;
            _runtimeSettings.SetScreenToPanelSpaceFunction(null);
            _mapperInstalled = false;
        }

        private void ApplyRootClass()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            root?.EnableInClassList(PsxUiPresentation.RootClass, _presentation.Class != null);
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
