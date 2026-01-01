// PlayerStartPoint.cs
using UnityEngine;

public class PlayerStartPoint : MonoBehaviour
{
    public enum Role { Traveller, Navigator }
    [SerializeField] public Role role;

    public Vector3 Position => transform.position;
    public Quaternion Rotation => transform.rotation;
}
