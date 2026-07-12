// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  LuzMala.cs — el fenómeno que da nombre al juego. Un fuego fatuo
//  del folklore patagónico: una luz flotante que aparece de NOCHE.
//
//  Comportamiento:
//   · Solo de noche (DayNightController.IsNight). De día se esconde.
//   · Deriva/flota (bob + wander Perlin) a la altura del terreno.
//   · Si estás cerca y NO la mirás/iluminás → se ACERCA lento.
//   · Si la mirás de frente o le apuntás con la linterna → RETROCEDE
//     y se atenúa (mecánica de tensión: para escapar hay que mirarla).
//   · Si te alcanza → susto (parpadea tu linterna) y se aleja de golpe.
//
//  Es solo una luz + esfera emisiva: el bloom del grade VHS la hace
//  brillar. Sin assets. Tuneable desde el inspector.
// ============================================================
using UnityEngine;

namespace FolkloreArchives
{
    public class LuzMala : MonoBehaviour
    {
        [Header("Aspecto")]
        public Color color = new Color(1f, 0.92f, 0.55f);  // amarillo pálido fantasmal
        public float lightRange = 18f;
        public float baseIntensity = 6f;
        public float orbSize = 0.85f;

        [Header("Flotación")]
        public float hoverHeight = 1.6f;   // altura sobre el terreno
        public float bobAmplitude = 0.5f;
        public float bobSpeed = 1.3f;
        public float wanderRadius = 6f;    // deriva alrededor del punto de aparición
        public float wanderSpeed = 0.35f;

        [Header("Acecho")]
        public float activateDistance = 55f; // desde acá te empieza a notar
        public float approachSpeed = 2.2f;   // se acerca lento
        public float retreatSpeed = 9f;      // huye rápido si la mirás/iluminás
        public float killDistance = 2.2f;    // te alcanzó
        [Range(0f, 1f)] public float lookThreshold = 0.9f;  // cuán de frente hay que mirarla

        Light _light;
        Transform _orb;
        Material _orbMat;
        DayNightController _dnc;
        Terrain _terrain;
        Vector3 _home;
        float _seed, _flashUntil, _scareCooldown;
        float _intensity;

