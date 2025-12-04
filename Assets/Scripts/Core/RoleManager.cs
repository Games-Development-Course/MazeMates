using UnityEngine;

public enum PlayerRole
{
    Traveler,
    Navigator,
}

public class RoleManager : MonoBehaviour
{
    public static PlayerRole Role = PlayerRole.Traveler; // ����� ����

    public Camera travelerCam;
    public Camera navigatorCam;

    void Start()
    {
        // ����� �� �� ������ ������� ������
        travelerCam.gameObject.SetActive(Role == PlayerRole.Traveler);
        navigatorCam.gameObject.SetActive(Role == PlayerRole.Navigator);
    }
}
