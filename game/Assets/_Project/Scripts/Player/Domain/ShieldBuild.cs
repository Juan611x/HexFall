using System;

namespace HexFall.Player
{
    public enum ShieldBuildPhase { NotStarted, Assembling, Holding, Disassembling, Done }

    // Cosmetic timing of the air shield: it assembles right after being raised, stays built for the
    // guard window and then disassembles. Pure function of the time since the shield was raised; it
    // never changes the guard rules. If the hold is shorter than the assembly, disassembly starts
    // from the progress reached, so the shield never jumps.
    public sealed class ShieldBuild
    {
        private readonly float _holdSeconds;
        private readonly float _assembleSeconds;
        private readonly float _disassembleSeconds;
        private readonly float _peak;

        public ShieldBuild(float holdSeconds, float assembleSeconds = 0.12f, float disassembleSeconds = 0.35f)
        {
            if (!IsFinite(holdSeconds) || holdSeconds < 0f) throw new ArgumentOutOfRangeException(nameof(holdSeconds));
            if (!IsFinite(assembleSeconds) || assembleSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(assembleSeconds));
            if (!IsFinite(disassembleSeconds) || disassembleSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(disassembleSeconds));

            _holdSeconds = holdSeconds;
            _assembleSeconds = assembleSeconds;
            _disassembleSeconds = disassembleSeconds;
            _peak = Math.Min(1f, holdSeconds / assembleSeconds);
        }

        // Negative or NaN elapsed means "not raised yet": empty and not done.
        public ShieldBuildPhase PhaseAt(float elapsed)
        {
            if (float.IsNaN(elapsed) || elapsed < 0f) return ShieldBuildPhase.NotStarted;
            if (elapsed >= _holdSeconds + _disassembleSeconds) return ShieldBuildPhase.Done;
            if (elapsed >= _holdSeconds) return ShieldBuildPhase.Disassembling;
            if (elapsed < _assembleSeconds) return ShieldBuildPhase.Assembling;
            return ShieldBuildPhase.Holding;
        }

        public bool IsDone(float elapsed) => PhaseAt(elapsed) == ShieldBuildPhase.Done;

        // Build progress in [0,1], eased with smoothstep.
        public float Progress(float elapsed)
        {
            switch (PhaseAt(elapsed))
            {
                case ShieldBuildPhase.NotStarted:
                case ShieldBuildPhase.Done:
                    return 0f;
                case ShieldBuildPhase.Assembling:
                    return Smooth(elapsed / _assembleSeconds);
                case ShieldBuildPhase.Holding:
                    return 1f;
                default:
                    return Smooth(_peak * (1f - (elapsed - _holdSeconds) / _disassembleSeconds));
            }
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static float Smooth(float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return t * t * (3f - 2f * t);
        }
    }
}
