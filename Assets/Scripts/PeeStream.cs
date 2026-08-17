// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  PeeStream.cs — chorro de meada PROCEDURAL (Particle System, sin
//  assets). owner: el personaje aparece parado meando en el árbol al
//  inicio (ver OpeningDriveSequence). Se prende con StartPee() y se
//  APAGA SOLO cuando el personaje se ALEJA del punto (camina al auto).
//
//  El emisor vive en ESPACIO-MUNDO (sin parent, escala 1) y se
//  reposiciona cada frame a la cintura del personaje -- así es inmune a
//  la escala del rig (que si no puede hacer las gotas gigantes o
//  invisibles). El origen queda ADELANTE del cuerpo (fuera del collider)
//  para que la colisión no mate las gotas al nacer.
// ============================================================
using UnityEngine;

namespace FolkloreArchives
{
    public class PeeStream : MonoBehaviour
    {
        [Header("Origen (local al personaje) y dirección")]
        public Vector3 localOffset = new Vector3(0f, 1.15f, 0.45f); // a la altura de la PANZA, ADELANTE (fuera del collider)
        public Vector3 localAim    = new Vector3(0f, -0.25f, 1f);   // arco más plano = llega más lejos

        [Header("Look (ajustar en vivo)")]
        public Color color    = new Color(0.95f, 0.90f, 0.35f, 1f); // amarillo (el borde negro sale de la textura)
        public float rate     = 180f;  // gotas por segundo (denso = línea continua, no bolitas)
        public float speed    = 4.5f;  // m/s (más = chorro más largo)
        public float gravity  = 0.6f;  // menos = arco más plano/lejano
        public float dropSize = 0.055f;// m (fino)
        public float lifetime = 1.1f;  // seg (más = llega más lejos)
        public float stopWhenMovedAway = 1.0f;
        public bool  collide = false;  // OFF por ahora (evita que muera al nacer); prendé cuando esté todo ok
        public float peeVolume = 0.5f;      // volumen del sonido del chorro
        public float peeStartOffset = 1.2f; // seg que se saltea el clip (el sample tarda en "arrancar" el agua)

        ParticleSystem _ps;
        Transform _emitter;
        AudioSource _audio;
        bool _peeing, _logged;
        Vector3 _startPos;

        void Awake() => Build();

        void Build()
        {
            var go = new GameObject("PeeStream_FX");   // SIN parent: espacio-mundo, escala 1
            _emitter = go.transform;

            _ps = go.AddComponent<ParticleSystem>();
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = _ps.main;
            main.startLifetime   = lifetime;
            main.startSpeed      = speed;
            main.startSize       = dropSize;
            main.startColor      = color;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode     = ParticleSystemScalingMode.Hierarchy;
            main.playOnAwake     = false;
            main.maxParticles    = 500;

            var em = _ps.emission; em.rateOverTime = rate;
            var sh = _ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 5f; sh.radius = 0.02f;
            var col = _ps.collision; col.enabled = collide; col.type = ParticleSystemCollisionType.World;
            col.mode = ParticleSystemCollisionMode.Collision3D; col.lifetimeLoss = 0.3f;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode    = ParticleSystemRenderMode.Stretch; // estirado a lo largo del movimiento = chorro, no bolitas
            r.lengthScale   = 2.6f;
            r.velocityScale = 0.08f;
            r.material = new Material(Shader.Find("Sprites/Default")) { mainTexture = MakeDotTexture() };

            // sonido del chorro (procedural, sin assets): ruido filtrado en loop
            _audio = go.AddComponent<AudioSource>();
            // si dropeás tu propio asset en Assets/Resources/pee_loop.* lo usa; si no, cae
            // al chorro procedural. Ideal: un loop de "agua cayendo / chorro / trickle".
            var customPee       = Resources.Load<AudioClip>("pee_loop");
            _audio.clip         = customPee != null ? customPee : MakePeeClip();
            _audio.loop         = true;
            _audio.playOnAwake  = false;
            _audio.spatialBlend = 1f;   // 3D: sale del pj
            _audio.rolloffMode  = AudioRolloffMode.Linear;
            _audio.minDistance  = 2f;
            _audio.maxDistance  = 18f;
            _audio.volume       = peeVolume;

            Debug.Log($"<color=cyan>[PEE] Build — shader={r.material.shader.name}, collide={collide}</color>");
        }

