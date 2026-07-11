using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SongCard : MonoBehaviour
{
    [Header("UI")]
    public Image background;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI bpmText;
    public Image coverImage;

    [Header("Colors")]
    public Color selectedColor = new Color(0.85f, 0.68f, 0.18f);  // 황금색
    public Color normalColor  = new Color(0.20f, 0.12f, 0.06f);   // 고동색

    [Header("Text Colors")]
    public Color selectedTextColor = new Color(0.15f, 0.15f, 0.15f);
    public Color normalTextColor  = new Color(0.45f, 0.45f, 0.45f); // 어두워지되 배경(0.15)보다는 밝게 유지해 가독성 확보

    private static readonly Vector3 SelectedScale = new Vector3(1.10f, 1.22f, 1f); // 족자 펼침 느낌
    private static readonly Vector3 NormalScale   = Vector3.one;

    private Coroutine animCoroutine;

    public void Setup(ChartDataSO chart)
    {
        titleText.text = chart.songName;
        bpmText.text   = $"BPM  {chart.bpm}";

        if (chart.coverSprite != null)
            coverImage.sprite = chart.coverSprite;
    }

    public void SetSelected(bool selected, bool instant = false)
    {
        if (animCoroutine != null) StopCoroutine(animCoroutine);

        Vector3 targetScale     = selected ? SelectedScale       : NormalScale;
        Color   targetColor     = selected ? selectedColor       : normalColor;
        Color   targetTextColor = selected ? selectedTextColor   : normalTextColor;

        if (instant)
        {
            transform.localScale = targetScale;
            background.color     = targetColor;
            coverImage.color     = targetColor;
            titleText.color      = targetTextColor;
            bpmText.color        = targetTextColor;
        }
        else
        {
            animCoroutine = StartCoroutine(Animate(targetScale, targetColor, targetTextColor));
        }
    }

    IEnumerator Animate(Vector3 targetScale, Color targetColor, Color targetTextColor)
    {
        float elapsed     = 0f;
        const float dur   = 0.18f;
        Vector3 startScale     = transform.localScale;
        Color   startColor     = background.color;
        Color   startTextColor = titleText.color;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t  = elapsed / dur;
            transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            background.color     = Color.Lerp(startColor,  targetColor,  t);
            coverImage.color     = Color.Lerp(startColor,  targetColor,  t);
            titleText.color      = Color.Lerp(startTextColor, targetTextColor, t);
            bpmText.color        = Color.Lerp(startTextColor, targetTextColor, t);
            yield return null;
        }

        transform.localScale = targetScale;
        background.color     = targetColor;
        coverImage.color     = targetColor;
        titleText.color      = targetTextColor;
        bpmText.color         = targetTextColor;
    }
}
