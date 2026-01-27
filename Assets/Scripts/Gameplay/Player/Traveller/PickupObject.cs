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
    [SerializeField] private Transform tutorialTravellerRespawnPoint; // assign in TutorialScene

    [Header("Custom Message Settings")]
    [TextArea(2, 5)] public string customMessage = "";
    public Color messageColor = Color.white;
    public TMP_FontAsset messageFont;

    [Header("Message Duration")]
    public float messageDuration = 1.5f;

    [Header("Trigger Filtering")]
    [SerializeField] private LayerMask ignoreRelayLayers;

    [Header("Bomb Reset Visuals (PlayerMovement1P BombResetAndTeleportClientRpc params)")]
    public float bombPreTeleportDelay = 0.25f;
    public float bombRedSeconds = 0.15f;
    public float bombFadeOut = 0.25f;
    public float bombFadeIn = 0.35f;

    private bool consumedServer = false;

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

        // Only local owner requests pickup (prevents duplicates)
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

            // skip colliders on ignore layers
            if (((1 << c.gameObject.layer) & ignoreRelayLayers.value) != 0)
            {
                c.isTrigger = true;
                var existingRelay = c.GetComponent<PickupTriggerRelay>();
                if (existingRelay != null) Destroy(existingRelay);
                continue;
            }

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
        if (pickupNetId != NetworkObjectId) return;
        if (consumedServer) return;

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetId, out var playerNo))
            return;

        consumedServer = true;

        bool pickedByTraveller = IsTravellerPlayer(playerNo);

        // tutorial notify happens on SERVER before despawn
        NotifyTutorialServerSide(type, pickedByTraveller);

        var gm = GameManager.Instance;
        var hud = HUDManager.Instance;

        bool gameOver = false;
        string finalMessage = customMessage;

        // bomb spotlight off (server) before despawn
        if (type == PickupType.Bomb)
        {
            var bt = GetComponentInChildren<BombTrigger>(true);
            if (bt != null)
                bt.ForceOff_Server();
        }

        switch (type)
        {
            case PickupType.Heart:
                if (gm != null) gm.lives++;
                if (string.IsNullOrWhiteSpace(finalMessage))
                    finalMessage = "אספת לב! קיבלת חיים נוספים.";
                break;

            case PickupType.Key:
                // ✅ IMPORTANT: keys are networked -> add on server (authoritative)
                if (gm != null) gm.AddKeys(1);
                if (string.IsNullOrWhiteSpace(finalMessage))
                    finalMessage = "אספת מפתח!";
                break;

            case PickupType.Lifebuoy:
                if (gm != null) gm.lifebuoys++;
                if (string.IsNullOrWhiteSpace(finalMessage))
                    finalMessage = "אספת מצוף הצלה!";
                break;

            case PickupType.Bomb:
                if (string.IsNullOrWhiteSpace(finalMessage))
                    finalMessage = "דרכת על פצצה! איבדת לב.";

                if (hud != null) hud.FlashTravellerLife();

                bool inTutorial = IsInTutorialContextOnServer();

                if (gm != null) gm.lives--;

                bool shouldRespawn = pickedByTraveller;

                if (gm != null && gm.lives <= 0)
                {
                    if (inTutorial)
                    {
                        gm.lives = 1; // prevent death in tutorial
                        if (shouldRespawn && TryGetLevelTravellerStart(out var pos, out var rot))
                            TryBombResetTeleportTo(playerNo, pos, rot);
                    }
                    else
                    {
                        gameOver = true;
                        ShowLoseClientRpc();
                    }
                }
                else
                {
                    if (shouldRespawn && TryGetLevelTravellerStart(out var pos2, out var rot2))
                        TryBombResetTeleportTo(playerNo, pos2, rot2);
                }
                break;
        }

        // ✅ Mirror ONLY non-networked fields to clients.
        // Keys are already synced via NetworkVariable in GameManager.
        if (gm != null)
        {
            ApplyPickupClientRpc(
                type,
                finalMessage,
                messageColor,
                messageDuration,
                gm.lives,
                gm.lifebuoys,
                gameOver
            );
        }

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

        // fallback
        return playerNo.OwnerClientId == NetworkManager.ServerClientId;
    }

    private void NotifyTutorialServerSide(PickupType pickupType, bool pickedByTraveller)
    {
        if (!IsServer) return;
        if (!IsInTutorialContextOnServer()) return;

        var tm = Object.FindFirstObjectByType<TutorialManager>();
        if (tm == null || !tm.IsSpawned) return;

        if (tm.TutorialActive != null && !tm.TutorialActive.Value) return;

        switch (pickupType)
        {
            case PickupType.Key:
                if (pickedByTraveller) tm.NotifyTravellerPickedKey();
                break;

            case PickupType.Heart:
                if (pickedByTraveller) tm.NotifyTravellerPickedHeart();
                break;

            case PickupType.Bomb:
                if (pickedByTraveller) tm.NotifyTravellerSteppedBomb();
                break;

            default:
                break;
        }
    }

    private bool IsInTutorialContextOnServer()
    {
        if (gameObject.scene.IsValid() && gameObject.scene.name == tutorialSceneName)
            return true;

        var sc = SceneManager.GetSceneByName(tutorialSceneName);
        if (sc.IsValid() && sc.isLoaded)
            return true;

        return false;
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
    // CLIENT MIRROR
    // ============================================================

    [ClientRpc]
    private void ApplyPickupClientRpc(
        PickupType pickupType,
        string msg,
        Color color,
        float duration,
        int lives,
        int lifebuoys,
        bool gameOver
    )
    {
        var hud = HUDManager.Instance;
        var gm = GameManager.Instance;
        if (hud == null || gm == null) return;

        // ✅ Only mirror non-networked fields.
        gm.lives = lives;
        gm.lifebuoys = lifebuoys;

        // ✅ DO NOT set gm.keys here (keys is NetworkVariable now)

        if (!string.IsNullOrEmpty(msg))
        {
            hud.SetMessageAppearanceForBoth(color, duration);
            hud.ShowMessageForBoth(msg);
        }

        if (pickupType == PickupType.Bomb)
            hud.FlashTravellerLife();

        hud.UpdateHUDs();

        if (gameOver)
            gm.EndLevel();
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
    [ClientRpc]
    private void ShowLoseClientRpc()
    {
        CornerUIButtons.SetLoseScreenForBothPlayers(true);
    }
}
