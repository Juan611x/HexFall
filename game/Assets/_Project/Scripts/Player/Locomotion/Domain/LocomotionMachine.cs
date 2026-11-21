using System;

namespace HexFall.Player.Locomotion
{
    // Runs the current locomotion state each tick, keeps the shared timers (time since grounded,
    // jump buffer, jump cooldown) and performs at most one transition per tick.
    public sealed class LocomotionMachine
    {
        private readonly ILocomotionState[] _states;
        private ILocomotionState _current;

        public LocomotionMachine(LocomotionSettings settings, LocomotionStateId initial)
            : this(new ILocomotionState[] { new GroundedState(settings), new JumpState(settings), new FallState(settings), new CrouchState(settings), new SlideState(settings) }, initial)
        {
        }

        public LocomotionMachine(ILocomotionState[] states, LocomotionStateId initial)
        {
            _states = states;
            Context = new LocomotionContext();
            _current = Find(initial);
            _current.Enter(Context);
        }

        public LocomotionContext Context { get; }

        public LocomotionStateId Current => _current.Id;

        public event Action<LocomotionStateId, LocomotionStateId> StateChanged;

        public void Tick(PlayerIntent intent, GroundSense ground, float deltaTime)
        {
            Context.TimeSinceGrounded = ground.Grounded ? 0f : Context.TimeSinceGrounded + deltaTime;
            Context.TimeSinceJumpPressed = intent.JumpPressed ? 0f : Context.TimeSinceJumpPressed + deltaTime;
            if (Context.JumpCooldown > 0f) Context.JumpCooldown -= deltaTime;

            var next = _current.Tick(Context, intent, ground, deltaTime);
            if (next == _current.Id) return;

            var target = Find(next);
            var from = _current.Id;
            _current.Exit(Context);
            _current = target;
            _current.Enter(Context);
            StateChanged?.Invoke(from, next);
        }

        private ILocomotionState Find(LocomotionStateId id)
        {
            foreach (var state in _states)
            {
                if (state.Id == id) return state;
            }
            throw new ArgumentException("No locomotion state registered for " + id);
        }
    }
}
