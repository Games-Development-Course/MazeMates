using UnityEngine;

public class PopupController : MonoBehaviour
{
    [SerializeField] private GameObject popup;

    public void HidePopup()
    {
        popup.SetActive(false);
    }
}
