using System;
using UnityEngine;

public class NavEnvWatcher : MonoBehaviour
{
    private void OnEnable()
    {
        Debug.Log("[NavEnvWatcher] ENABLED", this);
    }

    private void OnDisable()
    {
        Debug.Log("[NavEnvWatcher] DISABLED\n" + Environment.StackTrace, this);
    }
}
