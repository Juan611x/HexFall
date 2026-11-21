using UnityEngine;

namespace HexFall.Player.Locomotion
{
    public enum ClimbKind { None, ClimbUp, ClimbOver }

    // Tunable climb values; every threshold the classifier, the trigger and the scripted path use.
    // Lives inside LocomotionSettings (field "climb").
    [System.Serializable]
    public sealed class ClimbSettings
    {
        [Header("Classification (metres above the feet / along the approach)")]
        // Ledge tops below this are left to the step offset and a normal jump. Inclusive.
        public float climbMinHeight = 0.5f;
        // Ledge tops above this cannot be climbed yet. Inclusive.
        public float climbMaxHeight = 1.2f;
        // Top depth needed to stay on top. Inclusive.
        public float standDepth = 0.5f;
        // Tops up to this thick can be climbed over to the far side. Inclusive.
        public float overMaxThickness = 0.6f;
        // Free height above the top needed to stand up on it. Inclusive.
        public float minHeadroom = 1.8f;

        [Header("Sensing (used by the ledge probe)")]
        // How far ahead of the capsule a ledge is looked for.
        public float detectionDistance = 0.8f;
        // Free space needed behind a thin top to climb over it.
        public float landingClearDepth = 0.6f;
        // A ledge top counts as one flat surface while its height varies by no more than this.
        public float topSurfaceTolerance = 0.1f;
        // Spacing of the samples that measure how deep a top is; the end is refined below this.
        public float probeStep = 0.05f;
        // Lowest ground behind a thin top that still counts as a landing, below the top of the ledge.
        public float maxLandingDrop = 3f;

        [Header("Trigger")]
        // Minimum dot between the wanted direction and the approach direction.
        public float approachMinDot = 0.5f;
        // How long after leaving the ground an airborne jump press can still grab a ledge.
        public float airGrabWindowSeconds = 0.5f;

        [Header("Scripted path")]
        // Phase 1: reach the wall face below the top.
        public float reachSeconds = 0.35f;
        // Phase 2: pull up onto the top; match to the ClimbUp_1m clip length.
        public float pullUpSeconds = 0.9f;
        // Height of the hang point as a fraction of the way from the start up to the top.
        public float reachRiseFraction = 0.6f;
        // Distance from the wall face (the edge) at the hang point.
        public float wallStandoff = 0.3f;
        // How far past the edge the body ends on the top; never more than half the top depth.
        public float forwardOntoTop = 0.4f;
        // Climb over, phase 3 (after the pull-up): carries the body from the top to past the far edge.
        public float overSeconds = 0.3f;
        // How far past the far edge the body centre ends, at top height. Keep it above the controller
        // radius so the body is clear of the top before the fall starts.
        public float overClearDistance = 0.4f;
        // Planar speed handed to the Fall state after climbing over.
        public float overPushSpeed = 2.5f;
    }

    // What the ledge sensor measured in front of the body. Heights are metres above the feet.
    public readonly struct LedgeMeasurement
    {
        public readonly bool HasLedge;
        public readonly float TopHeight;
        // World point on the top surface at the edge, where the body climbs up.
        public readonly Vector3 TopPoint;
        // Horizontal unit direction into the obstacle (the opposite of the wall normal).
        public readonly Vector3 ApproachDirection;
        // How far the walkable top extends along the approach direction; infinity when it is long.
        public readonly float TopDepth;
        // Free height above the top.
        public readonly float Headroom;
        public readonly bool LandingClear;
        // Height of the surface behind the top above the feet (negative when below them).
        public readonly float LandingHeight;

        public LedgeMeasurement(bool hasLedge, float topHeight, Vector3 topPoint, Vector3 approachDirection,
            float topDepth, float headroom, bool landingClear, float landingHeight)
        {
            HasLedge = hasLedge;
            TopHeight = topHeight;
            TopPoint = topPoint;
            ApproachDirection = approachDirection;
            TopDepth = topDepth;
            Headroom = headroom;
            LandingClear = landingClear;
            LandingHeight = landingHeight;
        }
    }

    // Decides what to do with a measured ledge. All comparisons against the settings are inclusive;
    // NaN or negative measures classify as None.
    public static class LedgeClassifier
    {
        public static ClimbKind Classify(LedgeMeasurement ledge, ClimbSettings settings)
        {
            if (!ledge.HasLedge) return ClimbKind.None;
            if (!IsValid(ledge.TopHeight) || !IsValid(ledge.TopDepth) || !IsValid(ledge.Headroom)) return ClimbKind.None;
            if (ledge.TopHeight < settings.climbMinHeight || ledge.TopHeight > settings.climbMaxHeight) return ClimbKind.None;

            if (ledge.TopDepth >= settings.standDepth && ledge.Headroom >= settings.minHeadroom) return ClimbKind.ClimbUp;
            if (ledge.TopDepth <= settings.overMaxThickness && ledge.LandingClear) return ClimbKind.ClimbOver;
            return ClimbKind.None;
        }

        private static bool IsValid(float value) => !float.IsNaN(value) && value >= 0f;
    }
}
