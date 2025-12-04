using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public int lives = 3;
    public int keys;
    public bool inPuzzle = false;
    public int lifebuoys = 1; // ��� ����� ���� �� ���
    public int HeartPlacements = 1;
    public int BombRemovals = 1;
    public DoorController activePuzzleDoor;

    public int totalKeysInLevel = 0;

    private void Start()
    {
        HUDManager.Instance?.UpdateHUD();
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public bool AllKeysCollected()
    {
        return keys >= totalKeysInLevel;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
            return;

        HUDManager.Instance?.UpdateHUDs();
    }
#endif
}
