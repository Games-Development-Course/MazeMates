// Assets/Scripts/UI/LoadingDotsText.cs
using System.Collections;
using TMPro;
using UnityEngine;

public class LoadingDotsText : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text label;

    [Header("Text")]
    [SerializeField] private string baseText = "иетп";
    [SerializeField] private int maxDots = 3;

    [Header("Timing")]
    [SerializeField] private float stepSeconds = 0.35f;

    private Coroutine _co;

    private void OnEnable()
    {
        StartAnim();
    }

    private void OnDisable()
    {
        StopAnim();
    }

    public void StartAnim()
    {
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(Loop());
    }

    public void StopAnim()
    {
        if (_co != null) StopCoroutine(_co);
        _co = null;
    }

    /// Call this when loading is done (optional).
    public void SetDoneText(string doneText = "")
    {
        StopAnim();
        if (label != null) label.text = doneText;
    }

    private IEnumerator Loop()
    {
        if (label == null) yield break;

        int dots = 0;
        while (true)
        {
            // иетп / иетп. / иетп.. / иетп...
            string suffix = (dots == 0) ? "" : new string('.', dots);
            label.text = baseText + suffix;

            dots++;
            if (dots > maxDots) dots = 0;

            yield return new WaitForSeconds(stepSeconds);
        }
    }
}
