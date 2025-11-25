using UnityEngine;

public class DoorPadToggle : MonoBehaviour
{
    [Header("Solo Traveller Game?")]
    public bool allowSpaceActivation = true;

    public static DoorPadToggle Instance;

    void Awake()
    {
        Instance = this;
    }
}
