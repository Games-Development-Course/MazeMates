using UnityEngine;

public class NetworkBootstrap : MonoBehaviour
{
    public static NetworkBootstrap Instance { get; private set; }

    private void Awake()
    {
        // אם כבר קיים Bootstrap, משמידים את הכפול כדי לא ליצור שני NetworkManager וכדומה
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // חשוב: חייב להיות Root GameObject (לא ילד של משהו אחר)
        DontDestroyOnLoad(gameObject);
    }
}
