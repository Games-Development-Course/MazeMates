using Unity.Netcode;
using UnityEngine;

public class NavigatorActions : NetworkBehaviour
{
    private bool Safe()
    {
        if (NavigatorInteractionManager.Instance == null)
        {
            HUDManager.Instance?.ShowMessageToNavigator("המערכת עדיין נטענת...");
            return false;
        }

        return true;
    }

    public void UI_OpenDoor()
    {
        if (!Safe()) return;
        NavigatorInteractionManager.Instance.Execute(NavActionType.OpenDoor);
    }

    public void UI_ShowPuzzle()
    {
        if (!Safe()) return;
        NavigatorInteractionManager.Instance.Execute(NavActionType.ShowPuzzle);
    }

    public void UI_RemoveBomb()
    {
        if (!Safe()) return;

        NavigatorInteractionManager.Instance.Execute(NavActionType.RemoveBomb);

        // מודיע לטוטוריאל
        FindFirstObjectByType<TutorialManager>()?.NotifyNavigatorRemovedBomb();
    }


    public void UI_UseLifebuoy()
    {
        if (!Safe()) return;
        NavigatorInteractionManager.Instance.Execute(NavActionType.UseLifebuoy);
    }

    public void UI_PlaceHeart()
    {
        if (!Safe()) return;
        NavigatorInteractionManager.Instance.Execute(NavActionType.PlaceHeart);
    }
}
