using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.ContentEditor.UI
{
    [TestFixture]
    [Category("RequiresMedia")]
    public sealed class ContentEditorModOrderViewTests : ContentEditorViewTestBase
    {
        private const float _rowPitch = SortableRowView.RowHeight + SortableRowView.Gap;

        [Test]
        public void BeginDrag_ScaledList_KeepsTheBarsScreenHeight()
        {
            var (_, rows) = OpenLoadOrder();
            SortableRowView row = rows[1];
            PointerEventData pointer = CreatePointer(row);
            float height = row.Rect.rect.height * row.Rect.lossyScale.y;

            row.OnBeginDrag(pointer);
            float liftedHeight = row.Rect.rect.height * row.Rect.lossyScale.y;
            row.OnEndDrag(pointer);

            Assert.That(liftedHeight, Is.EqualTo(height).Within(0.01f));
        }

        [Test]
        public void EndDrag_DroppedAtFirstSlot_SnapsAllRowGapsImmediately()
        {
            var (_, rows) = OpenLoadOrder();
            PointerEventData pointer = CreatePointer(rows[2]);

            rows[2].OnBeginDrag(pointer);
            pointer.position = CenterOf(rows[0]) + Vector2.up * 8f;
            rows[2].OnEndDrag(pointer);

            Assert.That(
                new[] { rows[2], rows[0], rows[1] }.Select(row => row.Rect.anchoredPosition.y),
                Is.EqualTo(new[] { 0f, -_rowPitch, -2f * _rowPitch })
            );
        }

        [Test]
        public void EndDrag_DroppedOutsideViewport_RestoresStartingSlots()
        {
            var (dialog, rows) = OpenLoadOrder();
            PointerEventData pointer = CreatePointer(rows[2]);
            rows[2].OnBeginDrag(pointer);
            pointer.position = CenterOf(rows[0]) + Vector2.up * 8f;
            rows[2].OnDrag(pointer);

            RectTransform viewport = dialog.GetComponentInChildren<ScrollAreaView>().ViewportRoot;
            pointer.position = RectTransformUtility.WorldToScreenPoint(
                null,
                viewport.TransformPoint(
                    new Vector2(viewport.rect.xMax + 4f, viewport.rect.center.y)
                )
            );
            rows[2].OnEndDrag(pointer);

            Assert.That(
                rows.Select(row => row.Rect.anchoredPosition.y),
                Is.EqualTo(new[] { 0f, -_rowPitch, -2f * _rowPitch })
            );
        }

        [Test]
        public void ShowMoveMenu_MoveLater_StagesTheSelectedCardAtTheNextSlot()
        {
            var (dialog, rows) = OpenLoadOrder();
            rows[0].onClick.Invoke();
            dialog
                .GetComponentsInChildren<Button>()
                .Single(button => button.name == "Move later")
                .onClick.Invoke();

            Assert.That(rows[0].Rect.anchoredPosition.y, Is.EqualTo(-_rowPitch));
        }

        private (ContentEditorModOrderView Dialog, SortableRowView[] Rows) OpenLoadOrder()
        {
            view.ModManager.Show();
            var dialog = view.ModManager.GetComponentInChildren<ContentEditorModOrderView>(true);
            var era = new ContentPackDefinition { ID = "era", Version = "1.0.0" };
            dialog.Show(
                "Test era",
                new[] { "first", "second", "third" }
                    .Select(id => ContentModDefinition.Create(id, "1.0.0", id, era))
                    .ToArray(),
                _ => { }
            );
            Canvas.ForceUpdateCanvases();
            SortableRowView[] rows = dialog
                .GetComponentsInChildren<SortableRowView>()
                .OrderByDescending(row => row.Rect.anchoredPosition.y)
                .ToArray();
            return (dialog, rows);
        }

        private static PointerEventData CreatePointer(SortableRowView row)
        {
            Vector2 start = CenterOf(row);
            return new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = start,
                pressPosition = start,
            };
        }

        private static Vector2 CenterOf(SortableRowView row) =>
            RectTransformUtility.WorldToScreenPoint(
                null,
                row.Rect.TransformPoint(row.Rect.rect.center)
            );
    }
}
