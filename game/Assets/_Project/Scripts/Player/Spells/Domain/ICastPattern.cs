namespace HexFall.Player.Spells
{
    // What a pattern may do to its caster; keeps patterns free of events and cooldown storage.
    public interface ICastSink
    {
        void Fire(ProjectileShot shot);
        void ChannelStart();
        void ChannelTick(int damage);
        void ChannelEnd(float channelledSeconds);
        void StartCooldown(float from, float seconds);
    }

    // The caster only calls Press when the spell is ready; times are already validated.
    public interface ICastPattern
    {
        bool IsBusy { get; }
        void Press(float now, ICastSink sink);
        void Release(float now, ICastSink sink);
        void Tick(float now, ICastSink sink);
        void Cancel(float now, ICastSink sink);
        // 0..1 progress of a charge in progress; 0 for patterns that do not charge.
        float ChargeLevel(float now);
    }
}
