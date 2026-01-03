//using UnityEngine;
//using Unity.Netcode;
//using System.Collections;

//public class LocalBoot : MonoBehaviour
//{
//    [Header("Local Test Mode")]
//    [SerializeField] private bool enableLocalSpawn = true;

//    [Header("Traveller")]
//    [SerializeField] private GameObject travellerPrefab;

//    [Header("Spawn Position")]
//    [SerializeField] private Vector3 spawnPosition = new Vector3(-9.38f, 0.5f, -27.81f);

//    private void Start()
//    {
//        if (!enableLocalSpawn)
//            return;

//        StartCoroutine(BootLocal());
//    }

//    private IEnumerator BootLocal()
//    {
//        // ===============================
//        // 1. Find existing NetworkManager
//        // ===============================
//        NetworkManager nm = FindFirstObjectByType<NetworkManager>();
//        if (nm == null)
//        {
//            Debug.LogError("[LocalBoot] NetworkManager not found in scene.");
//            yield break;
//        }

//        // ===============================
//        // 2. Start Host (local)
//        // ===============================
//        if (!nm.IsListening)
//        {
//            nm.StartHost();
//            yield return null; // תן ל־Netcode פריים אחד
//        }

//        // ===============================
//        // 3. Spawn Traveller
//        // ===============================
//        GameObject traveller = Instantiate(travellerPrefab, spawnPosition, Quaternion.identity);
//        traveller.name = "Traveller_LOCAL";

//        NetworkObject netObj = traveller.GetComponent<NetworkObject>();
//        if (netObj != null && !netObj.IsSpawned)
//        {
//            netObj.Spawn(true); // owner = host
//        }
//    }
//}
