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
            _dog = GetComponent<DogController>();
            _model = transform.Find("Model");
            if (_model != null) { _modelBaseRot = _model.localRotation; _modelBasePos = _model.localPosition; }
            lastPos = transform.position;
        }

        [Header("Sentado (modo Idle)")]
        public float sitPitch = -30f;   // inclina el cuerpo: ancas abajo, pecho arriba
        public float sitDrop = 0.15f;
        DogController _dog;
        Transform _model;
        Quaternion _modelBaseRot;
        Vector3 _modelBasePos;
        float _sitT;

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

            // SENTADO: cuando el perro está en Idle (le dijiste que se quede), inclina
            // el cuerpo hacia atrás → pose de sentado.
            if (_model != null)
            {
                bool sitting = _dog != null && _dog.mode == DogController.Mode.Idle;
                _sitT = Mathf.Lerp(_sitT, sitting ? 1f : 0f, 8f * dt);
                _model.localRotation = _modelBaseRot * Quaternion.Euler(sitPitch * _sitT, 0f, 0f);
                _model.localPosition = _modelBasePos + Vector3.down * (sitDrop * _sitT);
            }
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
