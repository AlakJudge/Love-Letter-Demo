using System.Collections;
using UnityEngine;

public class CardPlayAnimator : MonoBehaviour
{
    [Tooltip("Prefab to use for temporary 'played' cards.")]
    public CardView cardViewPrefab;
    [Tooltip("Shield prefab shown when Handmaid is played.")]
    public GameObject handmaidShieldPrefab;
    [Tooltip("Target RectTransform where played cards should fly to (e.g., CardPlayArea).")]
    public RectTransform singleCardPlayedContainer;
    public RectTransform sourceCardPlayedContainer;
    public RectTransform targetCardPlayedContainer;

    [Tooltip("Seconds it takes for a played card to fly to the center.")]
    public float flyDuration = 0.4f;

    [Tooltip("Seconds to keep the card visible at the center before destroying it.")]
    public float holdDuration = 0.6f;

    [Tooltip("Seconds it takes for the Handmaid shield to fade out.")]
    public float shieldFadeDuration = 0.3f;

    private Canvas canvas;

    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            Debug.LogWarning("CardPlayAnimator: no Canvas found in parents.");
    }

    // Animate a copy of this card from its current UI position to the play area center.
    public void PlayCardAnimation(CardView sourceView, CardData card)
    {
        if (cardViewPrefab == null || singleCardPlayedContainer == null || sourceCardPlayedContainer == null || targetCardPlayedContainer == null || sourceView == null || card == null)
        {
            Debug.LogWarning("CardPlayAnimator: missing references, cannot play animation.");
            return;
        }

        StartCoroutine(PlayCardAnimationRoutine(sourceView, card));
    }

    public IEnumerator PlayCardAnimationRoutine(CardView sourceView, CardData card)
    {
        // Instantiate a temporary CardView
        var tempCard = Instantiate(cardViewPrefab, singleCardPlayedContainer);
        tempCard.Set(card); // show the front
        tempCard.onClick = null;
        tempCard.onLongPress = null;
        tempCard.onLongPressRelease = null;

        var tempRect = (RectTransform)tempCard.transform;
        var sourceRect = (RectTransform)sourceView.transform;
        var canvasRect = (RectTransform)canvas.transform;

        // Convert source world position to local position in canvas space
        Vector2 startLocalPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, sourceRect.position),
            canvas.worldCamera,
            out startLocalPos
        );

        // Convert play area center to local position
        Vector2 endLocalPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, singleCardPlayedContainer.position),
            canvas.worldCamera,
            out endLocalPos
        );
        
        tempRect.anchoredPosition = startLocalPos;

        // Set the size of the card to be the same as the end position container's. Same height and aspect ratio.
        Vector2 startSize = tempRect.sizeDelta;
        float targetHeight = singleCardPlayedContainer.rect.height;
        float aspect = startSize.x / Mathf.Max(1f, startSize.y); // prevent division by zero
        float targetWidth = targetHeight * aspect;

        Vector2 endSize = new Vector2(targetWidth, targetHeight);

        float t = 0f;
        while (t < flyDuration)
        {
            t += Time.deltaTime;
            float lerp = Mathf.Clamp01(t / flyDuration);
            // Move position
            tempRect.anchoredPosition = Vector2.Lerp(startLocalPos, endLocalPos, lerp);
            // Scale size
            tempRect.sizeDelta = Vector2.Lerp(startSize, endSize, lerp);
            yield return null;
        }

        tempRect.anchoredPosition = endLocalPos;
        tempRect.sizeDelta = endSize;

        // Handmaid Shield Effect
        if (card.type == CardType.Handmaid && handmaidShieldPrefab != null)
        {
            Debug.Log("Spawning Handmaid shield effect");
            // Instantiate shield as a child of the temp card
            var shieldEffect = Instantiate(handmaidShieldPrefab, tempRect);

            var shieldRect = (RectTransform)shieldEffect.transform;

            // Start same size as card
            Vector2 shieldStartSize = endSize;
            // End at triple size
            Vector2 shieldEndSize   = endSize * 3f;

            shieldRect.sizeDelta        = shieldStartSize;
            shieldRect.anchoredPosition = Vector2.zero; // center on card
            shieldRect.localScale       = Vector3.one;

            var cg = shieldEffect.GetComponent<CanvasGroup>();
            if (cg == null)
                    cg = shieldEffect.gameObject.AddComponent<CanvasGroup>();
            
            cg.alpha = 1f;

            if (shieldFadeDuration > 0f)
            {
                float tShield = 0f;
                while (tShield < shieldFadeDuration)
                {
                    tShield += Time.deltaTime;
                    float lerp = Mathf.Clamp01(tShield / shieldFadeDuration);
                    
                    // Grow size
                    shieldRect.sizeDelta = Vector2.Lerp(shieldStartSize, shieldEndSize, lerp);
                    // Fade out
                    cg.alpha = 1f - lerp;
                    
                    yield return null;
                }
            }
            else
                Debug.LogWarning("Handmaid shield prefab is missing CanvasGroup component for fading effect.");
        }

        // Optional hold at center
        if (holdDuration > 0f)
            yield return new WaitForSeconds(holdDuration);

        Destroy(tempCard.gameObject);
    }
}