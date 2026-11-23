using NUnit.Framework;
using Rebellion.Tactical;
using Rebellion.Tactical.Rendering;
using UnityEngine;
using Vector4N = global::System.Numerics.Vector4;

namespace Rebellion.Tests.Tactical.Rendering
{
    [TestFixture]
    public sealed class TacticalCombatRendererRegistrationTests
    {
        [Test]
        public void Refresh_UnknownRegistration_DoesNotCreatePresentation()
        {
            GameObject root = new("Empty renderer", typeof(TacticalCombatRenderer));
            try
            {
                ITacticalCombatArrivalPresentation presentation =
                    root.GetComponent<TacticalCombatRenderer>();
                presentation.ResetArrival(42);
                presentation.SetTransform(42, Vector4N.Zero, Vector4N.UnitZ);
                presentation.Refresh(42);

                Assert.AreEqual(0, root.transform.childCount);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
