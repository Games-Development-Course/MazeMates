// Assets/Scripts/Gameplay/UI/NavigatorSpotlights.cs
using Unity.Netcode;
using UnityEngine;

public class NavigatorSpotlights : MonoBehaviour
{
    public static NavigatorSpotlights I;

    [Header("Spotlight GameObjects in Navigator room")]
    [SerializeField] private GameObject bombRemoveSpot;
    [SerializeField] private GameObject openDoorSpot;
    [SerializeField] private GameObject hintSpot;

    private int _nearBombCount = 0;

    // ✅ זה מייצג "האם המטייל על הפד" (לא קשור אם להציג OpenDoor)
    private int _travellerOnPadCount = 0;

    // ✅ האם כרגע מותר להציג את זרקור "פתח דלת"
    private bool _openDoorAvailable = false;

    // ✅ האם כרגע צריך להציג "רמז"
    private bool _hintReady = false;

    private void Awake()
    {
        I = this;
        Refresh();
    }

    private bool IsNavigatorClient()
    {
        // אצלכם: traveller הוא ה-Host והnavigator הוא Client.
        return NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer;
    }

    private void Refresh()
    {
        if (!IsNavigatorClient())
        {
            Set(bombRemoveSpot, false);
            Set(openDoorSpot, false);
            Set(hintSpot, false);
            return;
        }

        var gm = GameManager.Instance;
        int bombRemovals = gm != null ? gm.BombRemovals : 0;
        int lifebuoys = gm != null ? gm.lifebuoys : 0;

        bool showBomb = _nearBombCount > 0 && bombRemovals > 0;

        // ✅ openDoor תלוי גם ב"על הפד" וגם בזה שהוא "זמין"
        bool showDoor = _travellerOnPadCount > 0 && _openDoorAvailable;

        // ✅ hint תלוי ב"על הפד" + hintReady + שיש lifebuoys
        bool showHint = _travellerOnPadCount > 0 && _hintReady && lifebuoys > 0;

        Set(bombRemoveSpot, showBomb);
        Set(openDoorSpot, showDoor);
        Set(hintSpot, showHint);
    }

    private void Set(GameObject go, bool v)
    {
        if (!go) return;
        if (go.activeSelf != v) go.SetActive(v);
    }

    // נקרא ע"י RPC של BombTrigger
    public void SetNearBomb(bool entered)
    {
        _nearBombCount += entered ? 1 : -1;
        if (_nearBombCount < 0) _nearBombCount = 0;
        Refresh();
    }

    // ✅ נקרא רק כשמטייל נכנס/יוצא מהפד (נוכחות)
    public void SetTravellerOnPad(bool entered)
    {
        _travellerOnPadCount += entered ? 1 : -1;
        if (_travellerOnPadCount < 0) _travellerOnPadCount = 0;

        // אם המטייל כבר לא על פד בכלל – ננקה מצבים
        if (_travellerOnPadCount == 0)
        {
            _hintReady = false;
            _openDoorAvailable = false;
        }

        Refresh();
    }

    // ✅ נקרא כשצריך להציג/להסתיר "פתח דלת" בלי לשקר שהמטייל לא על הפד
    public void SetOpenDoorAvailable(bool available)
    {
        _openDoorAvailable = available;
        Refresh();
    }

    public void SetHintReady(bool ready)
    {
        _hintReady = ready;
        Refresh();
    }
}
