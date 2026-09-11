// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  SheepWalkAnim.cs — animación de caminata PROCEDURAL para la oveja.
//  owner: "necesito que las ovejas tengan huesos como el perro no que
//  se muevan todas duras".
//
//  A DIFERENCIA del perro (PS1_Dog.glb, ver DogWalkAnim): el modelo de
//  la oveja (sheep.obj) es un OBJ SIN esqueleto -- el formato OBJ no
//  soporta huesos/skinning en absoluto, así que no hay patas que
//  balancear por código como con el perro. Mientras no se consiga un
//  asset de oveja RIGUEADO (con huesos), esto simula el trote
//  bamboleando el CUERPO entero (sube-baja + vaivén lateral) al
//  moverse -- mucho mejor que quedar rígida deslizándose como estatua,
//  aunque no son patas de verdad.
// ============================================================
using UnityEngine;

namespace FolkloreArchives
{
    public class SheepWalkAnim : MonoBehaviour
    {
        public Transform model;              // el mesh (hijo "Model") que se bambolea -- NUNCA el root (lo usa la IA para posición/colisión)
        public float bobHeight = 0.06f;       // cuánto sube/baja el cuerpo
        public float rockAngle = 6f;          // cuánto se ladea de lado a lado
        public float cadence = 7f;            // velocidad del ciclo al caminar
        public float moveThreshold = 0.15f;   // m/s para considerar que se está moviendo

        Vector3 baseLocalPos;
        Quaternion baseLocalRot;
        float phase, amp;
        Vector3 lastPos;

        void Start()
        {
            if (model == null) model = transform;
            baseLocalPos = model.localPosition;
            baseLocalRot = model.localRotation;
            lastPos = transform.position;
        }

        // LateUpdate: después de que la IA mueve el root, bambolea el modelo encima.
        void LateUpdate()
        {
            float dt = Mathf.Max(1e-5f, Time.deltaTime);
            float speed = (transform.position - lastPos).magnitude / dt;
            lastPos = transform.position;

            bool moving = speed > moveThreshold;
            amp = Mathf.Lerp(amp, moving ? 1f : 0f, 8f * dt);
            if (moving) phase += dt * cadence;

            float bob  = Mathf.Abs(Mathf.Sin(phase)) * bobHeight * amp;   // rebotito (2 por ciclo)
            float rock = Mathf.Sin(phase) * rockAngle * amp;              // vaivén lateral (1 por ciclo)

            model.localPosition = baseLocalPos + Vector3.up * bob;
            model.localRotation = baseLocalRot * Quaternion.AngleAxis(rock, Vector3.forward);
        }
    }
}
