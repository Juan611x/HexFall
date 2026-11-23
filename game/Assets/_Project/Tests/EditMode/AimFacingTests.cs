using HexFall.Player;
using NUnit.Framework;
using UnityEngine;

namespace HexFall.UI.Tests
{
    public class AimFacingTests
    {
        [Test]
        public void TargetStraightAheadFacesZero()
        {
            Assert.AreEqual(0f, AimFacing.YawToward(Vector3.zero, new Vector3(0f, 0f, 10f), 45f), 0.001f);
        }

        [Test]
        public void TargetToTheRightFacesNinety()
        {
            Assert.AreEqual(90f, AimFacing.YawToward(Vector3.zero, new Vector3(10f, 0f, 0f), 0f), 0.001f);
        }

        [Test]
        public void TargetToTheLeftFacesMinusNinety()
        {
            Assert.AreEqual(-90f, AimFacing.YawToward(new Vector3(5f, 0f, 5f), new Vector3(-5f, 0f, 5f), 0f), 0.001f);
        }

        [Test]
        public void HeightDifferenceIsIgnored()
        {
            Assert.AreEqual(0f, AimFacing.YawToward(Vector3.zero, new Vector3(0f, 50f, 1f), 30f), 0.001f);
        }

        [Test]
        public void TargetDirectlyAboveKeepsTheCurrentYaw()
        {
            Assert.AreEqual(30f, AimFacing.YawToward(Vector3.zero, new Vector3(0f, 5f, 0f), 30f), 0.001f);
        }

        [Test]
        public void TurnStepsTowardTheTargetByAtMostTheGivenAngle()
        {
            Assert.AreEqual(30f, AimFacing.Turn(0f, 90f, 30f), 0.001f);
        }

        [Test]
        public void TurnStopsOnTheTargetWhenCloseEnough()
        {
            Assert.AreEqual(90f, AimFacing.Turn(80f, 90f, 30f), 0.001f);
        }

        [Test]
        public void TurnTakesTheShortWayAround()
        {
            // From 350° toward 10° the short way is through 360°, so a 5° step lands on 355°, not 345°.
            Assert.AreEqual(0f, Mathf.DeltaAngle(355f, AimFacing.Turn(350f, 10f, 5f)), 0.001f);
        }
    }
}