        void Start()
        {
            _home = transform.position;
            _seed = Random.value * 100f;
            _terrain = Terrain.activeTerrain;
            _dnc = FindFirstObjectByType<DayNightController>();

            // luz puntual
            _light = GetComponentInChildren<Light>();
            if (_light == null)
            {
                var lg = new GameObject("LuzMalaLight");
                lg.transform.SetParent(transform, false);
                _light = lg.AddComponent<Light>();
            }
            _light.type = LightType.Point;
            _light.color = color;
            _light.range = lightRange;
            _light.shadows = LightShadows.None;

            // esfera emisiva (el "cuerpo" de la luz)
            _orb = transform.Find("Orb");
            if (_orb == null)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                s.name = "Orb";
                Destroy(s.GetComponent<Collider>());
                s.transform.SetParent(transform, false);
                s.transform.localScale = Vector3.one * orbSize;
                _orb = s.transform;
            }
            var mr = _orb.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit");
                _orbMat = new Material(sh);
                if (_orbMat.HasProperty("_BaseColor")) _orbMat.SetColor("_BaseColor", color);
                _orbMat.EnableKeyword("_EMISSION");
                if (_orbMat.HasProperty("_EmissionColor")) _orbMat.SetColor("_EmissionColor", color * 6f); // HDR → bloom
                mr.sharedMaterial = _orbMat;
            }
            _intensity = baseIntensity;
        }

        void Update()
        {
            bool night = _dnc == null || _dnc.IsNight;
            if (_light != null && _light.enabled != night) _light.enabled = night;
            if (_orb != null && _orb.gameObject.activeSelf != night) _orb.gameObject.SetActive(night);
            if (!night) return;

            float dt = Time.deltaTime;
            var cam = Camera.main;
            Vector3 pos = transform.position;

            // ¿el jugador la mira de frente o le apunta con la linterna?
            bool lookedAt = false, lit = false;
            float dist = 999f;
            if (cam != null)
            {
                Vector3 toCam = cam.transform.position - pos;
                dist = toCam.magnitude;
                Vector3 dirToLuz = (-toCam).normalized;
                float look = Vector3.Dot(cam.transform.forward, dirToLuz);
                lookedAt = look > lookThreshold && dist < activateDistance;      // mirándola de frente
                lit = FlashlightOn(cam) && look > 0.7f && dist < 32f;            // linterna (cono más ancho)
            }

            // ---- movimiento ----
            Vector3 planar = Vector3.zero;
            if (cam != null && (lookedAt || lit))
            {
                // huye del jugador
                Vector3 away = (pos - cam.transform.position); away.y = 0f;
                planar = away.normalized * retreatSpeed;
                _intensity = Mathf.Lerp(_intensity, baseIntensity * 0.25f, 6f * dt); // se atenúa
            }
            else if (cam != null && dist < activateDistance && dist > killDistance)
            {
                // te acecha: se acerca lento
                Vector3 toward = (cam.transform.position - pos); toward.y = 0f;
                planar = toward.normalized * approachSpeed;
                _intensity = Mathf.Lerp(_intensity, baseIntensity * 1.15f, 3f * dt);
            }
            else
            {
                // deriva alrededor de su punto de aparición
                float nx = Mathf.PerlinNoise(_seed, Time.time * wanderSpeed) - 0.5f;
                float nz = Mathf.PerlinNoise(Time.time * wanderSpeed, _seed) - 0.5f;
                Vector3 target = _home + new Vector3(nx, 0f, nz) * wanderRadius * 2f;
                planar = Vector3.ClampMagnitude((target - pos), 1f); planar.y = 0f;
                planar *= wanderSpeed * 4f;
                _intensity = Mathf.Lerp(_intensity, baseIntensity, 2f * dt);
            }

            pos += planar * dt;

            // altura: flota sobre el terreno con bob
            float groundY = _terrain != null ? _terrain.SampleHeight(pos) + _terrain.transform.position.y : pos.y;
            float bob = Mathf.Sin(Time.time * bobSpeed + _seed) * bobAmplitude;
            pos.y = groundY + hoverHeight + bob;
            transform.position = pos;

            // parpadeo del brillo (Perlin) + susto reciente
            float flick = 0.75f + 0.25f * Mathf.PerlinNoise(_seed * 2f, Time.time * 7f);
            float inten = _intensity * flick;
            if (_light != null) _light.intensity = inten;
            if (_orbMat != null && _orbMat.HasProperty("_EmissionColor"))
                _orbMat.SetColor("_EmissionColor", color * Mathf.Max(0.5f, inten));

            // ---- te alcanzó: susto ----
            if (cam != null && dist < killDistance && Time.time >= _scareCooldown)
                Scare(cam, pos);
        }

        // busca el foco (spot) de la linterna bajo la cámara y ve si está encendido
        bool FlashlightOn(Camera cam)
        {
            foreach (var l in cam.GetComponentsInChildren<Light>())
                if (l != _light && l.type == LightType.Spot && l.enabled) return true;
            return false;
        }

        // susto: parpadea la linterna del jugador y la Luz se aleja de golpe. (v1: sin
        // game-over todavía; se puede enganchar acá un fade/respawn/daño de cordura.)
        void Scare(Camera cam, Vector3 pos)
        {
            foreach (var l in cam.GetComponentsInChildren<Light>())
                if (l.type == LightType.Spot) l.enabled = false;   // se te apaga la linterna
            _flashUntil = Time.time + 0.6f;
            _scareCooldown = Time.time + 6f;
            // se aleja de golpe (reaparece lejos)
            Vector3 away = (pos - cam.transform.position); away.y = 0f;
            transform.position = pos + away.normalized * 25f;
            _home = transform.position;
            Debug.Log("<color=orange>[LuzMala] te alcanzó — susto</color>");
        }

        void LateUpdate()
        {
            // devolver la linterna tras el parpadeo del susto
            if (_flashUntil > 0f && Time.time >= _flashUntil)
            {
                _flashUntil = 0f;
                var cam = Camera.main;
                if (cam != null)
                    foreach (var l in cam.GetComponentsInChildren<Light>(true))
                        if (l.type == LightType.Spot) l.enabled = true;
            }
        }
    }
}
