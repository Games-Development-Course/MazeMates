using UnityEngine;

/// --------------------------------------------------------------
/// ❤️ HEART — Simple spinning animation
/// --------------------------------------------------------------
public class HeartSpin : MonoBehaviour
{
    public float rotationSpeed = 90f;

    void Update()
    {
        transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f, Space.World);
    }

}
