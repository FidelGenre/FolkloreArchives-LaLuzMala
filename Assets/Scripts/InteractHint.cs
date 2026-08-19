// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  InteractHint.cs — cartel de interacción UNIFICADO. owner: "me gustó
//  cómo quedó el [E] Abrir cajuela, ese tamaño y debajo del círculo,
//  hacelo con todas las demás cosas interactuables". Un solo lugar/estilo
//  para TODO lo que se toca con E (auto, puertas, cajuela, objetos, etc.).
// ============================================================
using UnityEngine;

namespace FolkloreArchives
{
    public static class InteractHint
    {
        static GUIStyle _style;

        // dibuja el cartel chico, centrado, justo DEBAJO de la mira (el círculo).
        public static void Draw(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (_style == null)
                _style = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter, wordWrap = true, richText = true };
            _style.normal.textColor = Color.white;
            GUI.Label(new Rect(Screen.width * 0.5f - 200f, Screen.height * 0.5f + 34f, 400f, 40f), text, _style);
        }
    }
}
