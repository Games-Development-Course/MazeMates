using UnityEngine;
public enum PlayerRole
{
    Traveler,
    Navigator
}


public class RoleManager : MonoBehaviour
{
    public static PlayerRole Role = PlayerRole.Traveler; // ברירת מחדל

    public Camera travelerCam;
    public Camera navigatorCam;

    void Start()
    {
        // מפעיל רק את המצלמה המתאימה לתפקיד
        travelerCam.gameObject.SetActive(Role == PlayerRole.Traveler);
        navigatorCam.gameObject.SetActive(Role == PlayerRole.Navigator);
    }
}
