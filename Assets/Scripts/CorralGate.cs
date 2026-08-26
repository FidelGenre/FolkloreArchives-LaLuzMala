// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  CorralGate.cs — puerta/tranquera GENÉRICA que se abre/cierra girando sobre
//  una bisagra VERTICAL (eje Y) en un extremo, como una puerta real. Se toca
//  con E apuntándola de cerca. La arma el botón de editor RanchoNpcSetup
//  (BuildHingeDoor, reusado por la tranquera del corral y la puerta de la
//  casa); la secuencia de la misión la puede abrir sola (SetOpen) y
//  consultar IsOpen (ej. para soltar las ovejas cuando se abre la tranquera).
//  La rotación que TENÍA el objeto al arrancar Play queda grabada como
//  "cerrada" (Awake) -- para corregir cómo se ve cerrada, rotar el objeto
//  (o su hijo "Plank") en el Editor ANTES de dar Play.
// ============================================================
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FolkloreArchives
{
    public class CorralGate : MonoBehaviour
    {
        public float openDeg = 95f;    // cuánto abre (grados alrededor de Y). + o - cambia el lado.
        public float reach = 3.5f;     // distancia para tocarla con E
        public float aimAngle = 45f;   // hay que estar apuntándola
        public float volume = 0.7f;
        public string hintClosed = "[E] Abrir la tranquera";   // cartel [E] con la puerta/tranquera cerrada
        public string hintOpen   = "[E] Cerrar la tranquera";  // cartel [E] con la puerta/tranquera abierta

        Quaternion _closed;
        bool _open;
        Coroutine _anim;
        bool _showHint;
        AudioClip _openClip, _closeClip;

        public bool IsOpen => _open;

        void Awake()
        {
            _closed = transform.localRotation;
            _openClip  = Resources.Load<AudioClip>("car_door_open");   // reuso; se puede cambiar por uno de tranquera
            _closeClip = Resources.Load<AudioClip>("car_door_close");
        }

        // abre/cierra (lo llama la secuencia o el toggle con E).
        public void SetOpen(bool open)
        {
            _open = open;
            var clip = open ? _openClip : _closeClip;
            if (clip != null) AudioSource.PlayClipAtPoint(clip, transform.position, volume);
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(Animate(open));
        }

        void Update()
        {
            _showHint = false;
            if (SettingsMenu.IsOpen) return;
            var kb = Keyboard.current;
            var cam = Camera.main;
            if (kb == null || cam == null) return;
            Vector3 to = transform.position - cam.transform.position;
            if (to.magnitude > reach) return;
            if (Vector3.Angle(cam.transform.forward, to) > aimAngle) return;
            _showHint = true;
            if (kb[Key.E].wasPressedThisFrame) SetOpen(!_open);
        }

        IEnumerator Animate(bool open)
        {
            Quaternion openRot = Quaternion.AngleAxis(openDeg, Vector3.up) * _closed;
            Quaternion from = transform.localRotation, target = open ? openRot : _closed;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.7f;
                transform.localRotation = Quaternion.Slerp(from, target, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
                yield return null;
            }
            transform.localRotation = target;
        }

        void OnGUI()
        {
            if (!_showHint) return;
            InteractHint.Draw(_open ? hintOpen : hintClosed);
        }
    }
}
