using UnityEngine;

public class NavigatorActions : MonoBehaviour
{
    private GameWorldController world;

    private void Start()
    {
        world = GameWorldController.Instance;
    }

    public void OpenDoor()
    {
        world.OpenNormalDoor();
    }

    public void PlaceHeart()
    {
        world.PlaceHeart();
    }
    public Sprite debugImage;   // התמונה שתרצה להציג לבדיקה


    public void RemoveBomb()
    {
        world.RemoveBomb();
    }

    public void ShowPuzzle()
    {
        world.ShowPuzzle();
    }

    public void UseLifebuoy()
    {
        world.UseLifebuoy();
    }
}
