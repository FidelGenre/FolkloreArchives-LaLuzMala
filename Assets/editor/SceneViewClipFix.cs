// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  SceneViewClipFix.cs — arregla el "recorte" del Scene view en mapas grandes
//  (cuando te acercás a algo, se empieza a borrar). Es el "Dynamic Clipping" del
//  Scene view: con un mapa enorme, Unity agranda el near-clip automáticamente y
//  recorta lo cercano. Acá lo APAGAMOS y fijamos near/far razonables.
//
//  OJO: esto es config del EDITOR (la vista Scene), NO se hornea en la escena y
//  NO afecta al juego — la Main Camera del juego tiene su propio near/far. Por eso
//  no se puede "guardar" en un asset: se re-aplica solo cada vez que Unity carga o
//  recompila (así el owner no tiene que tocar ningún ícono escondido).
// ============================================================
using UnityEditor;
using UnityEngine;

namespace FolkloreArchives.EditorTools
{
    [InitializeOnLoad]
    public static class SceneViewClipFix
    {
        const float Near = 0.1f;    // ver de cerca sin que se borre
        const float Far  = 6000f;   // suficiente para ver todo el mapa

        static SceneViewClipFix()
        {
            EditorApplication.delayCall += Apply;   // en load/recompile
            SceneView.duringSceneGui   += OnSceneGui;
        }

        static void OnSceneGui(SceneView sv) => ApplyTo(sv);

        static void Apply()
        {
            foreach (SceneView sv in SceneView.sceneViews) ApplyTo(sv);
        }

        static void ApplyTo(SceneView sv)
        {
            if (sv == null) return;
            var cs = sv.cameraSettings;
            if (!cs.dynamicClip && Mathf.Approximately(cs.nearClip, Near) &&
                Mathf.Approximately(cs.farClip, Far))
                return;                              // ya está OK → no repintar de gusto
            cs.dynamicClip = false;
            cs.nearClip = Near;
            cs.farClip  = Far;
            sv.Repaint();
        }
    }
}
