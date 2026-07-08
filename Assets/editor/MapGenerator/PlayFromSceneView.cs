// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  PlayFromSceneView.cs — al darle Play, teletransporta el
//  jugador de prueba al punto que estás mirando en la Scene view
//  (el foco/pivot de la cámara del editor), mirando en esa
//  dirección. Comodidad de dev: probás desde donde estás viendo.
//
//  Se puede prender/apagar en:
//    Tools > Folklore Archives > Play From Scene View  (tilde)
// ============================================================
using UnityEditor;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    [InitializeOnLoad]
    public static class PlayFromSceneView
    {
        const string MenuPath = "Tools/Folklore Archives/Play From Scene View";
        const string PrefEnabled = "Folklore_PFSV_Enabled";
        // SessionState sobrevive el domain reload entre ExitingEditMode y EnteredPlayMode
        const string SHas = "Folklore_PFSV_Has";
        const string SX = "Folklore_PFSV_X", SY = "Folklore_PFSV_Y", SZ = "Folklore_PFSV_Z", SYaw = "Folklore_PFSV_Yaw";

        static PlayFromSceneView()
        {
            EditorApplication.playModeStateChanged += OnChange;
        }

        static bool Enabled => EditorPrefs.GetBool(PrefEnabled, true);

        [MenuItem(MenuPath)]
        static void Toggle() => EditorPrefs.SetBool(PrefEnabled, !Enabled);
        [MenuItem(MenuPath, true)]
        static bool ToggleValidate() { Menu.SetChecked(MenuPath, Enabled); return true; }

        static void OnChange(PlayModeStateChange s)
        {
            if (s == PlayModeStateChange.ExitingEditMode)
            {
                SessionState.SetBool(SHas, false);
                if (!Enabled) return;
                var sv = SceneView.lastActiveSceneView;
                if (sv == null || sv.camera == null) return;
                Vector3 pivot = sv.pivot; // el punto que estás enfocando
                SessionState.SetFloat(SX, pivot.x);
                SessionState.SetFloat(SY, pivot.y);
                SessionState.SetFloat(SZ, pivot.z);
                SessionState.SetFloat(SYaw, sv.camera.transform.eulerAngles.y);
                SessionState.SetBool(SHas, true);
            }
            else if (s == PlayModeStateChange.EnteredPlayMode)
            {
                if (!Enabled || !SessionState.GetBool(SHas, false)) return;
                Vector3 pivot = new Vector3(SessionState.GetFloat(SX, 0f), SessionState.GetFloat(SY, 0f), SessionState.GetFloat(SZ, 0f));
                MovePlayer(pivot, SessionState.GetFloat(SYaw, 0f));
            }
        }

        static void MovePlayer(Vector3 pivot, float yaw)
        {
            var mx = Object.FindFirstObjectByType<MapExplorer>();
            var player = mx != null ? mx.gameObject : GameObject.Find("TEST_PLAYER");
            if (player == null) return;

            // apoyar en el suelo (el pivot puede estar flotando)
            float groundY = pivot.y;
            var terrain = Terrain.activeTerrain;
            if (terrain != null) groundY = terrain.SampleHeight(pivot) + terrain.transform.position.y;
            Vector3 spawn = new Vector3(pivot.x, groundY + 1.2f, pivot.z);

            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false; // el CharacterController pisa transform.position si está activo
            player.transform.position = spawn;
            player.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (cc != null) cc.enabled = true;

            Debug.Log("<color=cyan>Play From Scene View:</color> jugador teletransportado al foco de la Scene view.");
        }
    }
}
