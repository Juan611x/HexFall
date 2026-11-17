using System;
using System.Collections.Generic;

namespace HexFall.UI
{
    public sealed class ScreenRouter
    {
        private readonly Stack<ScreenId> _history = new Stack<ScreenId>();

        public ScreenRouter(ScreenId initial) => Current = initial;

        public ScreenId Current { get; private set; }
        public bool CanGoBack => _history.Count > 0;
        public event Action<ScreenId, ScreenId> ScreenChanged;

        public void Navigate(ScreenId screen)
        {
            if (screen == Current) return;
            _history.Push(Current);
            Change(screen);
        }

        public bool Back()
        {
            if (!CanGoBack) return false;
            Change(_history.Pop());
            return true;
        }

        private void Change(ScreenId next)
        {
            var previous = Current;
            Current = next;
            ScreenChanged?.Invoke(previous, next);
        }
    }
}
