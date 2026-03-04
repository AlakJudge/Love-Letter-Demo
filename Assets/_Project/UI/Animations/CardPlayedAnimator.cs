using System;
using System.Collections;
using UnityEngine;

public class CardPlayAnimator : MonoBehaviour
{
    [Tooltip("Prefab to use for temporary 'played' cards.")]
    public CardView cardViewPrefab;
    [Tooltip("Shield prefab shown when Handmaid is played.")]
    public GameObject handmaidShieldPrefab;
    [Tooltip("Target RectTransform where played cards should fly to (e.g., CardPlayArea).")]
    public RectTransform sourceCardPlayedContainer;
    public RectTransform targetCardPlayedContainer;

    [Tooltip("Seconds it takes for a played card to fly to the center.")]
    public float flyDuration = 0.5f;

    [Tooltip("Seconds to keep the card visible at the center before destroying it.")]
    public float holdDuration = 0.8f;

    [Tooltip("Seconds it takes for the Handmaid shield to fade out.")]
    public float shieldFadeDuration = 0.8f;

    private Canvas canvas;
    private CardView lastCompareSource;
    private CardView lastCompareTarget;
    private CardData lastSourceCardData;
    private CardData lastTargetCardData;

    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            Debug.LogWarning("CardPlayAnimator: no Canvas found in parents.");
    }

    // Animate a copy of this card from its current UI position to the play area center.
    public void PlayCardAnimation(CardView sourceView, CardData card)
    {
        if (cardViewPrefab == null || sourceCardPlayedContainer == null || sourceView == null || card == null)
        {
            Debug.LogWarning("CardPlayAnimator: missing references, cannot play animation.");
            return;
        }

        StartCoroutine(PlaySingleCardRoutine(sourceView, card));
    }
    
    public IEnumerator PlaySingleCardRoutine(CardView sourceView, CardData card)
    {
        var canvasRect = (RectTransform)canvas.transform;

        // Instantiate temp cards
        var tempSource = Instantiate(cardViewPrefab, canvasRect);
        tempSource.onClick = null;
        tempSource.onLongPress = null;
        tempSource.onLongPressRelease = null;
        
        // Set sprites based on reveal flags
        tempSource.Set(card);
        var tempSourceRect = (RectTransform)tempSource.transform;
        var sourceRect = (RectTransform)sourceView.transform;

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
            RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, sourceCardPlayedContainer.position),
            canvas.worldCamera,
            out endLocalPos
        );
        
        tempSourceRect.anchoredPosition = startLocalPos;

        // Set the size of the card to be the same as the end position container's. Same height and aspect ratio.
        Vector2 startSize = tempSourceRect.sizeDelta;
        float targetHeight = sourceCardPlayedContainer.rect.height;
        float aspect = startSize.x / Mathf.Max(1f, startSize.y); // prevent division by zero
        float targetWidth = targetHeight * aspect;
        Vector2 endSize = new Vector2(targetWidth, targetHeight);

        tempSourceRect.sizeDelta = startSize;

        float t = 0f;
        while (t < flyDuration)
        {
            t += Time.deltaTime;
            float lerp = Mathf.Clamp01(t / flyDuration);
            // Move position
            tempSourceRect.anchoredPosition = Vector2.Lerp(startLocalPos, endLocalPos, lerp);
            // Scale size
            tempSourceRect.sizeDelta = Vector2.Lerp(startSize, endSize, lerp);

            yield return null;
        }

        tempSourceRect.anchoredPosition = endLocalPos;
        tempSourceRect.sizeDelta = endSize;

        // Handmaid Shield Effect
        if (card.type == CardType.Handmaid && handmaidShieldPrefab != null)
        {
            Debug.Log("Spawning Handmaid shield effect");
            // Instantiate shield as a child of the temp card
            var shieldEffect = Instantiate(handmaidShieldPrefab, tempSourceRect);

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

        Destroy(tempSourceRect.gameObject);
    }            
    
    public IEnumerator PlayCompareRoutine(
        CardView sourceView, CardData sourceCard,
        CardView targetView, CardData targetCard,
        bool revealSource, bool revealTarget,
        bool destroyAtEnd = true)
    {
        if (cardViewPrefab == null || canvas == null ||
            sourceCardPlayedContainer == null || targetCardPlayedContainer == null ||
            sourceView == null || targetView == null)
        {
            Debug.LogWarning("CardPlayAnimator: missing references for compare animation.");
            yield break;
        }

        var canvasRect = (RectTransform)canvas.transform;

        // Instantiate temp cards
        var tempSource = Instantiate(cardViewPrefab, canvasRect);
        tempSource.onClick = null;
        tempSource.onLongPress = null;
        tempSource.onLongPressRelease = null;
        var tempTarget = Instantiate(cardViewPrefab, canvasRect);
        tempTarget.onClick = null;
        tempTarget.onLongPress = null;
        tempTarget.onLongPressRelease = null;
        
        // Set sprites based on reveal flags
        if (revealSource)
            tempSource.Set(sourceCard);
        else
            tempSource.ShowBack(sourceCard);

        if (revealTarget)
            tempTarget.Set(targetCard);
        else
            tempTarget.ShowBack(targetCard);

        var tempSourceRect = (RectTransform)tempSource.transform;
        var tempTargetRect = (RectTransform)tempTarget.transform;
        
        var sourceRect = (RectTransform)sourceView.transform;
        var targetRect = (RectTransform)targetView.transform;

        // Convert source world position to local position in canvas space
        Vector2 startLocalPos, targetStartPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, sourceRect.position),
            canvas.worldCamera,
            out startLocalPos
        );
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, targetRect.position),
            canvas.worldCamera,
            out targetStartPos
        );

        // Convert play area center to local position
        Vector2 endLocalPos, targetEndPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, sourceCardPlayedContainer.position),
            canvas.worldCamera,
            out endLocalPos
        );
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, targetCardPlayedContainer.position),
            canvas.worldCamera,
            out targetEndPos
        );
        
        tempSourceRect.anchoredPosition = startLocalPos;
        tempTargetRect.anchoredPosition = targetStartPos;

        // Set the size of the card to be the same as the end position container's. Same height and aspect ratio.
        Vector2 startSize = tempSourceRect.sizeDelta;
        float targetHeight = sourceCardPlayedContainer.rect.height;
        float aspect = startSize.x / Mathf.Max(1f, startSize.y); // prevent division by zero
        float targetWidth = targetHeight * aspect;
        Vector2 endSize = new Vector2(targetWidth, targetHeight);

        tempSourceRect.sizeDelta = startSize;
        tempTargetRect.sizeDelta = startSize;

        float t = 0f;
        while (t < flyDuration)
        {
            t += Time.deltaTime;
            float lerp = Mathf.Clamp01(t / flyDuration);
            // Move position
            tempSourceRect.anchoredPosition = Vector2.Lerp(startLocalPos, endLocalPos, lerp);
            tempTargetRect.anchoredPosition = Vector2.Lerp(targetStartPos, targetEndPos, lerp);
            // Scale size
            tempSourceRect.sizeDelta = Vector2.Lerp(startSize, endSize, lerp);
            tempTargetRect.sizeDelta = Vector2.Lerp(startSize, endSize, lerp);

            yield return null;
        }

        tempSourceRect.anchoredPosition = endLocalPos;
        tempSourceRect.sizeDelta = endSize;
        tempTargetRect.anchoredPosition = targetEndPos;
        tempTargetRect.sizeDelta = endSize;

        // Handmaid Shield Effect
        if (sourceCard.type == CardType.Handmaid && handmaidShieldPrefab != null)
        {
            Debug.Log("Spawning Handmaid shield effect");
            // Instantiate shield as a child of the temp card
            var shieldEffect = Instantiate(handmaidShieldPrefab, tempSourceRect);

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

        // Remember for potential later reveal
        lastCompareSource     = tempSource;
        lastCompareTarget     = tempTarget;
        lastSourceCardData    = sourceCard;
        lastTargetCardData    = targetCard;

        if (destroyAtEnd)
        {
            Destroy(tempSourceRect.gameObject);
            Destroy(tempTargetRect.gameObject);
            lastCompareSource  = null;
            lastCompareTarget  = null;
            lastSourceCardData = null;
            lastTargetCardData = null;
        }
    }

    public IEnumerator RevealLastCompare(bool revealSource, bool revealTarget)
    {
        if (lastCompareSource == null || lastCompareTarget == null ||
            lastSourceCardData == null || lastTargetCardData == null)
            yield break;

        if (revealSource)
            lastCompareSource.Set(lastSourceCardData);
        else
            lastCompareSource.ShowBack(lastSourceCardData);

        if (revealTarget)
            lastCompareTarget.Set(lastTargetCardData);
        else
            lastCompareTarget.ShowBack(lastTargetCardData);
    }
    
    public void DestroyLastCompare()
    {
        if (lastCompareSource != null)
            Destroy(lastCompareSource.gameObject);
        if (lastCompareTarget != null)
            Destroy(lastCompareTarget.gameObject);

        lastCompareSource  = null;
        lastCompareTarget  = null;
        lastSourceCardData = null;
        lastTargetCardData = null;
    }

    public void ToggleFastMode(bool isFast)
    {
        // If currently fast, reset to defaults. If currently normal, set to fast values.
        if (isFast == true)
        {
            flyDuration = 0.2f;
            holdDuration = 0.4f;
            shieldFadeDuration = 0.2f;
        }
        else
        {
            flyDuration = 0.5f;
            holdDuration = 0.8f;
            shieldFadeDuration = 0.8f;
        }
    }
}