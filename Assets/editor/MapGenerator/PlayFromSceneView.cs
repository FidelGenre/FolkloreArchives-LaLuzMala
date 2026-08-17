// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  PlayFromSceneView.cs — al darle Play, teletransporta TU jugador
//  al punto que estás mirando en la Scene view (foco/pivot de la
//  cámara del editor), mirando en esa dirección. Comodidad de dev.
//
//  Prender/apagar en:
//    Tools > Folklore Archives > Play From Scene View  (tilde)
//
//  100% EDITOR-ONLY: nunca entra al build; jamás toca el netcode del
//  juego. Si el toggle está APAGADO, no hace absolutamente nada → el
//  spawn del inicio (NetGameSpawner, el que trabaja el compañero) queda
//  intacto.
//
//  El spawn del inicio es EN RED (Netcode): el jugador local lo spawnea
//  NetGameSpawner DESPUÉS de conectar, así que teletransportar apenas
//  entrás a Play (como hacía la versión vieja) no servía — la red te
//  colocaba después y te pisaba. Ahora ESPERAMOS a que tu jugador local
//  esté spawneado en red y recién ahí lo movemos. Como el proyecto usa
//  OwnerNetworkTransform (owner-authoritative), mover tu propio jugador
//  sincroniza solo. Fallback single-player: TEST_PLAYER / MapExplorer.
// ============================================================
using UnityEditor;
using UnityEngine;
using Unity.Netcode;

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

        static Vector3 _pivot; static float _yaw; static bool _armed; static double _firstSeen;
        const double SettleDelay = 0.5; // segundos a esperar tras aparecer el jugador (que la red lo asiente)

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
                _pivot = new Vector3(SessionState.GetFloat(SX, 0f), SessionState.GetFloat(SY, 0f), SessionState.GetFloat(SZ, 0f));
                _yaw = SessionState.GetFloat(SYaw, 0f);
                _armed = true;
                _firstSeen = 0;
                EditorApplication.update += Poll; // esperamos al spawn de red (no teletransportamos ya)
            }
            else if (s == PlayModeStateChange.ExitingPlayMode || s == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= Poll;
                _armed = false;
            }
        }

        // Corre cada frame del editor mientras estamos en Play, hasta que el jugador
        // local exista (spawneado por la red) y se asiente; ahí lo mueve UNA vez.
        static void Poll()
        {
            if (!_armed || !EditorApplication.isPlaying)
            {
                EditorApplication.update -= Poll; _armed = false; return;
            }
            var player = LocalPlayer();
            if (player == null) return; // seguir esperando a que la red lo spawnee

            if (_firstSeen == 0) { _firstSeen = EditorApplication.timeSinceStartup; return; }
            if (EditorApplication.timeSinceStartup - _firstSeen < SettleDelay) return; // dejar que se asiente

            MovePlayer(player, _pivot, _yaw);
            _armed = false;
            EditorApplication.update -= Poll;
        }

        // TU jugador local. IMPORTANTE: si hay NetworkManager en la escena (juego en red),
        // ESPERAMOS a tu personaje de red (LocalClient.PlayerObject) y NO caemos al
        // TEST_PLAYER — porque el TEST_PLAYER es solo el que se ve detrás de la pantalla
        // "Crear sala (HOST)" antes de spawnear; moverlo a él no sirve (era el bug: se
        // teletransportaba el temporal en vez de tu personaje real). Devolvemos null hasta
        // que crees la sala y te spawnee → el poll sigue esperando.
        // Solo si NO hay NetworkManager (single-player puro) usamos TEST_PLAYER/MapExplorer.
        static GameObject LocalPlayer()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null)
            {
                if (nm.IsListening && nm.LocalClient != null && nm.LocalClient.PlayerObject != null)
                    return nm.LocalClient.PlayerObject.gameObject;
                return null; // hay red pero todavía no spawneaste (falta crear la sala) → esperar
            }

            var mx = Object.FindFirstObjectByType<MapExplorer>();
            if (mx != null && mx.gameObject.activeInHierarchy) return mx.gameObject;
            var tp = GameObject.Find("TEST_PLAYER");
            return (tp != null && tp.activeInHierarchy) ? tp : null;
        }

        static void MovePlayer(GameObject player, Vector3 pivot, float yaw)
        {
            // apoyar en el suelo (el pivot puede estar flotando)
            float groundY = pivot.y;
            var terrain = Terrain.activeTerrain;
            if (terrain != null) groundY = terrain.SampleHeight(pivot) + terrain.transform.position.y;
            Vector3 spawn = new Vector3(pivot.x, groundY + 1.2f, pivot.z);

            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false; // el CharacterController pisa transform.position si está activo
            player.transform.SetPositionAndRotation(spawn, Quaternion.Euler(0f, yaw, 0f));
            if (cc != null) cc.enabled = true;

            Debug.Log("<color=cyan>Play From Scene View:</color> jugador movido al foco de la Scene view (después del spawn de red).");
        }
    }
}
