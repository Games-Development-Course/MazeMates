using System.Collections;
using UnityEngine;

public class DoorDissolver : MonoBehaviour
{
    public float dissolveTime = 1.2f;
    private Material mat;

    private void Awake()
    {
        // ����� instance �� ����� ��� �� ����� �� �� ������
        Renderer r = GetComponentInChildren<Renderer>();
        mat = new Material(r.material);
        r.material = mat;

        // ���� ������ "������" ���� 1
        mat.SetFloat("_DissolveAmount", 1f);
    }

    public void TriggerDissolve()
    {
        StartCoroutine(DissolveReverseRoutine());
    }

    private IEnumerator DissolveReverseRoutine()
    {
        float t = 0f;

        while (t < dissolveTime)
        {
            t += Time.deltaTime;

            float dissolveValue = Mathf.Lerp(1f, 0f, t / dissolveTime);
            mat.SetFloat("_DissolveAmount", dissolveValue);

            yield return null;
        }

        mat.SetFloat("_DissolveAmount", 0f);

        gameObject.SetActive(false);
    }
}
