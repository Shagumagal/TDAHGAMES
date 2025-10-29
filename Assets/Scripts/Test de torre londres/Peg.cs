using UnityEngine;
using System.Collections.Generic;

public class Peg : MonoBehaviour
{
    public int capacity = 3;
    public Transform snapRoot; // donde apilan
    private Stack<Ball> stack = new Stack<Ball>();

    public int Count => stack.Count;
    public bool CanPlace => Count < capacity;
    public Ball Top => Count > 0 ? stack.Peek() : null;

    public void Push(Ball b)
    {
        stack.Push(b);
        // Posicionar “apilado”
        b.transform.SetParent(snapRoot, true);
        var y = Count - 1;
        b.transform.localPosition = new Vector3(0, y * 0.35f, 0);
        b.CurrentPeg = this;
    }

    public Ball Pop()
    {
        var b = stack.Pop();
        return b;
    }

    // Inicialización helper
    public void ClearAll()
    {
        while (stack.Count > 0) stack.Pop();
        foreach (Transform t in snapRoot) Destroy(t.gameObject);
    }
}
