using System.Collections.Generic;
using UnityEngine;

public class TurnLogger : MonoBehaviour
{
    public static TurnLogger Instance { get; private set; }

    private List<string> logEntries = new();

    public System.Action<string> OnLogAdded;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject); // Avoid duplicates
        }
    }

    public void Log(string message, int turnNumber)
    {
        string timestamp = $"[Turn {turnNumber}] {message}";
        logEntries.Add(timestamp);
        Debug.Log(timestamp);
        OnLogAdded?.Invoke(timestamp);
    }

    public List<string> GetAllLogs() => new List<string>(logEntries);

    public void Clear()
    {
        logEntries.Clear();
    }
}