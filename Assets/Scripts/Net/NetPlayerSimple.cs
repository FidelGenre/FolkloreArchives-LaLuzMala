// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  NetPlayerSimple.cs — jugador de PRUEBA en red (cápsula).
//  Objetivo de la etapa 1b: dos instancias se conectan y se ven
//  moverse. Cada cliente controla SOLO su cápsula (IsOwner) y
//  prende SOLO su cámara. El OwnerNetworkTransform sincroniza la
//  posición/rotación al resto.
//  (Después reemplazamos la cápsula por persona/perro reales.)
// ============================================================
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FolkloreArchives.Net
{
    public class NetPlayerSimple : NetworkBehaviour
    {
        public float speed = 4.5f;
        public float mouseSensitivity = 0.08f;

        Camera cam;

        void Awake() => cam = GetComponentInChildren<Camera>(true);

        public override void OnNetworkSpawn()
        {
            // solo mi cámara (+ AudioListener) se prende; la del otro jugador NO
            if (cam != null) cam.gameObject.SetActive(IsOwner);

            if (IsOwner)
            {
                Cursor.lockState = CursorLockMode.Locked;
                // aparecer cerca del campamento, separados por clientId para no encimarse
                float x = 408f + (OwnerClientId % 4) * 2f;
                float z = 440f;
                transform.position = OnGround(new Vector3(x, 0f, z));
            }
        }

        void Update()
        {
            if (!IsOwner) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            var mouse = Mouse.current;
            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
                transform.Rotate(0f, mouse.delta.ReadValue().x * mouseSensitivity, 0f);

            float h = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
            float v = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
            Vector3 move = transform.forward * v + transform.right * h;
            if (move.sqrMagnitude > 1f) move.Normalize();

            Vector3 p = transform.position + move * speed * Time.deltaTime;
            transform.position = OnGround(p);
        }

        // pega la posición al terreno (sin CharacterController: para la prueba alcanza)
        static Vector3 OnGround(Vector3 p)
        {
            var t = Terrain.activeTerrain;
            if (t != null)
                p.y = t.SampleHeight(p) + t.transform.position.y + 1f;
            return p;
        }
    }
}
