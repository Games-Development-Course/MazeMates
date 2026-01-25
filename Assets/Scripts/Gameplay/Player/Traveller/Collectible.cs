// Assets/Scripts/Collectible.cs
using UnityEngine;

public sealed class Collectible : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioClip pickupClip;
    [SerializeField] [Range(0f, 1f)] private float volume = 1f;

    [Header("Pickup")]
    [SerializeField] private bool destroyOnPickup = true;

    private bool _pickedUp;

    private void OnTriggerEnter(Collider other)
    {
        if (_pickedUp) return;
        if (!other.CompareTag("Player")) return;

        _pickedUp = true;

        if (pickupClip != null)
            AudioSource.PlayClipAtPoint(pickupClip, transform.position, volume);

        // TODO: add score/inventory here

        if (destroyOnPickup) Destroy(gameObject);
        else gameObject.SetActive(false);
    }
}
