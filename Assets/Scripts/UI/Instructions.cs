using System.Collections;
using UnityEngine;

public static class Instructions
{
  /// Muestra una pantalla y espera a que el usuario continúe.
  public static IEnumerator ShowAndWait(InstructionData data)
  {
    // Asegura que existe el panel (usa tu StartUIPanel; lo crea si no está)
    if (!StartUIPanel.Instance) {
      var go = new GameObject("StartUIPanel_Runtime");
      var sp = go.AddComponent<StartUIPanel>();
      sp.BuildInScene();
    }

    if (StartUIPanel.Instance) {
      if (data.continueKey != KeyCode.None)
        StartUIPanel.Instance.continueKey = data.continueKey;

      bool done = false;
      StartUIPanel.Instance.Show(data.title, data.body, () => done = true);
      if (data.sfx) { var src = Object.FindFirstObjectByType<AudioSource>(); if (src) src.PlayOneShot(data.sfx); }

      while (!done) yield return null;
    }
  }

  /// Ejecuta una SECuencia completa (lista de pasos).
  public static IEnumerator RunSequence(InstructionData[] steps)
  {
    for (int i = 0; i < steps.Length; i++)
      yield return ShowAndWait(steps[i]);
  }
}
