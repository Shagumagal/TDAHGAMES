using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FarmHUDBuilder : MonoBehaviour
{
    [Tooltip("Si está activo, construye el HUD automáticamente al iniciar la escena.")]
    public bool autoBuildOnPlay = true;

    void Start()
    {
        if (autoBuildOnPlay) BuildHUD();
    }

    [ContextMenu("Build Farm HUD (TargetsPanel + PhaseTimerText)")]
    public void BuildHUD()
    {
        // Canvas
        var canvas = FindObjectOfType<Canvas>();
        if (!canvas)
        {
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
        }

        // EventSystem
        if (!FindObjectOfType<UnityEngine.EventSystems.EventSystem>())
        {
            var es = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
            es.transform.SetParent(canvas.transform, false);
        }

        // ===== PhaseTimerText (arriba-izquierda)
        if (!GameObject.Find("PhaseTimerText"))
        {
            var timerGO = new GameObject("PhaseTimerText", typeof(RectTransform), typeof(TextMeshProUGUI));
            timerGO.transform.SetParent(canvas.transform, false);
            var rt = timerGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(16f, -16f);
            rt.sizeDelta = new Vector2(420f, 64f);

            var tmp = timerGO.GetComponent<TextMeshProUGUI>();
            tmp.text = "Tiempo: 0.0s";
            tmp.enableAutoSizing = true; tmp.fontSizeMin = 18; tmp.fontSizeMax = 36;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.color = new Color32(255,255,255,255);
        }

        // ===== TargetsPanel (arriba-derecha) con 3 TMPs
        GameObject panelGO = GameObject.Find("TargetsPanel");
        if (!panelGO)
        {
            panelGO = new GameObject("TargetsPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panelGO.transform.SetParent(canvas.transform, false);

            var rt = panelGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-16f, -16f);
            rt.sizeDelta = new Vector2(260f, 140f);

            var bg = panelGO.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.25f); // fondo sutil

            var vlg = panelGO.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(12, 12, 12, 12);
            vlg.spacing = 6f;
            vlg.childAlignment = TextAnchor.UpperLeft;

            var fitter = panelGO.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Crea 3 labels
            CreateTargetLabel(panelGO.transform, "Target1", "• Pala");
            CreateTargetLabel(panelGO.transform, "Target2", "• Regadera");
            CreateTargetLabel(panelGO.transform, "Target3", "• Hoz");
        }

        Debug.Log("[FarmHUDBuilder] HUD listo: PhaseTimerText y TargetsPanel.");
    }

    private void CreateTargetLabel(Transform parent, string name, string text)
    {
        if (parent.Find(name) != null) return;

        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(220f, 32f);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.enableAutoSizing = true; tmp.fontSizeMin = 18; tmp.fontSizeMax = 34;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.color = new Color32(255,255,255,255);
        tmp.raycastTarget = false;
    }
}
