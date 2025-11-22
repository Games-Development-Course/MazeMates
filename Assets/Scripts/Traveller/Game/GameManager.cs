using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public int lives;
    public int keys;
    public bool inPuzzle = false;


    public int totalKeysInLevel = 0;


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

        if (HUD.Instance != null)
            HUD.Instance.UpdateHUD();
    }
#endif
}
