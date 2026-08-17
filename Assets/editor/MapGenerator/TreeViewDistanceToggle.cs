// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  TreeViewDistanceToggle.cs — owner: "una herramienta (botón que se
//  tilda/destilda, como Play From Scene View) para ver los árboles desde
//  MÁS LEJOS mientras trabajo. Ahora solo los veo si me acerco mucho".
//
//  Prender/apagar en:
//    Tools > Folklore Archives > Ver árboles de lejos (editor)   (tilde)
//
//  Mientras está TILDADO, fuerza en TODOS los terrenos una distancia de
//  render de árboles grande (y billboard grande, porque los PSX no
//  billboardean bien → se dibujan a malla completa). Es SOLO comodidad de
//  editor: NO entra al build; y al entrar a Play vuelve a la distancia
//  normal para no cargar el juego. Al destildar, restaura la distancia
//  normal (MapLayout.TreeRenderDistance).
//
//  Nota: a malla completa y lejos son MUCHOS árboles → puede poner lento
//  el editor. Es para trabajar cómodo; destildalo cuando no lo necesites.
// ============================================================
using UnityEditor;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    [InitializeOnLoad]
    public static class TreeViewDistanceToggle
    {
        const string MenuPath = "Tools/Folklore Archives/Ver árboles de lejos (editor)";
        const string Pref = "Folklore_TreeViewFar";
        const float FarDistance = 2000f; // distancia de render mientras está tildado (ajustable)

        static TreeViewDistanceToggle()
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
            if (on) Apply(FarDistance, FarDistance);      // aplicar ya, sin esperar el update
            else RestoreNormal();                          // al destildar, volver a lo normal
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
            Apply(FarDistance, FarDistance);
        }

        // Al entrar a Play, restaurar lo normal (para no cargar el juego con árboles lejanos a
        // malla completa). El tilde queda como estaba: al volver a edición, el Enforce lo re-aplica.
        static void OnPlayMode(PlayModeStateChange s)
        {
            if (s == PlayModeStateChange.ExitingEditMode && Enabled) RestoreNormal();
        }

        static void Apply(float treeDist, float billboardDist)
        {
            foreach (var t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t == null) continue;
                if (!Mathf.Approximately(t.treeDistance, treeDist)) t.treeDistance = treeDist;
                if (!Mathf.Approximately(t.treeBillboardDistance, billboardDist)) t.treeBillboardDistance = billboardDist;
            }
        }

        static void RestoreNormal()
        {
            foreach (var t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t == null) continue;
                t.treeDistance = MapLayout.TreeRenderDistance;
                t.treeBillboardDistance = MapLayout.TreeBillboardDistance;
            }
        }
    }
}
