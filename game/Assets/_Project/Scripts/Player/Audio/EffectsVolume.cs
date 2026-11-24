namespace HexFall.Player
{
    // The effects level the player saved in the menu's sound settings (HexFall.Player already
    // references HexFall.UI, so the saved mix is read straight from the PlayerPrefs store).
    public static class EffectsVolume
    {
        public static float Current() => HexFall.World.SpatialSfx.EffectsLevel();
    }
}
