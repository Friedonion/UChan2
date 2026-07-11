using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// 뮤즈 대쉬 스타일 곡 선택 UI.
/// songSelectPanel 안에 아래 계층을 만들어 주세요:
///   SongSelectPanel
///     CarouselRoot         ← cardContainer (Horizontal Layout Group)
///     InfoPanel
///       SongNameText
///       BpmText
///     PrevButton
///     NextButton
///     PlayButton
/// </summary>
public class SongSelectManager : MonoBehaviour
{
    [Header("Charts")]
    public ChartDataSO[] charts;

    [Header("UI")]
    public GameObject songSelectPanel;
    public Transform  cardContainer;
    public SongCard   cardPrefab;

    [Header("Info Panel")]
    public TextMeshProUGUI songNameText;
    public TextMeshProUGUI bpmText;

    [Header("Buttons")]
    public Button prevButton;
    public Button nextButton;
    public Button playButton;

    [Header("Carousel")]
    public float cardSpacing  = 320f;
    public float scrollSpeed  = 12f;

    [Header("Preview Audio")]
    public AudioSource previewAudioSource;
    public float previewVolume   = 0.6f;
    public float fadeInDuration  = 1.5f;

    private Coroutine previewCoroutine;
    private SongCard[] cards;
    private int        virtualIndex = 0;
    private float      currentScroll = 0f;
    private float      lastSelectTime = -1f;
    private const float SelectCooldown = 0.3f;

    void OnPreviewVolumeChanged(float v)
    {
        if (previewAudioSource != null && previewAudioSource.isPlaying)
            previewAudioSource.volume = previewVolume * v;
    }

    void OnDestroy()
    {
        VolumeSettings.OnMusicVolumeChanged -= OnPreviewVolumeChanged;
    }

    void Start()
    {
        if (previewAudioSource == null)
            previewAudioSource = gameObject.AddComponent<AudioSource>();
        VolumeSettings.OnMusicVolumeChanged += OnPreviewVolumeChanged;

        var noNav = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
        prevButton.navigation = noNav;
        nextButton.navigation = noNav;
        playButton.navigation = noNav;

        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go.name == "Locomotion" && go.scene == gameObject.scene)
                go.SetActive(false);
        }

        if (songSelectPanel != null)
            songSelectPanel.SetActive(true);

        prevButton.onClick.RemoveAllListeners();
        nextButton.onClick.RemoveAllListeners();
        playButton.onClick.RemoveAllListeners();
        prevButton.onClick.AddListener(SelectPrev);
        nextButton.onClick.AddListener(SelectNext);
        playButton.onClick.AddListener(OnPlayPressed);

        BuildCards();
        RefreshSelection(instant: true);

        if (charts.Length > 0)
        {
            int realIndex = (virtualIndex % charts.Length + charts.Length) % charts.Length;
            PlayPreview(charts[realIndex].audioClip);
        }
    }

    void Update()
    {
        if (cards == null || charts.Length == 0) return;

        currentScroll = Mathf.Lerp(currentScroll, virtualIndex, Time.deltaTime * scrollSpeed);

        float N = charts.Length;
        float halfN = N / 2f;

        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null) continue;

            float offset = i - currentScroll;
            
            while (offset > halfN) offset -= N;
            while (offset < -halfN) offset += N;
            
            Vector3 pos = cards[i].transform.localPosition;
            pos.x = offset * cardSpacing;
            cards[i].transform.localPosition = pos;
        }
    }

    void BuildCards()
    {
        if (cardPrefab == null)
        {
            Debug.LogError("[SongSelectManager] CardPrefab이 연결되지 않았습니다!");
            return;
        }
        cards = new SongCard[charts.Length];
        for (int i = 0; i < charts.Length; i++)
        {
            SongCard card = Instantiate(cardPrefab, cardContainer);
            if (card == null)
            {
                Debug.LogError($"[SongSelectManager] 카드 {i} 생성 실패 — SongCard 컴포넌트가 프리팹에 없습니다.");
                continue;
            }
            card.Setup(charts[i]);
            // 각 카드를 간격에 맞게 배치 (Horizontal Layout Group 없이도 동작)
            var rt = card.GetComponent<RectTransform>();
            if (rt != null)
                rt.anchoredPosition = new Vector2(i * cardSpacing, 0f);
            else
                card.transform.localPosition = new Vector3(i * cardSpacing, 0f, 0f);
            cards[i] = card;
        }
    }

    void SelectNext()
    {
        if (Time.unscaledTime - lastSelectTime < SelectCooldown) return;
        if (charts.Length == 0) return;
        lastSelectTime = Time.unscaledTime;
        virtualIndex++;
        RefreshSelection();
    }

    void SelectPrev()
    {
        if (Time.unscaledTime - lastSelectTime < SelectCooldown) return;
        if (charts.Length == 0) return;
        lastSelectTime = Time.unscaledTime;
        virtualIndex--;
        RefreshSelection();
    }

    void RefreshSelection(bool instant = false)
    {
        if (cards == null || charts.Length == 0) return;

        if (instant)
        {
            currentScroll = virtualIndex;
            cardContainer.localPosition = Vector3.zero; // 컨테이너 자체는 이동하지 않음
        }

        int realIndex = (virtualIndex % charts.Length + charts.Length) % charts.Length;

        for (int i = 0; i < cards.Length; i++)
            if (cards[i] != null)
                cards[i].SetSelected(i == realIndex, instant);

        ChartDataSO chart = charts[realIndex];
        if (songNameText != null) songNameText.text = chart.songName;
        if (bpmText != null) bpmText.text = $"BPM  {chart.bpm}";

        prevButton.interactable = true;
        nextButton.interactable = true;

        if (!instant)
            PlayPreview(chart.audioClip);
    }

    void PlayPreview(AudioClip clip)
    {
        if (previewCoroutine != null) StopCoroutine(previewCoroutine);
        if (clip == null) return;
        previewCoroutine = StartCoroutine(FadeInPreview(clip));
    }

    IEnumerator FadeInPreview(AudioClip clip)
    {
        previewAudioSource.Stop();
        previewAudioSource.clip   = clip;
        previewAudioSource.loop   = true;
        previewAudioSource.volume = 0f;
        previewAudioSource.Play();

        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            previewAudioSource.volume = Mathf.Lerp(0f, previewVolume * VolumeSettings.MusicVolume, elapsed / fadeInDuration);
            yield return null;
        }
        previewAudioSource.volume = previewVolume * VolumeSettings.MusicVolume;
    }

    void OnPlayPressed()
    {
        if (previewCoroutine != null) StopCoroutine(previewCoroutine);
        previewAudioSource.Stop();
        int realIndex = (virtualIndex % charts.Length + charts.Length) % charts.Length;
        SongSelection.Chart = charts[realIndex];
        SceneManager.LoadScene("BasicScene");
    }
}
