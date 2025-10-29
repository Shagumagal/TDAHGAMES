using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class ToLGame : MonoBehaviour
{
    public static ToLGame Instance { get; private set; }
    public Peg[] pegs;       // asignar 3 en el Inspector
    public Ball[] balls;     // asignar 3 en el Inspector (id = 0..2)
    public Text movesText;
    public Button resetButton;

    [Header("Estado inicial y meta (por id de bola => índice de peg)")]
    public int[] startPegByBall = new int[] { 0, 0, 0 }; // ej: las 3 en peg 0
    public int[] goalPegByBall  = new int[] { 2, 1, 0 }; // meta ejemplo

    private int moves;

    void Awake()
    {
        Instance = this;
        resetButton.onClick.AddListener(ResetPuzzle);
    }

    void Start()
    {
        SetupStart();
        UpdateMovesUI();
    }

    public void RegisterMove()
    {
        moves++;
        UpdateMovesUI();
    }

    void UpdateMovesUI() => movesText.text = $"Movimientos: {moves}";

    public void CheckSolved()
    {
        // estado actual: por bola, en qué peg está
        for (int i = 0; i < balls.Length; i++)
        {
            if (balls[i].CurrentPeg == null) return;
            int pegIdx = System.Array.IndexOf(pegs, balls[i].CurrentPeg);
            if (pegIdx != goalPegByBall[i]) return;
        }
        OnSolved();
    }

    void OnSolved()
    {
        Debug.Log($"[ToL] ¡Resuelto! Movs={moves}");
        // Aquí luego añadimos timer/score/log
    }

    public void ResetPuzzle()
    {
        foreach (var p in pegs) p.ClearAll();
        SetupStart();
        moves = 0; UpdateMovesUI();
    }

    void SetupStart()
    {
        // colocar cada bola en su peg inicial
        for (int i = 0; i < balls.Length; i++)
        {
            var b = balls[i];
            var peg = pegs[startPegByBall[i]];
            peg.Push(b);
        }
    }
}
