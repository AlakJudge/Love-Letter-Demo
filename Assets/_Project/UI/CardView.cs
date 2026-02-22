using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class CardView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public Image image;
    public Button button;
    public CardData boundCard;
    public bool isRevealed = false;
    
    public Action onClick;
    public Action onLongPress;
    public Action onLongPressRelease;

    [SerializeField] private float longPressTime = 0.4f;

    private bool isPointerDown;
    private bool longPressTriggered;
    private float pointerDownTime;

    public void Set(CardData card)
    {
        boundCard = card;
        image.sprite = card.cardFront;
        isRevealed = true;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(HandleClick);
    }
    public void ShowBack(CardData card)
    {
        boundCard = card;
        image.sprite = card.cardBack;
        isRevealed = false;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(HandleClick);
    }

    public void SetColor(Color color)
    {
        if (image != null)
            image.color = color;
    }

    public void ResetColor()
    {
        if (image != null)
            image.color = Color.white;
    }

    private void HandleClick()
    {
        if (!longPressTriggered)
            onClick?.Invoke();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPointerDown = true;
        longPressTriggered = false;
        pointerDownTime = Time.unscaledTime;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPointerDown = false;
        if (longPressTriggered)
            onLongPressRelease?.Invoke();
        longPressTriggered = false;
    }

    private void Update()
    {
        if (!isPointerDown || longPressTriggered)
            return;

        if (Time.unscaledTime - pointerDownTime >= longPressTime)
        {
            longPressTriggered = true;
            onLongPress?.Invoke();
        }
    }
}