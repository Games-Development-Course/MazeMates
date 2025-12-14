using UnityEngine;

public class ExitDoorKeysTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
{
    if (!other.CompareTag("Player")) return;

    if (!GameManager.Instance.AllKeysCollected())
        return;

    // מוצא את הפלטה ומרים אותה
    var plate = FindAnyObjectByType<VictoryPlateRevealer>();
    plate?.RaisePlate();
}
}
