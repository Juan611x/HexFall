using UnityEditor;

namespace HexFall.Editor.Import
{
    // Quaternius' Universal Animation Library is authored facing -Z (Blender's forward), not Unity's +Z.
    // Travelling clips (walk, jog, run, sprint) must face where they travel: with Unity's default
    // "Based Upon: Body Orientation" their facing is estimated from a twisted torso and came out about
    // 27 degrees (jog) and 21 degrees (sprint) to the right, so they use their original orientation
    // turned 180 degrees (the crouch-walk and the slide are travelling too, though their names have no
    // such marker), one exact axis conversion instead of a measured offset per clip. In-place
    // clips (idle, jumps, spells) have no travel direction and some are authored side-on (the idle is
    // 14 degrees off its root), so Unity's body estimate is what keeps them square to the front.
    public static class UalClipOrientation
    {
        private static readonly string[] LibraryFolders = { "UniversalAnimationLibrary/", "UniversalAnimationLibrary2/" };
        private static readonly string[] TravellingMarkers = { "Walk", "Jog", "Run", "Sprint", "Crouch_Fwd", "Slide" };

        public static bool AppliesTo(string assetPath)
        {
            if (assetPath == null) return false;
            var path = assetPath.Replace('\\', '/');
            foreach (var folder in LibraryFolders)
            {
                if (path.Contains(folder)) return true;
            }
            return false;
        }

        public static bool IsTravelling(string clipName)
        {
            if (string.IsNullOrEmpty(clipName)) return false;
            foreach (var marker in TravellingMarkers)
            {
                if (clipName.Contains(marker)) return true;
            }
            return false;
        }

        public static void Apply(ModelImporterClipAnimation clip)
        {
            var travelling = IsTravelling(clip.name);
            clip.lockRootRotation = true;
            clip.keepOriginalOrientation = travelling;
            clip.rotationOffset = travelling ? 180f : 0f;
        }
    }

    // Applies UalClipOrientation to every clip of the library whenever it is (re)imported, so the rule
    // survives reimports and covers clips added later.
    public sealed class UalClipOrientationPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!UalClipOrientation.AppliesTo(assetPath)) return;
            var importer = (ModelImporter)assetImporter;
            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            foreach (var clip in clips) UalClipOrientation.Apply(clip);
            importer.clipAnimations = clips;
        }
    }
}
