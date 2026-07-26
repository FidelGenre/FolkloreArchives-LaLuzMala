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

        // owner: "necesito que este en pose de conduccion... necesito que lo sientes
        // en la silla como corresponde" -- sin esto el personaje se ve PARADO adentro
        // del auto. PlayerVehicleInteractor prende/apaga esto al subir/bajar.
        public bool seated;
        // owner: "no se le ven las piernas al humano sentado" -- con +75 desaparecían
        // (probablemente doblaban para el lado contrario, atravesando/deformando la
        // malla). Probando el signo invertido.
        public float seatedThighAngle = -75f;  // grados que se doblan los muslos hacia adelante

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
                // owner: "el de barba sigue con los brazos extendidos" -- Contains() es
                // case-SENSITIVE y el rig Mixamo nombra el hueso "mixamorig:LeftArm" (con
                // "Arm" en mayúscula) -- nunca matcheaba "arm" en minúscula, así que ese
                // personaje nunca tuvo la corrección de brazo (T-pose) NI, sentado, se
                // libraba de que le apliquen el ángulo de MUSLO (quedaba tratado como
                // pierna) -- de ahí el brazo "extendido" hacia adelante en el auto.
                bool isArm = limbs[i].bone.ToLowerInvariant().Contains("arm");
                Transform tip = isArm ? DeepestChild(_t[i]) : null;
                if (tip != null && tip != _t[i] && _t[i].parent != null)
                {
                    // dirección real del brazo (hombro → mano) y la roto para que apunte a -Y
                    Vector3 dir = (tip.position - _t[i].position).normalized;
                    Quaternion worldDelta = Quaternion.FromToRotation(dir, Vector3.down);
                    Quaternion pW = _t[i].parent.rotation;
                    _rest[i] = Quaternion.Inverse(pW) * worldDelta * (pW * baseLocal);
                }
                else _rest[i] = baseLocal;

                // owner: "la chica sigue con los brazos muy abiertos" -- el cálculo de
                // arriba solo endereza el HOMBRO (usa hombro→mano completo como
                // referencia), así que si el rig tiene el codo apenas doblado en su bind
                // pose (común, no todos los personajes vienen con el brazo perfectamente
                // recto en T-pose), ese doblez queda "colgando" sin corregir y el brazo
                // sigue viéndose abierto/en V aunque el hombro ya apunte para abajo.
                // Se aplica YA la corrección del hombro (para que el resto de la cadena
                // vea su posición final) y se endereza el resto del brazo (antebrazo,
                // mano) con el mismo criterio, hueso por hueso.
                if (isArm && tip != null && tip != _t[i])
                {
                    _t[i].localRotation = _rest[i];
                    StraightenArmChain(_t[i].childCount > 0 ? _t[i].GetChild(0) : null, tip);
                }
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

            if (seated)
            {
                // pose fija: muslos doblados hacia adelante (sentado), brazos en
                // reposo -- nada de ciclo de caminata ni agachado mientras estás en
                // el auto.
                for (int i = 0; i < limbs.Length; i++)
                {
                    if (_t[i] == null) continue;
                    bool isArm = limbs[i].bone.ToLowerInvariant().Contains("arm");
                    _t[i].localRotation = isArm ? _rest[i] : _rest[i] * Quaternion.AngleAxis(seatedThighAngle, axis);
                }
                return;
            }

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
                bool isArm = limbs[i].bone.ToLowerInvariant().Contains("arm");
                float amt = (isArm ? armSwing : legSwing) * limbs[i].phase * _amp * swingMul;
                float ang = Mathf.Sin(_phase) * amt;
                if (isArm && _t[i].parent != null)
                {
                    // owner: "necesito que muevan los brazos igual que el personaje
                    // principal" -- _rest[i] de un brazo ya viene TORCIDO por la
                    // corrección de T-pose (FromToRotation arbitraria), así que girar
                    // en el eje LOCAL "axis" de ese hueso ya no cae en un plano
                    // adelante-atrás predecible (varía según cómo haya quedado
                    // orientado ese giro en cada rig) -- con las piernas no pasa porque
                    // su _rest nunca se toca. Se gira en el eje "derecha" del PERSONAJE
                    // (mundo), no del hueso, así el vaivén queda igual sin importar el rig.
                    Quaternion worldSwing = Quaternion.AngleAxis(ang, transform.right);
                    Quaternion pW = _t[i].parent.rotation;
                    _t[i].localRotation = Quaternion.Inverse(pW) * worldSwing * (pW * _rest[i]);
                }
                else
                {
                    _t[i].localRotation = _rest[i] * Quaternion.AngleAxis(ang, axis);
                }
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

        // endereza el resto de la cadena del brazo (antebrazo, mano...) bajando desde
        // "from" hasta (sin incluir) "tip": el hombro/upper_arm ya se corrigió arriba
        // usando el vector completo hombro→mano, pero eso NO cambia la rotación LOCAL
        // de los huesos intermedios -- si el bind pose del rig tenía el codo doblado,
        // ese doblez queda intacto y el brazo se ve "en V" (abierto) aunque el hombro ya
        // apunte para abajo. Mismo truco (FromToRotation hacia abajo) repetido hueso por
        // hueso, cada uno viendo ya la posición FINAL de su padre (por eso el hombro se
        // aplica antes de llamar acá).
        static void StraightenArmChain(Transform from, Transform tip)
        {
            var bone = from;
            int guard = 0;
            while (bone != null && bone != tip && bone.parent != null && guard++ < 16)
            {
                var child = bone.childCount > 0 ? bone.GetChild(0) : null;
                if (child == null) break;
                Vector3 dir = child.position - bone.position;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    Quaternion delta = Quaternion.FromToRotation(dir.normalized, Vector3.down);
                    Quaternion pW = bone.parent.rotation;
                    bone.localRotation = Quaternion.Inverse(pW) * delta * (pW * bone.localRotation);
                }
                bone = child;
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
