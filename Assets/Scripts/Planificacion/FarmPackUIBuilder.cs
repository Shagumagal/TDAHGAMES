using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System.Linq;

[ExecuteAlways]
[RequireComponent(typeof(FarmPackGameController))]
public class FarmPackUIBuilder : MonoBehaviour
{
    [Header("Auto build")]
    public bool buildOnPlay = true;
    public bool createDefaultSpawnPointsIfEmpty = true;
    public int  defaultSpawnCount = 12;

    [Header("Spawns grid")]
    public Vector2    spawnGridSize   = new Vector2(6f, 4f);    // metros (X,Z)
    public Vector2Int spawnGridCells  = new Vector2Int(4, 3);   // columnas x filas

    [Header("HUD")]
    public float hudMargin = 16f;

    private FarmPackGameController gc;

    void Awake()
    {
        gc = GetComponent<FarmPackGameController>();
        if (Application.isPlaying && buildOnPlay) BuildOrWire();
    }

    [ContextMenu("Build / Update FarmPack UI & Wiring")]
    public void BuildOrWire()
    {
        if (!gc) gc = GetComponent<FarmPackGameController>();

        // --- Autowire básicos en escena ---
        if (!gc.fpc)
            gc.fpc = FindObjectOfType<EasyPeasyFirstPersonController.FirstPersonController>();
        if (!gc.pickup)
            gc.pickup = FindObjectOfType<PickUp>();
        if (gc.dropZones == null || gc.dropZones.Length == 0)
            gc.dropZones = FindObjectsOfType<DropZone>();

        if ((gc.spawnPoints == null || gc.spawnPoints.Length == 0) && createDefaultSpawnPointsIfEmpty)
            CreateSpawnGrid();

        // --- Canvas + EventSystem ---
        var canvas = FindObjectOfType<Canvas>();
        if (!canvas) canvas = CreateCanvas();
        EnsureEventSystem();

        // --- Root UI ---
        var root = canvas.transform.Find("FarmPackUI");
        if (!root)
        {
            var go = new GameObject("FarmPackUI", typeof(RectTransform));
            root = go.transform;
            root.SetParent(canvas.transform, false);
        }

        // === CHECKLIST (desactivado para que no tape) ==========================
        GameObject checklistPanel = canvas.transform.Find("FarmPackUI/ChecklistPanel")
            ? canvas.transform.Find("FarmPackUI/ChecklistPanel").gameObject
            : null;

        if (!checklistPanel)
            checklistPanel = CreatePanel("ChecklistPanel", (RectTransform)root, centered: true, size: new Vector2(520, 220));

        TMP_Text checklistText = checklistPanel.GetComponentInChildren<TMP_Text>();
        if (!checklistText)
        {
            checklistText = CreateTMPText("ChecklistText", checklistPanel.transform as RectTransform);
            checklistText.alignment = TextAlignmentOptions.Center;
            checklistText.enableWordWrapping = true;
            checklistText.margin = new Vector4(20, 20, 20, 20);
        }
        var clImg = checklistPanel.GetComponent<Image>() ?? checklistPanel.AddComponent<Image>();
        clImg.color = new Color(0f, 0f, 0f, 0.65f);
        checklistPanel.SetActive(false);

        // === HUD IZQUIERDA (Tiempo / Score) ====================================
        var hud = root.Find("HUD");
        if (!hud)
        {
            var hudGO = new GameObject("HUD", typeof(RectTransform));
            hud = hudGO.transform;
            hud.SetParent(root, false);
            var rt = (RectTransform)hud;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot     = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(hudMargin, -hudMargin);
            rt.sizeDelta = new Vector2(600, 100);
        }

        TMP_Text hudTimer = root.Find("HUD/TimerText") ? root.Find("HUD/TimerText").GetComponent<TMP_Text>() : null;
        if (!hudTimer)
        {
            hudTimer = CreateTMPText("TimerText", (RectTransform)hud);
            var rt = hudTimer.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = Vector2.zero;
            hudTimer.fontSize = 28;
        }
        hudTimer.color = Color.black;

        TMP_Text hudScore = root.Find("HUD/ScoreText") ? root.Find("HUD/ScoreText").GetComponent<TMP_Text>() : null;
        if (!hudScore)
        {
            hudScore = CreateTMPText("ScoreText", (RectTransform)hud);
            var rt = hudScore.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(0, -34);
            hudScore.fontSize = 24;
        }
        hudScore.color = Color.black;

        // === HUD DERECHA (Instrucciones fijas) =================================
        Transform hudRight = root.Find("HUD_Right");
        if (!hudRight)
        {
            var go = new GameObject("HUD_Right", typeof(RectTransform));
            hudRight = go.transform;
            hudRight.SetParent(root, false);
            var rt = (RectTransform)hudRight;
            rt.anchorMin = rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(-hudMargin, -hudMargin);
            rt.sizeDelta = new Vector2(640, 140);
        }

        TMP_Text hudInstr = root.Find("HUD_Right/InstructionsText")
            ? root.Find("HUD_Right/InstructionsText").GetComponent<TMP_Text>()
            : null;
        if (!hudInstr)
            hudInstr = CreateTMPText("InstructionsText", (RectTransform)hudRight);

        // Fuerza arriba-derecha siempre
        {
            var rt = hudInstr.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = Vector2.zero;
            hudInstr.alignment = TextAlignmentOptions.TopRight;
            hudInstr.fontSize = 24;
            hudInstr.enableWordWrapping = true;
            hudInstr.color = Color.black;
            hudInstr.raycastTarget = false;
        }

        // 👉 Pin persistente (corrige cada frame si algo lo mueve)
        var pin = hudInstr.GetComponent<PinToTopRight>();
        if (!pin) pin = hudInstr.gameObject.AddComponent<PinToTopRight>();
        pin.margin = new Vector2(hudMargin, hudMargin);
        pin.forceEveryFrame = true;

        // === START OVERLAY (ENTER para comenzar) ===============================
        GameObject startPanel = root.Find("StartPanel") ? root.Find("StartPanel").gameObject : null;
        if (!startPanel)
        {
            startPanel = CreatePanel("StartPanel", (RectTransform)root, centered: true, size: new Vector2(700, 200));
            var img = startPanel.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.60f);
        }

