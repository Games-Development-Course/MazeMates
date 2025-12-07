using Unity.Netcode;
using UnityEngine;

public class ResourceManager : NetworkBehaviour
{
    public static ResourceManager Instance;

    public float bombRemoveRange = 3f;
    public GameObject heartPrefab;
    public GameObject lifebuoyEffectPrefab;

    public override void OnNetworkSpawn()
    {
        Debug.Log("ResourceManager SPAWNED   IsServer=" + IsServer + "  IsClient=" + IsClient);

        // נשמור תמיד את האינסטנס
        Instance = this;
    }

    private void Awake()
    {
        // גיבוי במקרה שהסצנה נטענת לפני NetworkSpawn
        if (Instance == null)
            Instance = this;
    }

    // ============================================================
    // API – נקרא מהנווט (NavigatorInteractionManager)
    // ============================================================

    public void TryPlaceHeart()
    {
        if (!IsServer)
        {
            RequestPlaceHeartRpc();
            return;
        }

        ServerPlaceHeart();
    }

    public void TryRemoveBomb()
    {
        if (!IsServer)
        {
            RequestRemoveBombRpc();
            return;
        }

        ServerRemoveBomb();
    }

    public void TryUseLifebuoy()
    {
        if (!IsServer)
        {
            RequestUseLifebuoyRpc();
            return;
        }

        ServerUseLifebuoy();
    }

    // ============================================================
    // SERVER LOGIC – PLACE HEART
    // ============================================================

