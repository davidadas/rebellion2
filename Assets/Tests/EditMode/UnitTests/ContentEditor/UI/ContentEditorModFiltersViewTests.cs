using System;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.ContentEditor.UI
{
    [TestFixture]
    [Category("RequiresMedia")]
    public sealed class ContentEditorModFiltersViewTests : ContentEditorViewTestBase
    {
        /// <summary>
        /// Opens the browse filters using the same controls as a user.
        /// </summary>
        [SetUp]
        public void ShowFilters()
        {
            view.SetWorkspaceNavigation(ContentEditorPage.Browse);
            view.ModManager.Show();
            view.ModManager.RenderRows(Array.Empty<ContentEditorModEntry>(), false, 0, true);
            view.ModManager.Filters.SetOptions(null, new[] { "Blue", "Red" });
            view.ModManager.GetComponentsInChildren<Button>()
                .Single(button => button.name == "filterButton")
                .onClick.Invoke();
        }

        [Test]
        public void SetInteractable_ClosedCategoryPickers_PreservesExistingKeyboardFocus()
        {
            var events = new GameObject("Dropdown focus", typeof(EditModeEventSystem));
            try
            {
                var system = events.GetComponent<UnityEngine.EventSystems.EventSystem>();
                UnityEngine.EventSystems.EventSystem.current = system;
                Button refresh = view
                    .ModManager.GetComponentsInChildren<Button>()
                    .Single(button => button.name == "refreshButton");
                system.SetSelectedGameObject(refresh.gameObject);
                view.ModManager.Filters.SetInteractable(false);
                Assert.That(system.currentSelectedGameObject, Is.EqualTo(refresh.gameObject));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(events);
            }
        }

        [Test]
        public void OnSelect_CategoryBelowViewport_RevealsFocusedCheckbox()
        {
            view.ModManager.Filters.SetOptions(
                null,
                Enumerable.Range(0, 20).Select(index => "Category " + index.ToString("D2"))
            );
            MultiSelectDropdown picker = view
                .ModManager.Filters.GetComponentsInChildren<MultiSelectDropdown>()
                .Single(item => item.name == "IncludeCategories");
            picker.GetComponent<Button>().onClick.Invoke();
            UICheckboxView row = picker
                .GetComponentsInChildren<UICheckboxView>()
                .Single(item =>
                    item.GetComponentsInChildren<TMP_Text>()
                        .Any(label => label.text == "Category 19")
                );
            row.OnSelect(null);
            ScrollAreaView scroll = picker.GetComponentInChildren<ScrollAreaView>();
            float top = -((RectTransform)row.transform).anchoredPosition.y;
            Assert.That(
                top + ((RectTransform)row.transform).rect.height,
                Is.LessThanOrEqualTo(
                    scroll.ContentRoot.anchoredPosition.y + scroll.ViewportHeight + 1
                )
            );
        }

        [TestCase(1120)]
        [TestCase(640)]
        [TestCase(434)]
        public void SetWidth_CompactAndWideLayouts_KeepControlsInsideWithoutOverlapping(int width)
        {
            var filters = view.ModManager.Filters;
            var panel = (RectTransform)filters.transform;
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            filters.SetWidth(width);
            Canvas.ForceUpdateCanvases();
            Rect[] bounds = filters
                .GetComponentsInChildren<Selectable>()
                .Where(control => control.gameObject.activeInHierarchy)
                .Select(control => BoundsIn(panel, (RectTransform)control.transform))
                .ToArray();

            foreach (Rect rect in bounds)
            {
                Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(panel.rect.xMin));
                Assert.That(rect.xMax, Is.LessThanOrEqualTo(panel.rect.xMax));
                Assert.That(rect.yMin, Is.GreaterThanOrEqualTo(panel.rect.yMin));
                Assert.That(rect.yMax, Is.LessThanOrEqualTo(panel.rect.yMax));
            }
            for (int first = 0; first < bounds.Length; first++)
            for (int second = first + 1; second < bounds.Length; second++)
                Assert.That(
                    bounds[first].Overlaps(bounds[second]),
                    Is.False,
                    $"Controls {first} and {second} overlap at width {width}."
                );
        }

        [TestCase(0.08f, 1f)]
        [TestCase(0.5f, 1f)]
        [TestCase(0.92f, 1f)]
        [TestCase(0.08f, 1.75f)]
        [TestCase(0.5f, 1.75f)]
        [TestCase(0.92f, 1.75f)]
        public void TogglePopup_ShortCanvasAtDifferentScales_KeepsListAndFooterOnscreen(
            float position,
            float scale
        )
        {
            RectTransform canvas = (RectTransform)view.transform;
            canvas.sizeDelta = new Vector2(1000, 400);
            view.ModManager.Filters.SetOptions(
                null,
                Enumerable.Range(0, 40).Select(index => "Category " + index.ToString("D2"))
            );
            MultiSelectDropdown picker = view
                .ModManager.Filters.GetComponentsInChildren<MultiSelectDropdown>()
                .Single(item => item.name == "IncludeCategories");
            Canvas.ForceUpdateCanvases();
            RectTransform field = (RectTransform)picker.transform;
            field.localScale = Vector3.one * scale;
            field.position = canvas.TransformPoint(
                new Vector3(0, Mathf.Lerp(canvas.rect.yMin, canvas.rect.yMax, position), 0)
            );

            picker.GetComponent<Button>().onClick.Invoke();
            Canvas.ForceUpdateCanvases();

            RectTransform popup = (RectTransform)
                picker.GetComponentsInChildren<Canvas>().Single().transform;
            Rect bounds = BoundsIn(canvas, popup);
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(canvas.rect.xMin));
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(canvas.rect.xMax));
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(canvas.rect.yMin));
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(canvas.rect.yMax));
            ScrollAreaView scroll = picker.GetComponentInChildren<ScrollAreaView>();
            Assert.That(scroll.ViewportHeight, Is.GreaterThanOrEqualTo(32));
            Button clear = picker
                .GetComponentsInChildren<Button>()
                .Single(button => button.name == "ClearSelection");
            Rect footer = BoundsIn(popup, (RectTransform)clear.transform);
            Assert.That(footer.yMin, Is.GreaterThanOrEqualTo(popup.rect.yMin));
            Assert.That(footer.yMax, Is.LessThanOrEqualTo(popup.rect.yMax));
        }

        [Test]
        public void TogglePopup_CanvasShrinksWhileOpen_RefitsList()
        {
            RectTransform canvas = (RectTransform)view.transform;
            view.ModManager.Filters.SetOptions(
                null,
                Enumerable.Range(0, 40).Select(index => "Category " + index.ToString("D2"))
            );
            MultiSelectDropdown picker = view
                .ModManager.Filters.GetComponentsInChildren<MultiSelectDropdown>()
                .Single(item => item.name == "IncludeCategories");
            picker.GetComponent<Button>().onClick.Invoke();

            canvas.sizeDelta = new Vector2(1000, 400);
            Canvas.ForceUpdateCanvases();

            RectTransform popup = (RectTransform)
                picker.GetComponentsInChildren<Canvas>().Single().transform;
            Rect bounds = BoundsIn(canvas, popup);
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(canvas.rect.yMin));
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(canvas.rect.yMax));
        }

        [Test]
        public void Read_InstallationOptionMovesToFirstPosition_KeepsItsMeaningAndActiveCount()
        {
            TMP_Dropdown dropdown = view
                .ModManager.Filters.GetComponentsInChildren<TMP_Dropdown>()
                .Single(item => item.name == "installation");
            var installed = dropdown.options.Single(option => option.text == "Installed");
            dropdown.options.Remove(installed);
            dropdown.options.Insert(0, installed);
            dropdown.value = 0;

            Assert.That(
                view.ModManager.Filters.Read().Installation,
                Is.EqualTo(ContentEditorModFilter.Installed)
            );
            Assert.That(view.ModManager.Filters.ActiveCount, Is.EqualTo(1));
            ThunderstorePackage[] catalog = { Package("Installed"), Package("Available") };
            var result = ContentEditorModCatalog.Find(
                catalog,
                new[] { new InstalledModPackage { PackageID = "Team-Installed" } },
                false,
                "",
                view.ModManager.Filters.Read(),
                ContentEditorModSort.Name
            );
            CollectionAssert.AreEqual(new[] { "Team-Installed" }, result.Select(item => item.ID));
        }

        [Test]
        public void Read_CategoryChanges_ReconcilesControlsAndRetainsSelectionAfterMetadataRefresh()
        {
            MultiSelectDropdown include = view
                .ModManager.Filters.GetComponentsInChildren<MultiSelectDropdown>()
                .Single(item => item.name == "IncludeCategories");
            MultiSelectDropdown exclude = view
                .ModManager.Filters.GetComponentsInChildren<MultiSelectDropdown>()
                .Single(item => item.name == "ExcludeCategories");
            int notifications = 0;
            view.ModManager.Filters.Changed += () => notifications++;
            Select(include, "Blue");
            Select(exclude, "Blue");
            view.ModManager.Filters.SetOptions(null, new[] { "Blue", "Red", "Green" });

            CollectionAssert.IsEmpty(include.SelectedValues);
            CollectionAssert.AreEquivalent(new[] { "Blue" }, exclude.SelectedValues);
            CollectionAssert.IsEmpty(view.ModManager.Filters.Read().IncludedCategories);
            CollectionAssert.AreEqual(
                new[] { "Blue" },
                view.ModManager.Filters.Read().ExcludedCategories
            );
            Assert.That(notifications, Is.EqualTo(2));
            view.ModManager.Filters.Clear();
            Assert.That(view.ModManager.Filters.ActiveCount, Is.Zero);
            Assert.That(notifications, Is.EqualTo(3));
        }

        [Test]
        public void Sort_NameOptionMovesToFirstPosition_KeepsItsMeaning()
        {
            TMP_Dropdown dropdown = view
                .ModManager.GetComponentsInChildren<TMP_Dropdown>()
                .Single(item => item.name == "sorting");
            var name = dropdown.options.Single(option => option.text == "Name (A–Z)");
            dropdown.options.Remove(name);
            dropdown.options.Insert(0, name);
            dropdown.value = 0;

            Assert.That(view.ModManager.Sort, Is.EqualTo(ContentEditorModSort.Name));
        }

        /// <summary>
        /// Measures a control in its panel's coordinates.
        /// </summary>
        /// <param name="panel">The containing panel.</param>
        /// <param name="control">The measured control.</param>
        /// <returns>The control bounds.</returns>
        private static Rect BoundsIn(RectTransform panel, RectTransform control)
        {
            var corners = new Vector3[4];
            control.GetWorldCorners(corners);
            Vector3 lower = panel.InverseTransformPoint(corners[0]);
            Vector3 upper = panel.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(lower.x, lower.y, upper.x, upper.y);
        }

        /// <summary>
        /// Checks a named option through the native toggle while retaining the expanded dropdown.
        /// </summary>
        /// <param name="picker">The category picker.</param>
        /// <param name="caption">The category label.</param>
        private static void Select(MultiSelectDropdown picker, string caption)
        {
            picker.GetComponent<Button>().onClick.Invoke();
            picker
                .GetComponentsInChildren<Toggle>()
                .Single(toggle =>
                    toggle.GetComponentsInChildren<TMP_Text>().Any(label => label.text == caption)
                )
                .isOn = true;
            picker.TryCancel();
        }

        /// <summary>
        /// Creates one catalog entry with a distinct installation identity.
        /// </summary>
        /// <param name="name">The package name.</param>
        /// <returns>The package listing.</returns>
        private static ThunderstorePackage Package(string name) =>
            new()
            {
                name = name,
                owner = "Team",
                full_name = "Team-" + name,
                categories = Array.Empty<string>(),
                versions = new[]
                {
                    new ThunderstorePackageVersion { version_number = "1.0.0", description = name },
                },
            };
    }
}
