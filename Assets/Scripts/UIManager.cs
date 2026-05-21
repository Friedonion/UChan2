using UnityEngine;
using TMPro;
using System.Collections;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("Main HUD")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI comboText;
    public TextMeshProUGUI healthText;

    [Header("Judgment")]
    public TextMeshProUGUI judgmentText;
    public Color perfectColor = Color.yellow;
    public Color greatColor = Color.cyan;
    public Color missColor = Color.red;

    [Header("Game State UI")]
    public GameObject startPanel;
    public GameObject resultPanel;
    public TextMeshProUGUI finalScoreText;

    private Coroutine judgmentCoroutine;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        
        if (judgmentText) judgmentText.text = "";
    }

    public void UpdateScore(int score) => scoreText.text = $"SCORE: {score:N0}";
    public void UpdateCombo(int combo) => comboText.text = combo > 0 ? $"{combo} COMBO" : "";
    public void UpdateHealth(int health) => healthText.text = $"HP: {health}%";

    public void ShowJudgment(string text, Color color)
    {
        if (judgmentText == null) return;
        
        if (judgmentCoroutine != null) StopCoroutine(judgmentCoroutine);
        judgmentText.text = text;
        judgmentText.color = color;
        judgmentCoroutine = StartCoroutine(JudgmentAnimation());
    }

    IEnumerator JudgmentAnimation()
    {
        judgmentText.transform.localScale = Vector3.one * 1.5f;
        float elapsed = 0f;
        while (elapsed < 0.5f)
        {
            elapsed += Time.deltaTime;
            judgmentText.transform.localScale = Vector3.Lerp(Vector3.one * 1.5f, Vector3.one, elapsed / 0.5f);
            yield return null;
        }
        yield return new WaitForSeconds(0.5f);
        judgmentText.text = "";
    }

    public void ShowStartUI(bool show) => startPanel.SetActive(show);
    
    public void ShowResultUI(int finalScore)
    {
        resultPanel.SetActive(true);
        finalScoreText.text = $"FINAL SCORE\n{finalScore:N0}";
    }
}
