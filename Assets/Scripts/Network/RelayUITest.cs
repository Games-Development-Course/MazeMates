// using TMPro;
// using Unity.Netcode;
// using Unity.VisualScripting;
// using UnityEngine;

// public class RelayUITest : NetworkBehaviour
// {
//     [Header("UI References")]
//     [SerializeField]
//     private GameObject connectionPanel; // הפאנל עם Host/Join/קוד

//     [SerializeField]
//     private TMP_Text codeLabel; // הטקסט שמציג את הקוד (רק הקוד / או "Creating...")

//     [SerializeField]
//     private TMP_InputField codeInput; // השדה שבו הנווט מזין קוד

//     // אם יש לך אובייקטים נפרדים לכפתורים/אזור ג׳וין – גרור אותם כאן (מומלץ)
//     [Header("Optional UI Groups (recommended)")]
//     [SerializeField]
//     private GameObject hostJoinButtonsRoot; // קבוצה שמכילה את כפתורי Host/Join

//     [SerializeField]
//     private GameObject joinAreaRoot; // קבוצה שמכילה את ה-Input + כפתור Join

//     private bool lobbyHidden = false;

//     // מונע לחיצות כפולות / race
//     private bool hostInProgress = false;
//     private int hostRequestVersion = 0;

//     // ===========================
//     // NETCODE LIFECYCLE
//     // ===========================
//     public override void OnNetworkSpawn()
//     {
//         Debug.Log(
//             $"[RelayTestUI] OnNetworkSpawn | IsServer={IsServer} IsClient={IsClient} IsOwner={IsOwner}"
//         );

//         if (IsServer && NetworkManager.Singleton != null)
//         {
//             Debug.Log("[RelayTestUI] Subscribing to OnClientConnectedCallback (SERVER)");
//             NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedServer;
//         }

//         if (connectionPanel == null)
//             Debug.LogWarning("[RelayTestUI] connectionPanel is NULL in inspector!");
//     }

//     public override void OnNetworkDespawn()
//     {
//         if (IsServer && NetworkManager.Singleton != null)
//         {
//             Debug.Log("[RelayTestUI] Unsubscribing from OnClientConnectedCallback (SERVER)");
//             NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedServer;
//         }
//     }

//     private void Start()
//     {
//         Debug.Log("[RelayTestUI] Ready (Start).");
//     }

//     // ===========================
//     // SERVER: כשקליינט מתחבר
//     // ===========================
//     private void OnClientConnectedServer(ulong clientId)
//     {
//         if (NetworkManager.Singleton == null)
//         {
//             Debug.LogWarning(
//                 "[RelayTestUI] OnClientConnectedServer called but NetworkManager is NULL"
//             );
//             return;
//         }

//         var ids = NetworkManager.Singleton.ConnectedClientsIds;
//         int count = ids != null ? ids.Count : 0;

//         Debug.Log($"[RelayTestUI] OnClientConnectedServer (clientId={clientId}), total={count}");

//         // כשיש 2 שחקנים או יותר – להסתיר לובי אצל כולם
//         if (!lobbyHidden && count >= 2)
//         {
//             Debug.Log("[RelayTestUI] Two players connected → calling HideLobbyPanelClientRpc");
//             lobbyHidden = true;
//             HideLobbyPanelClientRpc();
//         }
//     }

//     // ===========================
//     // CLIENT RPC – רץ אצל כולם
//     // ===========================
//     [ClientRpc]
//     private void HideLobbyPanelClientRpc()
//     {
//         Debug.Log(
//             $"[RelayTestUI] HideLobbyPanelClientRpc on client | IsServer={IsServer} IsClient={IsClient}"
//         );

//         if (connectionPanel != null)
//         {
//             Debug.Log("[RelayTestUI] Hiding connectionPanel on this client");
//             connectionPanel.SetActive(false);
//         }
//         else
//         {
//             Debug.LogWarning(
//                 "[RelayTestUI] connectionPanel is NULL on this client – cannot hide UI"
//             );
//         }
//     }

