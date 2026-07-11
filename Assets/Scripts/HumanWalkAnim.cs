// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  HumanWalkAnim.cs — caminata PROCEDURAL para el personaje humano
//  (Simple PSX Character de JashiPSX — rig completo, sin clips).
//  Balancea muslos y brazos con marcha humana: pierna y brazo
//  CONTRALATERALES en fase (izq-adelante + brazo der-adelante).
//  Amplitud sube/baja según si el jugador se mueve.
//  Reemplazable por animaciones de Mixamo más adelante.
// ============================================================
using UnityEngine;

namespace FolkloreArchives
{
    public class HumanWalkAnim : MonoBehaviour
    {
        [System.Serializable]
        public struct Limb { public string bone; public float phase; } // phase = +1 / -1

        // marcha: muslo.L adelante con brazo.R adelante (contralateral)
        public Limb[] limbs = {
            new Limb { bone = "thigh.L",     phase =  1f },
            new Limb { bone = "thigh.R",     phase = -1f },
            new Limb { bone = "upper_arm.L", phase = -1f },
            new Limb { bone = "upper_arm.R", phase =  1f },
        };
        public float legSwing = 26f;
        public float armSwing = 20f;
        public float cadence = 6.5f;
        public Vector3 axis = Vector3.right;   // eje de balanceo (local del hueso)
        public float moveThreshold = 0.3f;

        Transform[] _t;
        Quaternion[] _base;
        float _phase, _amp;
        Vector3 _lastPos;

        void Start()
        {
            _t = new Transform[limbs.Length];
            _base = new Quaternion[limbs.Length];
            for (int i = 0; i < limbs.Length; i++)
            {
                _t[i] = FindDeep(transform, limbs[i].bone);
                if (_t[i] != null) _base[i] = _t[i].localRotation;
            }
            _lastPos = transform.position;
        }

        void LateUpdate()
        {
            float dt = Mathf.Max(1e-5f, Time.deltaTime);
            float speed = (transform.position - _lastPos).magnitude / dt;
            _lastPos = transform.position;

            bool moving = speed > moveThreshold;
            _amp = Mathf.Lerp(_amp, moving ? 1f : 0f, 8f * dt);
            if (moving) _phase += dt * cadence;

            for (int i = 0; i < limbs.Length; i++)
            {
                if (_t[i] == null) continue;
                bool isArm = limbs[i].bone.Contains("arm");
                float amt = (isArm ? armSwing : legSwing) * limbs[i].phase * _amp;
                float ang = Mathf.Sin(_phase) * amt;
                _t[i].localRotation = _base[i] * Quaternion.AngleAxis(ang, axis);
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
