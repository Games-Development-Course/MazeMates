using TMPro;
using UnityEngine;
using MazeMates.Authentication;

public class PlayerStatusUI : MonoBehaviour
{
    [Header("Status UI")]
    [SerializeField] private GameObject root;            // הפאנל עצמו (UserDetails)
    [SerializeField] private TMP_Text usernameLabel;     // TMP של שם המשתמש
    [SerializeField] private TMP_Text connectedLabel;    // TMP של "מחובר"

    [Header("Optional")]
    [Tooltip("שים כאן את LobbyRoot כדי שנוכל להדליק את כל השרשרת עד אליו.")]
    [SerializeField] private GameObject lobbyRoot;

    [Header("Behavior")]
    [Tooltip("אם true: כשנכנסים ללובי והוא כבר SignedIn מסשן קודם, יציג 'אורח'. אם false: לא מציג כלום עד SetUser/SetGuest.")]
    [SerializeField] private bool autoShowIfAlreadySignedIn = false;

    [Header("Debug")]
    [SerializeField] private bool verbose = true;

    private void Awake()
    {
        Hide();
    }

    private void OnEnable()
    {
        if (autoShowIfAlreadySignedIn)
            RefreshFromAuth();
    }

    public void SetGuest() => SetName("אורח");

    public void SetUser(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            SetGuest();
        else
            SetName(username.Trim());
    }

    public void Clear() => Hide();

    public void Hide()
    {
        if (root != null) root.SetActive(false);

        if (usernameLabel) usernameLabel.text = "";
        if (connectedLabel) connectedLabel.text = "";

        if (verbose) Debug.Log($"[PlayerStatusUI] Hide | {Dump()}");
    }

    public void RefreshFromAuth()
    {
        if (UgsAuthManager.Instance != null && UgsAuthManager.Instance.IsSignedIn)
            SetGuest();
        else
            Hide();
    }

    // -------------------- Internals --------------------

    private void SetName(string name)
    {
        ForceShow();

        ApplyUsernameDirection(name);

        if (usernameLabel) usernameLabel.text = name;
        if (connectedLabel) connectedLabel.text = "מחובר";

        if (verbose)
            Debug.Log($"[PlayerStatusUI] SetName('{name}') | {Dump()} | user='{(usernameLabel ? usernameLabel.text : "<NULL>")}' connected='{(connectedLabel ? connectedLabel.text : "<NULL>")}'");
    }

    private void ApplyUsernameDirection(string text)
    {
        if (usernameLabel == null) return;

        bool rtl = ContainsRTL(text);

        usernameLabel.isRightToLeftText = rtl;
    }

    private static bool ContainsRTL(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;

        foreach (char c in s)
        {
            // Hebrew + Arabic ranges
            if ((c >= '\u0590' && c <= '\u05FF') ||
                (c >= '\u0600' && c <= '\u06FF') ||
                (c >= '\u0750' && c <= '\u077F') ||
                (c >= '\u08A0' && c <= '\u08FF'))
                return true;
        }
        return false;
    }

    /// <summary>
    /// מדליק את root וגם מוודא שההורים עד LobbyRoot פעילים.
    /// </summary>
    private void ForceShow()
    {
        if (root == null)
        {
            gameObject.SetActive(true);
            return;
        }

        // מדליקים שרשרת הורים עד lobbyRoot (אם קיים)
        Transform t = root.transform;
        while (t != null)
        {
            t.gameObject.SetActive(true);

            if (lobbyRoot != null && t.gameObject == lobbyRoot)
                break;

            t = t.parent;
        }

        root.SetActive(true);
    }

    private string Dump()
    {
        string r = root ? $"{root.name} activeSelf={root.activeSelf} activeInHierarchy={root.activeInHierarchy}" : "<NULL root>";
        string u = usernameLabel ? $"{usernameLabel.name} activeInHierarchy={usernameLabel.gameObject.activeInHierarchy}" : "<NULL usernameLabel>";
        string c = connectedLabel ? $"{connectedLabel.name} activeInHierarchy={connectedLabel.gameObject.activeInHierarchy}" : "<NULL connectedLabel>";
        string lr = lobbyRoot ? $"{lobbyRoot.name} activeSelf={lobbyRoot.activeSelf} activeInHierarchy={lobbyRoot.activeInHierarchy}" : "<NULL lobbyRoot>";
        return $"this={name} activeInHierarchy={gameObject.activeInHierarchy} | lobbyRoot={lr} | root={r} | username={u} | connected={c}";
    }
}
