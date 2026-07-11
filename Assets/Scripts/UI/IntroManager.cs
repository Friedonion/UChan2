using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using Unity.XR.CoreUtils;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;

public class IntroManager : MonoBehaviour
{
    [Header("Canvas Groups")]
    public CanvasGroup titleGroup;
    public CanvasGroup startPromptGroup;

    [Header("Canvas")]
    public Transform canvasTransform;
    public float distanceFromPlayer = 2.0f;

    [Header("Timing")]
    public float titleFadeInDuration = 2.5f;
    public float promptAppearDelay   = 1.2f;
    public float blinkSpeed          = 0.9f;

    [Header("Scene")]
    public string nextSceneName = "MusicSelectUI";

    private bool   canStart      = false;
    private bool   transitioning = false;
    private Camera vrCam;

    void Start()
    {
        var xrOrigin = FindObjectOfType<XROrigin>();
        vrCam = xrOrigin != null ? xrOrigin.Camera : Camera.main;

        if (titleGroup)       titleGroup.alpha      = 0f;
        if (startPromptGroup) startPromptGroup.alpha = 0f;

        StartCoroutine(InitCanvas());
        StartCoroutine(PlayIntroSequence());
    }

    // XR 트래킹이 초기화될 때까지 2프레임 대기 후 캔버스를 플레이어 정면에 1회 고정
    IEnumerator InitCanvas()
    {
        yield return null;
        yield return null;

        if (canvasTransform == null || vrCam == null) yield break;

        Vector3 forward = new Vector3(vrCam.transform.forward.x, 0f, vrCam.transform.forward.z).normalized;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;

        canvasTransform.position = vrCam.transform.position + forward * distanceFromPlayer;
        canvasTransform.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }

    void Update()
    {
        if (!canStart || transitioning) return;
        if (DetectAnyInput()) StartCoroutine(TransitionToNextScene());
    }

    bool DetectAnyInput()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;

        var left  = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        var right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        bool lt = false, rt = false, lg = false, rg = false;
        left.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out lt);
        right.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out rt);
        left.TryGetFeatureValue(UnityEngine.XR.CommonUsages.gripButton, out lg);
        right.TryGetFeatureValue(UnityEngine.XR.CommonUsages.gripButton, out rg);

        return lt || rt || lg || rg;
    }

    IEnumerator PlayIntroSequence()
    {
        yield return new WaitForSeconds(0.5f);
        yield return StartCoroutine(Fade(titleGroup, 0f, 1f, titleFadeInDuration));
        yield return new WaitForSeconds(promptAppearDelay);
        yield return StartCoroutine(Fade(startPromptGroup, 0f, 1f, 0.6f));
        canStart = true;
        StartCoroutine(BlinkPrompt());
    }

    IEnumerator BlinkPrompt()
    {
        while (!transitioning)
        {
            yield return StartCoroutine(Fade(startPromptGroup, 1f, 0.15f, blinkSpeed));
            yield return StartCoroutine(Fade(startPromptGroup, 0.15f, 1f, blinkSpeed));
        }
    }

    IEnumerator TransitionToNextScene()
    {
        transitioning = true;
        float duration = 1.2f;
        float t0 = titleGroup      ? titleGroup.alpha      : 1f;
        float p0 = startPromptGroup ? startPromptGroup.alpha : 1f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            if (titleGroup)      titleGroup.alpha      = Mathf.Lerp(t0, 0f, t);
            if (startPromptGroup) startPromptGroup.alpha = Mathf.Lerp(p0, 0f, t);
            yield return null;
        }
        SceneManager.LoadScene(nextSceneName);
    }

    IEnumerator Fade(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null) yield break;
        float elapsed = 0f;
        group.alpha = from;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            group.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        group.alpha = to;
    }
}
