using UnityEngine;
using UnityEngine.EventSystems;

public class UIButtonProbe : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    public string label;

    public void OnPointerDown(PointerEventData eventData)
        => Debug.Log($"[UIButtonProbe] DOWN label={label} go={name}");

    public void OnPointerUp(PointerEventData eventData)
        => Debug.Log($"[UIButtonProbe] UP label={label} go={name}");

    public void OnPointerClick(PointerEventData eventData)
        => Debug.Log($"[UIButtonProbe] CLICK label={label} go={name}");
}
