using UnityEngine;

public class NavigatorActions : MonoBehaviour
{
    private GameWorldController world;

    private void Start()
    {
        world = GameWorldController.Instance;
    }

    public void OpenDoor() => world.OpenNormalDoor();

    public void ShowPuzzle() => world.ShowPuzzle();

    public void RemoveBomb() => world.RemoveBomb();

    public void PlaceHeart() => world.PlaceHeart();

    public void UseLifebuoy() => world.UseLifebuoy();
}
