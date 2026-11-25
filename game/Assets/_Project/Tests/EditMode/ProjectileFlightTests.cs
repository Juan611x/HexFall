using System;
using HexFall.Player.Spells;
using NUnit.Framework;
using UnityEngine;

namespace HexFall.UI.Tests
{
    // The projectile's flight is plain maths: gravity pulls the velocity down, the curve yaws it around
    // world up, and every step reports the segment the hit sweep must cover.
    public class ProjectileFlightTests
    {
        private const float Tol = 1e-3f;

        private static ProjectileFlight Straight(float gravity = 0f, float curve = 0f, float lifetime = 3f, float drag = 0f) =>
            new ProjectileFlight(Vector3.zero, new Vector3(0f, 0f, 30f), gravity, curve, lifetime, drag);

        [Test]
        public void StraightFlightMovesAlongTheVelocityAndKeepsIt()
        {
            var flight = Straight();
            var (from, to) = flight.Step(0.1f);
            Assert.AreEqual(0f, from.z, Tol);
            Assert.AreEqual(3f, to.z, Tol);
            Assert.AreEqual(0f, to.y, Tol);
            Assert.AreEqual(30f, flight.Velocity.z, Tol);
            Assert.AreEqual(3f, flight.Position.z, Tol);
        }

        [Test]
        public void EachStepStartsWhereTheLastOneEnded()
        {
            var flight = Straight();
            var first = flight.Step(0.05f);
            var second = flight.Step(0.05f);
            Assert.AreEqual(first.To.z, second.From.z, Tol);
            Assert.AreEqual(3f, second.To.z, Tol);
        }

        [Test]
        public void GravityDropsTheBoltAndSpeedsUpItsFall()
        {
            var flight = Straight(gravity: 10f);
            for (var i = 0; i < 10; i++) flight.Step(0.1f);
            Assert.AreEqual(-10f, flight.Velocity.y, Tol);
            // Semi-implicit Euler: sum of 10 * 0.1 * (0.1 * k) for k = 1..10 = 5.5 m.
            Assert.AreEqual(-5.5f, flight.Position.y, Tol);
            Assert.AreEqual(30f, flight.Position.z, Tol);
        }

        // ---- Drag: v *= exp(-drag x dt) each step, so the speed after a time is independent of the step size ----

        [Test]
        public void DragSlowsTheProjectileExponentially()
        {
            var flight = Straight(drag: 0.5f);
            for (var i = 0; i < 10; i++) flight.Step(0.1f);
            Assert.AreEqual(30f * (float)Math.Exp(-0.5), flight.Velocity.z, Tol);
            Assert.Less(flight.Position.z, 30f);
            Assert.Greater(flight.Position.z, 0f);
        }

        [Test]
        public void DragGivesTheSameSpeedWhateverTheFrameRate()
        {
            var coarse = Straight(drag: 0.8f);
            coarse.Step(1f);
            var fine = Straight(drag: 0.8f);
            for (var i = 0; i < 100; i++) fine.Step(0.01f);
            Assert.AreEqual(coarse.Velocity.z, fine.Velocity.z, Tol);
        }

        [Test]
        public void ZeroDragLeavesTheFlightExactlyAsBefore()
        {
            var plain = Straight(gravity: 10f, curve: 20f);
            var explicitZero = Straight(gravity: 10f, curve: 20f, drag: 0f);
            for (var i = 0; i < 10; i++) { plain.Step(0.1f); explicitZero.Step(0.1f); }
            Assert.AreEqual(plain.Position, explicitZero.Position);
            Assert.AreEqual(plain.Velocity, explicitZero.Velocity);
            Assert.AreEqual(-5.5f, plain.Position.y, Tol);
        }

        [Test]
        public void DragNeverReversesTheProjectile()
        {
            var flight = Straight(drag: 1000f);
            flight.Step(0.5f);
            Assert.GreaterOrEqual(flight.Velocity.z, 0f);
            Assert.AreEqual(0f, flight.Velocity.z, Tol);
        }

