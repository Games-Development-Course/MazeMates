using UnityEngine;


/// --------------------------------------------------------------
/// ❤️ HEART — Simple spinning animation
/// --------------------------------------------------------------
public class HeartSpin : MonoBehaviour
{
    public float rotationSpeed = 90f;

    void Update()
    {
        transform.Rotate(0, rotationSpeed * Time.deltaTime, 0);
    }
}