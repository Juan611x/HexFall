using UnityEngine;

namespace HexFall.Player.Locomotion
{
    // Decides whether a jump press becomes a climb. Used by Grounded, Jump and Fall.
    public static class ClimbEntry
    {
        // Needs a (buffered) jump press, move input toward the ledge and a ledge the classifier accepts.
        // On success the ledge and kind are left in the context for ClimbState.Enter.
        public static bool TryStart(LocomotionContext ctx, LocomotionSettings settings, PlayerIntent intent, GroundSense ground)
        {
            if (!ground.Ledge.HasLedge) return false;
            if (!JumpBuffered(ctx.TimeSinceJumpPressed, settings)) return false;
            if (!HasMoveInput(intent)) return false;

            var approach = new Vector3(ground.Ledge.ApproachDirection.x, 0f, ground.Ledge.ApproachDirection.z);
            if (approach.sqrMagnitude <= 0.0001f) return false;

            var local = new Vector3(intent.Move.x, 0f, intent.Move.y).normalized;
            var wanted = Quaternion.Euler(0f, intent.CameraYaw, 0f) * local;
            if (Vector3.Dot(wanted, approach.normalized) < settings.climb.approachMinDot) return false;

            var kind = LedgeClassifier.Classify(ground.Ledge, settings.climb);
            if (kind == ClimbKind.None) return false;

            ctx.ClimbKind = kind;
            ctx.ClimbLedge = ground.Ledge;
            return true;
        }

        // Cheap pre-check for the motor, run before the machine ticks: whether a ledge measurement could
        // matter this frame. It applies the same jump-buffer and move-input rules as TryStart (using the
        // timers as the machine will have updated them) plus the air grab window, so the motor never
        // probes in a situation where TryStart would refuse anyway, and never skips one it would accept.
        public static bool ShouldProbe(LocomotionContext ctx, PlayerIntent intent, LocomotionSettings settings,
            float deltaTime, bool grounded)
        {
            if (!HasMoveInput(intent)) return false;
            var sinceJump = intent.JumpPressed ? 0f : ctx.TimeSinceJumpPressed + deltaTime;
            if (!JumpBuffered(sinceJump, settings)) return false;
            var sinceGrounded = grounded ? 0f : ctx.TimeSinceGrounded + deltaTime;
            return sinceGrounded <= settings.climb.airGrabWindowSeconds;
        }

        private static bool JumpBuffered(float timeSinceJumpPressed, LocomotionSettings settings) =>
            timeSinceJumpPressed <= settings.jumpBufferSeconds;

        private static bool HasMoveInput(PlayerIntent intent) => intent.Move.sqrMagnitude > 0.0001f;
    }

    // Scripted climb: the domain owns the path, the motor only follows it. Each tick sets
    // ctx.ClimbTarget (where the body must be at the end of the tick) and ctx.Velocity as the step
    // from ctx.Position to it divided by the delta time, so a CharacterController can follow with
    // Move(Velocity * dt) or the motor can place the body at ClimbTarget directly. Input is ignored.
    //
    // Path: phase 1 (reachSeconds) goes straight from the start to the hang point at the wall face,
    // below the top; phase 2 (pullUpSeconds) rises first and moves forward later (eased) onto the top.
    // Climbing over then adds phase 3 (overSeconds): a straight move from the top to past the far
    // edge at top height. The body ends on the first tick after the path is complete, so the last step is always applied.
    // The capsule is kept at the crouch height: it fits under low ceilings over the top and the
    // Grounded state grows it back (headroom is guaranteed for a climb up).
    public sealed class ClimbState : ILocomotionState
    {
        private const float Epsilon = 0.00001f;

        private readonly LocomotionSettings _settings;
        private Vector3 _start;
        private Vector3 _hang;
        private Vector3 _top;
        private Vector3 _far;
        private Vector3 _approach;
        private float _total;
        private float _pullEnd;

        public ClimbState(LocomotionSettings settings)
        {
            _settings = settings;
        }

        public LocomotionStateId Id => LocomotionStateId.Climb;

