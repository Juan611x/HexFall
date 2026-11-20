using System.IO;
using System.Linq;
using HexFall.Player.Locomotion;
using NUnit.Framework;
using UnityEngine;

namespace HexFall.UI.Tests
{
    // LookAngles already turns a positive vertical delta into "look up", so the Look bindings must hand
    // it the raw device delta. An InvertVector2 processor left over from the Starter Assets controller
    // (which added look.y straight to the pitch) inverts the camera a second time.
    public class LookBindingsTests
    {
        private const string ControlsPath = "_Project/Input/HexFallControls.inputactions";

        [Test]
        public void LookBindingsDoNotInvertTheDelta()
        {
            var lookBindings = File.ReadAllLines(Path.Combine(Application.dataPath, ControlsPath))
                .Where(line => line.Contains("\"action\": \"Look\""))
                .ToList();

            Assert.IsNotEmpty(lookBindings, "No Look bindings found in the controls asset.");
            foreach (var binding in lookBindings)
                StringAssert.DoesNotContain("InvertVector2", binding);
        }

        [Test]
        public void MouseMovedUpLooksUp()
        {
            var mouseUp = new Vector2(0f, 4f);

            var (_, pitch) = LookAngles.Apply(0f, 0f, mouseUp, 1f, -30f, 70f);

            Assert.Less(pitch, 0f, "A negative pitch tilts the camera target up.");
        }
    }
}
