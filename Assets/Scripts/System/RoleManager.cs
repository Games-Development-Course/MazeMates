using UnityEngine;

public enum PlayerRole { Traveler, Navigator }

public class RoleManager: MonoBehaviour
{
    public static PlayerRole Role;

    void Awake()
    {
        Debug.Log("I am: " + Role);
    }
}
