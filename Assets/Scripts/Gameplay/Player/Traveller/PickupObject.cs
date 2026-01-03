// File: Assets/Scripts/Gameplay/Pickups/PickupObject.cs

using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PickupObject : NetworkBehaviour
{
    public enum PickupType { Heart, Key, Bomb, Lifebuoy }

    [Header("Pickup Type")]
    public PickupType type;

    [Header("Tutorial Override")]
    [SerializeField] private string tutorialSceneName = "TutorialScene";
    [SerializeField] private Transform tutorialTravellerRespawnPoint; // שים נקודת spawn בטוטוריאל

    [Header("Custom Message Settings")]
    [TextArea(2, 5)] public string customMessage = "";
    public Color messageColor = Color.white;
    public TMP_FontAsset messageFont;

    [Header("Message Duration")]
    public float messageDuration = 1.5f;

    [Header("Bomb Reset Visuals (PlayerMovement1P BombResetAndTeleportClientRpc params)")]
    public float bombPreTeleportDelay = 0.25f;
    public float bombRedSeconds = 0.15f;
    public float bombFadeOut = 0.25f;
    public float bombFadeIn = 0.35f;

    private bool consumedServer = false;
    private TutorialManager tutorialCache;

    private void Awake()
    {
        EnsureRelayOnAllColliders();
    }

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleTriggerEnter(other);
    }

    internal void HandleTriggerEnter(Collider other)
    {
        if (NetworkObject == null || !NetworkObject.IsSpawned) return;

        var playerNo = GetPlayerNetworkObjectFromCollider(other);
        if (playerNo == null) return;

        // רק ה-Owner של השחקן שולח בקשה (מונע כפילויות)
        if (!playerNo.IsOwner) return;

        RequestPickupServerRpc(NetworkObjectId, playerNo.NetworkObjectId);
    }

    private static NetworkObject GetPlayerNetworkObjectFromCollider(Collider other)
    {
        if (other == null) return null;

        var root = other.transform.root;
        if (root == null) return null;

        if (!root.CompareTag("Player") && !other.CompareTag("Player"))
            return null;

        var no = root.GetComponent<NetworkObject>();
        if (no != null && no.IsSpawned) return no;

        no = other.GetComponentInParent<NetworkObject>();
        if (no != null && no.IsSpawned) return no;

        return null;
    }

    private void EnsureRelayOnAllColliders()
    {
        var cols = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            var c = cols[i];
            if (c == null) continue;

            c.isTrigger = true;

            var relay = c.GetComponent<PickupTriggerRelay>();
            if (relay == null)
                relay = c.gameObject.AddComponent<PickupTriggerRelay>();

            relay.SetOwner(this);
        }
    }

    private TutorialManager EnsureTutorial()
    {
        if (tutorialCache == null)
            tutorialCache = Object.FindFirstObjectByType<TutorialManager>();
        return tutorialCache;
    }

    private bool IsTutorialActiveOnServer(TutorialManager tm)
    {
        // ✅ FIX: אל תתלה ב-TutorialActive (זה מה שחסם לך Notify על מפתח)
        if (tm == null) return false;
        if (!tm.IsSpawned) return false;
        return SceneManager.GetActiveScene().name == tutorialSceneName;
    }

    private Vector3 GetTutorialRespawnPos()
        => tutorialTravellerRespawnPoint != null ? tutorialTravellerRespawnPoint.position : Vector3.zero;

    private Quaternion GetTutorialRespawnRot()
        => tutorialTravellerRespawnPoint != null ? tutorialTravellerRespawnPoint.rotation : Quaternion.identity;

    // =====================================================================
    // SERVER AUTHORITATIVE PICKUP
    // =====================================================================

    [ServerRpc(RequireOwnership = false)]
    private void RequestPickupServerRpc(ulong pickupNetId, ulong playerNetId, ServerRpcParams rpcParams = default)
    {
        if (pickupNetId != NetworkObjectId) return;
        if (consumedServer) return;

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetId, out var playerNo))
            return;

        consumedServer = true;

        var gm = GameManager.Instance;
        if (gm == null)
        {
            DespawnNow();
            return;
        }

        bool pickedByTraveller = (playerNo.OwnerClientId == NetworkManager.ServerClientId); // Host = traveller
        var tm = EnsureTutorial();

        // ---- Apply server state + tutorial notify ----
        string finalMessage = customMessage;
        bool gameOver = false;

        switch (type)
        {
            case PickupType.Heart:
                gm.lives++;
                if (string.IsNullOrEmpty(finalMessage))
                    finalMessage = "אספת לב! קיבלת חיים נוספים.";
                if (pickedByTraveller && IsTutorialActiveOnServer(tm))
                    tm.NotifyTravellerPickedHeart();
                break;

            case PickupType.Key:
                gm.keys++;
                if (string.IsNullOrEmpty(finalMessage))
                    finalMessage = "אספת מפתח!";
                if (pickedByTraveller && IsTutorialActiveOnServer(tm))
                    tm.NotifyTravellerPickedKey();
                break;

            case PickupType.Lifebuoy:
                gm.lifebuoys++;
                if (string.IsNullOrEmpty(finalMessage))
                    finalMessage = "אספת מצוף הצלה!";
                break;

            case PickupType.Bomb:
                {
                    gm.lives--;
                    if (string.IsNullOrEmpty(finalMessage))
                        finalMessage = "דרכת על פצצה! איבדת לב.";

                    bool inTutorial = pickedByTraveller && IsTutorialActiveOnServer(tm);

                    // תנאי טוטוריאל
                    if (inTutorial)
                        tm.NotifyTravellerSteppedBomb();

                    // ✅ אפקט פצצה + טלפורט:
                    // בטוטוריאל תמיד טלפורט לנקודת ההתחלה (לא משנה כמה חיים נשארו)
                    // במשחק רגיל: רק אפקט; ואם החיים נגמרו -> GameOver
                    if (inTutorial)
                    {
                        // בטוטוריאל לא נרצה להיכנס ל-GameOver
                        if (gm.lives <= 0) gm.lives = 1;

                        TryBombResetTeleportTo(playerNo, GetTutorialRespawnPos(), GetTutorialRespawnRot());
                    }
                    else
                    {
                        // אפקט בלבד (טלפורט לעצמו) כדי לקבל את אותו Visual reset
                        TryBombResetTeleportTo(playerNo, playerNo.transform.position, playerNo.transform.rotation);

                        if (gm.lives <= 0)
                            gameOver = true;
                    }

                    break;
                }
        }

        // ---- Broadcast to clients (mirror counters + HUD) ----
        ApplyPickupClientRpc(
            type,
            finalMessage,
            messageColor,
            messageDuration,
            gm.lives,
            gm.keys,
            gm.lifebuoys,
            gameOver
        );

        DespawnNow();
    }

    private void TryBombResetTeleportTo(NetworkObject playerNo, Vector3 pos, Quaternion rot)
    {
        if (playerNo == null) return;

        var move = playerNo.GetComponentInChildren<PlayerMovement1P>(true);
        if (move == null) return;

        var p = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { playerNo.OwnerClientId },
            },
        };

        move.BombResetAndTeleportClientRpc(
            pos,
            rot,
            bombPreTeleportDelay,
            bombRedSeconds,
            bombFadeOut,
            bombFadeIn,
            p
        );
    }

    private void DespawnNow()
    {
        var netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
            netObj.Despawn(true);
        else
            Destroy(gameObject);
    }

    // =====================================================================
    // CLIENT MIRROR (GameManager is not Networked in your project)
    // =====================================================================

    [ClientRpc]
    private void ApplyPickupClientRpc(
        PickupType pickupType,
        string msg,
        Color color,
        float duration,
        int lives,
        int keys,
        int lifebuoys,
        bool gameOver
    )
    {
        var hud = HUDManager.Instance;
        var gm = GameManager.Instance;
        if (hud == null || gm == null) return;

        gm.lives = lives;
        gm.keys = keys;
        gm.lifebuoys = lifebuoys;

        if (!string.IsNullOrEmpty(msg))
        {
            hud.SetMessageAppearanceForBoth(color, duration);
            hud.ShowMessageForBoth(msg);
        }

        if (pickupType == PickupType.Bomb)
            hud.FlashTravellerLife();

        hud.UpdateHUDs();

        if (gameOver)
            NetworkManager.Singleton.SceneManager.LoadScene("GameOver", LoadSceneMode.Single);
    }

    // =====================================================================
    // Relay for child colliders
    // =====================================================================

    private sealed class PickupTriggerRelay : MonoBehaviour
    {
        private PickupObject owner;
        public void SetOwner(PickupObject o) => owner = o;

        private void OnTriggerEnter(Collider other)
        {
            if (owner != null)
                owner.HandleTriggerEnter(other);
        }
    }
}
