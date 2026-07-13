// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  LuzMala.cs — el fenómeno que da nombre al juego. Un fuego fatuo
//  del folklore patagónico: una luz flotante que aparece de NOCHE.
//
//  Visual: NO es una esfera sólida. Son billboards ADITIVOS que
//  miran a la cámara — núcleo caliente + halo difuso + rayos — con
//  texturas generadas por código. El bloom del grade VHS los funde
//  en un resplandor tipo "luz mala" (difuso, con halo y destellos).
//  Sin assets.
//
//  Comportamiento:
//   · Solo de noche (DayNightController.IsNight). De día se esconde.
//   · Flota (bob + wander). Si estás cerca y NO la mirás → se ACERCA.
//   · Si la mirás de frente o le apuntás con la linterna → RETROCEDE.
//   · Si te alcanza → susto (apaga tu linterna) y se aleja de golpe.
// ============================================================
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace FolkloreArchives
{
    public class LuzMala : MonoBehaviour
    {
        [Header("Aspecto")]
        public Color idleColor = new Color(0.9f, 0.95f, 1f);   // BLANCA cuando está tranquila
        public Color aggroColor = new Color(1f, 0.18f, 0.12f); // ROJA cuando se pone agresiva
        public float lightRange = 36f;
        public float baseIntensity = 8f;
        public float glowSize = 11.5f;     // tamaño del resplandor (grande, un toque menos)

        [Header("Pruebas")]
        public bool holdStill = true;      // no se mueve (para verla bien). Poner false para el acecho.

        Color _curColor;
        bool _manualRed;   // tecla L: fuerza roja (para verla)

        [Header("Flotación")]
        public float hoverHeight = 4f;
        public float bobAmplitude = 0.5f;
        public float bobSpeed = 1.3f;
        public float wanderRadius = 6f;
        public float wanderSpeed = 0.35f;

        [Header("Acecho")]
        public float activateDistance = 55f;
        public float approachSpeed = 2.2f;
        public float retreatSpeed = 9f;
        public float killDistance = 2.2f;
        [Range(0f, 1f)] public float lookThreshold = 0.9f;

        Light _light;
        DayNightController _dnc;
        Terrain _terrain;
        Vector3 _home;
        float _seed, _flashUntil, _scareCooldown, _intensity;

        // capas del resplandor (billboards)
        Transform[] _bb;
        Material[] _bbMat;
        float[] _bbScale, _bbFlickerSeed;

        void Start()
        {
            _home = transform.position;
            _seed = Random.value * 100f;
            _terrain = Terrain.activeTerrain;
            _dnc = FindFirstObjectByType<DayNightController>();

            // luz puntual (ilumina el entorno)
            _light = GetComponentInChildren<Light>();
            if (_light == null)
            {
                var lg = new GameObject("LuzMalaLight");
                lg.transform.SetParent(transform, false);
                _light = lg.AddComponent<Light>();
            }
            _curColor = idleColor;
            _light.type = LightType.Point;
            _light.color = _curColor;
            _light.range = lightRange;
            _light.shadows = LightShadows.None;

            // halo radial suave y MOTEADO (neblina), sin rayos → difuso, no "estrella"
            var radial = MakeRadialTex();

            // 3 capas de halo: exterior tenue + medio + núcleo. Todo círculos suaves.
            _bb = new Transform[3];
            _bbMat = new Material[3];
            _bbScale = new[] { glowSize * 2.4f, glowSize * 1.0f, glowSize * 3.6f };
            _bbFlickerSeed = new[] { Random.value * 10f, Random.value * 10f, Random.value * 10f };
            var texs = new[] { radial, radial, radial };
            var tints = new[] { _curColor * 0.4f, _curColor * 1.3f, _curColor * 0.18f };
            for (int i = 0; i < 3; i++)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = "Glow" + i;
                Destroy(q.GetComponent<Collider>());
                q.transform.SetParent(transform, false);
                q.transform.localScale = Vector3.one * _bbScale[i];
                _bbMat[i] = MakeAdditive(texs[i], tints[i]);
                q.GetComponent<MeshRenderer>().sharedMaterial = _bbMat[i];
                _bb[i] = q.transform;
            }
            _intensity = baseIntensity;
        }

        void Update()
        {
            bool night = _dnc == null || _dnc.IsNight;
            if (_light != null && _light.enabled != night) _light.enabled = night;
            if (_bb != null) foreach (var b in _bb) if (b != null && b.gameObject.activeSelf != night) b.gameObject.SetActive(night);
            if (!night) return;

            float dt = Time.deltaTime;
            var cam = Camera.main;
            Vector3 pos = transform.position;

            // ¿la mira de frente o la ilumina?
            bool lookedAt = false, lit = false;
            float dist = 999f;
            if (cam != null)
            {
                Vector3 toCam = cam.transform.position - pos;
                dist = toCam.magnitude;
                Vector3 dirToLuz = (-toCam).normalized;
                float look = Vector3.Dot(cam.transform.forward, dirToLuz);
                lookedAt = look > lookThreshold && dist < activateDistance;
                lit = FlashlightOn(cam) && look > 0.7f && dist < 32f;
            }

            // Tecla L: alterna ROJO/BLANCO a mano (para verla).
            if (Keyboard.current != null && Keyboard.current.lKey.wasPressedThisFrame)
                _manualRed = !_manualRed;

            // ESTADO: agresiva = está cerca y NO la mirás (te acecha) → se pone ROJA.
            //         tranquila/mirándola/lejos → BLANCA. La L la fuerza a roja.
            bool aggro = cam != null && dist < activateDistance * 0.7f && dist > killDistance && !(lookedAt || lit);
            bool wantRed = _manualRed || aggro;
            _curColor = Color.Lerp(_curColor, wantRed ? aggroColor : idleColor, 4f * dt);
            if (_light != null) _light.color = _curColor;

            // movimiento (si holdStill, se queda quieta para verla bien)
            Vector3 planar = Vector3.zero;
            if (!holdStill)
            {
                if (cam != null && (lookedAt || lit))
                {
                    Vector3 away = pos - cam.transform.position; away.y = 0f;
                    planar = away.normalized * retreatSpeed;
                    _intensity = Mathf.Lerp(_intensity, baseIntensity * 0.25f, 6f * dt);
                }
                else if (cam != null && dist < activateDistance && dist > killDistance)
                {
                    Vector3 toward = cam.transform.position - pos; toward.y = 0f;
                    planar = toward.normalized * approachSpeed;
                    _intensity = Mathf.Lerp(_intensity, baseIntensity * 1.15f, 3f * dt);
                }
                else
                {
                    float nx = Mathf.PerlinNoise(_seed, Time.time * wanderSpeed) - 0.5f;
                    float nz = Mathf.PerlinNoise(Time.time * wanderSpeed, _seed) - 0.5f;
                    Vector3 target = _home + new Vector3(nx, 0f, nz) * wanderRadius * 2f;
                    planar = Vector3.ClampMagnitude(target - pos, 1f); planar.y = 0f;
                    planar *= wanderSpeed * 4f;
                    _intensity = Mathf.Lerp(_intensity, baseIntensity, 2f * dt);
                }
            }
            pos += planar * dt;

            float groundY = _terrain != null ? _terrain.SampleHeight(pos) + _terrain.transform.position.y : pos.y;
            float bob = Mathf.Sin(Time.time * bobSpeed + _seed) * bobAmplitude;
            pos.y = groundY + hoverHeight + bob;
            transform.position = pos;

            float flick = 0.75f + 0.25f * Mathf.PerlinNoise(_seed * 2f, Time.time * 7f);
            float inten = _intensity * flick;
            if (_light != null) _light.intensity = inten;
            UpdateGlow(cam, inten);

            if (!holdStill && cam != null && dist < killDistance && Time.time >= _scareCooldown)
                Scare(cam, pos);
        }

        // orienta cada capa hacia la cámara y le da un latido/parpadeo propio
        void UpdateGlow(Camera cam, float inten)
        {
            if (_bb == null) return;
            float k = Mathf.Clamp01(inten / Mathf.Max(0.01f, baseIntensity));
            for (int i = 0; i < _bb.Length; i++)
            {
                if (_bb[i] == null) continue;
                if (cam != null) _bb[i].rotation = Quaternion.LookRotation(_bb[i].position - cam.transform.position);
                // latido: cada capa palpita con su propia fase (fuego fatuo "vivo")
                float pulse = 0.85f + 0.15f * Mathf.PerlinNoise(_bbFlickerSeed[i], Time.time * (5f + i));
                _bb[i].localScale = Vector3.one * (_bbScale[i] * pulse);
                if (_bbMat[i] != null && _bbMat[i].HasProperty("_BaseColor"))
                {
                    Color baseT = (i == 1) ? _curColor * 1.3f : (i == 0 ? _curColor * 0.4f : _curColor * 0.18f);
                    _bbMat[i].SetColor("_BaseColor", baseT * (0.5f + k));
                }
            }
        }

        bool FlashlightOn(Camera cam)
        {
            foreach (var l in cam.GetComponentsInChildren<Light>())
                if (l != _light && l.type == LightType.Spot && l.enabled) return true;
            return false;
        }

        void Scare(Camera cam, Vector3 pos)
        {
            foreach (var l in cam.GetComponentsInChildren<Light>())
                if (l.type == LightType.Spot) l.enabled = false;
            _flashUntil = Time.time + 0.6f;
            _scareCooldown = Time.time + 6f;
            Vector3 away = pos - cam.transform.position; away.y = 0f;
            transform.position = pos + away.normalized * 25f;
            _home = transform.position;
            Debug.Log("<color=orange>[LuzMala] te alcanzó — susto</color>");
        }

        void LateUpdate()
        {
            if (_flashUntil > 0f && Time.time >= _flashUntil)
            {
                _flashUntil = 0f;
                var cam = Camera.main;
                if (cam != null)
                    foreach (var l in cam.GetComponentsInChildren<Light>(true))
                        if (l.type == LightType.Spot) l.enabled = true;
            }
        }

        // ---- materiales/texturas procedurales ----

        static Material MakeAdditive(Texture2D tex, Color tint)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Surface", 1f);                 // transparent
            m.SetFloat("_Blend", 2f);                   // additive
            m.SetFloat("_SrcBlend", (float)BlendMode.One);
            m.SetFloat("_DstBlend", (float)BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", (float)CullMode.Off);   // doble cara (no importa a qué lado mire)
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        // halo radial suave y MOTEADO: núcleo caliente + desvanecido con ruido tipo
        // niebla, y corte limpio a 0 antes del borde (para que NO se vea el cuadrado
        // del billboard). Nada de gradiente perfecto → menos "plástico".
        static Texture2D MakeRadialTex()
        {
            const int N = 128;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float halo = Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f);
                    float core = Mathf.Pow(Mathf.Clamp01(1f - d * 2.4f), 3f);
                    // ruido moteado (dos octavas) → aspecto de niebla, no plástico
                    float n = 0.6f * Mathf.PerlinNoise(dx * 5f + 11f, dy * 5f + 7f)
                            + 0.4f * Mathf.PerlinNoise(dx * 11f + 3f, dy * 11f + 19f);
                    float mottle = 0.65f + 0.7f * n;   // ~0.65..1.35
                    float v = Mathf.Clamp01((halo * 0.75f + core) * mottle);
                    // corte limpio a cero entre d=0.8 y 1.0 → círculo, sin borde cuadrado
                    v *= Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((d - 0.8f) / 0.2f));
                    px[y * N + x] = new Color(v, v, v, v);
                }
            t.SetPixels(px); t.Apply();
            return t;
        }
    }
}
