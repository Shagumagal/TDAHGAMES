using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

/// Tarjeta de instrucciones arriba-derecha (solo visual).
/// Mismo estilo que el panel de tiempo de la izquierda (PrettyCornerHUDLeft).
public class InstructionsOnlyUI : MonoBehaviour
{
    public string domesticos = "gallina, gato, caballo";
    public string noDomesticos = "tigre, pinguino, lobo";
    public Vector2 margin = new Vector2(16f, 16f);

    [Header("Estilo")]
    public bool showOrderLine = true;   // muestra la regla de orden si el juego la usa
    public int fontSize = 24;
    public Color panelColor = new Color(1f, 1f, 1f, 0.85f);
    public Color borderColor = new Color(0f, 0f, 0f, 0.15f);
    public Color textColor = new Color32(17, 24, 39, 255);
    public Color domColor = new Color32(21, 128, 61, 255);    // verde
    public Color noDomColor = new Color32(194, 65, 12, 255);  // naranja

    void Awake()
    {
        // Canvas existente o nuevo (ScreenSpaceOverlay)
        var canvas = FindObjectOfType<Canvas>();
        if (!canvas) canvas = CreateCanvas();

        // Raíz HUD estirada a toda la pantalla
        var root = canvas.transform.Find("TopRightHUD");
        if (!root)
        {
            var goRoot = new GameObject("TopRightHUD", typeof(RectTransform));
            root = goRoot.transform;
            root.SetParent(canvas.transform, false);
        }
        var rtRoot = (RectTransform)root;
        rtRoot.anchorMin = Vector2.zero;
        rtRoot.anchorMax = Vector2.one;
        rtRoot.pivot     = new Vector2(1, 1);
        rtRoot.offsetMin = Vector2.zero;
        rtRoot.offsetMax = Vector2.zero;

        // Tarjeta (se ajusta al tamaño del texto)
        var card = new GameObject("InstructionsCard", typeof(RectTransform), typeof(Image),
                                  typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        card.transform.SetParent(root, false);

        var img = card.GetComponent<Image>();
        img.color = panelColor;
        img.raycastTarget = false;

        var outline = card.AddComponent<Outline>();
        outline.effectColor = borderColor;
        outline.effectDistance = new Vector2(2f, -2f);
        var shadow = card.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.25f);
        shadow.effectDistance = new Vector2(3, -3);

        var layout = card.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 12, 12);
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;  layout.childControlHeight = true;
        layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;

        var fitter = card.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        // Texto (sin cortes de línea automáticos)
        var go = new GameObject("InstructionsText", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(card.transform, false);
        var txt = go.GetComponent<TextMeshProUGUI>();
        txt.enableWordWrapping = false;
        txt.fontSize = fontSize;
        txt.color = textColor;
        txt.alignment = TextAlignmentOptions.TopLeft;
        txt.lineSpacing = 12f;
        txt.raycastTarget = false;
        txt.text = BuildText();

        // Fijar la tarjeta arriba-derecha (PinToTopRight solo toca el color si hay texto en el mismo objeto)
        var pin = card.AddComponent<PinToTopRight>();
        pin.margin = margin;
        pin.forceEveryFrame = true;

        card.transform.SetAsLastSibling();
        root.SetAsLastSibling();
    }

    string BuildText()
    {
        var gc = FindObjectOfType<FarmPackGameController>();

        // Las listas se muestran en el MISMO orden que puntúa el juego (tabla de tamaños de la escena).
        // Si no hay controlador, se usan los textos del Inspector como antes.
        string dom = gc ? OrderedList(gc, gc.domesticos)   : domesticos;
        string nod = gc ? OrderedList(gc, gc.noDomesticos) : noDomesticos;
        dom = Pretty(dom);
        nod = Pretty(nod);
        string domHex = ColorUtility.ToHtmlStringRGB(domColor);
        string nodHex = ColorUtility.ToHtmlStringRGB(noDomColor);

        string s = "";
        if (showOrderLine)
        {
            if (gc && gc.rule == FarmPackGameController.Rule.DomBySizeThenNoDomBySize)
            {
                string tam = gc.sizeAscending ? "de pequeño a grande" : "de grande a pequeño";
                s += $"<b>Orden:</b> {tam},\nprimero domésticos y luego no domésticos\n";
            }
        }
        s += $"<color=#{domHex}><b>Domésticos:</b></color> {dom}\n";
        s += $"<color=#{nodHex}><b>No domésticos:</b></color> {nod}";
        return s;
    }

    // Ordena un grupo igual que FarmPackGameController.BuildExpectedSequence:
    // solo los animales en juego, por 'rank' de la Size Table, y al revés si sizeAscending = false.
    static string OrderedList(FarmPackGameController gc, string[] grupo)
    {
        if (grupo == null) return "";
        int Rank(string id)
        {
            if (gc.sizeTable != null)
                foreach (var a in gc.sizeTable) if (a.id == id) return a.rank;
            return 999;
        }
        var lista = new System.Collections.Generic.List<string>();
        foreach (var id in grupo)
            if (gc.targetAnimals == null || gc.targetAnimals.Contains(id)) lista.Add(id);
        // Orden estable (como List.Sort no lo es, se desempata por la posición original)
        var conIndice = new System.Collections.Generic.List<(string id, int idx)>();
        for (int i = 0; i < lista.Count; i++) conIndice.Add((lista[i], i));
        conIndice.Sort((x, y) => { int c = Rank(x.id).CompareTo(Rank(y.id)); return c != 0 ? c : x.idx.CompareTo(y.idx); });
        if (!gc.sizeAscending) conIndice.Reverse();
        var nombres = new System.Collections.Generic.List<string>();
        foreach (var p in conIndice) nombres.Add(p.id);
        return string.Join(", ", nombres);
    }

    // Solo para mostrar: los ids internos ("pinguino", "lobo") no se tocan,
    // porque se usan en el orden esperado y en los resultados.
    // El modelo 3D del id "lobo" es un alce.
    static string Pretty(string list) => list.Replace("pinguino", "pingüino").Replace("lobo", "alce");

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
