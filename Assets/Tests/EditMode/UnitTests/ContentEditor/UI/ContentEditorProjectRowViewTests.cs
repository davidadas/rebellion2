using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine.UI;

namespace Rebellion.Tests.ContentEditor.UI
{
    [TestFixture]
    [Category("RequiresMedia")]
    public sealed class ContentEditorProjectRowViewTests : ContentEditorViewTestBase
    {
        [Test]
        public void Render_SecondaryActionIsRemovedAndRestored_RefreshesLabelsAndCallbacks()
        {
            view.ShowProjects();
            ContentEditorProjectRowView row =
                view.GetComponentsInChildren<ContentEditorProjectRowView>().First();
            int primaryCalls = 0;
            int secondaryCalls = 0;
            row.Render(
                "First",
                "1.0.0",
                "First folder",
                "Open",
                () => primaryCalls++,
                "Publish",
                () => secondaryCalls++
            );
            Button primary = row.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "OpenProject");
            Button secondary = row.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "ProjectAction");
            primary.onClick.Invoke();
            secondary.onClick.Invoke();

            row.Render("Second", "2.0.0", "Second folder", "View page", () => primaryCalls += 10);
            Assert.That(secondary.gameObject.activeSelf, Is.False);
            primary.onClick.Invoke();
            row.Render(
                "Third",
                "3.0.0",
                "Third folder",
                "Edit",
                () => primaryCalls += 100,
                "Export",
                () => secondaryCalls += 100
            );

            Assert.That(secondary.gameObject.activeSelf, Is.True);
            Assert.That(primary.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Edit"));
            Assert.That(secondary.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Export"));
            Assert.That(primary.interactable, Is.True);
            Assert.That(secondary.interactable, Is.True);
            primary.onClick.Invoke();
            secondary.onClick.Invoke();
            Assert.That(primaryCalls, Is.EqualTo(111));
            Assert.That(secondaryCalls, Is.EqualTo(101));
        }
    }
}
