using UnityEngine;

public class MultiDisplayManager : MonoBehaviour
{
    void Start()
    {
        // מפעיל את כל המסכים שמחוברים
        for (int i = 1; i < Display.displays.Length; i++)
        {
            Display.displays[i].Activate();
        }
    }
}
