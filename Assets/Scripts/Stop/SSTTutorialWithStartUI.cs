using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SSTIntuitiveTutorial : MonoBehaviour
{
    [Header("Integración")]
    public StartUIPanel startUI;         // Déjalo vacío para usar StartUIPanel.Instance
    public GameObject gameRoot;          // Raíz del juego real (SSTSemaforoManager o similar)

    [Header("Controles")]
    public KeyCode responseKey = KeyCode.Space;

    [Header("Flujo")]
    public int cycles = 2;               // VERDE→ROJO repetido N veces

    [Header("Duraciones (s)")]
    public float goDuration = 5f;        // tiempo que se mantiene VERDE
    public float stopDuration = 5f;      // tiempo que se mantiene ROJO
    public float feedbackTime = 0.8f;    // “¡Bien!” / recordatorio

    [Header("Sonido (opcional)")]
    public AudioSource sfx;
    public AudioClip stopBeep;

    [Header("Estética del indicador")]
    [Range(0f, 1f)] public float lightAlpha = 0.75f;
    public Vector2 lightMargin = new Vector2(64, 64); // margen interior (para no tapar bordes)

    [Header("Textos")]
    public string goTitle = "¡VERDE!";
    public string goBody  = "Presiona ESPACIO";
    public string stopTitle = "¡DETENTE!";
    public string stopBody  = "No presiones nada";

    // ---- Internos ----
    TextMeshProUGUI _btnLabel;
    bool _btnWasActive;
    string _btnPrevText;

    Image _lightRect;                    // panel de color en el Canvas del StartUIPanel
    Color _greenCol, _redCol;
    Sprite _whiteSprite;

    void Awake()
    {
        _greenCol = new Color(0.17f, 0.82f, 0.38f, lightAlpha); // verde
        _redCol   = new Color(0.95f, 0.22f, 0.25f, lightAlpha); // rojo
        _whiteSprite = Sprite.Create(Texture2D.whiteTexture,
            new Rect(0,0,1,1), new Vector2(0.5f,0.5f));
    }

    void Start()
    {
        if (!startUI) startUI = StartUIPanel.Instance;
        if (!startUI)
        {
            Debug.LogError("SSTIntuitiveTutorial: no se encontró StartUIPanel en la escena.");
            enabled = false; return;
        }

        if (gameRoot) gameRoot.SetActive(false); // bloquear juego real
        HideContinueButtonForTutorial(true);     // ocultar botón del panel

        EnsureLightRect();                       // crear panel de color (una sola vez)
        StartCoroutine(RunTutorial());
    }

    IEnumerator RunTutorial()
    {
        // Intro breve
        yield return ShowMessage("¡Vamos a practicar!",
            "Aprende las dos reglas: VERDE = ESPACIO. ROJO = NO presiones.");

        // Repite N ciclos: VERDE -> ROJO
        for (int k = 0; k < cycles; k++)
        {
            // --- Fase VERDE ---
            yield return GoPhase();

            // --- Fase ROJO ---
            yield return StopPhase();
        }

        // Cierre + cuenta atrás
        yield return ShowMessage("¡Listo!",
            "Recuerda: VERDE = ESPACIO. Si suena un bip y está ROJO, no presiones.");
        yield return new WaitForSeconds(0.8f);
        for (int c = 3; c >= 1; c--)
        {
            startUI.Show("Empezamos…", $"Iniciamos en {c}…", () => { });
            HideContinueButtonForTutorial(true);
            yield return new WaitForSeconds(0.8f);
        }
        startUI.Hide();

        // Restaurar UI y habilitar juego real
        SetLightVisible(false);
        RestoreContinueButtonAfterTutorial();
        if (gameRoot) gameRoot.SetActive(true);
        Destroy(gameObject);
    }

    // ----------------- Fases -----------------
    IEnumerator GoPhase()
    {
        // Mostrar VERDE y esperar hasta que presione (máx goDuration, luego insistir)
        SetLightColor(_greenCol);
        SetLightVisible(true);
        yield return ShowMessage(goTitle, goBody);

        float t = 0f; bool pressed = false;
        while (t < goDuration)
        {
            if (Input.GetKeyDown(responseKey)) { pressed = true; break; }
            t += Time.deltaTime; yield return null;
        }

        if (!pressed)
        {
            // Insistir hasta que presione (sin límite) para asegurar comprensión
            startUI.Show(goTitle, "Inténtalo ahora: presiona ESPACIO", () => { });
            HideContinueButtonForTutorial(true);
            while (!Input.GetKeyDown(responseKey)) yield return null;
        }

        startUI.Show("¡Bien!", "Eso es: en VERDE presiona ESPACIO.", () => { });
        HideContinueButtonForTutorial(true);
        yield return new WaitForSeconds(feedbackTime);
        startUI.Hide();
        SetLightVisible(false);
    }

    IEnumerator StopPhase()
    {
        // Mostrar ROJO y requerir 5 s SIN presionar. Si presiona, se reinicia la fase.
        if (sfx && stopBeep) sfx.PlayOneShot(stopBeep);
        SetLightColor(_redCol);
        SetLightVisible(true);
        yield return ShowMessage(stopTitle, stopBody);

        bool success;
        do
        {
            success = true;
            float t = 0f;
            while (t < stopDuration)
            {
                if (Input.GetKeyDown(responseKey))
                {
                    success = false;
                    // Recordatorio y reintento
                    startUI.Show("Recuerda", "En ROJO, no presiones nada. Intentemos de nuevo.", () => { });
                    HideContinueButtonForTutorial(true);
                    yield return new WaitForSeconds(feedbackTime);
                    startUI.Hide();
                    // Reiniciar luz y contador
                    if (sfx && stopBeep) sfx.PlayOneShot(stopBeep);
                    yield return ShowMessage(stopTitle, stopBody);
                    t = 0f;
                }
                else
                {
                    t += Time.deltaTime;
                    yield return null;
                }
            }
        } while (!success);

        startUI.Show("¡Perfecto!", "En ROJO, te quedas quieto.", () => { });
        HideContinueButtonForTutorial(true);
        yield return new WaitForSeconds(feedbackTime);
        startUI.Hide();
        SetLightVisible(false);
    }

    // ----------------- Helpers de UI -----------------
    IEnumerator ShowMessage(string title, string body)
    {
        startUI.Show(title, body, () => { });
        HideContinueButtonForTutorial(true); // botón oculto durante tutorial
        yield return null;
    }

    void HideContinueButtonForTutorial(bool hide, string newText = null)
    {
        if (!startUI || !startUI.continueButton) return;

        if (_btnLabel == null)
        {
            _btnLabel = startUI.continueButton.GetComponentInChildren<TextMeshProUGUI>(true);
            _btnWasActive = startUI.continueButton.gameObject.activeSelf;
            _btnPrevText  = _btnLabel ? _btnLabel.text : null;
        }

        if (_btnLabel && newText != null) _btnLabel.text = newText;
        startUI.continueButton.gameObject.SetActive(!hide);
    }

    void RestoreContinueButtonAfterTutorial()
    {
        if (!startUI || !startUI.continueButton) return;
        if (_btnLabel && _btnPrevText != null) _btnLabel.text = _btnPrevText;
        startUI.continueButton.gameObject.SetActive(_btnWasActive);
    }

    // ----------------- Indicador de color en el mismo Canvas -----------------
    void EnsureLightRect()
    {
        if (_lightRect) return;

        // Crear un Image pantalla completa como hijo del panel del StartUIPanel
        var root = startUI.panel ? startUI.panel.transform : startUI.transform;
        var go = new GameObject("LightCue", typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        go.transform.SetParent(root, false);

        // Insertar entre "Overlay" y "Card" (suele ser índice 1)
        go.transform.SetSiblingIndex(1);

        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = lightMargin;  rt.offsetMax = -lightMargin;

        var img = go.GetComponent<Image>();
        img.sprite = _whiteSprite;
        img.raycastTarget = false;
        img.enabled = false; // empieza oculto

        _lightRect = img;
    }

    void SetLightVisible(bool visible)
    {
        if (!_lightRect) EnsureLightRect();
        _lightRect.enabled = visible;
    }

    void SetLightColor(Color c)
    {
        if (!_lightRect) EnsureLightRect();
        _lightRect.color = c;
    }
}
