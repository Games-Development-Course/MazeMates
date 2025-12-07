    using UnityEngine;

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance;

        // נקבע אוטומטית מה־PlayerSpawnManager
        [HideInInspector]
        public GameObject traveller;

        public int lives = 3;
        public int keys;
        public bool inPuzzle = false;

        public int lifebuoys = 1;
        public int HeartPlacements = 1;
        public int BombRemovals = 1;

        public DoorController activePuzzleDoor;
        public int totalKeysInLevel = 0;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);
        }

        private void Start()
        {
            HUDManager.Instance?.UpdateHUD();
        }

        public bool AllKeysCollected()
        {
            return keys >= totalKeysInLevel;
        }
    }
