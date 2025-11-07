using UnityEngine;
using TMPro;

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class PinToTopRight : MonoBehaviour
{
    public Vector2 margin = new Vector2(16f, 16f);
    public bool forceEveryFrame = true;

    RectTransform rt;
    TMP_Text tmp;

    void OnEnable()
    {
        rt = GetComponent<RectTransform>();
        tmp = GetComponent<TMP_Text>();
        Apply();
    }

    void LateUpdate()
    {
        if (forceEveryFrame) Apply();
    }

    void Apply()
    {
        if (!rt) return;

        // Anclas fijas arriba-derecha
        rt.anchorMin = new Vector2(1, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot     = new Vector2(1, 1);
        rt.anchoredPosition = new Vector2(-margin.x, -margin.y);

        // Estilo seguro
        if (tmp)
        {
            tmp.alignment = TextAlignmentOptions.TopRight;
            tmp.color = Color.black;
            tmp.raycastTarget = false;
        }

        // Dibuja por encima de hermanos (por si un overlay intenta taparla)
        transform.SetAsLastSibling();
    }
}
