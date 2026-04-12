using System.Collections.Generic;
using UnityEngine;

public class TurnLogger : MonoBehaviour
{
    public static TurnLogger Instance { get; private set; }

    private List<string> logEntries = new();

    public System.Action<string> OnLogAdded;

    private int lastTurnNumber = 0;

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
        // If we moved to a new turn, insert a separator line first
        if (turnNumber != lastTurnNumber)
        {
            string separator = $"\n----- Turn {turnNumber} -----\n";
            logEntries.Add(separator);
            OnLogAdded?.Invoke(separator);
            
            lastTurnNumber = turnNumber;
        }
        string logEntry = $"{message}";
        logEntries.Add(logEntry);
        Debug.Log(logEntry);
        OnLogAdded?.Invoke(logEntry);
    }

    public List<string> GetAllLogs() => new List<string>(logEntries);

    public void Clear()
    {
        logEntries.Clear();
        lastTurnNumber = 0;
    }
}