        // gota SUAVE (bordes difuminados) con apenas un dejo oscuro translúcido en el
        // borde -- lo justo para despegarla del pasto amarillo, SIN el contorno negro
        // marcado de antes (que la hacía parecer una bolita de dibujito).
        static Texture2D MakeDotTexture()
        {
            const int N = 32;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
            var c = new Color[N * N];
            Vector2 mid = new Vector2((N - 1) * 0.5f, (N - 1) * 0.5f);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), mid) / (N * 0.5f); // 0 centro .. 1 borde
                    float rgb, a;
                    if (d < 0.5f) { rgb = 1f; a = 1f; }               // centro lleno -> amarillo
                    else if (d < 1f)
                    {
                        float k = (d - 0.5f) / 0.5f;                  // 0..1 hacia el borde
                        rgb = Mathf.Lerp(1f, 0.30f, k);              // se oscurece apenas
                        a   = Mathf.Lerp(1f, 0f, k * k);             // se desvanece suave (anti-alias)
                    }
                    else { rgb = 0f; a = 0f; }
                    c[y * N + x] = new Color(rgb, rgb, rgb, a);
                }
            t.SetPixels(c); t.Apply(); t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        void Track()
        {
            if (_emitter == null) return;
            _emitter.position = transform.TransformPoint(localOffset);
            _emitter.rotation = transform.rotation * Quaternion.LookRotation(
                localAim.sqrMagnitude > 1e-4f ? localAim.normalized : Vector3.forward);
        }

        public void StartPee()
        {
            _peeing = true; _logged = false;
            _startPos = transform.position;
            Track();
            if (_ps != null) _ps.Play();
            if (_audio != null)
            {
                _audio.volume = peeVolume;
                _audio.Play();
                // adelantar: saltear el arranque del sample (agua que tarda en salir)
                if (_audio.clip != null) _audio.time = Mathf.Clamp(peeStartOffset, 0f, _audio.clip.length - 0.1f);
            }
            Debug.Log("<color=lime>[PEE] StartPee ON</color>");
        }

        public void StopPee()
        {
            _peeing = false;
            if (_ps != null) _ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (_audio != null) _audio.Stop();
        }

        // sonido de chorro: ruido blanco pasado por un low-pass simple (queda "psss"
        // líquido) + un borboteo lento de amplitud. 1 s en loop (con fundido para no
        // hacer click). Todo procedural, sin assets.
        static AudioClip MakePeeClip()
        {
            const int sr = 44100;
            int len = sr;
            var data = new float[len];
            var rng = new System.Random(777);
            float lp = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / sr;
                float n = (float)rng.NextDouble() * 2f - 1f;
                lp = Mathf.Lerp(lp, n, 0.35f);                          // low-pass
                float amp = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 6f * t); // borboteo
                data[i] = Mathf.Clamp(lp * amp, -1f, 1f) * 0.6f;
            }
            const int fade = 800;
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                data[len - 1 - i] = Mathf.Lerp(data[len - 1 - i], data[i], 1f - k);
            }
            var clip = AudioClip.Create("PeeLoop", len, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        void LateUpdate()
        {
            if (!_peeing) return;
            Track();

            if (!_logged && _ps != null && _ps.particleCount > 0)
            {
                _logged = true;
                var cam = Camera.main;
                float dist = cam != null ? Vector3.Distance(cam.transform.position, _emitter.position) : -1f;
                Debug.Log($"<color=yellow>[PEE] emitterPos={_emitter.position} particles={_ps.particleCount} " +
                          $"camDist={dist:0.0} playerScale={transform.lossyScale}</color>");
            }

            Vector3 d = transform.position - _startPos; d.y = 0f;
            if (d.magnitude > stopWhenMovedAway) StopPee();
        }

        void OnDestroy()
        {
            if (_emitter != null) Destroy(_emitter.gameObject); // el emisor no es hijo -> limpiarlo a mano
        }
    }
}
