using UnityEngine;
using UnityEngine.EventSystems;

public class DraggablePiece : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public RectTransform rectTransform;
    public CanvasGroup canvasGroup;

    public RectTransform target;
    public float snapDistance = 50f;

    private bool placed = false;
    private Canvas rootCanvas;
    private Vector2 originalPos;

    void Awake()
    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        rootCanvas = GetComponentInParent<Canvas>();

        originalPos = rectTransform.anchoredPosition;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (placed)
            return;

        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0.7f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (placed)
            return;

        Vector3 worldPos;
        if (
            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                rectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out worldPos
            )
        )
        {
            rectTransform.position = worldPos;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (placed)
            return;

        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;

        if (target == null)
        {
            rectTransform.anchoredPosition = originalPos;
            return;
        }

        float dist = Vector2.Distance(rectTransform.anchoredPosition, target.anchoredPosition);

        if (dist < snapDistance)
        {
            rectTransform.anchoredPosition = target.anchoredPosition;
            placed = true;

            GameManager.Instance.activePuzzleDoor?.PuzzleSolved();
        }
        else
        {
            rectTransform.anchoredPosition = originalPos;
        }
    }

    public bool IsSnapped() => placed;
}
