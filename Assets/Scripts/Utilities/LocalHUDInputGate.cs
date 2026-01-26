using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class LocalHudInputGate : MonoBehaviour
{
    [Header("Roots in the scene")]
    [SerializeField] private GameObject travellerHudRoot; // UI/TravellerHUD
    [SerializeField] private GameObject navigatorHudRoot; // UI/NavigatorHUD

    private void Start()
    {
        Apply();
    }

    private void Apply()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogWarning("[LocalHudInputGate] No NetworkManager yet.");
            return;
        }

        // ✅ חד משמעי אצלך: Host=Traveller, ClientOnly=Navigator
        bool localIsTraveller = NetworkManager.Singleton.IsHost;

        SetHudInput(travellerHudRoot, localIsTraveller);
        SetHudInput(navigatorHudRoot, !localIsTraveller);

        Debug.Log($"[LocalHudInputGate] IsHost={NetworkManager.Singleton.IsHost} => local={(localIsTraveller ? "TRAVELLER" : "NAVIGATOR")}");
    }

    private static void SetHudInput(GameObject root, bool enableInput)
    {
        if (!root) return;

        // CanvasGroup רק לשליטה בקלט (לא נוגעים ב-alpha בכלל)
        var cg = root.GetComponent<CanvasGroup>();
        if (!cg) cg = root.AddComponent<CanvasGroup>();

        cg.interactable = enableInput;
        cg.blocksRaycasts = enableInput;

        // GraphicRaycaster קובע אם UI בכלל מקבל קליקים
        foreach (var gr in root.GetComponentsInChildren<GraphicRaycaster>(true))
            gr.enabled = enableInput;

        // (אופציונלי) להחזיר Buttons להיות interactable אם משהו כיבה אותם
        foreach (var b in root.GetComponentsInChildren<Button>(true))
            b.interactable = enableInput;
    }
}
