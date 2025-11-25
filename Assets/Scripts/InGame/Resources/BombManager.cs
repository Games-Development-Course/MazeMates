using UnityEngine;

public class BombManager : MonoBehaviour
{
    public static BombManager Instance;
    public GameManager gameManager;

    private void Awake()
    {
        Instance = this;
    }

    public void RemoveClosestBomb(Transform player)
    {
        GameObject[] bombs = GameObject.FindGameObjectsWithTag("Bomb");
        if (gameManager.BombRemovals == 0)
        {
            HUDManager.Instance.ShowMessageToNavigator("אין לך מספיק.");
            return;
        }
        if (bombs.Length == 0)
        {
            HUDManager.Instance.ShowMessageToNavigator("אין לך פצצות להסיר.");
            return;
        }

        GameObject closest = null;
        float minDist = Mathf.Infinity;

        foreach (var b in bombs)
        {
            float dist = Vector3.Distance(player.position, b.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                closest = b;
            }
        }

        if (closest != null)
        {
            Destroy(closest);
            gameManager.BombRemovals--;
            Debug.Log("Removed bomb: " + closest.name);
        }
    }
}
