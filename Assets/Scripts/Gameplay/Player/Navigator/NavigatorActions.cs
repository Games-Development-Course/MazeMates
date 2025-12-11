using Fusion;
using UnityEngine;

public class NavigatorActions : NetworkBehaviour
{
    public static NavigatorActions Instance { get; private set; }

    private TutorialManager tutorial;

    // ============================================================
    // UNITY LIFECYCLE
    // ============================================================

    private void Awake()
    {
        Debug.Log($"[NavigatorActions][Awake] enabled={enabled} active={gameObject.activeSelf}");
    }

    private void Start()
    {
        Debug.Log($"[NavigatorActions][Start] enabled={enabled} active={gameObject.activeSelf}");
    }

    public override void Spawned()
    {
        // בדומה ל-NGO IsOwner
        if (HasInputAuthority)
            Instance = this;

        tutorial = FindFirstObjectByType<TutorialManager>();

        Debug.Log($"[NavigatorActions][Spawned] InputAuth={HasInputAuthority} StateAuth={HasStateAuthority}");
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this)
            Instance = null;

        Debug.Log("[NavigatorActions][Despawned] Instance cleared");
    }

    // ============================================================
    // COMPATIBILITY HELPERS (להחליף IsOwner/IsHost/IsServer של NGO)
    // ============================================================

    // ב-NGO: IsOwner → ב-Fusion: HasInputAuthority
    public bool IsOwner_F => HasInputAuthority;

    // ב-NGO: IsHost → ב-Fusion: HasStateAuthority + HasInputAuthority באותו שחקן
    public bool IsHost_F => HasStateAuthority && HasInputAuthority;

    // ב-NGO: IsServer → ב-Fusion: HasStateAuthority
    public bool IsServer_F => HasStateAuthority;

    // אצלך IsLocalNavigator חזר false לשחקן ה"Host"
    // עכשיו ההיגיון מומר ל-Fusion בצורה תקינה:
    private bool IsLocalNavigator()
    {
        // "Navigator" = תמיד ה-Client (אין StateAuthority)
        return HasInputAuthority && !HasStateAuthority;
    }

    // ============================================================
    // UI — ACTIONS
    // ============================================================

    public void UI_OpenDoor()
    {
        if (!IsLocalNavigator())
            return;

        var door = DoorController.FindDoorPlayerIsOn();

        if (door == null)
        {
            HUDManager.Instance?.ShowMessageForNavigator("אין דלת כאן");
            return;
        }

        if (door.doorType == DoorType.Puzzle)
        {
            HUDManager.Instance?.ShowMessageForNavigator("דלת זו דורשת לפתור חידה");
            return;
        }

        door.Interact(); // צריך להיות מתורגם ל-Fusion — זה תקין אם door עצמו כבר הומר
    }

    public void UI_ShowPuzzle()
    {
        if (!IsLocalNavigator())
            return;

        var door = DoorController.FindDoorPlayerIsOn(DoorType.Puzzle);

        Debug.Log($"[NAV-ACT] UI_ShowPuzzle | door={(door ? door.name : "null")}");

        if (door == null)
        {
            HUDManager.Instance?.ShowMessageForNavigator("אין דלת חידה כאן");
            return;
        }

        // NGO RPC → Fusion RPC
        door.RequestOpenPuzzleDoorRpc();

        tutorial?.NotifyNavigatorOpenedPuzzleDoor();
    }

    public void UI_RemoveBomb()
    {
        Debug.Log($"[NAV] RemoveBomb pressed | InputAuth={HasInputAuthority} StateAuth={HasStateAuthority}");

        if (!IsLocalNavigator())
            return;

        if (ResourceManager.Instance == null)
        {
            HUDManager.Instance?.ShowMessageForNavigator("מערכת משאבים לא מוכנה");
            return;
        }

        ResourceManager.Instance.TryRemoveBomb();
    }

    public void UI_UseLifebuoy()
    {
        if (!IsLocalNavigator())
            return;

        if (ResourceManager.Instance == null)
        {
            HUDManager.Instance?.ShowMessageForNavigator("מערכת משאבים לא מוכנה");
            return;
        }

        ResourceManager.Instance.TryUseLifebuoy();
    }

    public void UI_PlaceHeart()
    {
        if (!IsLocalNavigator())
            return;

        if (ResourceManager.Instance == null)
        {
            HUDManager.Instance?.ShowMessageForNavigator("מערכת משאבים לא מוכנה");
            return;
        }

        ResourceManager.Instance.TryPlaceHeart();
    }
}
