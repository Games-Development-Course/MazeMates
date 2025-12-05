using UnityEngine;

[CreateAssetMenu(fileName = "MapIcon", menuName = "Scriptable Objects/MapIcon")]
public class MapIcon : ScriptableObject
{
    public Image iconImage;

    [HideInInspector] public ScriptableObject data;
    [HideInInspector] public string dataId;
    public Vector2 mapPosition;
    public void Setup(ScriptableObject so)
    {
        data = so;

        if (so is DoorTypeSO door)
        {
            dataId = door.id;
            iconImage.sprite = door.icon;
        }
        else if (so is ResourceRuntimeData r)
        {
            dataId = r.id;
            iconImage.sprite = r.type.icon;
            mapPosition = r.position;
        }

        UpdateIconPosition();
    }

    // Called for dynamically placed resources
    public void SetupFromRuntime(ResourceTypeSO type, Vector2 pos)
    {
        data = type;
        id = type.id;
        iconImage.sprite = type.icon;
        mapPosition = pos;

        UpdateIconPosition();
    }

    private void UpdateIconPosition()
    {
    // Converts map coordinates → UI position.
    // Replace with actual map conversion method.

        transform.localPosition = new Vector3(mapPosition.x, mapPosition.y, 0);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        CommandRoomManager.Instance.SelectIcon(this);
    }
}
