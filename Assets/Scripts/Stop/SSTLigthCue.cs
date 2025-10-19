using UnityEngine;
using System.Collections;

public class SSTLightCue : MonoBehaviour
{
    public CanvasGroup green;   // Image verde
    public CanvasGroup red;     // Image roja
    public float greenAlpha = 0.6f;
    public float redAlpha = 0.6f;

    // NUEVO: referencia al cue que gira (la gallina con sombrero)
    public StopCueLookAt stopCue;   // arrástralo en el Inspector (el GO que tiene StopCueLookAt)

    void Awake(){ Set(green,0); Set(red,0); }

    public void ShowGreen(float fadeMs = 200f){
        StopAllCoroutines();
        StartCoroutine(FadeTo(green, greenAlpha, fadeMs/1000f));
        StartCoroutine(FadeTo(red, 0f, 0.12f));

        // NUEVO: en verde mira de ESPALDAS
        if (stopCue) stopCue.SetStop(false);
    }

    public void ShowRedInstant(){
        StopAllCoroutines();
        Set(green,0); Set(red,redAlpha);

        // NUEVO: en rojo MIRA al jugador
        if (stopCue) stopCue.SetStop(true);
    }

    public void Clear(float fadeMs=120f){
        StopAllCoroutines();
        StartCoroutine(FadeTo(green,0f, fadeMs/1000f));
        StartCoroutine(FadeTo(red,0f, fadeMs/1000f));
    }

    IEnumerator FadeTo(CanvasGroup cg, float a, float t){
        if(!cg) yield break; float s=cg.alpha; float tt=0f;
        cg.gameObject.SetActive(true);
        while(tt<t){ tt+=Time.unscaledDeltaTime; cg.alpha=Mathf.Lerp(s,a,tt/t); yield return null; }
        cg.alpha=a; cg.gameObject.SetActive(cg.alpha>0.001f);
    }

    void Set(CanvasGroup cg,float a){ if(!cg) return; cg.alpha=a; cg.gameObject.SetActive(a>0.001f); }
}
