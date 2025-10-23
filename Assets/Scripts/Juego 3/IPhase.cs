using System.Collections.Generic;

public interface IPhase
{
    void StartPhase();          // Se llama al activar la fase
    void Tick();                // Llamado en Update del manager
    bool IsDone { get; }        // Señala fin de la fase
    Dictionary<string, object> GetSummary(); // Métricas de la fase
}
