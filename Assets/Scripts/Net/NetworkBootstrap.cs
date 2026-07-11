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
using Unity.Services.Vivox;
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
        string _voiceStatus = "";
        bool _voiceReady, _muted;
        bool _pushToTalk;   // false = micrófono abierto (V mutea); true = hablar con T
        bool _transmitting; // estado real del micrófono, para no spamear Mute/Unmute
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
            if (kb != null)
            {
                if (kb.f9Key.wasPressedThisFrame) SetCursorFree(!_cursorFree);
                if (kb.vKey.wasPressedThisFrame && _voiceReady) ToggleMute();
            }
            // mientras esté libre, forzarlo (el jugador re-bloquea si no)
            if (_cursorFree)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            Update3DVoice();       // posición 3D del jugador en el canal de voz
            UpdateVoiceTransmit(); // aplica mute / push-to-talk
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
                return;
            }

            // Voz (Vivox): init + login. Usa el mismo proyecto UGS.
            try
            {
                await VivoxService.Instance.InitializeAsync();
                await VivoxService.Instance.LoginAsync();
                _voiceReady = true;
                _voiceStatus = "Voz lista";
            }
            catch (System.Exception e)
            {
                _voiceStatus = "Voz OFF (¿activaste Vivox en el dashboard?): " + e.Message;
            }
        }

        string _channel;   // canal de voz activo (para el update 3D)

        async Task JoinVoice()
        {
            if (!_voiceReady || _session == null) return;
            try
            {
                // Canal POSICIONAL 3D: se escucha al otro según distancia y dirección.
                //   audible 45m (a partir de ahí no se oye), conversacional 4m (adentro
                //   se oye a volumen pleno), caída inversa (natural).
                var props = new Channel3DProperties(45, 4, 1.0f, AudioFadeModel.InverseByDistance);
                _channel = _session.Code;
                await VivoxService.Instance.JoinPositionalChannelAsync(_channel, ChatCapability.AudioOnly, props);
                _voiceStatus = "🎙 Voz 3D conectada (V = mutear)";
            }
            catch (System.Exception e) { _voiceStatus = "Voz: error al unir canal — " + e.Message; }
        }

        // Actualiza la posición/orientación 3D del jugador local en el canal, cada frame.
        // Camera.main = la cámara del personaje que controlás (persona o perro), así que
        // Vivox sabe desde dónde hablás y desde dónde escuchás.
        void Update3DVoice()
        {
            if (!_voiceReady || string.IsNullOrEmpty(_channel)) return;
            var cam = Camera.main;
            if (cam != null) VivoxService.Instance.Set3DPosition(cam.gameObject, _channel);
        }

        async Task LeaveVoice()
        {
            _channel = null;
            if (!_voiceReady) return;
            try { await VivoxService.Instance.LeaveAllChannelsAsync(); } catch { }
        }

        void ToggleMute() => _muted = !_muted;   // el flag; lo aplica UpdateVoiceTransmit

        // Decide si el micrófono transmite según el modo:
        //   - micrófono abierto: transmite salvo que estés muteado (V)
        //   - push-to-talk: transmite solo mientras mantenés T
        void UpdateVoiceTransmit()
        {
            if (!_voiceReady || string.IsNullOrEmpty(_channel)) return;
            var kb = Keyboard.current;
            bool want = _pushToTalk ? (kb != null && kb.tKey.isPressed) : !_muted;
            if (want == _transmitting) return;
            _transmitting = want;
            if (want) VivoxService.Instance.UnmuteInputDevice();
            else VivoxService.Instance.MuteInputDevice();
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

        // Si una conexión anterior dejó el NetworkManager prendido, cerrarlo antes de
        // arrancar otra (si no, tira "Failed to start the network manager").
        async Task EnsureNmStopped()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            if (nm.IsListening || nm.ShutdownInProgress)
            {
                nm.Shutdown();
                float t = 0f;
                while ((nm.IsListening || nm.ShutdownInProgress) && t < 2f)
                { await Task.Delay(50); t += 0.05f; }
            }
        }

        async Task Host()
        {
            if (_busy) return;
            if (!NetReady(out var nerr)) { _status = nerr; return; }
            _busy = true; _status = "Creando sala…";
            try
            {
                await EnsureNmStopped();
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
                await EnsureNmStopped();
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
            await LeaveVoice();
            if (_session != null)
            {
                try { await _session.LeaveAsync(); } catch { /* ya cerrada */ }
                _session = null;
            }
            _status = "Desconectado";
            _voiceStatus = _voiceReady ? "Voz lista" : _voiceStatus;
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
            _ = JoinVoice();      // entrar al canal de voz de la sala
        }

        static string Short(string id) => string.IsNullOrEmpty(id) ? "?" :
            (id.Length > 6 ? id.Substring(0, 6) : id);

        void OnGUI()
        {
            if (_box == null) _box = new GUIStyle(GUI.skin.box) { richText = true, alignment = TextAnchor.UpperLeft, wordWrap = true };
            const float w = 280f;
            GUILayout.BeginArea(new Rect(Screen.width - w - 12f, 12f, w, 300f), _box);
            GUILayout.Label("<b>ONLINE (co-op)</b>   <size=10>[F9: mouse]</size>");
            if (!_cursorFree) GUILayout.Label("<color=yellow>Apretá F9 para liberar el mouse y clickear</color>");
            GUILayout.Label(_status);
            if (!string.IsNullOrEmpty(_voiceStatus))
                GUILayout.Label(_muted ? "<color=orange>" + _voiceStatus + "</color>" : _voiceStatus);
            GUILayout.Space(6);
            if (_session == null)
            {
                // elección de personaje ANTES de entrar — botones grandes, el elegido
                // queda resaltado en verde.
                GUILayout.Label("<b>ELEGÍ TU PERSONAJE:</b>");
                GUILayout.BeginHorizontal();
                var prev = GUI.color;
                GUI.color = _role == 0 ? Color.green : Color.gray;
                if (GUILayout.Button("PERSONA", GUILayout.Height(30))) _role = 0;
                GUI.color = _role == 1 ? Color.green : Color.gray;
                if (GUILayout.Button("PERRO", GUILayout.Height(30))) _role = 1;
                GUI.color = prev;
                GUILayout.EndHorizontal();
                GUILayout.Label(_role == 1 ? "→ vas a ser el PERRO 🐕" : "→ vas a ser la PERSONA");
                GUILayout.Space(6);

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
                // ── controles de voz (solo si el canal está activo) ──
                if (_voiceReady && !string.IsNullOrEmpty(_channel))
                {
                    if (_pushToTalk)
                        GUILayout.Label(_transmitting ? "<color=lime>🎙 HABLANDO (T)</color>" : "Push-to-talk: mantené <b>T</b> para hablar");
                    else
                        GUILayout.Label(_muted ? "<color=orange>🔇 Muteado (V)</color>" : "🎙 Micrófono abierto (V = mutear)");

                    if (GUILayout.Button(_pushToTalk ? "Modo: Push-to-talk → pasar a Mic abierto"
                                                     : "Modo: Mic abierto → pasar a Push-to-talk"))
                    {
                        _pushToTalk = !_pushToTalk;
                        _muted = false;
                    }
                    GUILayout.Space(4);
                }
                if (GUILayout.Button("Salir de la sala")) _ = Leave();
            }
            GUILayout.EndArea();
        }
    }
}
