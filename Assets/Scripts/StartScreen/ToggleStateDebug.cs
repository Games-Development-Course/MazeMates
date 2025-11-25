using UnityEngine;
using UnityEngine.UI;

public class ToggleStateDebug : MonoBehaviour
{
    private Toggle t;
    private bool lastState;

    void Start()
    {
        t = GetComponent<Toggle>();
        lastState = t.isOn;
    }

    void Update()
    {
        if (t.isOn != lastState)
        {
            Debug.Log(gameObject.name + " changed -> isOn = " + t.isOn);
            lastState = t.isOn;
        }
    }
}
