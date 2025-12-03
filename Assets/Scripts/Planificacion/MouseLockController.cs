using UnityEngine;

public class MouseLockController : MonoBehaviour
{
    [Header("Opciones")]
    [Tooltip("Bloquear el mouse automáticamente al iniciar el juego.")]
    public bool lockOnStart = true;

    [Tooltip("Tecla para liberar / volver a bloquear el cursor.")]
    public KeyCode toggleKey = KeyCode.Escape;

    bool locked;

    void Start()
    {
        if (lockOnStart)
            LockCursor();
    }

    void Update()
    {
        // Si el juego está pausado (Time.timeScale == 0), no hacer nada
        if (Time.timeScale == 0f) return;

        // Con la tecla (por defecto ESC) liberamos / bloqueamos
        if (Input.GetKeyDown(toggleKey))
        {
            if (locked) UnlockCursor();
            else        LockCursor();
        }

        // Si está desbloqueado y el jugador hace clic, lo volvemos a bloquear
        // (útil en WebGL, después de que el usuario da permiso con un clic)
        if (!locked && Input.GetMouseButtonDown(0))
        {
            LockCursor();
        }
    }

    void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked; // bloquea en el centro
        Cursor.visible   = false;                 // oculta el puntero
        locked = true;
    }

    void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;   // lo suelta
        Cursor.visible   = true;                  // muestra el puntero
        locked = false;
    }
}
