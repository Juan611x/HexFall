using System.Linq;
using HexFall.Player;
using HexFall.Player.Locomotion;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HexFall.UI.Tests
{
    public class PlayerLookOrderTests
    {
        private static int OrderOf(System.Func<MonoScript, bool> match)
        {
            var script = MonoImporter.GetAllRuntimeMonoScripts().FirstOrDefault(match);
            Assert.IsNotNull(script, "Script not found");
            // The attribute is the runtime source of truth; the importer order covers scripts without one.
            var type = script.GetClass();
            var attribute = type == null ? null : (DefaultExecutionOrder)System.Attribute.GetCustomAttribute(type, typeof(DefaultExecutionOrder));
            return attribute != null ? attribute.order : MonoImporter.GetExecutionOrder(script);
        }

        private static int OrderOf(System.Type type) => OrderOf(s => s.GetClass() == type);

        [Test]
        public void LookRunsAfterCombat()
        {
            Assert.Greater(OrderOf(typeof(PlayerLook)), OrderOf(typeof(PlayerCombat)));
        }

        [Test]
        public void LookRunsAfterTheMotor()
        {
            Assert.Greater(OrderOf(typeof(PlayerLook)), OrderOf(typeof(PlayerMotor)));
        }

        [Test]
        public void LookRunsBeforeTheCinemachineBrain()
        {
            var brain = OrderOf(s => s.name == "CinemachineBrain");

            Assert.Less(OrderOf(typeof(PlayerLook)), brain);
        }
    }
}
