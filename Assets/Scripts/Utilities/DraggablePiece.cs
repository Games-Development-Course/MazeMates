using UnityEngine;
using UnityEngine.EventSystems;

public class DraggablePiece : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public RectTransform rectTransform;
    public CanvasGroup canvasGroup;

    // ✅ target (hint rect)
    public RectTransform target;

    // ✅ MUST be the shared root that contains BOTH pieces and targets
    public RectTransform commonRoot;

    // snap in "root-local" units
    public float snapDistance = 50f;

    private bool placed = false;
    private Canvas rootCanvas;
    private Vector2 originalAnchoredPos;

    void Awake()
    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        rootCanvas = GetComponentInParent<Canvas>();

        CaptureOriginalPos();
    }

    // ✅ call after layout/spawner moved pieces
    public void CaptureOriginalPos()
    {
        if (rectTransform != null)
            originalAnchoredPos = rectTransform.anchoredPosition;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (placed) return;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0.7f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (placed) return;

        // drag normally
        RectTransform dragPlane = (commonRoot != null) ? commonRoot : (RectTransform)rootCanvas.transform;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                dragPlane,
                eventData.position,
                eventData.pressEventCamera,
                out var worldPos))
        {
            rectTransform.position = worldPos;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (placed) return;

        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;

        if (target == null || commonRoot == null)
        {
            rectTransform.anchoredPosition = originalAnchoredPos;
            return;
        }

        // ✅ compare in SAME space: commonRoot local space
        Vector2 pieceLocal = commonRoot.InverseTransformPoint(rectTransform.position);
        Vector2 targetLocal = commonRoot.InverseTransformPoint(target.position);

        float dist = Vector2.Distance(pieceLocal, targetLocal);

        if (dist < snapDistance)
        {
            rectTransform.position = target.position;
            placed = true;

            Object.FindFirstObjectByType<TutorialManager>()?.NotifyTravellerPlacedPuzzlePiece();
            GameManager.Instance.activePuzzleDoor?.GetPuzzle()?.PuzzleSolved();
        }
        else
        {
            rectTransform.anchoredPosition = originalAnchoredPos;
        }
    }

    public bool IsSnapped() => placed;
}
