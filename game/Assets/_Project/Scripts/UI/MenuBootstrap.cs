using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace HexFall.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class MenuBootstrap : MonoBehaviour
    {
        [SerializeField] private VisualTreeAsset titleScreen;

        private ScreenRouter _router;
        private ScreenHost _host;
        private IDisposable _presenter;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            _router = new ScreenRouter(ScreenId.Title);
            _host = new ScreenHost(root, _router, new Dictionary<ScreenId, VisualTreeAsset>
            {
                { ScreenId.Title, titleScreen },
            });
            _host.ScreenShown += OnScreenShown;
            _host.ScreenHidden += OnScreenHidden;
            _host.ShowCurrent();
        }

        private void OnDisable()
        {
            _presenter?.Dispose();
            _presenter = null;
            _host?.Dispose();
            _host = null;
        }

        private void OnScreenShown(ScreenId id, VisualElement screen)
        {
            if (id == ScreenId.Title)
                _presenter = new TitlePresenter(new TitleView(screen), _router);
        }

        private void OnScreenHidden(ScreenId id)
        {
            _presenter?.Dispose();
            _presenter = null;
        }
    }
}
