using System;

namespace HexFall.UI
{
    public sealed class SpellPicker
    {
        private readonly Func<int, int> _nextInt;
        private int _last = -1;

        // nextInt(max) returns a value in [0, max).
        public SpellPicker(Func<int, int> nextInt) => _nextInt = nextInt;

        public int Next(int count)
        {
            if (count <= 0)
            {
                return -1;
            }

            if (count == 1)
            {
                _last = 0;
                return 0;
            }

            int pick;
            if (_last < 0 || _last >= count)
            {
                pick = _nextInt(count);
            }
            else
            {
                pick = _nextInt(count - 1);
                if (pick >= _last)
                {
                    pick++;
                }
            }

            _last = pick;
            return pick;
        }
    }
}