//     // ===========================
//     // HOST BUTTON
//     // ===========================
//     public async void OnHostClicked()
//     {
//         Debug.Log("[RelayTestUI] Host button clicked");

//         // כבר בתהליך
//         if (hostInProgress)
//             return;

//         // אם כבר Host/Client רץ – לא עושים שוב
//         if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
//             return;

//         hostInProgress = true;
//         int myVersion = ++hostRequestVersion;

//         // UI: אחרי לחיצה על Host - להציג רק קוד (או "Creating...") ולהסתיר כפתורים
//         ShowHostCodeOnlyUI(creating: true, code: "");

//         string joinCode = await RelayManager.Instance.StartHostWithRelayAsync();

//         // אם נלחץ Host שוב בזמן שהמתנו - מתעלמים מתוצאה ישנה
//         if (myVersion != hostRequestVersion)
//             return;

//         hostInProgress = false;

//         if (!string.IsNullOrEmpty(joinCode))
//         {
//             ShowHostCodeOnlyUI(creating: false, code: joinCode);

//             // מציגים את הקוד אצל ההוסט (רק הקוד / בלי "Code:\n")
//             if (codeLabel != null)
//                 codeLabel.text = joinCode;

//             // ממלאים גם את ה-Input שיהיה נוח להעתיק/להדביק (ואפשר גם לנעול לעריכה)
//             if (codeInput != null)
//             {
//                 codeInput.text = joinCode;
//                 codeInput.interactable = false; // Host לא צריך לערוך
//             }

//             Debug.Log($"[RelayTestUI] Host got joinCode = {joinCode}");
//         }
//         else
//         {
//             Debug.LogWarning("[RelayTestUI] Host failed, no join code.");

//             // חזרה למסך בחירה רגיל במקרה כשל
//             ShowLobbyButtons(true);
//             if (codeLabel != null)
//                 codeLabel.text = "";
//             if (codeInput != null)
//             {
//                 codeInput.interactable = true;
//                 codeInput.text = "";
//             }
//         }
//     }

//     // ===========================
//     // JOIN BUTTON (נווט)
//     // ===========================
//     public async void OnJoinClicked()
//     {
//         Debug.Log("[RelayTestUI] Join button clicked");

//         string joinCode = codeInput != null ? codeInput.text.Trim().ToUpper() : "";

//         if (string.IsNullOrEmpty(joinCode))
//         {
//             Debug.LogWarning("[RelayTestUI] Join code is empty.");
//             return;
//         }

//         Debug.Log($"[RelayTestUI] Trying to join with code = {joinCode}");

//         bool ok = await RelayManager.Instance.StartClientWithRelayAsync(joinCode);
//         Debug.Log("[RelayTestUI] StartClient returned = " + ok);
//         codeLabel.gameObject.SetActive(false);
//         // אופציונלי: אחרי Join להסתיר את הפאנל אצל הלקוח (אם רוצים)
//         if (ok && connectionPanel != null) connectionPanel.SetActive(false);
//         if(codeLabel != null)
//         {
//             codeLabel.gameObject.SetActive(false);
//         }
//     }

//     // ===========================
//     // UI HELPERS
//     // ===========================

//     private void ShowLobbyButtons(bool show)
//     {
//         // אם לא חיברת roots - ננסה לפחות להשאיר את panel פעיל
//         if (hostJoinButtonsRoot != null)
//             hostJoinButtonsRoot.SetActive(show);
//         if (joinAreaRoot != null)
//             joinAreaRoot.SetActive(show);
//     }

//     private void ShowHostCodeOnlyUI(bool creating, string code)
//     {
//         // להשאיר את הפאנל עצמו פעיל, אבל להעלים כפתורים/אזור ג׳וין אם יש
//         if (connectionPanel != null)
//             connectionPanel.SetActive(true);

//         ShowLobbyButtons(false);

//         // במצב Host: לא צריך Join
//         if (codeInput != null)
//             codeInput.gameObject.SetActive(false);

//         if (codeLabel != null)
//             codeLabel.text = creating ? "Creating room..." : code;
//     }
// }
