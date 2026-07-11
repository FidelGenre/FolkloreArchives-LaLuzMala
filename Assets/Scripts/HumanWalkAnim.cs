// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  HumanWalkAnim.cs — animación PROCEDURAL del personaje humano
//  (Simple PSX Character de JashiPSX — rig completo, sin clips).
//   · Caminata: balanceo de piernas y brazos (brazos bajados desde
//     la T-pose calculando su dirección real → apuntar a -Y).
//   · Agacharse: se sincroniza por RED (NetworkVariable) y agacha
//     el modelo (lo ve el compañero). El perro NO usa este script.
//  Reemplazable por animaciones de Mixamo más adelante.
// ============================================================
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FolkloreArchives
{
    public class HumanWalkAnim : NetworkBehaviour
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

        // agachado, replicado a todos (lo escribe el dueño)
        readonly NetworkVariable<bool> _crouch = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        Transform[] _t;
        Quaternion[] _rest;
        Transform _model;
        Vector3 _modelScale;
        float _phase, _amp;
        Vector3 _lastPos;

        void Start()
        {
            _t = new Transform[limbs.Length];
            _rest = new Quaternion[limbs.Length];
            for (int i = 0; i < limbs.Length; i++)
            {
                _t[i] = FindDeep(transform, limbs[i].bone);
                if (_t[i] == null) continue;
                Quaternion baseLocal = _t[i].localRotation;
                if (limbs[i].bone.Contains("arm") && _t[i].childCount > 0 && _t[i].parent != null)
                {
                    Vector3 dir = (_t[i].GetChild(0).position - _t[i].position).normalized;
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
            _lastPos = transform.position;
        }
        Vector3 _modelBasePos;
        float _crouchT;

        void Update()
        {
            if (IsSpawned && IsOwner)
            {
                var kb = Keyboard.current;
                bool want = kb != null && (kb.leftCtrlKey.isPressed || kb.cKey.isPressed);
                if (want != _crouch.Value) _crouch.Value = want;
            }
        }

        void LateUpdate()
        {
            float dt = Mathf.Max(1e-5f, Time.deltaTime);
            float speed = (transform.position - _lastPos).magnitude / dt;
            _lastPos = transform.position;

            bool crouched = IsSpawned && _crouch.Value;

            // agacharse: baja el modelo y lo achica en Y → se ve claramente más bajo
            // (el compañero te ve agacharte; vos no, porque en 1ª persona tu cuerpo va oculto).
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
