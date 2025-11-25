using UnityEngine;

public static class WorldPlacer
{
    public static Transform traveller
        => GameObject.FindWithTag("Player").transform;

    // הנחת אובייקט קרוב לשחקן (1 מטר קדימה)
    public static void PlacePrefabNearTraveller(GameObject prefab)
    {
        Vector3 pos = traveller.position + traveller.forward * 1f;
        Object.Instantiate(prefab, pos, Quaternion.identity);
    }

    // מציאת אובייקט קרוב לפי שם
    public static GameObject FindClosestObjectToPlayer(string namePart)
    {
        GameObject[] objects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        GameObject best = null;
        float bestDist = Mathf.Infinity;

        foreach (var obj in objects)
        {
            if (!obj.name.ToLower().Contains(namePart.ToLower()))
                continue;

            float d = Vector3.Distance(traveller.position, obj.transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = obj;
            }
        }

        return best;
    }

    // דלת הקרובה
    public static DoorController FindClosestDoor()
    {
        DoorController[] doors = Object.FindObjectsByType<DoorController>(FindObjectsSortMode.None);
        DoorController best = null;
        float bestDist = Mathf.Infinity;

        foreach (var d in doors)
        {
            float dist = Vector3.Distance(traveller.position, d.transform.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = d;
            }
        }

        return best;
    }

    // דלת חידה קרובה בלבד
    public static DoorController FindClosestPuzzleDoor()
    {
        DoorController[] doors = Object.FindObjectsByType<DoorController>(FindObjectsSortMode.None);
        DoorController best = null;
        float bestDist = Mathf.Infinity;

        foreach (var d in doors)
        {
            if (d.doorType != DoorType.Puzzle)
                continue;

            float dist = Vector3.Distance(traveller.position, d.transform.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = d;
            }
        }

        return best;
    }
}
