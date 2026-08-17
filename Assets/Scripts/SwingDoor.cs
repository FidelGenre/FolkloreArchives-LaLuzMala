// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  SwingDoor.cs — puerta batiente que abre/cierra con E (edificio YPF, baño).
//  owner: "las puertas de la YPF y la del baño se puedan abrir". Gira la malla
//  alrededor de una bisagra vertical en su borde. Muestra cartel "[E] Abrir"
//  cuando estás cerca y mirándola. Busca la cámara ACTIVA por el AudioListener
//  (aguanta el cambio persona/perro; no depende de Camera.main).
// ============================================================
using UnityEngine;
using UnityEngine.InputSystem;

namespace FolkloreArchives
{
    public class SwingDoor : MonoBehaviour
    {
        public float openAngle = 95f;         // cuánto abre
        public float speed     = 220f;        // grados/seg
        public float range     = 3.5f;        // distancia para interactuar
        public float lookAngle = 45f;         // tolerancia de mira (°)
        public bool  hingeOnPositive = false; // qué borde es la bisagra (invertir si abre raro)
        public bool  locked = false;          // trabada: no se abre con E ni muestra cartel (guion)
        public bool  knockToUnlock = false;   // trabada pero: apuntándola + E = GOLPEAR (dispara onKnock)
        public System.Action onKnock;         // lo escucha la secuencia del guion

        Vector3 _hinge;
        float _cur, _target;
        bool _open, _canNow;
        Transform _cam;
        Renderer _rend;
        GUIStyle _style;
        AudioSource _audio;
        AudioClip _openClip, _closeClip;

        void Start()
        {
            ComputeHinge();

            // audio (3D): dropeás Assets/Resources/door_open(.wav) y door_close; si no hay, mudo
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.spatialBlend = 1f;
            _audio.rolloffMode = AudioRolloffMode.Linear;
            _audio.minDistance = 1.5f;
            _audio.maxDistance = 15f;
            _audio.playOnAwake = false;
            _audio.volume = 0.4f;   // owner: bajado
            // owner: "el creak corto para ambas acciones" -> mismo clip corto para abrir y
            // cerrar. Baño usa su propio sonido; las demás el creak corto (door_close).
            bool bathroom = name.ToLowerInvariant().Contains("toilet") || name.ToLowerInvariant().Contains("bath");
            _openClip  = Resources.Load<AudioClip>(bathroom ? "door_bath" : "door_close");
            _closeClip = _openClip;
        }

        // cámara activa = la que tiene el AudioListener prendido (persona o perro)
        Transform ActiveCam()
        {
            if (_cam != null && _cam.gameObject.activeInHierarchy) return _cam;
            foreach (var l in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (l.isActiveAndEnabled) { _cam = l.transform; return _cam; }
            if (Camera.main != null) { _cam = Camera.main.transform; return _cam; }
            return null;
        }

        void Update()
        {
            // animación
            if (Mathf.Abs(_cur - _target) > 0.05f)
            {
                float nc = Mathf.MoveTowards(_cur, _target, speed * Time.deltaTime);
                transform.RotateAround(_hinge, Vector3.up, nc - _cur);
                _cur = nc;
            }

            _canNow = false;
            var cam = ActiveCam();
            if (cam == null) return;
            Vector3 doorPos = _rend != null ? _rend.bounds.center : transform.position;
            Vector3 to = doorPos - cam.position;
            if (to.magnitude > range) return;
            if (Vector3.Angle(cam.forward, to) > lookAngle) return;

            if (locked)
            {
                // trabada: si es modo GOLPEAR, mostrar cartel apuntándola y disparar onKnock con E;
                // si no, ni cartel ni E.
                if (!knockToUnlock) { _canNow = false; return; }
                _canNow = true;
                var kbk = Keyboard.current;
                if (kbk != null && kbk.eKey.wasPressedThisFrame && !SettingsMenu.IsOpen) onKnock?.Invoke();
                return;
            }
            _canNow = true; // cerca + mirándola -> mostrar cartel
            var kb = Keyboard.current;
            if (kb != null && kb.eKey.wasPressedThisFrame && !SettingsMenu.IsOpen)
            {
                _open = !_open;
                _target = _open ? openAngle : 0f;
                var clip = _open ? _openClip : _closeClip;
                if (_audio != null && clip != null) _audio.PlayOneShot(clip);
            }
        }

        public bool IsOpen => _open;   // lo lee la secuencia (ej. no zoomear hasta que abrió la puerta)

        // calcula la bisagra (borde de la puerta según hingeOnPositive). Llamar con la puerta CERRADA.
        void ComputeHinge()
        {
            _rend = GetComponentInChildren<Renderer>();
            Bounds b = _rend != null ? _rend.bounds : new Bounds(transform.position, Vector3.one);
            bool wideX = b.size.x >= b.size.z;
            Vector3 widthDir = wideX ? Vector3.right : Vector3.forward;
            float half = wideX ? b.extents.x : b.extents.z;
            _hinge = b.center + widthDir * (hingeOnPositive ? half : -half);
            _hinge.y = b.center.y;
        }

        // cambia el lado de la bisagra por CÓDIGO y recalcula (owner: "que abra desde el otro lado").
        public void SetHinge(bool onPositive) { hingeOnPositive = onPositive; ComputeHinge(); }

        // Abrir/cerrar por CÓDIGO (para la secuencia del guion: la chica entra al baño, etc.).
        // Mismo efecto que apretar E: fija el objetivo de giro y suena el clip.
        public void SetOpen(bool open)
        {
            if (_open == open) return;
            _open = open;
            _target = _open ? openAngle : 0f;
            var clip = _open ? _openClip : _closeClip;
            if (_audio != null && clip != null) _audio.PlayOneShot(clip);
        }

        void OnGUI()
        {
            if (!_canNow) return;
            if (_style == null)
                _style = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _style.normal.textColor = Color.white;
            string txt = (locked && knockToUnlock) ? "[ E ]  Golpear la puerta"
                       : _open ? "[ E ]  Cerrar puerta" : "[ E ]  Abrir puerta";
            GUI.Label(new Rect(Screen.width * 0.5f - 130f, Screen.height * 0.5f + 34f, 260f, 26f),
                      txt, _style);
        }
    }
}
