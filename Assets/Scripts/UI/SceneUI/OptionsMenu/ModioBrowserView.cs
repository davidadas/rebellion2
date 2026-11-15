using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays the community catalog inside the existing Mods options page.
/// </summary>
public sealed class ModioBrowserView : MonoBehaviour
{
    [SerializeField]
    private TMP_InputField _searchField;

    [SerializeField]
    private Button _searchButton;

    [SerializeField]
    private Button _backButton;

    [SerializeField]
    private Button _previousButton;

    [SerializeField]
    private Button _nextButton;

    [SerializeField]
    private ScrollAreaView _scrollArea;

    [SerializeField]
    private ModioCatalogRowView _rowTemplate;

    [SerializeField]
    private TextMeshProUGUI _statusText;

    private readonly List<ModioCatalogRowView> _rows = new List<ModioCatalogRowView>();
    private bool _bound;
    private string _previousSearch;

    public event Action Closed;
    public event Action<string> SearchRequested;
    public event Action<int> PageRequested;
    public event Action<int> InstallRequested;
    public event Action<bool> TextEditingChanged;

    /// <summary>
    /// Renders a catalog page while retaining the user's in-progress search text.
    /// </summary>
    /// <param name="data">The current catalog presentation, or null when unavailable.</param>
    public void Render(ModioBrowserRenderData data)
    {
        bool visible = data?.Visible == true;
        gameObject.SetActive(visible);
        if (!visible)
            return;
        BindControls();
        bool reset = _previousSearch != data.Search;
        _previousSearch = data.Search;
        if (!_searchField.isFocused)
            _searchField.SetTextWithoutNotify(data.Search);
        _searchButton.interactable = data.CanSearch;
        _searchField.interactable = !data.Busy;
        _previousButton.interactable = data.CanGoBack;
        _nextButton.interactable = data.CanGoForward;
        UILayout.SetTextContent(_statusText, data.Status);
        for (int index = 0; index < data.Rows.Count; index++)
        {
            while (_rows.Count <= index)
            {
                ModioCatalogRowView row = Instantiate(_rowTemplate, _scrollArea.ContentRoot);
                row.name = "CommunityModRow" + _rows.Count;
                row.InstallRequested += HandleInstall;
                _rows.Add(row);
            }
            ModioCatalogRowView view = _rows[index];
            UILayout.SetSourcePosition((RectTransform)view.transform, 0, index * 44);
            view.gameObject.SetActive(true);
            view.Render(index, data.Rows[index], data.Busy);
        }
        for (int index = data.Rows.Count; index < _rows.Count; index++)
            _rows[index].gameObject.SetActive(false);
        _scrollArea.SetContentHeight(data.Rows.Count * 44, 44, reset);
    }

    /// <summary>
    /// Connects authored controls to the owning menu's browser session.
    /// </summary>
    private void BindControls()
    {
        if (_bound)
            return;
        _bound = true;
        _backButton.onClick.AddListener(() => Closed?.Invoke());
        _searchButton.onClick.AddListener(() => SearchRequested?.Invoke(_searchField.text));
        _searchField.onSubmit.AddListener(value => SearchRequested?.Invoke(value));
        _searchField.onSelect.AddListener(_ => TextEditingChanged?.Invoke(true));
        _searchField.onDeselect.AddListener(_ => TextEditingChanged?.Invoke(false));
        _previousButton.onClick.AddListener(() => PageRequested?.Invoke(-1));
        _nextButton.onClick.AddListener(() => PageRequested?.Invoke(1));
    }

    /// <summary>
    /// Forwards a published entry's installation action.
    /// </summary>
    /// <param name="index">The selected catalog row.</param>
    private void HandleInstall(int index) => InstallRequested?.Invoke(index);

    /// <summary>
    /// Releases text-editing ownership when its panel is hidden.
    /// </summary>
    private void OnDisable() => TextEditingChanged?.Invoke(false);
}
