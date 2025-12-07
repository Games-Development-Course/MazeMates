    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using Unity.Netcode;
    using UnityEngine;

    public class DoorController : NetworkBehaviour
    {
        [Header("Door Settings")]
        public DoorType doorType;
        public float openAngle = 90f;
        public float openSpeed = 3f;

        [Header("Puzzle Settings")]
        public GameObject puzzlePrefab;
        public Sprite navigatorPreview;

        // OLD SYSTEM SUPPORT
        public List<GameObject> spawnedHints = new List<GameObject>();

        private Transform pivot;
        private IDoor door;
        private PadTrigger pad;

        // =====================================================================
        // NETWORK SPAWN — THE ONLY SAFE PLACE TO INITIALIZE PIVOT + DOOR LOGIC
        // =====================================================================
        public override void OnNetworkSpawn()
        {
            Debug.Log($"[DOOR SPAWN] {name} | NetId={NetworkObjectId}");

            pad = GetComponentInChildren<PadTrigger>();

            if (pivot == null)
                FindOrCreatePivot();      // ← עכשיו בטוח לעשות את זה

            InitDoorLogic();              // ← עכשיו door לא יהיה Null ולא יישבר ב־Clients
        }

        private void InitDoorLogic()
        {
            switch (doorType)
            {
                case DoorType.Puzzle:
                    navigatorPreview = ExtractPreviewFromPrefab();
                    door = new PuzzleDoor(this);
                    break;

                case DoorType.Normal:
                    door = new NormalDoor(this);
                    break;

                case DoorType.Exit:
                    door = new ExitDoor(this);
                    break;
            }
        }

        // =====================================================================
        // INTERACTION
        // =====================================================================
        public void Interact()
        {
            // Puzzle door should NOT open by "interact"
            if (doorType == DoorType.Puzzle)
            {
                // Only navigator button should open puzzle
                return;
            }

            RequestOpenDoorRpc();
        }


        public bool TravellerIsOnPad() => pad != null && pad.IsPlayerOnPad();
        public bool IsOpen() => door != null && door.IsOpen();
        public PuzzleDoor GetPuzzle() => door as PuzzleDoor;

        private Sprite ExtractPreviewFromPrefab()
        {
            if (puzzlePrefab == null) return null;

            Transform original = puzzlePrefab.transform.Find("OriginalImage");
            if (original == null) return null;

            var img = original.GetComponentInChildren<UnityEngine.UI.Image>();
            return img != null ? img.sprite : null;
        }

        // =====================================================================
        // RPC SYSTEM
        // =====================================================================
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestOpenDoorRpc()
        {
            if (!IsServer) return;

            StartCoroutine(OpenRoutine(openAngle));

            // Mirror to all clients
            OpenDoorRpc();
        }
    [Rpc(SendTo.Server)]
    private void RequestBombRemovalRpc(ulong objectId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects
            .TryGetValue(objectId, out var obj))
        {
            var netObj = obj.GetComponent<NetworkObject>();
            if (netObj != null)
                netObj.Despawn();  // מחיקה נכונה ב-Netcode
        }
    }


    [Rpc(SendTo.Everyone)]
        private void OpenDoorRpc()
        {
            StartCoroutine(OpenRoutine(openAngle));
        }

        private IEnumerator OpenRoutine(float angle)
        {
            Quaternion target = Quaternion.Euler(0, angle, 0);

            while (Quaternion.Angle(pivot.localRotation, target) > 0.1f)
            {
                pivot.localRotation = Quaternion.Lerp(
                    pivot.localRotation,
                    target,
                    Time.deltaTime * openSpeed);

                yield return null;
            }

            pivot.localRotation = target;
        }


        // =====================================================================
        // CREATE PIVOT AFTER NETCODE SYNC (IMPORTANT!)
        // =====================================================================
        private void FindOrCreatePivot()
        {
            MeshFilter mf = GetComponentsInChildren<MeshFilter>(true)
                .FirstOrDefault(m => m.CompareTag("Door"));

            if (mf == null)
            {
                Debug.LogError("DoorController: No child with tag 'Door' found.");
                return;
            }

            Transform doorModel = mf.transform;
            Bounds b = mf.sharedMesh.bounds;
            float half = b.size.x * 0.5f;

            Vector3 leftLocal = new Vector3(b.center.x - half, b.center.y, b.center.z);
            Vector3 pivotWorld = doorModel.TransformPoint(leftLocal);

            GameObject pivotObj = new GameObject("Pivot");
            pivotObj.transform.SetParent(transform, worldPositionStays: true);
            pivotObj.transform.position = pivotWorld;
            pivotObj.transform.rotation = doorModel.rotation;

            // Reparent children into pivot
            foreach (Transform child in transform)
            {
                if (child == pivotObj.transform) continue;
                if (child.name.ToLower().Contains("trigger")) continue;
                if (child.name.ToLower().Contains("pad")) continue;

                child.SetParent(pivotObj.transform, true);
            }

            pivot = pivotObj.transform;
        }
    }
