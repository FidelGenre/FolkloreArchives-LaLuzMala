// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  DogWalkAnim.cs — animación de caminata PROCEDURAL para el perro.
//  El modelo PS1 tiene esqueleto (huesos de las 4 patas) pero NO
//  trae clips de animación, así que balanceo los huesos de las patas
//  por código: ciclo de trote con los pares DIAGONALES en contrafase,
//  con amplitud que sube/baja según si el perro se mueve.
// ============================================================
using UnityEngine;

namespace FolkloreArchives
{
    public class DogWalkAnim : MonoBehaviour
    {
        // huesos de las patas altas del rig (del glb): FR, BR, FL, BL
        public string[] legBones = { "Bone.003", "Bone.005", "Bone.007", "Bone.009" };
        public float swing = 22f;             // grados de balanceo
        public float cadence = 9f;            // velocidad del ciclo al caminar
        public Vector3 axis = Vector3.right;  // eje de balanceo (local del hueso)
        public float moveThreshold = 0.4f;    // m/s para considerar que se mueve

        Transform[] legs;
        Quaternion[] baseRot;
        float phase, amp;
        Vector3 lastPos;

        void Start()
        {
            legs = new Transform[legBones.Length];
            baseRot = new Quaternion[legBones.Length];
            for (int i = 0; i < legBones.Length; i++)
            {
                legs[i] = FindDeep(transform, legBones[i]);
                if (legs[i] != null) baseRot[i] = legs[i].localRotation;
            }
            lastPos = transform.position;
        }

        // LateUpdate: después de mover al perro, sobreescribe la pose de las patas
        void LateUpdate()
        {
            float dt = Mathf.Max(1e-5f, Time.deltaTime);
            float speed = (transform.position - lastPos).magnitude / dt;
            lastPos = transform.position;

            bool moving = speed > moveThreshold;
            amp = Mathf.Lerp(amp, moving ? 1f : 0f, 8f * dt);
            if (moving) phase += dt * cadence;

            for (int i = 0; i < legs.Length; i++)
            {
                if (legs[i] == null) continue;
                float sign = (i == 0 || i == 3) ? 1f : -1f;     // diagonales en contrafase
                float ang = Mathf.Sin(phase) * swing * amp * sign;
                legs[i].localRotation = baseRot[i] * Quaternion.AngleAxis(ang, axis);
            }

            // QUIETO: cuando el perro está en Idle (le dijiste que se quede) simplemente
            // se queda PARADO quieto (la caminata ya se apaga sola con amp→0). No se
            // sienta: la pose de sentado sobre un rig sin huesos de patas plegables
            // quedaba mal, así que la sacamos.
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform c in root)
            {
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
