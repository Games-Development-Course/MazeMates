using UnityEngine;
using MazeMates.Authentication;

public class GuestButtonHandler : MonoBehaviour
{
    [SerializeField] private GameObject authRoot;
    [SerializeField] private GameObject lobbyRoot;

    [Header("UI")]
    [SerializeField] private AuthMessageUI authMessageUI;   // פאנל הודעות באותנטיקציה
    [SerializeField] private PlayerStatusUI lobbyPlayerStatusUI; // סטטוס בלובי

    public async void OnGuestClicked()
    {
        authMessageUI?.ShowInfo("מתחבר כאורח...");

        bool ok = await UgsAuthManager.Instance.SignInAsGuestAsync();
        if (!ok)
        {
            // תרגום פשוט – אותו לוגיקה כמו בלוגין (הכי חשוב: בעברית)
            string heb = TranslateFromMessage(UgsAuthManager.Instance.LastErrorRaw);
            authMessageUI?.ShowError(heb);
            return;
        }

        authMessageUI?.ShowSuccess("התחברת כאורח!");

        if (lobbyRoot != null) lobbyRoot.SetActive(true);
        if (authRoot != null) authRoot.SetActive(false);

        await System.Threading.Tasks.Task.Yield();

        lobbyPlayerStatusUI?.SetGuest();
    }

    private static string TranslateFromMessage(string msg)
    {
        if (string.IsNullOrWhiteSpace(msg))
            return "הפעולה נכשלה. נסה שוב.";

        string m = msg.ToUpperInvariant();

        if (m.Contains("NETWORK") || m.Contains("TIMEOUT") || m.Contains("CONNECTION"))
            return "בעיית תקשורת/רשת. בדוק אינטרנט ונסה שוב.";

        if (m.Contains("RATE_LIMIT") || m.Contains("TOO MANY"))
            return "יותר מדי ניסיונות. המתן קצת ונסה שוב.";

        return "ההתחברות כאורח נכשלה. נסה שוב.";
    }
}
