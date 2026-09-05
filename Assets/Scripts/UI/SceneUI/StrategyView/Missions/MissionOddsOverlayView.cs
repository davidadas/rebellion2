using TMPro;
using UnityEngine;

/// <summary>
/// Renders compact mission odds using an authored prefab hierarchy.
/// </summary>
public sealed class MissionOddsOverlayView : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI foilTextField;

    [SerializeField]
    private TextMeshProUGUI overallSuccessTextField;

    /// <summary>
    /// Applies the supplied odds, or hides the overlay when no estimate is available.
    /// </summary>
    /// <param name="odds">The mission odds to display.</param>
    public void Render(MissionOddsRenderData odds)
    {
        if (odds == null)
        {
            gameObject.SetActive(false);
            return;
        }

        VerifyReferences();
        foilTextField.text = odds.FoilLabel;
        overallSuccessTextField.text = odds.OverallSuccessLabel;
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Verifies the authored text references.
    /// </summary>
    public void VerifyReferences()
    {
        if (foilTextField == null)
            throw new MissingReferenceException($"{name}/FoilTextField is missing.");
        if (overallSuccessTextField == null)
            throw new MissingReferenceException($"{name}/OverallSuccessTextField is missing.");
    }
}
