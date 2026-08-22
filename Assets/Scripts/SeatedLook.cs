// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  SeatedLook.cs — free-look SENTADO (junto a la fogata). Deja mover la
//  cámara con el mouse dentro de un CONO (arriba/abajo + costados) pero
//  NO permite darse vuelta 180° ni mover el cuerpo. Se agrega a la cámara
//  de la persona durante la cinemática y se saca al levantarse.
// ============================================================
using UnityEngine;
using UnityEngine.InputSystem;

namespace FolkloreArchives
{
    public class SeatedLook : MonoBehaviour
    {
        public float yawLimit = 78f;    // cuánto podés girar a cada lado (no 180)
        public float pitchLimit = 55f;  // arriba/abajo
        public float sensitivity = 0.08f;

        float _yaw, _pitch;

        void OnEnable()
        {
            // arrancar desde la orientación local actual de la cámara.
            Vector3 e = transform.localEulerAngles;
            _pitch = Norm(e.x);
            _yaw   = Norm(e.y);
        }

        void Update()
        {
            var m = Mouse.current;
            if (m == null || SettingsMenu.IsOpen) return;
            Vector2 d = m.delta.ReadValue();
            _yaw   = Mathf.Clamp(_yaw + d.x * sensitivity, -yawLimit, yawLimit);
            _pitch = Mathf.Clamp(_pitch - d.y * sensitivity, -pitchLimit, pitchLimit);
            transform.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        static float Norm(float a) { a %= 360f; if (a > 180f) a -= 360f; return a; }
    }
}
