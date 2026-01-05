using UnityEngine;

namespace MazeMates.Authentication
{
    public class AuthGateUI : MonoBehaviour
    {
        [SerializeField] private GameObject authRoot;
        [SerializeField] private GameObject lobbyRoot;

        private void Awake()
        {
            // כדי שלא יהיה "פלאש", ננעל על Auth כבר ב-Awake
            ShowAuth();
        }

        private async void Start()
        {
            if (UgsAuthManager.Instance == null)
            {
                Debug.LogError("UgsAuthManager is missing. Add it to StartScene.");
                return;
            }

            await UgsAuthManager.Instance.InitializeAsync();

            // אין Auto Sign-In מכל סוג שהוא.
            // פשוט מציגים Lobby רק אם כבר SignedIn (למשל אחרי Login מוצלח באותו Play).
            EnforceGateNow();

            UgsAuthManager.Instance.SignedIn += EnforceGateNow;
            UgsAuthManager.Instance.SignedOut += ShowAuth;
            UgsAuthManager.Instance.AuthError += msg => Debug.LogWarning(msg);
        }

        private void OnDestroy()
        {
            if (UgsAuthManager.Instance == null) return;
            UgsAuthManager.Instance.SignedIn -= EnforceGateNow;
            UgsAuthManager.Instance.SignedOut -= ShowAuth;
        }

        private void EnforceGateNow()
        {
            if (UgsAuthManager.Instance != null && UgsAuthManager.Instance.IsSignedIn)
                ShowLobby();
            else
                ShowAuth();
        }

        private void ShowAuth()
        {
            if (authRoot != null) authRoot.SetActive(true);
            if (lobbyRoot != null) lobbyRoot.SetActive(false);
        }

        private void ShowLobby()
        {
            if (authRoot != null) authRoot.SetActive(false);
            if (lobbyRoot != null) lobbyRoot.SetActive(true);
        }
    }
}
