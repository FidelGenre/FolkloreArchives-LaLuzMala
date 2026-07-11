// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  DogController.cs — el perro. Tres modos:
//    Follow  = IA, sigue a la persona (modo solo, por defecto)
//    Player  = lo controlás vos (WASD/flechas para girar + avanzar)
//    Idle    = quieto
//  Locomoción sobre CharacterController (no crouch/estamina/linterna:
//  es un cuadrúpedo, no la persona).
//  Modelo: "PS1 Dog" by Jo_Zinn5632 — CC-BY (ver DEV_LOG.md).
// ============================================================
using UnityEngine;
using UnityEngine.InputSystem;

namespace FolkloreArchives
{
    [RequireComponent(typeof(CharacterController))]
    public class DogController : MonoBehaviour
    {
        public enum Mode { Idle, Follow, Player }
        public Mode mode = Mode.Follow;

        [Header("Locomoción")]
        public float walkSpeed = 3.2f;
        public float runSpeed  = 6.5f;
        public float turnSpeed = 200f;   // grados/seg (giro de la IA)
        public float mouseSensitivity = 0.08f; // giro con mouse (modo jugador)
        public float gravity   = 18f;
        public float jumpHeight = 0.9f;   // saltar (Espacio)
        public float crouchRatio = 0.5f;  // agacharse (Ctrl/C): baja a esta fracción del alto

        [Header("Follow (IA)")]
        public Transform followTarget;             // la persona
        public float followStopDistance = 3.0f;    // se planta a esta distancia
        public float followRunDistance  = 8f;      // trota si está más lejos que esto
        public float followTeleportDistance = 45f; // si quedó MUY lejos (atascado), aparece al lado

        [Header("Input Jugador 2 (co-op, teclado compartido)")]
        // Si es true, en modo Player usa las FLECHAS (jugador 2). Si es false, WASD
        // (jugador 1). En modo solo el perro se controla con WASD (useArrowKeys=false).
        public bool useArrowKeys = false;

        CharacterController cc;
        float verticalVel;
        Transform camT;
        float standHeight, camBaseY;

        void Start()
        {
            cc = GetComponent<CharacterController>();
            standHeight = cc.height;
            var camGo = GetComponentInChildren<Camera>(true);
            if (camGo != null) { camT = camGo.transform; camBaseY = camT.localPosition.y; }
        }

        void Update()
        {
            Vector3 planar = Vector3.zero;
            switch (mode)
            {
                case Mode.Player: planar = PlayerMove(); break;
                case Mode.Follow: planar = FollowMove(); break;
            }

            bool grounded = cc.isGrounded;
            if (grounded) verticalVel = -1f;
            else verticalVel -= gravity * Time.deltaTime;

            if (mode == Mode.Player) JumpCrouch(grounded);   // saltar/agacharse solo cuando lo controlás

            Vector3 move = planar;
            move.y = verticalVel;
            cc.Move(move * Time.deltaTime);
        }

        // Espacio = saltar (si está en el piso y no agachado). Ctrl/C = agacharse
        // (baja el alto del collider y la cámara).
        void JumpCrouch(bool grounded)
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            bool wantCrouch = kb.leftCtrlKey.isPressed || kb.cKey.isPressed;
            float targetH = wantCrouch ? standHeight * crouchRatio : standHeight;
            cc.height = Mathf.Lerp(cc.height, targetH, 10f * Time.deltaTime);
            cc.center = new Vector3(0f, cc.height * 0.5f, 0f);
            if (camT != null)
            {
                float targetCamY = camBaseY - (standHeight - cc.height);
                camT.localPosition = new Vector3(camT.localPosition.x,
                    Mathf.Lerp(camT.localPosition.y, targetCamY, 10f * Time.deltaTime),
                    camT.localPosition.z);
            }

            if (grounded && !wantCrouch && kb.spaceKey.wasPressedThisFrame)
                verticalVel = Mathf.Sqrt(2f * gravity * jumpHeight);
        }

        // --- controlado por el jugador (1ª persona: mouse gira, WASD mueve) ---
        Vector3 PlayerMove()
        {
            var kb = Keyboard.current;
            if (kb == null || SettingsMenu.IsOpen) return Vector3.zero;

            // girar con el MOUSE
            var mouse = Mouse.current;
            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
                transform.Rotate(0f, mouse.delta.ReadValue().x * mouseSensitivity, 0f);

            float strafe, fwd; bool run;
            if (useArrowKeys) // jugador 2 (co-op local, no usado en online)
            {
                strafe = (kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.leftArrowKey.isPressed ? 1f : 0f);
                fwd    = (kb.upArrowKey.isPressed   ? 1f : 0f) - (kb.downArrowKey.isPressed ? 1f : 0f);
                run    = kb.rightShiftKey.isPressed;
            }
            else // WASD (jugador 1 / online / solo)
            {
                strafe = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
                fwd    = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
                run    = kb.leftShiftKey.isPressed;
            }

            float speed = run ? runSpeed : walkSpeed;
            Vector3 move = transform.forward * fwd + transform.right * strafe; // A/D = lateral
            if (move.sqrMagnitude > 1f) move.Normalize();
            return move * speed;
        }

        // --- IA: seguir a la persona ---
        Vector3 FollowMove()
        {
            if (followTarget == null) return Vector3.zero;
            Vector3 to = followTarget.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;

            // atascado/perdido → reaparece detrás de la persona
            if (dist > followTeleportDistance)
            {
                Vector3 behind = followTarget.position - followTarget.forward * followStopDistance;
                var cont = GetComponent<CharacterController>();
                cont.enabled = false;                 // mover un CC directo requiere desactivarlo
                transform.position = behind;
                cont.enabled = true;
                return Vector3.zero;
            }

            FaceToward(to);
            if (dist <= followStopDistance) return Vector3.zero;  // llegó: se planta
            float speed = dist > followRunDistance ? runSpeed : walkSpeed;
            return to.normalized * speed;
        }

        void FaceToward(Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            Quaternion target = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
        }
    }
}
