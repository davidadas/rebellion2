using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.Components
{
    [TestFixture]
    public sealed class MenuBarHeadingViewTests
    {
        [Test]
        public void OnPointerEnter_HeadingHovered_ReportsItsButtonWithoutWorkspaceDependencies()
        {
            GameObject root = new GameObject("Heading", typeof(RectTransform), typeof(Button));
            try
            {
                MenuBarHeadingView view = root.AddComponent<MenuBarHeadingView>();
                Button reported = null;
                view.Hovered += button => reported = button;
                view.OnPointerEnter(new PointerEventData(null));
                Assert.AreSame(root.GetComponent<Button>(), reported);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
