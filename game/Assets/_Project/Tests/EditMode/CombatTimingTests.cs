using HexFall.Player;
using NUnit.Framework;

namespace HexFall.UI.Tests
{
    public class AttackCooldownTests
    {
        [Test]
        public void FirstAttackIsAllowed()
        {
            Assert.IsTrue(new AttackCooldown(0.5f).TryAttack(3f));
        }

        [Test]
        public void AttackDuringTheCooldownIsRefused()
        {
            var attack = new AttackCooldown(0.5f);
            attack.TryAttack(3f);

            Assert.IsFalse(attack.TryAttack(3.4f));
        }

        [Test]
        public void AttackAfterTheCooldownIsAllowed()
        {
            var attack = new AttackCooldown(0.5f);
            attack.TryAttack(3f);

            Assert.IsTrue(attack.TryAttack(3.5f));
        }

        [Test]
        public void ARefusedAttackDoesNotRestartTheCooldown()
        {
            var attack = new AttackCooldown(0.5f);
            attack.TryAttack(3f);
            attack.TryAttack(3.4f);

            Assert.IsTrue(attack.TryAttack(3.5f));
        }
    }
}
