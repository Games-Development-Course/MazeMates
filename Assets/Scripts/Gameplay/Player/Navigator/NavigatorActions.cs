using Unity.Netcode;
using UnityEngine;

public class NavigatorActions : NetworkBehaviour
{
    public static NavigatorActions Instance { get; private set; }

    private TutorialManager tutorial;

    private void Awake()
    {
        Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            null,
            $"[NavigatorActions][Awake] enabled={enabled} active={gameObject.activeSelf} hierarchyActive={gameObject.activeInHierarchy}"
        );
    }

    private void Start()
    {
        Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            null,
            $"[NavigatorActions][Start] enabled={enabled} active={gameObject.activeSelf} hierarchyActive={gameObject.activeInHierarchy}"
        );
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
            Instance = this;

        tutorial = Object.FindFirstObjectByType<TutorialManager>();

        Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            null,
            $"[NavigatorActions][OnNetworkSpawn] IsOwner={IsOwner} IsHost={IsHost} IsServer={IsServer}"
        );
    }

    public override void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            null,
            "[NavigatorActions][OnDestroy] Instance cleared"
        );
    }

    // =====================================================================
    // UI — BUTTON EVENTS
    // =====================================================================

    public void UI_OpenDoor()
    {
        if (!IsLocalNavigator())
            return;

        DoorController door = DoorController.FindDoorPlayerIsOn();

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

        door.Interact();
    }

    public void UI_ShowPuzzle()
    {
        if (!IsLocalNavigator())
            return;

        // במקום FindNearestDoorOnPad לפי מיקום הנווט:
        // DoorController door = DoorController.FindNearestDoorOnPad(DoorType.Puzzle, transform.position);

        DoorController door = DoorController.FindDoorPlayerIsOn(DoorType.Puzzle);

        Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            null,
            $"[NAV-ACT] UI_ShowPuzzle | door={(door == null ? "null" : door.name)}"
        );

        if (door == null)
        {
            HUDManager.Instance?.ShowMessageForNavigator("אין דלת חידה כאן");
            return;
        }

        door.RequestOpenPuzzleDoorRpc();
        tutorial?.NotifyNavigatorOpenedPuzzleDoor();
    }

    private bool IsLocalNavigator()
    {
        return !IsHost;
    }

    public void UI_RemoveBomb()
    {
        Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            null,
            $"[NAV] RemoveBomb pressed | Owner={IsOwner} Server={IsServer} Host={IsHost}"
        );

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
