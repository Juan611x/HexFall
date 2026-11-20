using UnityEngine;

namespace HexFall.UI
{
    // Background music for gameplay maps: a single looping track that fades in when the map starts and
    // follows the music level the player saved in the menu's sound settings. Reuses MenuMusicPlayer.
    // No gesture gate: maps are reached from the menu, so the browser has already unlocked audio.
    [RequireComponent(typeof(AudioSource))]
    public class MapMusic : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float volume = 0.4f;
        [SerializeField, Min(0f)] private float fadeInSeconds = 3f;

        private MenuMusicPlayer _player;
        private SoundSettings _sound;

        private void OnEnable()
        {
            var source = GetComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = false;
            source.volume = 0f;
            _sound = new SoundSettings(new PlayerPrefsSoundSettingsStore().Load());
            _player = new MenuMusicPlayer(new UnityMusicLayers(source), new[] { volume }, fadeInSeconds, false, () => _sound.MusicMultiplier);
            _player.Begin();
        }

        private void Update() => _player?.Tick(Time.unscaledDeltaTime);
    }
}
