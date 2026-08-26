// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  DebugCheckpointButtons.cs — owner: "botones para dar Play y aparecer desde
//  cierta escena, así no pruebo toda la secuencia de 0". Dibuja botones en el
//  Scene View (arriba a la izquierda): cada uno setea el checkpoint de DEBUG
//  (EditorPrefs, leído por OpeningDriveSequence/YpfStorySequence) y entra a Play.
//    · Meando        -> secuencia completa desde el principio.
//    · YPF bajada    -> saltea el meado y el viaje: aparece con todos bajándose.
//    · Tienda        -> además saltea la dispersión: chica en el baño + amigos
//                       ubicados, listo para golpear la oficina.
//    · Rancho (cañas)-> además saltea TODO el campamento (carpas/noche/Rufus-Luz
//                       Mala/despertar/charla mañana): arranca LIBRE en "andá al
//                       rancho a pedir unas cañas" (letrina/viejo/vieja/ovejas).
// ============================================================
using UnityEditor;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    [InitializeOnLoad]
    public static class DebugCheckpointButtons
    {
        static readonly string[] Names = { "▶ 0 · Meando", "▶ 1 · YPF bajada", "▶ 2 · En el auto (al campamento)", "▶ 3 · Rancho (cañas)" };

        static DebugCheckpointButtons()
        {
            SceneView.duringSceneGui += Draw;
        }

        static void Draw(SceneView sv)
        {
            Handles.BeginGUI();

            float x = 8f, y = 8f, w = 230f, h = 24f;
            var title = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.white } };
            GUI.Label(new Rect(x, y, 220f, 18f), "▶ PLAY desde checkpoint:", title);
            y += 20f;

            int cur = EditorPrefs.GetInt(FolkloreArchives.OpeningDriveSequence.CheckpointKey, 0);
            for (int i = 0; i < Names.Length; i++)
            {
                var style = new GUIStyle(GUI.skin.button) { fontSize = 11, alignment = TextAnchor.MiddleLeft };
                if (i == cur) { style.fontStyle = FontStyle.Bold; style.normal.textColor = new Color(0.4f, 1f, 0.4f); }
                if (GUI.Button(new Rect(x, y, w, h), Names[i], style))
                {
                    EditorPrefs.SetInt(FolkloreArchives.OpeningDriveSequence.CheckpointKey, i);
                    FolkloreArchives.OpeningDriveSequence.SkipForTesting = false; // que no corte la secuencia
                    if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
                }
                y += h + 3f;
            }

            Handles.EndGUI();
        }
    }
}
