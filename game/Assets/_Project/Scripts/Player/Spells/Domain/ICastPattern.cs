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
        // True while a held cast waits for its pose before it really starts (channels with a wind-up).
        bool IsWindingUp => false;
        // 1..0 remaining share of a running channel; 0 for patterns that do not channel or a channel not running.
        float ChannelRemaining(float now) => 0f;
        // The adapter reports the cast pose reached; only a pattern that is winding up reacts.
        void PoseReached(float now, ICastSink sink) { }
    }
}
