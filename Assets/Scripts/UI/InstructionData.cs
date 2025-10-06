using UnityEngine;

[System.Serializable]
public struct InstructionData {
  public string title;
  [TextArea] public string body;
  public AudioClip sfx;        // opcional: sonido al abrir
  public KeyCode continueKey;  // si quieres cambiar Enter por otro
}
