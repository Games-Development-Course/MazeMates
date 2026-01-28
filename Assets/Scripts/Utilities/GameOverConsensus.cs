// Assets/Scripts/Net/GameOverConsensus.cs
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem; // אם אתה משתמש ב-Input System

public sealed class GameOverConsensus : NetworkBehaviour
{
    [Header("UI (assign in GameScene)")]
    [SerializeField] private GameObject travellerLoseWindow;
    [SerializeField] private GameObject navigatorLoseWindow;

    [Header("Input lock (optional)")]
    [Tooltip("אם יש לך PlayerInput על כל שחקן - זה הכי קל לנעול איתו.")]
    [SerializeField] private bool disableAllPlayerInputs = true;

    private bool _triggered;

    // קרא לזה מהשרת כשמגלים שהלבבות של הנווט הגיעו ל-0
    [ServerRpc(RequireOwnership = false)]
    public void TriggerNavigatorOutOfHeartsServerRpc()
    {
        if (_triggered) return;
        _triggered = true;

        TriggerGameOverClientRpc();
    }

    [ClientRpc]
    private void TriggerGameOverClientRpc()
    {
        if (travellerLoseWindow) travellerLoseWindow.SetActive(true);
        if (navigatorLoseWindow) navigatorLoseWindow.SetActive(true);

        LockKeyboardLocal();
    }

    private void LockKeyboardLocal()
    {
        // 1) לעצור קלט (מומלץ)
        if (disableAllPlayerInputs)
        {
            foreach (var pi in FindObjectsOfType<PlayerInput>(true))
                pi.enabled = false;
        }

        // 2) (אופציונלי) לשחרר עכבר/להראות סמן אם צריך תפריט
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 3) (אופציונלי) אם יש לך scripts של תזוזה בלי PlayerInput,
        // אפשר גם לכבות אותם פה (CharacterController scripts וכו').
        // לדוגמה:
        // foreach (var m in FindObjectsOfType<PlayerMovement>(true)) m.enabled = false;
    }
}
