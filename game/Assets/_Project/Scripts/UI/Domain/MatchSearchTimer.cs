namespace HexFall.UI
{
    public sealed class MatchSearchTimer
    {
        private float _elapsed;

        public bool IsSearching { get; private set; }
        public int ElapsedSeconds => (int)_elapsed;
        public string Label => TimeLabel.MinSec(ElapsedSeconds);

        public void Start()
        {
            _elapsed = 0f;
            IsSearching = true;
        }

        public void Cancel()
        {
            _elapsed = 0f;
            IsSearching = false;
        }

        public void Tick(float deltaSeconds)
        {
            if (IsSearching) _elapsed += deltaSeconds;
        }
    }
}
