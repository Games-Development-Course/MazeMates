using UnityEngine;

public class PlayerStartPoint : MonoBehaviour
{
    public static PlayerStartPoint Instance;
    public Vector3 startPosition;

    private void Awake()
    {
        Instance = this;
        startPosition = transform.position;
    }
}
