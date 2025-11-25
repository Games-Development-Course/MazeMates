using UnityEngine;

public class HeartManager : MonoBehaviour
{
    public static HeartManager Instance;
    public GameManager gameManager;

    public GameObject HeartPrefab;
    public float placeDistance = 1.5f;

    private void Awake()
    {
        Instance = this;
    }

    public void PlaceHeart()
    {
        Transform traveller = GameObject.FindWithTag("Player").transform;
        if (gameManager.HeartPlacements == 0)
        {
            HUDManager.Instance.ShowMessageToNavigator("אין לך לבבות להניח.");
            return;
        }
        // קרן קדימה למרחק 3 מטר
        RaycastHit hit;
        if (Physics.Raycast(traveller.position, traveller.forward, out hit, 3f))
        {
            // בודקים שזה באמת ה- FLOOR
            if (hit.collider.CompareTag("Floor"))
            {
                // יוצרים לב בדיוק איפה שפגענו
                GameObject heart = Instantiate(
                    Resources.Load<GameObject>("HeartModel"),
                    hit.point,
                    Quaternion.Euler(-90, 0, 0) // הרוטציה הנכונה
                );

                HUDManager.Instance.ShowMessageToNavigator("לב הונח!");
                gameManager.HeartPlacements--;
                return;
            }
            else
            {
                HUDManager.Instance.ShowMessageToNavigator("אי אפשר להניח לב על קיר!");
                return;
            }
        }

        HUDManager.Instance.ShowMessageToNavigator("לא נמצא מקום להניח לב.");
    }
}
