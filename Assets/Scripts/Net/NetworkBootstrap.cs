// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  NetworkBootstrap.cs — conexión online 2 jugadores por CÓDIGO.
//  Distributed Authority + Relay vía la Sessions API de
//  com.unity.services.multiplayer (2.2.4). NGO 2.13.
//
//  Host: "Crear sala" → muestra un código. El otro pega el código
//  en "Unirse". Sin abrir puertos ni compartir IP (Relay).
//
//  ETAPA 1a: solo la conexión + UI. El spawn de personajes en red
//  (persona = host, perro = cliente) y los controladores por-dueño
//  vienen en la etapa 1b (necesitan el NetworkManager en la escena).
// ============================================================
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FolkloreArchives.Net
{
    public class NetworkBootstrap : MonoBehaviour
    {
        public int maxPlayers = 2;

        ISession _session;
        string _status = "Iniciando servicios…";
        string _joinCode = "";
        bool _busy;
        bool _cursorFree;   // F9 libera el mouse para poder clickear el panel
        int _role;          // 0 = persona, 1 = perro (elección antes de entrar)
        GUIStyle _box;

        // pasa la elección (1 byte) al servidor por la ConnectionData; el NetGameSpawner
        // la lee en el Connection Approval y spawnea persona o perro.
        void SendRole()
        {
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.NetworkConfig.ConnectionData = new byte[] { (byte)_role };
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f9Key.wasPressedThisFrame)
                SetCursorFree(!_cursorFree);
            // mientras esté libre, forzarlo (el jugador re-bloquea si no)
            if (_cursorFree)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        void SetCursorFree(bool free)
        {
            _cursorFree = free;
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = free;
        }

        async void Awake()
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                _status = "Listo. ID: " + Short(AuthenticationService.Instance.PlayerId);
            }
            catch (System.Exception e)
            {
                _status = "Error de servicios: " + e.Message +
                          "\n(¿activaste Relay + Authentication anónima en el dashboard?)";
            }
        }

        static bool NetReady(out string err)
        {
            if (NetworkManager.Singleton == null)
            { err = "Falta el NetworkManager en la escena.\nRegenerá el mapa (Tools > Folklore Archives > Generate)."; return false; }
            if (NetworkManager.Singleton.NetworkConfig == null ||
                NetworkManager.Singleton.NetworkConfig.NetworkTransport == null)
            { err = "NetworkManager sin transporte configurado.\nRegenerá el mapa."; return false; }
            err = null; return true;
        }

        async Task Host()
        {
            if (_busy) return;
            if (!NetReady(out var nerr)) { _status = nerr; return; }
            _busy = true; _status = "Creando sala…";
            try
            {
                SendRole();
                var options = new SessionOptions { MaxPlayers = maxPlayers }
                    .WithRelayNetwork();   // host-client (el que crea = host)
                _session = await MultiplayerService.Instance.CreateSessionAsync(options);
                OnConnected();
                _status = "SALA CREADA\nCódigo: " + _session.Code + "\n(pasáselo al otro jugador)";
            }
            catch (System.Exception e) { _status = "Error al crear: " + e.Message; Debug.LogException(e); }
            _busy = false;
        }

        async Task Join(string code)
        {
            if (_busy || string.IsNullOrWhiteSpace(code)) return;
            if (!NetReady(out var nerr)) { _status = nerr; return; }
            _busy = true; _status = "Uniéndose…";
            try
            {
                SendRole();
                _session = await MultiplayerService.Instance
                    .JoinSessionByCodeAsync(code.Trim().ToUpperInvariant());
                OnConnected();
                _status = "CONECTADO a " + _session.Code;
            }
            catch (System.Exception e) { _status = "Error al unirse: " + e.Message; Debug.LogException(e); }
            _busy = false;
        }

        async Task Leave()
        {
            if (_session != null)
            {
                try { await _session.LeaveAsync(); } catch { /* ya cerrada */ }
                _session = null;
            }
            _status = "Desconectado";
        }

        // Al entrar a una sala, apagar el jugador single-player (su cámara/AudioListener
        // chocarían con los jugadores en red). El cielo/grade ya quedaron aplicados por
        // sus componentes al arrancar, así que se mantienen aunque lo apaguemos.
        void OnConnected()
        {
            var tp = GameObject.Find("TEST_PLAYER");
            if (tp != null) tp.SetActive(false);
            var dog = GameObject.Find("DOG");
            if (dog != null) dog.SetActive(false); // el perro single-player; en red se spawnea aparte
            SetCursorFree(false); // a jugar: mouse capturado para el jugador en red
        }

        static string Short(string id) => string.IsNullOrEmpty(id) ? "?" :
            (id.Length > 6 ? id.Substring(0, 6) : id);

        void OnGUI()
        {
            if (_box == null) _box = new GUIStyle(GUI.skin.box) { richText = true, alignment = TextAnchor.UpperLeft, wordWrap = true };
            const float w = 280f;
            GUILayout.BeginArea(new Rect(Screen.width - w - 12f, 12f, w, 240f), _box);
            GUILayout.Label("<b>ONLINE (co-op)</b>   <size=10>[F9: mouse]</size>");
            if (!_cursorFree) GUILayout.Label("<color=yellow>Apretá F9 para liberar el mouse y clickear</color>");
            GUILayout.Label(_status);
            GUILayout.Space(6);
            if (_session == null)
            {
                // elección de personaje ANTES de entrar
                GUILayout.Label("Jugar como:");
                GUILayout.BeginHorizontal();
                if (GUILayout.Toggle(_role == 0, " Persona", GUILayout.Width(120))) _role = 0;
                if (GUILayout.Toggle(_role == 1, " Perro")) _role = 1;
                GUILayout.EndHorizontal();
                GUILayout.Space(4);

                GUI.enabled = !_busy;
                if (GUILayout.Button("Crear sala (HOST)")) _ = Host();
                GUILayout.Space(4);
                GUILayout.Label("Código para unirse:");
                GUILayout.BeginHorizontal();
                _joinCode = GUILayout.TextField(_joinCode ?? "", 10);
                if (GUILayout.Button("Unirse", GUILayout.Width(70))) _ = Join(_joinCode);
                GUILayout.EndHorizontal();
                GUI.enabled = true;
            }
            else
            {
                if (GUILayout.Button("Salir de la sala")) _ = Leave();
            }
            GUILayout.EndArea();
        }
    }
}
