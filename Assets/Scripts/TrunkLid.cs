// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  TrunkLid.cs — tapa de baúl (cajuela) que se LEVANTA girando sobre una
//  bisagra HORIZONTAL (el right del auto), no de costado como una puerta.
//  Se puede abrir/cerrar con E apuntándola de cerca. La agrega
//  CampsiteSequence al abrir la cajuela; también sirve solo.
// ============================================================
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FolkloreArchives
{
    public class TrunkLid : MonoBehaviour
    {
        public Transform car;          // para el eje de la bisagra (right del auto)
        public float openDeg = 70f;    // cuánto se levanta (+ = arriba)
        public float reach = 3.5f;     // distancia para poder tocarla con E
        public float aimAngle = 32f;   // hay que estar apuntándola

        Quaternion _closed;
        bool _open;
        Coroutine _anim;
        bool _showHint;
        GUIStyle _style;
        AudioClip _openClip, _closeClip;   // ruido de cajuela (reuso el de puerta de auto)
        public float volume = 0.7f;

        void Awake()
        {
            _closed = transform.localRotation;
            _openClip  = Resources.Load<AudioClip>("car_door_open");
            _closeClip = Resources.Load<AudioClip>("car_door_close");
        }

        // abre/cierra (lo llama CampsiteSequence o el toggle con E).
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
            Vector3 worldAxis = car != null ? car.right : Vector3.right;
            Vector3 axis = transform.parent != null ? transform.parent.InverseTransformDirection(worldAxis) : worldAxis;
            Quaternion openRot = Quaternion.AngleAxis(openDeg, axis) * _closed;
            Quaternion from = transform.localRotation, target = open ? openRot : _closed;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.6f;
                transform.localRotation = Quaternion.Slerp(from, target, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
                yield return null;
            }
            transform.localRotation = target;
        }

        void OnGUI()
        {
            if (!_showHint) return;
            if (_style == null) _style = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter, richText = true };
            GUI.Label(new Rect(Screen.width / 2f - 120f, Screen.height / 2f + 34f, 240f, 24f),
                      _open ? "[E] Cerrar cajuela" : "[E] Abrir cajuela", _style);
        }
    }
}
