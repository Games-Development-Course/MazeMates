using UnityEngine;
using UnityEngine.EventSystems;

public class DraggablePiece : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public Transform target;
    public DoorController puzzle;

    private RectTransform rect;
    private Canvas canvas;

    private bool isSnapped = false;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        isSnapped = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (canvas == null)
            return;

        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out localPos
        );

        rect.anchoredPosition = localPos;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        float dist = Vector3.Distance(rect.position, target.position);

        // ������� ��� ���� ��� (���� ����� ��"�)
        float snapDistance = 120f;

        if (dist < snapDistance)
        {
            //  ���� ���� ����� ��Target
            rect.position = target.position;
            isSnapped = true;
        }
        else
        {
            isSnapped = false;
        }

        puzzle.PuzzleSolved();
    }

    public bool IsSnapped() => isSnapped;

    public bool IsInCorrectPlace() => isSnapped;
}
