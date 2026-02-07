using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TurnLogView : MonoBehaviour
{
    [Header("UI")]
    public ScrollRect scrollRect;
    public TextMeshProUGUI logText;
    public Button clearButton;

    private void Start()
    {
        if (TurnLogger.Instance != null)
        {
            TurnLogger.Instance.OnLogAdded += AddLogEntry;
        }

        if (clearButton != null)
        {
            clearButton.onClick.AddListener(() =>
            {
                TurnLogger.Instance?.Clear();
                logText.text = "";
            });
        }
    }

    private void AddLogEntry(string entry)
    {
        logText.text += entry + "\n";
        // Force layout rebuild and scroll to bottom
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f; // scroll to bottom
    }

    private void OnDestroy()
    {
        if (TurnLogger.Instance != null)
        {
            TurnLogger.Instance.OnLogAdded -= AddLogEntry;
        }
    }
}