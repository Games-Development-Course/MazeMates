using System.Runtime.InteropServices;
using UnityEngine;
using TMPro;

public class OpenSecondWindowButton : MonoBehaviour
{
    [SerializeField] private TMP_InputField joinCodeInput; // או שתזין דרך קוד ממנהל הלובי

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void OpenSecondTabWithJoinCode(string joinCode);
#endif

    public void OnClickOpenSecondWindow()
    {
        string joinCode = joinCodeInput != null ? joinCodeInput.text : null;

        if (string.IsNullOrWhiteSpace(joinCode))
        {
            Debug.LogWarning("Join code is empty - cannot open second window.");
            return;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        OpenSecondTabWithJoinCode(joinCode.Trim());
#else
        Debug.Log("OpenSecondWindow works only in WebGL build.");
#endif
    }
}
