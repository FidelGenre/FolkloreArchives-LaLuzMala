// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  GrassViewDistanceToggle.cs — owner: "un botón similar al de los árboles
//  para poder ver el pasto de lejos". Gemelo de TreeViewDistanceToggle, pero
//  para el CÉSPED (details del terreno).
//
//  Prender/apagar en:
//    Tools > Folklore Archives > Ver pasto de lejos (editor)   (tilde)
//
//  Mientras está TILDADO, fuerza en TODOS los terrenos una distancia de render
//  de details grande (detailObjectDistance) y corre el fade del shader de pasto
//  a esa misma distancia (ForestBuilder.SetGrassFadeGlobals), si no el césped se
//  desvanecería igual al corte corto. Es SOLO comodidad de editor: NO entra al
//  build; al entrar a Play vuelve a la distancia normal (MapLayout.
//  DetailRenderDistance) para no cargar el juego. Al destildar, restaura.
//
//  Nota: mucho pasto lejano puede poner lento el editor; destildalo cuando no lo
//  necesites.
// ============================================================
using UnityEditor;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    [InitializeOnLoad]
    public static class GrassViewDistanceToggle
    {
        const string MenuPath = "Tools/Folklore Archives/Ver pasto de lejos (editor)";
        const string Pref = "Folklore_GrassViewFar";
        const float FarDistance = 250f; // distancia de render de pasto mientras está tildado (ajustable)

        static GrassViewDistanceToggle()
        {
            EditorApplication.update += Enforce;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        static bool Enabled => EditorPrefs.GetBool(Pref, false);

        [MenuItem(MenuPath)]
        static void Toggle()
        {
            bool on = !Enabled;
            EditorPrefs.SetBool(Pref, on);
            if (on) Apply(FarDistance);        // aplicar ya, sin esperar el update
            else RestoreNormal();               // al destildar, volver a lo normal
            SceneView.RepaintAll();
        }

        [MenuItem(MenuPath, true)]
        static bool Validate() { Menu.SetChecked(MenuPath, Enabled); return true; }

        // Mientras está tildado y en modo EDICIÓN, re-aplica cada ~0.5s (por si Generate o el
        // toggle día/noche pisaron la distancia). En Play no toca nada (el juego usa lo normal).
        static double _next;
        static void Enforce()
        {
            if (!Enabled || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.timeSinceStartup < _next) return;
            _next = EditorApplication.timeSinceStartup + 0.5;
            Apply(FarDistance);
        }

        // Al entrar a Play, restaurar lo normal (para no cargar el juego con pasto lejano).
        // El tilde queda como estaba: al volver a edición, el Enforce lo re-aplica.
        static void OnPlayMode(PlayModeStateChange s)
        {
            if (s == PlayModeStateChange.ExitingEditMode && Enabled) RestoreNormal();
        }

        static void Apply(float detailDist)
        {
            foreach (var t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t == null) continue;
                if (!Mathf.Approximately(t.detailObjectDistance, detailDist)) t.detailObjectDistance = detailDist;
            }
            ForestBuilder.SetGrassFadeGlobals(detailDist); // el fade del shader de pasto acompaña la distancia
        }

        static void RestoreNormal()
        {
            foreach (var t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t == null) continue;
                t.detailObjectDistance = MapLayout.DetailRenderDistance;
            }
            ForestBuilder.SetGrassFadeGlobals(MapLayout.DetailRenderDistance);
        }
    }
}