        public void Enter(LocomotionContext ctx)
        {
            var climb = _settings.climb;
            var ledge = ctx.ClimbLedge;
            ctx.BodyHeight = _settings.crouchHeight;
            ctx.TimeSinceJumpPressed = float.PositiveInfinity;
            ctx.ClimbElapsed = 0f;
            ctx.ClimbProgress = 0f;
            ctx.ClimbOverPhase = false;
            ctx.Velocity = Vector3.zero;

            _approach = new Vector3(ledge.ApproachDirection.x, 0f, ledge.ApproachDirection.z);
            _approach = _approach.sqrMagnitude > 0.0001f ? _approach.normalized : Vector3.forward;
            _start = ctx.Position;
            var edge = ledge.TopPoint;
            var hangY = _start.y + (edge.y - _start.y) * climb.reachRiseFraction;
            var hang = edge - _approach * climb.wallStandoff;
            _hang = new Vector3(hang.x, hangY, hang.z);
            var forward = Mathf.Min(climb.forwardOntoTop, ledge.TopDepth * 0.5f);
            _top = edge + _approach * Mathf.Max(0f, forward);
            // Climbing over adds a third phase that carries the body from the top to past the far edge
            // at top height, so the fall starts with nothing under the body.
            var over = ctx.ClimbKind == ClimbKind.ClimbOver;
            _far = edge + _approach * (ledge.TopDepth + Mathf.Max(0f, climb.overClearDistance));
            _pullEnd = Mathf.Max(0f, climb.reachSeconds) + Mathf.Max(0f, climb.pullUpSeconds);
            _total = _pullEnd + (over ? Mathf.Max(0f, climb.overSeconds) : 0f);
            ctx.ClimbTarget = _start;
        }

        public LocomotionStateId Tick(LocomotionContext ctx, PlayerIntent intent, GroundSense ground, float deltaTime)
        {
            if (ctx.ClimbElapsed >= _total - Epsilon)
                return ctx.ClimbKind == ClimbKind.ClimbUp ? LocomotionStateId.Grounded : LocomotionStateId.Fall;

            ctx.ClimbElapsed = Mathf.Min(ctx.ClimbElapsed + deltaTime, _total);
            if (ctx.ClimbElapsed >= _total - Epsilon) ctx.ClimbElapsed = _total;
            ctx.ClimbProgress = _total > 0f ? ctx.ClimbElapsed / _total : 1f;

            ctx.ClimbOverPhase = ctx.ClimbKind == ClimbKind.ClimbOver && ctx.ClimbElapsed > _pullEnd;
            ctx.ClimbTarget = Evaluate(ctx.ClimbElapsed);
            ctx.Velocity = deltaTime > 0f ? (ctx.ClimbTarget - ctx.Position) / deltaTime : Vector3.zero;
            ctx.PlanarSpeed = new Vector2(ctx.Velocity.x, ctx.Velocity.z).magnitude;

            var yaw = Mathf.Atan2(_approach.x, _approach.z) * Mathf.Rad2Deg;
            ctx.FacingYaw = Mathf.SmoothDampAngle(ctx.FacingYaw, yaw, ref ctx.YawVelocity, _settings.rotationSmoothSeconds, Mathf.Infinity, deltaTime);
            return Id;
        }

        // Hands the body over: climbing up leaves it standing still on top, climbing over throws it
        // forward across the top into the fall.
        public void Exit(LocomotionContext ctx)
        {
            ctx.Velocity = ctx.ClimbKind == ClimbKind.ClimbOver
                ? _approach * _settings.climb.overPushSpeed
                : Vector3.zero;
            ctx.PlanarSpeed = new Vector2(ctx.Velocity.x, ctx.Velocity.z).magnitude;
        }

        private Vector3 Evaluate(float elapsed)
        {
            var reach = Mathf.Max(0f, _settings.climb.reachSeconds);
            if (elapsed < reach) return Vector3.Lerp(_start, _hang, elapsed / reach);

            if (elapsed > _pullEnd)
            {
                var o = _total > _pullEnd ? Mathf.Clamp01((elapsed - _pullEnd) / (_total - _pullEnd)) : 1f;
                return Vector3.Lerp(_top, _far, o);
            }

            var pull = _pullEnd - reach;
            var u = pull > 0f ? Mathf.Clamp01((elapsed - reach) / pull) : 1f;
            var horizontal = u * u;
            var vertical = 1f - (1f - u) * (1f - u);
            return new Vector3(
                Mathf.Lerp(_hang.x, _top.x, horizontal),
                Mathf.Lerp(_hang.y, _top.y, vertical),
                Mathf.Lerp(_hang.z, _top.z, horizontal));
        }
    }
}
