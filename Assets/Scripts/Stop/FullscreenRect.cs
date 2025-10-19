using UnityEngine;

[ExecuteAlways]
public class FullscreenRect : MonoBehaviour
{
    void OnEnable()
    {
        var rt = GetComponent<RectTransform>();
        if (!rt) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }
}
