using UnityEngine;
using UnityEngine.UI;

public class PreventDeselect : MonoBehaviour
{
    private ToggleGroup group;
    private Toggle lastSelected;

    void Start()
    {
        group = GetComponent<ToggleGroup>();

        // ����� ����� ������ ����� ���� ����� �����
        foreach (var t in group.GetComponentsInChildren<Toggle>())
        {
            if (t.isOn)
            {
                lastSelected = t;
                break;
            }
        }

        // ����� �������� �� �� �������
        foreach (var t in group.GetComponentsInChildren<Toggle>())
        {
            t.onValueChanged.AddListener(
                (isOn) =>
                {
                    // �� ����� ��� ���� � ���� �����
                    if (isOn)
                    {
                        lastSelected = t;
                    }
                    else
                    {
                        // ����� ����� ����� ����� �� �"� ���� ���
                        if (lastSelected == t)
                        {
                            t.isOn = true;
                        }
                    }
                }
            );
        }
    }
}