    private void ServerPlaceHeart()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null)
            return;

        if (gm.HeartPlacements <= 0)
        {
            NotifyNavigatorMessageRpc("לא נותרו לבבות");
            return;
        }

        if (gm.traveller == null)
        {
            NotifyNavigatorMessageRpc("אין מטייל במשחק");
            return;
        }

        if (heartPrefab == null)
        {
            Debug.LogError("ResourceManager: heartPrefab is null");
            return;
        }

        Transform t = gm.traveller.transform;

        float eyeHeight = 1.6f;
        float forwardDistance = 1.2f;
        float backFromWall = 0.3f;
        float dropRayHeight = 2f;
        float dropRayDown = 5f;

        Vector3 eyePos = t.position + Vector3.up * eyeHeight;
        Vector3 dropPos;

        RaycastHit hit;

        // אם מסתכל על קיר – ניפול מעט אחורה ממנו ונחפש רצפה משם
        if (Physics.Raycast(eyePos, t.forward, out hit, forwardDistance))
        {
            Vector3 ahead = hit.point - t.forward * backFromWall;
            Vector3 rayStart = ahead + Vector3.up * dropRayHeight;

            RaycastHit floorHit;
            if (Physics.Raycast(rayStart, Vector3.down, out floorHit, dropRayDown))
            {
                dropPos = floorHit.point;
            }
            else
            {
                dropPos = new Vector3(ahead.x, t.position.y, ahead.z);
            }
        }
        else
        {
            // אין קיר מול הפנים – סתם קדימה + Ray למטה
            Vector3 ahead = t.position + t.forward * forwardDistance;
            Vector3 rayStart = ahead + Vector3.up * dropRayHeight;

            RaycastHit floorHit;
            if (Physics.Raycast(rayStart, Vector3.down, out floorHit, dropRayDown))
            {
                dropPos = floorHit.point;
            }
            else
            {
                dropPos = new Vector3(ahead.x, t.position.y, ahead.z);
            }
        }

        GameObject heart = Instantiate(heartPrefab, dropPos, Quaternion.identity);
        NetworkObject netObj = heart.GetComponent<NetworkObject>();
        if (netObj != null)
            netObj.Spawn();

        gm.HeartPlacements--;
        SyncResourceCountsRpc(gm.lifebuoys, gm.HeartPlacements, gm.BombRemovals);
    }

    // ============================================================
    // SERVER LOGIC – REMOVE BOMB
    // ============================================================

    private void ServerRemoveBomb()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null)
            return;

        if (gm.BombRemovals <= 0)
        {
            NotifyNavigatorMessageRpc("לא נותרו ניסיונות להסרת פצצה");
            return;
        }

        if (gm.traveller == null)
        {
            NotifyNavigatorMessageRpc("אין מטייל במשחק");
            return;
        }

        Transform traveller = gm.traveller.transform;

        GameObject[] bombs = GameObject.FindGameObjectsWithTag("Bomb");
        if (bombs.Length == 0)
        {
            NotifyNavigatorMessageRpc("אין פצצות במפה");
            return;
        }

        GameObject closest = null;
        float bestDist = Mathf.Infinity;

        foreach (GameObject b in bombs)
        {
            float d = Vector3.Distance(traveller.position, b.transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                closest = b;
            }
        }

        if (closest == null)
        {
            NotifyNavigatorMessageRpc("לא נמצאה פצצה");
            return;
        }

        NetworkObject bombNetObj = closest.GetComponent<NetworkObject>();
        if (bombNetObj != null)
        {
            bombNetObj.Despawn(true);
        }
        else
        {
            Destroy(closest);
        }

        gm.BombRemovals--;
        SyncResourceCountsRpc(gm.lifebuoys, gm.HeartPlacements, gm.BombRemovals);
    }

    // ============================================================
    // SERVER LOGIC – USE LIFEBOUY
    // ============================================================

    private void ServerUseLifebuoy()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null)
            return;

        if (gm.lifebuoys <= 0)
        {
            NotifyNavigatorMessageRpc("לא נותרו מצופי הצלה");
            return;
        }

        if (!gm.inPuzzle || gm.activePuzzleDoor == null)
        {
            NotifyNavigatorMessageRpc("ניתן להשתמש במצוף רק כשהחידה פתוחה");
            return;
        }

        DoorController door = gm.activePuzzleDoor;
        NetworkObject doorNet = door.GetComponent<NetworkObject>();

        ulong doorId = 0;
        if (doorNet != null)
            doorId = doorNet.NetworkObjectId;

        // בקשה ללקוח של המטייל להדליק hint
        RevealHintForPuzzleRpc(doorId);

        // אפקט ויזואלי על המטייל
        if (lifebuoyEffectPrefab != null && gm.traveller != null)
        {
            GameObject eff = Instantiate(lifebuoyEffectPrefab, gm.traveller.transform.position, Quaternion.identity);
            NetworkObject effNet = eff.GetComponent<NetworkObject>();
            if (effNet != null)
                effNet.Spawn();
        }

        gm.lifebuoys--;
        SyncResourceCountsRpc(gm.lifebuoys, gm.HeartPlacements, gm.BombRemovals);
    }

    // ============================================================
    // RPC – CLIENT → SERVER
    // ============================================================

    [Rpc(SendTo.Server)]
    private void RequestPlaceHeartRpc()
    {
        ServerPlaceHeart();
    }

    [Rpc(SendTo.Server)]
    private void RequestRemoveBombRpc()
    {
        ServerRemoveBomb();
    }

    [Rpc(SendTo.Server)]
    private void RequestUseLifebuoyRpc()
    {
        ServerUseLifebuoy();
    }

    // ============================================================
    // RPC – SERVER → CLIENTS
    // ============================================================

    [Rpc(SendTo.Everyone)]
    private void SyncResourceCountsRpc(int lifebuoys, int hearts, int bombs)
    {
        GameManager gm = GameManager.Instance;
        if (gm == null)
            return;

        gm.lifebuoys = lifebuoys;
        gm.HeartPlacements = hearts;
        gm.BombRemovals = bombs;

        HUDManager.Instance?.UpdateHUDs();
    }

    [Rpc(SendTo.Everyone)]
    private void NotifyNavigatorMessageRpc(string msg)
    {
        HUDManager.Instance?.ShowMessageForNavigator(msg);
    }

    [Rpc(SendTo.Everyone)]
    private void RevealHintForPuzzleRpc(ulong doorId)
    {
        GameManager gm = GameManager.Instance;
        if (gm == null)
            return;

        // רק הלקוח של המטייל מפעיל את ה-Hint בפועל
        if (gm.traveller == null)
            return;

        NetworkObject travellerNet = gm.traveller.GetComponent<NetworkObject>();
        if (travellerNet == null || !travellerNet.IsOwner)
            return;

        DoorController door = null;

        if (doorId != 0)
        {
            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects
                    .TryGetValue(doorId, out var obj))
                return;

            door = obj.GetComponent<DoorController>();
        }
        else
        {
            door = gm.activePuzzleDoor;
        }

        if (door == null)
            return;

        // GetPuzzle מחזיר IDoor, אז נעשה cast ל-PuzzleDoor
        var puzzleDoor = door.GetPuzzle() as PuzzleDoor;
        if (puzzleDoor == null)
        {
            Debug.LogWarning("RevealHintForPuzzleRpc: door has no PuzzleDoor logic");
            return;
        }

        puzzleDoor.RevealRandomHint();
    }
}