        TMP_Text startText = root.Find("StartPanel/StartText")
            ? root.Find("StartPanel/StartText").GetComponent<TMP_Text>()
            : null;
        if (!startText)
        {
            startText = CreateTMPText("StartText", startPanel.transform as RectTransform);
            startText.alignment = TextAlignmentOptions.Center;
            startText.fontSize = 34;
            startText.text = "Pulsa ENTER para comenzar";
            startText.color = Color.white; // contraste contra overlay oscuro
        }

        // === Asignaciones al GameController ====================================
        gc.checklistPanel      = checklistPanel;
        gc.checklistText       = checklistText;
        gc.hudTimerText        = hudTimer;
        gc.hudScoreText        = hudScore;
        gc.hudInstructionsText = hudInstr;
        gc.startPanel          = startPanel;
        gc.startText           = startText;

        // === Orden de dibujo (muy importante) ==================================
        if (hudRight) hudRight.SetAsLastSibling();
        if (hud)      hud.SetAsLastSibling();
        if (startPanel) startPanel.transform.SetSiblingIndex(0);

        Debug.Log("[FarmPackUIBuilder] UI y referencias listas.");
    }

    // ======================= Helpers ===========================================

    Canvas CreateCanvas()
    {
        var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        return canvas;
    }

    void EnsureEventSystem()
    {
        if (!FindObjectOfType<EventSystem>())
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    GameObject CreatePanel(string name, RectTransform parent, bool centered, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        go.transform.SetParent(parent, false);
        if (centered)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
        }
        rt.sizeDelta = size;

        var txt = CreateTMPText("Text", rt);
        txt.alignment = TextAlignmentOptions.Center;
        txt.fontSize = 28;
        txt.text = "Checklist";
        txt.color = Color.black;
        return go;
    }

    TMP_Text CreateTMPText(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var txt = go.GetComponent<TextMeshProUGUI>();
        txt.raycastTarget = false;
        txt.color = Color.black;
        txt.fontSize = 26;
        txt.enableWordWrapping = false;
        return txt;
    }

    void CreateSpawnGrid()
    {
        var root = new GameObject("Spawns", typeof(RectTransform)).transform;
        root.SetParent(transform, false);
        root.localPosition = Vector3.zero;

        int col = Mathf.Max(1, spawnGridCells.x);
        int fil = Mathf.Max(1, spawnGridCells.y);
        float sizeX = spawnGridSize.x;
        float sizeZ = spawnGridSize.y;

        var spawnList = Enumerable.Range(0, col * fil).Select(i =>
        {
            int x = i % col;
            int y = i / col;

            var t = new GameObject($"Spawn_{i + 1}", typeof(RectTransform)).transform;
            t.SetParent(root, false);

            float px = (col == 1) ? 0 : (x / (float)(col - 1) - 0.5f) * sizeX;
            float pz = (fil == 1) ? 0 : (y / (float)(fil - 1) - 0.5f) * sizeZ;

            t.localPosition = new Vector3(px, 0f, pz + 2f);
            return t;
        }).ToArray();

        gc.spawnPoints = spawnList;
    }
}
