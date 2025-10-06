using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class InstructionPanelController : MonoBehaviour
{
    [Header("Refs")]
    public CanvasGroup canvasGroup;
    public TextMeshProUGUI title;
    public TextMeshProUGUI body;
    public Button continueButton;

    [Header("Keys")]
    public KeyCode continueKey = KeyCode.Return;

    [Header("Fade")]
    public float fadeIn = 0.25f;
    public float fadeOut = 0.25f;

    System.Action _onContinue;

    void Reset() {
        if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();
        if (!continueButton) continueButton = GetComponentInChildren<Button>(true);
        var tmps = GetComponentsInChildren<TextMeshProUGUI>(true);
        if (tmps.Length>0) title = tmps[0];
        if (tmps.Length>1) body  = tmps[1];
    }

    public void Set(string titleText, string bodyText, System.Action onContinue)
    {
        if (title) title.text = titleText;
        if (body)  body.text  = bodyText;
        _onContinue = onContinue;
    }

    public void Show()
    {
        if (continueButton){
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(()=> Continue());
        }
        StopAllCoroutines();
        StartCoroutine(Fade(1f, fadeIn, true));
    }

    public void Hide()
    {
        StopAllCoroutines();
        StartCoroutine(Fade(0f, fadeOut, false));
    }

    IEnumerator Fade(float target, float dur, bool blockRaycasts)
    {
        if (!canvasGroup) yield break;
        canvasGroup.blocksRaycasts = blockRaycasts;
        canvasGroup.interactable   = blockRaycasts;
        float start = canvasGroup.alpha, t=0f;
        while (t<dur){ t+=Time.deltaTime; canvasGroup.alpha=Mathf.Lerp(start,target,t/dur); yield return null; }
        canvasGroup.alpha = target;
    }

    void Update()
    {
        if (canvasGroup && canvasGroup.interactable && Input.GetKeyDown(continueKey))
            Continue();
    }

    void Continue()
    {
        Hide();
        _onContinue?.Invoke();
        _onContinue = null;
    }
}
