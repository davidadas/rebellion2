using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class TabGroup : MonoBehaviour
{
    public enum TabBehavior
    {
        TogglePanel,
        NotifyOnly,
    }

    [Serializable]
    private sealed class Tab
    {
        [SerializeField]
        public IconButton Button;

        [SerializeField]
        public GameObject Panel;

        [SerializeField]
        public TabBehavior Behavior = TabBehavior.TogglePanel;
    }

    [SerializeField]
    private List<Tab> tabs = new();

    [SerializeField]
    private IconButton defaultTab;

    private int activeIndex = -1;

    public event Action<int> TabSelected;

    private void Awake()
    {
        if (tabs == null || tabs.Count == 0)
            throw new InvalidOperationException("TabGroup has no tabs configured.");

        for (int i = 0; i < tabs.Count; i++)
        {
            int index = i;
            Tab tab = tabs[i];

            if (tab.Button == null)
                throw new InvalidOperationException($"Tab {i} missing IconButton.");

            Button unityButton = tab.Button.GetComponent<Button>();

            if (unityButton == null)
                throw new InvalidOperationException("IconButton requires Unity Button.");

            unityButton.onClick.AddListener(() => Select(index));
        }
    }

    private void Start()
    {
        if (defaultTab == null)
            return;

        int index = FindTabIndex(defaultTab);

        if (index < 0)
            return;

        Button unityButton = defaultTab.GetComponent<Button>();

        if (unityButton != null && unityButton.interactable)
            Select(index);
    }

    public void Select(int index)
    {
        if (index < 0 || index >= tabs.Count)
            return;

        if (activeIndex == index)
            return;

        Tab targetTab = tabs[index];

        Button unityButton = targetTab.Button.GetComponent<Button>();

        if (unityButton == null || !unityButton.interactable)
            return;

        int previousIndex = activeIndex;
        activeIndex = index;

        GameObject previousPanel = null;
        GameObject nextPanel = null;

        if (previousIndex >= 0)
        {
            Tab prevTab = tabs[previousIndex];

            if (prevTab.Behavior == TabBehavior.TogglePanel)
                previousPanel = prevTab.Panel;
        }

        if (targetTab.Behavior == TabBehavior.TogglePanel)
            nextPanel = targetTab.Panel;

        if (previousPanel != null && previousPanel != nextPanel)
            previousPanel.SetActive(false);

        if (nextPanel != null)
            nextPanel.SetActive(true);

        for (int i = 0; i < tabs.Count; i++)
        {
            bool isActive = i == activeIndex;

            Tab tab = tabs[i];

            if (tab.Button != null)
                tab.Button.SetSelected(isActive);
        }

        TabSelected?.Invoke(activeIndex);
    }

    public int GetActiveIndex()
    {
        return activeIndex;
    }

    private int FindTabIndex(IconButton button)
    {
        for (int i = 0; i < tabs.Count; i++)
        {
            if (tabs[i].Button == button)
                return i;
        }

        return -1;
    }
}
