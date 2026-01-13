using UnityEngine;

public class PopupClose : MonoBehaviour
{
    [SerializeField] private GameObject popupRoot;

    private void Awake()
    {
        // אם לא שויכת ידנית, נניח שהאובייקט שעליו יושב הסקריפט הוא הפופאפ
        if (popupRoot == null)
            popupRoot = gameObject;
    }

    public void OnUnderstoodClicked()
    {
        popupRoot.SetActive(false);
    }
}
