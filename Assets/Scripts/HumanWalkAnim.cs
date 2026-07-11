// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  HumanWalkAnim.cs — animación PROCEDURAL del personaje humano
//  (Simple PSX Character de JashiPSX — rig completo, sin clips).
//   · Caminata: balanceo de piernas y brazos (brazos bajados desde
//     la T-pose calculando su dirección real → apuntar a -Y).
//   · Agacharse: baja/achata el modelo. El estado de agachado viene
//     de NetCrouchSync si existe (online, replicado al compañero) o,
//     si no (modo solo), directo del teclado local.
//  Es un MonoBehaviour normal (sirve online Y en solo, sin necesitar
//  NetworkObject). El perro NO usa este script.
// ============================================================
using UnityEngine;
using UnityEngine.InputSystem;

namespace FolkloreArchives
{
    public class HumanWalkAnim : MonoBehaviour
    {
        [System.Serializable]
        public struct Limb { public string bone; public float phase; } // phase = +1 / -1

        public Limb[] limbs = {
            new Limb { bone = "thigh.L",     phase =  1f },
            new Limb { bone = "thigh.R",     phase = -1f },
            new Limb { bone = "upper_arm.L", phase = -1f },
            new Limb { bone = "upper_arm.R", phase =  1f },
        };
        public float legSwing = 26f;
        public float armSwing = 16f;
        public float cadence = 6.5f;
        public Vector3 axis = Vector3.right;
        public float moveThreshold = 0.3f;
        public float crouchScaleY = 0.62f;   // alto del modelo al agacharse (fracción)
        public float crouchDrop = 0.35f;      // cuánto baja el modelo al agacharse (m)

        Transform[] _t;
        Quaternion[] _rest;
        Transform _model;
        Vector3 _modelScale;
        float _phase, _amp;
        Vector3 _lastPos;
        NetCrouchSync _net;   // opcional: solo en el prefab de red

        void Start()
        {
            _t = new Transform[limbs.Length];
            _rest = new Quaternion[limbs.Length];
            for (int i = 0; i < limbs.Length; i++)
            {
                _t[i] = FindDeep(transform, limbs[i].bone);
                if (_t[i] == null) continue;
                Quaternion baseLocal = _t[i].localRotation;
                Transform tip = limbs[i].bone.Contains("arm") ? DeepestChild(_t[i]) : null;
                if (tip != null && tip != _t[i] && _t[i].parent != null)
                {
                    // dirección real del brazo (hombro → mano) y la roto para que apunte a -Y
                    Vector3 dir = (tip.position - _t[i].position).normalized;
                    Quaternion worldDelta = Quaternion.FromToRotation(dir, Vector3.down);
                    Quaternion pW = _t[i].parent.rotation;
                    _rest[i] = Quaternion.Inverse(pW) * worldDelta * (pW * baseLocal);
                }
                else _rest[i] = baseLocal;
            }
            _model = transform.Find("Model");
            if (_model == null) { var smr = GetComponentInChildren<SkinnedMeshRenderer>(); if (smr != null) _model = smr.transform.parent; }
            if (_model != null) { _modelScale = _model.localScale; _modelBasePos = _model.localPosition; }
            else _modelScale = Vector3.one;
            _net = GetComponent<NetCrouchSync>();
            _lastPos = transform.position;
        }
        Vector3 _modelBasePos;
        float _crouchT;

        // ¿está agachado? Online: lo decide/replica NetCrouchSync (el dueño escribe,
        // todos leen). Solo: teclado local (Ctrl/C).
        bool WantCrouch()
        {
            var kb = Keyboard.current;
            bool localInput = kb != null && (kb.leftCtrlKey.isPressed || kb.cKey.isPressed);
            if (_net != null)
            {
                if (_net.IsOwnerLocal) _net.SetLocal(localInput);
                return _net.Crouched;
            }
            return localInput;
        }

        void LateUpdate()
        {
            float dt = Mathf.Max(1e-5f, Time.deltaTime);
            float speed = (transform.position - _lastPos).magnitude / dt;
            _lastPos = transform.position;

            bool crouched = WantCrouch();

            // agacharse: baja el modelo y lo achica en Y → se ve claramente más bajo
            // (el compañero te ve agacharte).
            if (_model != null)
            {
                _crouchT = Mathf.Lerp(_crouchT, crouched ? 1f : 0f, 12f * dt);
                var s = _modelScale; s.y = _modelScale.y * Mathf.Lerp(1f, crouchScaleY, _crouchT);
                _model.localScale = s;
                _model.localPosition = _modelBasePos + Vector3.down * (crouchDrop * _crouchT);
            }

            bool moving = speed > moveThreshold;
            _amp = Mathf.Lerp(_amp, moving ? 1f : 0f, 8f * dt);
            if (moving) _phase += dt * cadence;

            float swingMul = crouched ? 0.4f : 1f;   // pasos más cortos agachado
            for (int i = 0; i < limbs.Length; i++)
            {
                if (_t[i] == null) continue;
                bool isArm = limbs[i].bone.Contains("arm");
                float amt = (isArm ? armSwing : legSwing) * limbs[i].phase * _amp * swingMul;
                float ang = Mathf.Sin(_phase) * amt;
                _t[i].localRotation = _rest[i] * Quaternion.AngleAxis(ang, axis);
            }
        }

        // desciende por el primer hijo hasta la punta (mano), saltando huesos de twist
        static Transform DeepestChild(Transform t)
        {
            var cur = t;
            int guard = 0;
            while (cur.childCount > 0 && guard++ < 16) cur = cur.GetChild(0);
            return cur;
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
