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
        public float armSpread = 0.22f;   // cuánto se separan los brazos del cuerpo (0 = pegados, para abajo)
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
        // owner (ajuste en vivo, con Play, sentado en el auto): -62 quedó perfecto,
        // junto con seatedScaleY/seatedModelDrop de abajo.
        public float seatedThighAngle = -62f;  // grados que se doblan los muslos hacia adelante

        // owner: "siguen por fuera" / "atravesando el asiento" (probado con los 3
        // amigos sentados decorativos en el auto) -- rotar solo el muslo NO reduce el
        // alto del personaje: la cabeza queda a la altura de PARADO (raíz + altura
        // completa) sin importar dónde se ubique la raíz, así que NINGÚN offset de
        // posición alcanza para que un personaje de pie entero (2.3m) quepa bajo el
        // techo de un auto (más bajo que eso). Nunca se notó con el jugador porque su
        // cuerpo sentado se OCULTA de su propia cámara (SelfHidden) -- este mismo bug
        // probablemente también afecta cómo lo ven los DEMÁS clientes en red, solo que
        // nadie lo había mirado de cerca todavía. Mismo truco que ya usa el agachado
        // de acá arriba (achicar + bajar el modelo), pero fijo (sin lerp, como el
        // resto de esta rama "seated").
        // owner: "siguen apareciendo detras y tambien debajo del auto" -- BUG REAL:
        // FriendNpcBuilder.SeatRootOffset (2.3) YA baja la raíz para alinear la cabeza
        // a la altura del asiento (como si estuviera parado); este drop ENCIMA la
        // bajaba OTRO METRO más -- las dos correcciones se sumaban y el personaje
        // terminaba bien por debajo del auto.
        // owner (ajuste en vivo, con Play): -0.8 los deja bien ubicados con scaleY=0.55.
        // Subir SOLO la escala a 0.8 (sin re-probar el combo) los mandó DENTRO del piso
        // del auto -- el achicado no pivotea desde los pies sino desde un punto más al
        // medio del modelo, así que escala y drop están ACOPLADOS: más escala = las
        // piernas se estiran más hacia abajo desde ese pivote, no solo "menos achatado".
        // owner (ajuste en vivo final, con Play, Friend_MaleCasual sentado en el auto):
        // combo confirmado que "quedó perfecto" -- scaleY=0.77 (subida de a poco desde
        // 0.55, re-chequeando el drop en cada paso como quedó anotado arriba) + drop
        // recalibrado a -0.63 para esa escala.
        public float seatedScaleY = 0.77f;     // alto del modelo sentado (fracción, además de doblar los muslos)
        public float seatedModelDrop = -0.63f; // cuánto sube/baja el modelo al sentarse (m) -- negativo = sube

        Transform[] _t;
        Quaternion[] _rest;
        Transform _model;
        Vector3 _modelScale;
        float _phase, _amp;
        Vector3 _lastPos;
        NetCrouchSync _net;   // opcional: solo en el prefab de red
        // owner: "cuando toco el control se agachan TODOS los personajes, no solo el que
        // manejo". Solo el humano JUGADOR tiene MapExplorer (los amigos decorativos no), y
        // PartyController lo deja habilitado únicamente cuando lo estás controlando (al
        // tomar el perro, se apaga). Se usa como filtro: el agachado por teclado solo
        // responde si ESTE es el personaje que controlás ahora.
        MapExplorer _explorer;

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
                // dirección de reposo del brazo: para abajo + un poco hacia AFUERA (según el
                // lado), así no quedan pegados al cuerpo (owner: "separales un poco los brazos").
                // El lado se detecta por POSICIÓN del hueso (a qué lado del cuerpo está), NO por
                // el nombre -- el rig de los amigos no usa ".r/.l" y quedaban los dos brazos para
                // el mismo lado (owner: "brazos torcidos para la izquierda").
                bool rightArm = Vector3.Dot(_t[i].position - transform.position, transform.right) > 0f;
                Vector3 armTarget = (Vector3.down + transform.right * (rightArm ? armSpread : -armSpread)).normalized;
                if (tip != null && tip != _t[i] && _t[i].parent != null)
                {
                    // dirección real del brazo (hombro → mano) y la roto para que apunte a armTarget
                    Vector3 dir = (tip.position - _t[i].position).normalized;
                    Quaternion worldDelta = Quaternion.FromToRotation(dir, armTarget);
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
                    StraightenArmChain(_t[i].childCount > 0 ? _t[i].GetChild(0) : null, tip, armTarget);
                }
            }
            _model = transform.Find("Model");
            if (_model == null)
            {
                var smr = GetComponentInChildren<SkinnedMeshRenderer>();
                // owner: "Richard se queda parado queriendo moverse pero en el mismo lugar" -- si
                // el fallback fuese la RAÍZ (personajes sin un hijo "Model", como Richard), el
                // LateUpdate le resetearía la localPosition cada frame y lo PINEA. En ese caso lo
                // dejamos null (solo animamos huesos; no manejamos escala/pos del modelo).
                if (smr != null && smr.transform.parent != transform) _model = smr.transform.parent;
            }
            if (_model != null) { _modelScale = _model.localScale; _modelBasePos = _model.localPosition; }
            else _modelScale = Vector3.one;
            _net = GetComponent<NetCrouchSync>();
            _explorer = GetComponent<MapExplorer>();
            _bodyCol = GetComponent<Collider>(); // colisión del cuerpo (la agregan los builders); se apaga sentado
            _lastPos = transform.position;
        }
        Vector3 _modelBasePos;
        float _crouchT;
        Collider _bodyCol;
        float _dbgTimer;

        // ¿está agachado? Online: lo decide/replica NetCrouchSync (el dueño escribe,
        // todos leen). Solo: teclado local (Ctrl/C).
        bool WantCrouch()
        {
            // solo el personaje que controlás ahora (humano jugador con su MapExplorer
            // activo) responde al teclado -- los amigos decorativos (sin MapExplorer) y el
            // humano mientras controlás al perro (MapExplorer apagado) nunca se agachan.
            bool controllingThis = _explorer != null && _explorer.enabled;
            var kb = Keyboard.current;
            bool localInput = controllingThis && kb != null && (kb.leftCtrlKey.isPressed || kb.cKey.isPressed);
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

            // Colisión del cuerpo: encendida cuando el personaje está en el mundo, APAGADA
            // mientras va sentado en el auto (si no, su collider estático choca contra el
            // Rigidbody del auto y traba/trepida el manejo).
            if (_bodyCol != null && _bodyCol.enabled == seated) _bodyCol.enabled = !seated;

            // TELEMETRÍA temporal: "los 3 amigos quedan enanos y bajo tierra al
            // bajarse" -- ver qué valores tiene realmente cada uno en el tiempo.
            if (name.StartsWith("Friend_"))
            {
                _dbgTimer += dt;
                if (_dbgTimer >= 0.5f)
                {
                    _dbgTimer = 0f;
                    Debug.Log($"[NPC] {name} seated={seated} baseScale={_modelScale} basePos={_modelBasePos} " +
                              $"curScale={(_model != null ? _model.localScale.ToString() : "null")} " +
                              $"curPos={(_model != null ? _model.localPosition.ToString() : "null")} " +
                              $"rootPos={transform.position} rootScale={transform.localScale}");
                }
            }

            if (seated)
            {
                // achicar+bajar el modelo (mismo mecanismo que el agachado): SIN esto,
                // rotar solo los muslos deja al personaje con el alto ENTERO de pie,
                // demasiado alto para caber bajo el techo del auto.
                if (_model != null)
                {
                    var s = _modelScale; s.y = _modelScale.y * seatedScaleY;
                    _model.localScale = s;
                    _model.localPosition = _modelBasePos + Vector3.down * seatedModelDrop;
                }
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
                // Brazos Y piernas giran en el eje "derecha" del PERSONAJE (mundo), no en el eje
                // LOCAL del hueso -- así el vaivén cae SIEMPRE en el plano adelante-atrás sin
                // importar cómo esté orientado el hueso en cada rig (owner: "las piernas de
                // Richard se deslizan de costado" -- su rig tiene el eje local del muslo apuntando
                // al costado; con el eje del personaje camina bien, igual que los brazos).
                if (_t[i].parent != null)
                {
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
        static void StraightenArmChain(Transform from, Transform tip, Vector3 target)
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
                    Quaternion delta = Quaternion.FromToRotation(dir.normalized, target);
                    Quaternion pW = bone.parent.rotation;
                    bone.localRotation = Quaternion.Inverse(pW) * delta * (pW * bone.localRotation);
                }
                bone = child;
            }
        }

        // owner: "el playero sentado en la silla, visible ya en el editor". Pose sentada
        // ESTÁTICA y robusta (una sola vez, sin componente vivo, sin depender de ejes locales
        // del rig): apunta cada hueso a una dirección de MUNDO usando FromToRotation.
        //   · brazos (armUpper, ej "shoulder.L"): hombro→mano hacia ABAJO (a los costados) +
        //     enderezar la cadena (antebrazo/mano).
        //   · muslos (thighs): hacia ADELANTE-abajo (sentado).
        //   · pantorrillas (calves): hacia ABAJO (pies al piso).
        // Llamalo DESPUÉS de ubicar/rotar al personaje (usa direcciones de mundo).
        // 'facing' = hacia dónde mira el personaje (horizontal, normalizado).
        public static void PoseSeatedStatic(Transform root, Vector3 facing,
                                            string[] armUpper, string[] thighs, string[] calves)
        {
            if (root == null) return;
            facing.y = 0f;
            facing = facing.sqrMagnitude < 1e-4f ? Vector3.forward : facing.normalized;
            Vector3 thighTarget = (facing * 0.9f + Vector3.down * 0.35f).normalized; // adelante y un poco abajo

            if (armUpper != null)
                foreach (var n in armUpper)
                {
                    var b = FindDeep(root, n);
                    if (b == null) continue;
                    var tip = DeepestChild(b);
                    if (tip == null || tip == b) continue;
                    Vector3 dir = (tip.position - b.position).normalized;
                    b.rotation = Quaternion.FromToRotation(dir, Vector3.down) * b.rotation;
                    StraightenArmChain(b.childCount > 0 ? b.GetChild(0) : null, tip, Vector3.down);
                }
            if (thighs != null)
                foreach (var n in thighs) PointBone(FindDeep(root, n), thighTarget);
            if (calves != null)
                foreach (var n in calves) PointBone(FindDeep(root, n), Vector3.down);
        }

        // Pose de PARADO estática: brazos colgando (hombro+antebrazo a -Y) y piernas rectas
        // (muslo+pantorrilla a -Y). Sirve para "levantar" a un NPC que estaba sentado con
        // PoseSeatedStatic (mismo rig, mano/pie ya reencadenados). Todo por dirección de mundo,
        // así funciona con cualquier yaw del cuerpo.
        public static void PoseStandingStatic(Transform root, string[] armUpper, string[] foreArms,
                                              string[] thighs, string[] calves)
        {
            if (root == null) return;
            if (armUpper != null) foreach (var n in armUpper) PointBone(FindDeep(root, n), Vector3.down);
            if (foreArms != null) foreach (var n in foreArms) PointBone(FindDeep(root, n), Vector3.down);
            if (thighs != null)   foreach (var n in thighs)   PointBone(FindDeep(root, n), Vector3.down);
            if (calves != null)   foreach (var n in calves)   PointBone(FindDeep(root, n), Vector3.down);
        }

        // rota 'b' para que apunte (b.head→primer hijo) hacia 'worldDir', sin importar sus ejes.
        static void PointBone(Transform b, Vector3 worldDir)
        {
            if (b == null || b.childCount == 0) return;
            Vector3 dir = (b.GetChild(0).position - b.position).normalized;
            if (dir.sqrMagnitude < 1e-6f) return;
            b.rotation = Quaternion.FromToRotation(dir, worldDir.normalized) * b.rotation;
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
