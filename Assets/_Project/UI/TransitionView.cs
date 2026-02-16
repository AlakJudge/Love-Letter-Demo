using System;
using UnityEngine;
using TMPro;

public class TransitionView : MonoBehaviour
{
    public Transform transitionPanel;
    public TextMeshProUGUI transitionText;

    public int transitionTime = 5; // seconds

    public event Action OnTransitionFinished;

    private void Start()
    {
        if (transitionPanel != null)
            transitionPanel.gameObject.SetActive(false);
    }

    public void ShowTransition(string text, string roundOrGameOver = null)
    {
        if (transitionPanel == null || transitionText == null) return;
        transitionText.text = text;
        transitionPanel.gameObject.SetActive(true);

        StopAllCoroutines();

        if (roundOrGameOver == "GameOver") // For game over, don't auto-hide the panel
            return;
        else
            StartCoroutine(HideAfterDelay());
    }

    public void HideTransition()
    {
        if (transitionPanel != null)
            transitionPanel.gameObject.SetActive(false);
        
        OnTransitionFinished.Invoke();
    }

    private System.Collections.IEnumerator HideAfterDelay()
    {
        yield return new WaitForSecondsRealtime(transitionTime);
        HideTransition();
    }
}
