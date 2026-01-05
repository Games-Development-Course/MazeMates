using UnityEngine;
using Unity.Netcode;

public class BombStopZone : NetworkBehaviour
{
    private BombStepHelper helper;
    private TutorialManager tutorial;

    private void Awake()
    {
        helper = FindAnyObjectByType<BombStepHelper>();
        tutorial = FindAnyObjectByType<TutorialManager>();
    }

    private void OnTriggerEnter(Collider other)
    {
        // אם TM נוצר דינמית, לפעמים Awake קדם לו
        if (tutorial == null)
            tutorial = FindAnyObjectByType<TutorialManager>();

        Debug.Log($"[BombStopZone] ENTER by={other.name} root={other.transform.root.name} IsServer={NetworkManager.Singleton?.IsServer}");

        // תמיד נרצה שהשרת יבצע את ההתקדמות
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
        {
            // הטריגר קרה על הלקוח -> מבקשים מהשרת להשלים
            RequestCompleteServerRpc();
            return;
        }

        CompleteOnServer(other);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestCompleteServerRpc(ServerRpcParams rpcParams = default)
    {
        // אנחנו בשרת עכשיו - אבל אין לנו Collider של הלקוח כאן,
        // לכן פשוט נשלים "אם אפשר" לפי מצב השלב הנוכחי.
        CompleteOnServer(null);
    }

    private void CompleteOnServer(Collider other)
    {
        // שרת בלבד
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return;

        if (tutorial == null)
        {
            Debug.LogWarning("[BombStopZone] TutorialManager not found on server");
            return;
        }

        // אם קיבלנו Collider (מקרה שרת נכנס לטריגר בעצמו), נוודא שזה באמת ה-Traveller
        if (other != null)
        {
            var enteredNo = other.GetComponentInParent<NetworkObject>();
            if (enteredNo == null)
            {
                Debug.LogWarning("[BombStopZone] entered object has no NetworkObject");
                return;
            }

            // Traveller = PlayerObject של השרת (Host)
            ulong travellerClientId = NetworkManager.ServerClientId;

            // הבדיקה הכי יציבה: זה PlayerObject והבעלים שלו הוא host
            if (!enteredNo.IsPlayerObject || enteredNo.OwnerClientId != travellerClientId)
            {
                Debug.LogWarning($"[BombStopZone] Not traveller. enteredOwner={enteredNo.OwnerClientId} expected={travellerClientId} isPlayer={enteredNo.IsPlayerObject}");
                return;
            }
        }

        Debug.Log("[BombStopZone] OK -> completing step via CustomEvent");

        helper?.DisableTargetCollider();

        tutorial.NotifyCustomEvent();
        Debug.Log("[BombStopZone] NotifyCustomEvent() called (SERVER)");
    }
}
