using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class InstructionsOnlyUI : MonoBehaviour
{
    public string domesticos = "gallina, gato, caballo";
    public string noDomesticos = "tigre, pinguino, lobo";
    public Vector2 margin = new Vector2(16f, 16f);

    void Awake()
    {
        // Canvas existente o nuevo (ScreenSpaceOverlay)
        var canvas = FindObjectOfType<Canvas>();
        if (!canvas) canvas = CreateCanvas();

        // Raíz HUD
        var root = canvas.transform.Find("TopRightHUD");
        if (!root)
        {
            var goRoot = new GameObject("TopRightHUD", typeof(RectTransform));
            root = goRoot.transform;
            root.SetParent(canvas.transform, false);
        }

        // 🔧 CLAVE: estira el padre a toda la pantalla
        var rtRoot = (RectTransform)root;
        rtRoot.anchorMin = Vector2.zero;
        rtRoot.anchorMax = Vector2.one;
        rtRoot.pivot     = new Vector2(1, 1);
        rtRoot.offsetMin = Vector2.zero;
        rtRoot.offsetMax = Vector2.zero;

        // Texto arriba-derecha
        var go = new GameObject("InstructionsText", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(root, false);

        var txt = go.GetComponent<TextMeshProUGUI>();
        txt.text = $"Domésticos: {domesticos}\nNo domésticos: {noDomesticos}";
        txt.fontSize = 24;
        txt.color = Color.black;
        txt.alignment = TextAlignmentOptions.TopRight;
        txt.raycastTarget = false;

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(1, 1);
        rt.anchoredPosition = new Vector2(-margin.x, -margin.y);

        // 🔒 Fijar por si algo lo mueve: usa tu componente PinToTopRight
        var pin = go.AddComponent<PinToTopRight>();
        pin.margin = margin;
        pin.forceEveryFrame = true;

        // Encima de todo
        go.transform.SetAsLastSibling();
        root.SetAsLastSibling();
    }

    Canvas CreateCanvas()
    {
        var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        var s = go.GetComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920, 1080);
        if (!FindObjectOfType<EventSystem>())
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        return c;
    }
}