        [Test]
        public void ANegativeDragNeverSpeedsTheProjectileUp()
        {
            var flight = Straight(drag: -2f);
            flight.Step(0.5f);
            Assert.AreEqual(30f, flight.Velocity.z, Tol);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void ANonFiniteDragIsRejected(float drag) =>
            Assert.Throws<ArgumentException>(() => Straight(drag: drag));

        [Test]
        public void ADragFlightStillReportsTheSegmentItCovered()
        {
            var flight = Straight(drag: 1f);
            var (from, to) = flight.Step(0.1f);
            Assert.AreEqual(0f, from.z, Tol);
            Assert.AreEqual(flight.Position.z, to.z, Tol);
            Assert.AreEqual(30f * (float)Math.Exp(-0.1) * 0.1f, to.z, Tol);
        }

        // ---- Weight: the acceleration the adapter passes in is weight x world gravity ----

        [Test]
        public void AHeavierProjectileDropsMoreOverTheSameDistance()
        {
            var light = Straight(gravity: 0.1f * 9.81f);
            var heavy = Straight(gravity: 1f * 9.81f);
            for (var i = 0; i < 10; i++) { light.Step(0.1f); heavy.Step(0.1f); }
            Assert.AreEqual(light.Position.z, heavy.Position.z, Tol);
            Assert.Less(heavy.Position.y, light.Position.y);
            Assert.Less(light.Position.y, 0f);
        }

        [Test]
        public void AWeightlessProjectileFliesStraightEvenWithDrag()
        {
            var flight = Straight(gravity: 0f, drag: 0.3f);
            for (var i = 0; i < 20; i++) flight.Step(0.05f);
            Assert.AreEqual(0f, flight.Position.y, Tol);
            Assert.AreEqual(0f, flight.Position.x, Tol);
        }

        [Test]
        public void ZeroGravityNeverMovesVertically()
        {
            var flight = Straight();
            for (var i = 0; i < 20; i++) flight.Step(0.05f);
            Assert.AreEqual(0f, flight.Position.y, Tol);
        }

        [Test]
        public void CurveTurnsTheVelocityAroundWorldUpWithoutChangingItsSpeed()
        {
            var flight = Straight(curve: 90f);
            for (var i = 0; i < 10; i++) flight.Step(0.1f);
            // 90 deg/s for 1 s: forward (+z) ends pointing to +x, like Quaternion.AngleAxis(+90, up).
            Assert.AreEqual(30f, flight.Velocity.x, 0.05f);
            Assert.AreEqual(0f, flight.Velocity.z, 0.05f);
            Assert.AreEqual(0f, flight.Velocity.y, Tol);
            var speed = Mathf.Sqrt(flight.Velocity.x * flight.Velocity.x + flight.Velocity.z * flight.Velocity.z);
            Assert.AreEqual(30f, speed, Tol);
            Assert.Greater(flight.Position.x, 0f);
        }

        [Test]
        public void NegativeCurveTurnsTheOtherWay()
        {
            var flight = Straight(curve: -90f);
            for (var i = 0; i < 10; i++) flight.Step(0.1f);
            Assert.Less(flight.Velocity.x, -29f);
        }

        [Test]
        public void CurveLeavesTheVerticalSpeedAlone()
        {
            var flight = new ProjectileFlight(Vector3.zero, new Vector3(0f, 5f, 30f), 0f, 45f, 3f);
            for (var i = 0; i < 10; i++) flight.Step(0.1f);
            Assert.AreEqual(5f, flight.Velocity.y, Tol);
        }

        [Test]
        public void ExpiresAtTheLifetime()
        {
            var flight = Straight(lifetime: 1f);
            Assert.IsFalse(flight.IsExpired);
            for (var i = 0; i < 9; i++) flight.Step(0.1f);
            Assert.IsFalse(flight.IsExpired);
            flight.Step(0.1f);
            Assert.IsTrue(flight.IsExpired);
            Assert.AreEqual(1f, flight.Age, Tol);
        }

        [Test]
        public void ALongStepStopsAtTheLifetimeInsteadOfOvershooting()
        {
            var flight = Straight(lifetime: 1f);
            var (_, to) = flight.Step(5f);
            Assert.AreEqual(30f, to.z, Tol);
            Assert.IsTrue(flight.IsExpired);
        }

        [Test]
        public void AnExpiredFlightStaysPut()
        {
            var flight = Straight(lifetime: 0.1f);
            flight.Step(0.1f);
            var (from, to) = flight.Step(0.1f);
            Assert.AreEqual(from.z, to.z, Tol);
            Assert.AreEqual(3f, flight.Position.z, Tol);
        }

        [Test]
        public void ZeroDtIsASafeNoOp()
        {
            var flight = Straight();
            var (from, to) = flight.Step(0f);
            Assert.AreEqual(from.z, to.z, Tol);
            Assert.AreEqual(0f, flight.Age, Tol);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        [TestCase(-0.1f)]
        public void StepRejectsABadDt(float dt)
        {
            var flight = Straight();
            Assert.Throws<ArgumentOutOfRangeException>(() => flight.Step(dt));
            Assert.AreEqual(0f, flight.Position.z, Tol, "a rejected step must not move the bolt");
        }

        [Test]
        public void ConstructorRejectsNonFiniteInput()
        {
            var ok = new Vector3(0f, 0f, 30f);
            Assert.Throws<ArgumentException>(() => new ProjectileFlight(new Vector3(float.NaN, 0f, 0f), ok, 0f, 0f, 3f));
            Assert.Throws<ArgumentException>(() => new ProjectileFlight(Vector3.zero, new Vector3(0f, float.PositiveInfinity, 0f), 0f, 0f, 3f));
            Assert.Throws<ArgumentException>(() => new ProjectileFlight(Vector3.zero, ok, float.NaN, 0f, 3f));
            Assert.Throws<ArgumentException>(() => new ProjectileFlight(Vector3.zero, ok, 0f, float.PositiveInfinity, 3f));
            Assert.Throws<ArgumentException>(() => new ProjectileFlight(Vector3.zero, ok, 0f, 0f, float.NaN));
        }

        [Test]
        public void ALifetimeOfZeroStartsExpired()
        {
            var flight = Straight(lifetime: 0f);
            Assert.IsTrue(flight.IsExpired);
        }
    }
}
