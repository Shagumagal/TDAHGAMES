using UnityEngine;

/// Sonidos de pasos para la gallina. Lee la velocidad (Rigidbody o Animator "Speed")
/// y emite pasos con cadencia variable. Soporta superficies por tag.
[RequireComponent(typeof(AudioSource))]
public class FootstepAudio : MonoBehaviour
{
    [Header("Refs (opcional)")]
    public Rigidbody runnerRb;            // Rigidbody del Runner (cápsula)
    public Animator animator;             // Animator de la gallina (param "Speed" 0..1)
    public Transform footRayOrigin;       // desde dónde chequear el suelo (si null = this)
    public LayerMask groundMask = ~0;     // capas consideradas suelo

    [Header("Clips")]
    public AudioClip[] defaultClips;
    [System.Serializable] public class SurfaceClips {
        public string tag;                // tag del collider del suelo
        public AudioClip[] clips;
    }
    public SurfaceClips[] surfaceOverrides;

    [Header("Tuning")]
    public float expectedMaxSpeed = 4f;   // igual a maxSpeed del SSTRunner
    public float minPlaySpeed = 0.15f;    // no sonar si la velocidad < umbral
    public float baseStepsPerSec = 1.8f;  // a Speed=0.5 aprox. 1.8 pasos/seg
    public float runMultiplier = 1.6f;    // a Speed=1 la cadencia = base*runMultiplier
    public float volume = 0.45f;
    public Vector2 pitchJitter = new Vector2(0.95f, 1.05f);
    public float groundCheckDist = 0.6f;  // raycast para saber si está “en piso”

    AudioSource _src;
    float _nextStepAt;
    bool _leftFoot = false;

    void Reset() {
        // autoconfigurar cuando se añade el componente
        _src = GetComponent<AudioSource>();
        if (!_src) _src = gameObject.AddComponent<AudioSource>();
        _src.playOnAwake = false;
        _src.spatialBlend = 1f;       // 3D
        _src.rolloffMode = AudioRolloffMode.Linear;
        _src.minDistance = 2f; _src.maxDistance = 12f;
    }

    void Awake() {
        if (!_src) _src = GetComponent<AudioSource>();
        if (!animator) animator = GetComponent<Animator>();
        if (!runnerRb) runnerRb = GetComponentInParent<Rigidbody>();
        if (!footRayOrigin) footRayOrigin = transform;
    }

    void Update() {
        float normSpeed = GetNormalizedSpeed();          // 0..1
        bool grounded  = IsGrounded();

        if (grounded && normSpeed >= minPlaySpeed) {
            float cadence = baseStepsPerSec * Mathf.Lerp(1f, runMultiplier, normSpeed); // pasos/seg
            float interval = 1f / Mathf.Max(0.01f, cadence);

            if (Time.time >= _nextStepAt) {
                PlayStep();
                _nextStepAt = Time.time + interval * 0.5f; // medio intervalo por pie alternado
                _leftFoot = !_leftFoot;
            }
        } else {
            // reinicia para que no “acumule” pasos al volver a moverse
            _nextStepAt = Time.time + 0.05f;
        }
    }

    float GetNormalizedSpeed() {
        // Prioridad 1: Animator "Speed" (0..1)
        if (animator && animator.runtimeAnimatorController) {
            if (animator.HasParameterOfType("Speed", AnimatorControllerParameterType.Float)) {
                float s = animator.GetFloat("Speed");
                return Mathf.Clamp01(s);
            }
        }
        // Prioridad 2: Rigidbody del Runner
        if (runnerRb) {
            Vector3 v = runnerRb.linearVelocity; v.y = 0f;
            return expectedMaxSpeed > 0f ? Mathf.Clamp01(v.magnitude / expectedMaxSpeed) : 0f;
        }
        return 0f;
    }

    bool IsGrounded() {
        Ray ray = new Ray(footRayOrigin.position + Vector3.up * 0.05f, Vector3.down);
        return Physics.Raycast(ray, groundCheckDist, groundMask, QueryTriggerInteraction.Ignore);
    }

    void PlayStep() {
        var clip = PickClipBySurface(out float pitch);
        if (!clip) return;
        _src.pitch = pitch;
        _src.volume = volume;
        _src.PlayOneShot(clip);
    }

    AudioClip PickClipBySurface(out float pitch) {
        pitch = Random.Range(pitchJitter.x, pitchJitter.y);

        // Raycast para saber el tag del suelo
        Ray ray = new Ray(footRayOrigin.position + Vector3.up * 0.05f, Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, groundCheckDist, groundMask, QueryTriggerInteraction.Ignore)) {
            string t = hit.collider.tag;
            if (!string.IsNullOrEmpty(t)) {
                foreach (var s in surfaceOverrides) {
                    if (s != null && s.tag == t && s.clips != null && s.clips.Length > 0) {
                        return s.clips[Random.Range(0, s.clips.Length)];
                    }
                }
            }
        }
        // por defecto
        if (defaultClips != null && defaultClips.Length > 0) {
            return defaultClips[Random.Range(0, defaultClips.Length)];
        }
        return null;
    }

    // --- Opcional: si prefieres exactitud con Animation Events ---
    // Crea eventos "StepL" y "StepR" en tus clips y llama a estos métodos.
    public void StepL(){ _leftFoot = true;  PlayStep(); }
    public void StepR(){ _leftFoot = false; PlayStep(); }
}

// Helper para chequear parámetros del Animator sin exceptions.
static class AnimatorExt {
    public static bool HasParameterOfType(this Animator self, string name, AnimatorControllerParameterType type){
        foreach (var p in self.parameters) if (p.type == type && p.name == name) return true;
        return false;
    }
}
