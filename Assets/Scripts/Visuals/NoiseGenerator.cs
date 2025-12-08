using UnityEngine;

public static class NoiseGenerator
{
    public static Texture2D GenerateNoise(int size = 256)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float v = Random.value;
                tex.SetPixel(x, y, new Color(v, v, v, 1f));
            }
        }

        tex.Apply();
        return tex;
    }
}
