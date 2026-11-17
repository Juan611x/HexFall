using System;
using NUnit.Framework;

namespace HexFall.UI.Tests
{
    public class SpellPickerTests
    {
        [Test]
        public void SingleClipAlwaysReturnsZero()
        {
            var picker = new SpellPicker(max => 0);
            Assert.AreEqual(0, picker.Next(1));
            Assert.AreEqual(0, picker.Next(1));
        }

        [Test]
        public void NoClipsReturnsMinusOne()
        {
            Assert.AreEqual(-1, new SpellPicker(max => 0).Next(0));
        }

        [Test]
        public void FirstPickUsesRandomDirectly()
        {
            var picker = new SpellPicker(max => max - 1);
            Assert.AreEqual(2, picker.Next(3));
        }

        [Test]
        public void NeverRepeatsThePreviousClipWithAnyRandomValue()
        {
            for (var forced = 0; forced < 3; forced++)
            {
                var value = forced;
                var picker = new SpellPicker(max => Math.Min(value, max - 1));
                var previous = picker.Next(3);
                for (var i = 0; i < 20; i++)
                {
                    var next = picker.Next(3);
                    Assert.AreNotEqual(previous, next);
                    previous = next;
                }
            }
        }

        [Test]
        public void SeededRandomNeverRepeatsAndCoversAllClips()
        {
            var rng = new Random(1234);
            var picker = new SpellPicker(max => rng.Next(max));
            var seen = new bool[3];
            var previous = -1;
            for (var i = 0; i < 200; i++)
            {
                var next = picker.Next(3);
                Assert.AreNotEqual(previous, next);
                Assert.That(next, Is.InRange(0, 2));
                seen[next] = true;
                previous = next;
            }
            CollectionAssert.AreEqual(new[] { true, true, true }, seen);
        }

        [Test]
        public void AsksRandomForOneLessThanCountAfterTheFirstPick()
        {
            var asked = -1;
            var picker = new SpellPicker(max => { asked = max; return 0; });
            picker.Next(3);
            Assert.AreEqual(3, asked);
            picker.Next(3);
            Assert.AreEqual(2, asked);
        }

        [Test]
        public void ShrinkingClipCountStaysInRange()
        {
            var picker = new SpellPicker(max => max - 1);
            Assert.AreEqual(2, picker.Next(3));
            Assert.That(picker.Next(2), Is.InRange(0, 1));
        }
    }
}
