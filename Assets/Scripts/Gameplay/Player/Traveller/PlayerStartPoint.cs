using UnityEngine;

public class PlayerStartPoint : MonoBehaviour
{
    public enum Role { Traveller, Navigator }
    public Role role;

    public static PlayerStartPoint TravellerPoint;
    public static PlayerStartPoint NavigatorPoint;

    public Vector3 startPosition;
    public Quaternion startRotation;

    private void Awake()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;

        if (role == Role.Traveller)
            TravellerPoint = this;
        else if (role == Role.Navigator)
            NavigatorPoint = this;
    }
}
