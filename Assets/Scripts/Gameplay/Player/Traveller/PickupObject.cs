// File: Assets/Scripts/Gameplay/Pickups/PickupObject.cs
//
// ✅ Fixes (based on the version that worked for you):
// 1) Tutorial notify happens on SERVER (authoritative) at the moment of pickup.
// 2) "Picked by Traveller" is detected by comparing the player NetworkObject to GameManager.Instance.traveller
//    (so it works even if Traveller is NOT the host).
// 3) Tutorial gating uses the pickup object's scene (gameObject.scene) + fallback isLoaded check.
// 4) No brittle caching of TutorialManager (won’t get stuck on null).
// 5) Bomb keeps the tutorial reset/teleport flow and calls the bomb tutorial notify.
//
// Notes:
// - ResourceManager expects: p.type and PickupObject.PickupType to be public -> kept public.
// - Colliders: relay is added to ALL child colliders and they are forced to isTrigger=true.

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
    [SerializeField] private Transform tutorialTravellerRespawnPoint; // assign in TutorialScene

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

    private bool triggerHandledLocal;   // prevents spamming ServerRpc from this client
    private bool consumedServer;        // authoritative (use inside ServerRpc too)


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
    public void HandleTriggerEnter(Collider other)
    {
        if (other == null) return;
        if (!other.CompareTag("Player")) return;

        var playerNo = other.GetComponentInParent<NetworkObject>();
        if (playerNo == null) return; // don't lock out on invalid collider

        if (triggerHandledLocal) return;
        triggerHandledLocal = true;

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

    // ============================================================
    // SERVER AUTHORITATIVE PICKUP
    // ============================================================

    [ServerRpc(RequireOwnership = false)]
    private void RequestPickupServerRpc(ulong pickupNetId, ulong playerNetId, ServerRpcParams rpcParams = default)
    {
        Debug.Log($"[Pickup][SERVER-RPC] type={type} pickupId={pickupNetId} playerId={playerNetId}");

        if (pickupNetId != NetworkObjectId) return;
        if (consumedServer) return;

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetId, out var playerNo))
            return;

        consumedServer = true;
        bool pickedByTraveller = IsTravellerPlayer(playerNo);
        NotifyTutorialServerSide(type, pickedByTraveller);
        var gm = GameManager.Instance;
        var hud = HUDManager.Instance;

        if (gm == null)
        {
            DespawnNow();
            return;
        }

        bool gameOver = false;
        string finalMessage = customMessage;

        switch (type)
        {
            case PickupType.Heart:
                Debug.Log($"[Pickup][SERVER] HEART before={gm.lives}");
                gm.lives++;
                Debug.Log($"[Pickup][SERVER] HEART after={gm.lives}");

                if (string.IsNullOrWhiteSpace(finalMessage))
                    finalMessage = "אספת לב! קיבלת חיים נוספים.";
                break;

            case PickupType.Key:
                gm.keys++;
                if (string.IsNullOrWhiteSpace(finalMessage))
                    finalMessage = "אספת מפתח!";
                break;


            case PickupType.Bomb:
                {
                    if (string.IsNullOrWhiteSpace(finalMessage))
                        finalMessage = "דרכת על פצצה! איבדת לב.";

                    if (hud != null)
                        hud.FlashTravellerLife();

                    bool inTutorial = IsInTutorialContextOnServer();
                    gm.lives--;
                    bool shouldRespawn = pickedByTraveller;

                    if (gm.lives <= 0)
                    {
                        if (inTutorial)
                        {
                            gm.lives = 1;

                            if (shouldRespawn && TryGetLevelTravellerStart(out var pos, out var rot))
                                TryBombResetTeleportTo(playerNo, pos, rot);
                        }
                        else
                        {
                            gameOver = true;
                        }
                    }
                    else
                    {
                        if (shouldRespawn && TryGetLevelTravellerStart(out var pos, out var rot))
                            TryBombResetTeleportTo(playerNo, pos, rot);
                    }

                    break;
                }
        }

        // Mirror to all clients (your GameManager isn’t networked)
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
    private bool IsTravellerPlayer(NetworkObject playerNo)
    {
        var gm = GameManager.Instance;
        if (gm != null && gm.traveller != null)
        {
            var tNo = gm.traveller.GetComponent<NetworkObject>();
            if (tNo != null && tNo.IsSpawned)
                return tNo.NetworkObjectId == playerNo.NetworkObjectId;
        }

        // Fallback (old assumption): host == traveller
        return playerNo.OwnerClientId == NetworkManager.ServerClientId;
    }

    private void NotifyTutorialServerSide(PickupType pickupType, bool pickedByTraveller)
    {
        if (!IsServer) return;
        if (!IsInTutorialContextOnServer()) return;

        var tm = Object.FindFirstObjectByType<TutorialManager>();
        if (tm == null || !tm.IsSpawned) return;

        // If your tutorial expects "only when tutorial is active", keep this gate:
        if (tm.TutorialActive != null && !tm.TutorialActive.Value) return;

        switch (pickupType)
        {
            case PickupType.Key:
                if (pickedByTraveller)
                    tm.NotifyTravellerPickedKey();
                break;

            case PickupType.Heart:
                if (pickedByTraveller)
                    tm.NotifyTravellerPickedHeart();
                break;

            case PickupType.Bomb:
                if (pickedByTraveller)
                    tm.NotifyTravellerSteppedBomb();
                break;

            case PickupType.Lifebuoy:
            default:
                break;
        }
    }

    private bool IsInTutorialContextOnServer()
    {
        // Prefer pickup object's own scene (works with additive / wrong active scene)
        if (gameObject.scene.IsValid() && gameObject.scene.name == tutorialSceneName)
            return true;

        // Fallback: if TutorialScene is loaded at all
        var sc = SceneManager.GetSceneByName(tutorialSceneName);
        if (sc.IsValid() && sc.isLoaded)
            return true;

        return false;
    }

    private Vector3 GetTutorialRespawnPos()
    {
        if (tutorialTravellerRespawnPoint != null)
            return tutorialTravellerRespawnPoint.position;

        return new Vector3(1f, 1f, 1f);
    }

    private Quaternion GetTutorialRespawnRot()
    {
        if (tutorialTravellerRespawnPoint != null)
            return tutorialTravellerRespawnPoint.rotation;

        return Quaternion.identity;
    }

    private void TryBombResetTeleportTo(NetworkObject playerNo, Vector3 pos, Quaternion rot)
    {
        if (playerNo == null) return;

        var move = playerNo.GetComponentInChildren<PlayerMovement>(true);
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

    // ============================================================
    // CLIENT MIRROR (GameManager is not Networked in your project)
    // ============================================================

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

    // ============================================================
    // Relay for child colliders
    // ============================================================

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
    private bool TryGetLevelTravellerStart(out Vector3 pos, out Quaternion rot)
    {
        pos = default;
        rot = default;

        var points = Object.FindObjectsByType<PlayerStartPoint>(FindObjectsSortMode.None);
        foreach (var p in points)
        {
            if (p != null && p.role == PlayerStartPoint.Role.Traveller)
            {
                pos = p.transform.position;
                rot = p.transform.rotation;
                return true;
            }
        }

        Debug.LogWarning("[PickupObject] No PlayerStartPoint(Role.Traveller) found in this scene.");
        return false;
    }

}
