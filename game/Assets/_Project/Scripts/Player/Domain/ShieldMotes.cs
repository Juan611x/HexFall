namespace HexFall.Player
{
    // Emission and radial motion of the air motes for one phase of the shield build. Radial is in local units per
    // second around the shield centre (negative = inward, positive = outward); SpawnRadius is the radius of the
    // spawn sphere in the same local units (the sphere mesh has radius SurfaceRadius). Pure data: the adapter
    // (MagicShield) copies it into the particle system when the phase changes.
    public readonly struct MoteProfile
    {
        public readonly float Rate;
        public readonly float Radial;
        public readonly float SpawnRadius;
        public readonly float OrbitalY;

        public MoteProfile(float rate, float radial, float spawnRadius, float orbitalY)
        {
            Rate = rate;
            Radial = radial;
            SpawnRadius = spawnRadius;
            OrbitalY = orbitalY;
        }
    }

    public static class ShieldMotes
    {
        public const float SurfaceRadius = 0.5f;
        public const int DisassemblePuff = 14;

        // Assembling: dense, spawned outside the surface, drifting in while spiralling. Held: a few motes orbiting on
        // the surface. Disassembling and after: no new motes; the live ones are pushed outward (the puff adds more).
        public static MoteProfile For(ShieldBuildPhase phase)
        {
            switch (phase)
            {
                case ShieldBuildPhase.Assembling: return new MoteProfile(110f, -0.9f, 0.8f, 1.5f);
                case ShieldBuildPhase.Holding: return new MoteProfile(22f, 0f, SurfaceRadius + 0.02f, 1.2f);
                case ShieldBuildPhase.Disassembling:
                case ShieldBuildPhase.Done: return new MoteProfile(0f, 1.8f, SurfaceRadius + 0.02f, 0.6f);
                default: return new MoteProfile(0f, 0f, SurfaceRadius, 0f);
            }
        }

        // A one-off outward puff when the disassembly starts (the held rate is low, so the blow-out needs a push).
        public static int PuffCount(ShieldBuildPhase previous, ShieldBuildPhase current) =>
            previous < ShieldBuildPhase.Disassembling && current >= ShieldBuildPhase.Disassembling ? DisassemblePuff : 0;
    }
}
