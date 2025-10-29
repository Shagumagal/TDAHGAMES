using UnityEngine;
using EasyPeasyFirstPersonController;

public class TOLActivator : MonoBehaviour
{
    public ToLGame tol;            // arrastra ToL_Game
    public FirstPersonController fpc; // arrastra tu Player (con este script)

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        // Bloquear movimiento y liberar cursor para usar el mouse en el puzzle
        fpc.SetControl(false);
        fpc.SetCursorVisibility(true);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        // Volver a habilitar el control del jugador
        fpc.SetCursorVisibility(false);
        fpc.SetControl(true);
    }
}
