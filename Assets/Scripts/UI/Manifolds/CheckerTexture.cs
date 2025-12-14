using UnityEngine;

public class CheckerTexture : MonoBehaviour
{
    public int size = 512;
    public int squares = 8;

    void Start()
    {
        Texture2D tex = new Texture2D(size, size);
        Color c1 = Color.white;
        Color c2 = Color.black;
        int s = size / squares;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool isEven = ((x / s) + (y / s)) % 2 == 0;
                tex.SetPixel(x, y, isEven ? c1 : c2);
            }
        }
        tex.Apply();

        GetComponent<Renderer>().material.mainTexture = tex;
    }
}
