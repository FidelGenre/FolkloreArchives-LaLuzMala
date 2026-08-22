// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  DayNightController.cs — togglea día/noche en runtime (Tab).
//  SetDay() maneja el "modo" (sol, cielo, color, tinte del pasto);
//  ApplyGraphics() aplica las DISTANCIAS (niebla, pasto, árboles,
//  cámara) partiendo de los valores base por modo y multiplicándolos
//  por los settings gráficos del jugador (GameSettings).
// ============================================================
using System.Collections;
using UnityEngine;

namespace FolkloreArchives
{
    public class DayNightController : MonoBehaviour
    {
        // owner: se sacó el DÍA. Ahora solo TARDE (Dusk) y NOCHE. Tab alterna tarde ↔ noche.
        // El enum conserva Day por compatibilidad, pero ya no se usa en el ciclo.
        public enum Phase { Day, Dusk, Night }

        [Header("Skyboxes (asignados por TestPlayerBuilder)")]
        public Material daySkybox;
        public Material duskSkybox;
        public Material nightSkybox;

        [Header("Referencias de escena")]
        public Light sun;
        public Terrain terrain;

        Phase _phase = Phase.Dusk;   // owner: el juego arranca de TARDE (ya no hay día)

        // Compatibilidad: el resto del código sigue pensando en día/noche. owner: la TARDE cuenta
        // como día -> IsDay es true en tarde (todo lo que no sea noche).
        public bool IsDay => _phase != Phase.Night;
        public bool IsNight => _phase == Phase.Night;   // lo usa la Luz Mala (solo de noche)
        public Phase CurrentPhase => _phase;

        // Aplica la fase inicial al entrar en Play. Sin esto, _phase decía "Night" pero
        // el cielo/sol/niebla de la escena quedaban como los dejó la generación (o el
        // toggle "Pasar a Día" del editor): el primer Tab llevaba a Day y no se veía
        // ningún cambio, como si Tab se hubiera saltado un paso.
        void Start() => SetPhase(_phase);

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab))
                Toggle();
        }

        // owner: Tab alterna SOLO tarde ↔ noche (sin día).
        void Toggle() => SetPhase(_phase == Phase.Night ? Phase.Dusk : Phase.Night);

        // Wrapper viejo: SetDay(true) = TARDE (antes era día), SetDay(false) = noche.
        public void SetDay(bool day) => SetPhase(day ? Phase.Dusk : Phase.Night);

        public void SetPhase(Phase phase)
        {
            _phase = phase;

            switch (_phase)
            {
                case Phase.Day:
                    // Día de cielo azul con nubes (Cold Sunset). Sol pleno, sombras duras.
                    // La niebla va gris-azulada, NO rosada: un tinte cálido acá se suma al
                    // grade VHS ámbar y el día entero se va a naranja.
                    if (sun != null)
                    {
                        sun.intensity = 1.0f;
                        sun.color     = new Color(1f, 0.96f, 0.88f);
                        sun.shadows   = LightShadows.Hard;
                    }
                    if (daySkybox != null) RenderSettings.skybox = daySkybox;
                    RenderSettings.ambientLight = new Color(0.30f, 0.30f, 0.36f);
                    RenderSettings.fogMode  = FogMode.Linear;
                    RenderSettings.fogColor = new Color(0.62f, 0.64f, 0.70f);
                    Shader.SetGlobalColor("_GrassTintMul", new Color(0.34f, 0.42f, 0.20f)); // verde oscuro/quemado
                    break;

                case Phase.Dusk:
                    // Atardecer con el cielo azul de Cold Sunset y el sol ya bajo: luz
                    // cálida y rasante, pero la niebla va MALVA/GRIS, no roja — una
                    // niebla saturada se suma al grade VHS ámbar y tiñe todo de sangre.
                    if (sun != null)
                    {
                        sun.intensity = 0.72f;
                        sun.color     = new Color(1f, 0.78f, 0.58f);
                        sun.shadows   = LightShadows.Hard;
                    }
                    if (duskSkybox != null) RenderSettings.skybox = duskSkybox;
                    RenderSettings.ambientLight = new Color(0.22f, 0.20f, 0.25f);
                    RenderSettings.fogMode  = FogMode.ExponentialSquared;
                    RenderSettings.fogColor = new Color(0.36f, 0.33f, 0.36f);
                    Shader.SetGlobalColor("_GrassTintMul", new Color(0.42f, 0.34f, 0.22f)); // pasto quemado por la última luz
                    break;

                default: // Phase.Night
                    if (sun != null)
                    {
                        sun.intensity = 0.16f;
                        sun.color     = new Color(0.42f, 0.52f, 0.78f);
                        sun.shadows   = LightShadows.Hard;
                    }
                    if (nightSkybox != null) RenderSettings.skybox = nightSkybox;
                    RenderSettings.ambientLight = new Color(0.016f, 0.026f, 0.052f);
                    RenderSettings.fogMode  = FogMode.ExponentialSquared;
                    RenderSettings.fogColor = new Color(0.035f, 0.055f, 0.105f);
                    Shader.SetGlobalColor("_GrassTintMul", Color.white); // de noche va normal
                    break;
            }

            ApplyGraphics(); // distancias/niebla con los multiplicadores de GameSettings
        }

        // Transición SUAVE de TARDE (Dusk) a NOCHE en 'secs' segundos (no un corte). Interpola sol,
        // ambiente, niebla y tinte del pasto; cambia el skybox a mitad de camino (la niebla ya
        // oculta el cielo). Al terminar deja la fase Night limpia.
        public IEnumerator FadeToNight(float secs)
        {
            SetPhase(Phase.Dusk);
            float t = 0f;
            while (t < secs) { t += Time.deltaTime; SetNightBlend(t / secs); yield return null; }
            SetPhase(Phase.Night);
        }

        // aplica un estado intermedio tarde(0)->noche(1). No toca las distancias del terreno (caras
        // de recalcular); esas quedan como Dusk hasta que FadeToNight llama SetPhase(Night) al final.
        public void SetNightBlend(float k)
        {
            k = Mathf.Clamp01(k);
            if (sun != null)
            {
                sun.intensity = Mathf.Lerp(0.72f, 0.16f, k);
                sun.color     = Color.Lerp(new Color(1f, 0.78f, 0.58f), new Color(0.42f, 0.52f, 0.78f), k);
                sun.shadows   = LightShadows.Hard;
            }
            RenderSettings.ambientLight = Color.Lerp(new Color(0.22f, 0.20f, 0.25f), new Color(0.016f, 0.026f, 0.052f), k);
            RenderSettings.fogMode  = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = Mathf.Lerp(0.018f, 0.05f, k) / Mathf.Max(0.3f, GameSettings.FogFarMul);
            RenderSettings.fogColor = Color.Lerp(new Color(0.36f, 0.33f, 0.36f), new Color(0.035f, 0.055f, 0.105f), k);
            Shader.SetGlobalColor("_GrassTintMul", Color.Lerp(new Color(0.42f, 0.34f, 0.22f), Color.white, k));
            if (k >= 0.5f && nightSkybox != null) RenderSettings.skybox = nightSkybox;
            else if (k < 0.5f && duskSkybox != null) RenderSettings.skybox = duskSkybox;
        }

        // Aplica las distancias del modo actual multiplicadas por los settings del
        // jugador. Lo llama SetDay() y también GameSettings.Apply() (al cambiar opciones).
        public void ApplyGraphics()
        {
            if (_phase == Phase.Day)
            {
                RenderSettings.fogStartDistance = 30f * GameSettings.FogNearMul;
                RenderSettings.fogEndDistance   = 115f * GameSettings.FogFarMul;
                float grassDist = 50f * GameSettings.GrassDistanceMul;
                if (terrain != null)
                {
                    terrain.detailObjectDistance = grassDist;
                    terrain.treeDistance         = 105f * GameSettings.TreeDistanceMul;
                    // full 3D mesh only within ~35m; cheap billboards beyond (fog hides
                    // the swap). Overrides the generation-time value that was disabled
                    // by the UseLowPolyTrees flag even though BOTD trees have billboards.
                    terrain.treeBillboardDistance = 35f * GameSettings.TreeBillboardMul;
                    terrain.detailObjectDensity  = 0.20f * GameSettings.GrassDensityMul;
                    terrain.Flush(); // reconstruye pasto/árboles YA (no de a poco al cambiar preset)
                }
                SetGameplayFarClip(140f * GameSettings.ViewDistanceMul);
                Shader.SetGlobalFloat("_GrassFadeEnd", grassDist);
                Shader.SetGlobalFloat("_GrassFadeStart", Mathf.Max(0f, grassDist - 4f));
            }
            else
            {
                // Atardecer y noche comparten niebla exp². El atardecer va a mitad de
                // camino: menos densa y más distancia de vista que la noche cerrada
                // (si no, en el atardecer no se ven ni las montañas del skybox).
                bool dusk = _phase == Phase.Dusk;

                // niebla: exp². "Más lejos" = menos densidad. owner: "aumentá la vista de día" ->
                // la TARDE tiene menos niebla y MÁS distancia de vista que antes.
                RenderSettings.fogDensity = (dusk ? 0.009f : 0.05f) / Mathf.Max(0.3f, GameSettings.FogFarMul);
                float grassDist = (dusk ? 48f : 15f) * GameSettings.GrassDistanceMul;
                if (terrain != null)
                {
                    terrain.detailObjectDistance = grassDist;
                    terrain.treeDistance         = (dusk ? 150f : 55f) * GameSettings.TreeDistanceMul;
                    // night: dense fog + short flashlight, billboards even closer (~22m)
                    terrain.treeBillboardDistance = (dusk ? 45f : 22f) * GameSettings.TreeBillboardMul;
                    terrain.detailObjectDensity  = (dusk ? 0.24f : 0.28f) * GameSettings.GrassDensityMul;
                    terrain.Flush(); // reconstruye pasto/árboles YA (no de a poco al cambiar preset)
                }
                SetGameplayFarClip((dusk ? 220f : 85f) * GameSettings.ViewDistanceMul);
                Shader.SetGlobalFloat("_GrassFadeEnd", grassDist);
                Shader.SetGlobalFloat("_GrassFadeStart", Mathf.Max(0f, grassDist - 4f));
            }
        }

        // owner: "el perro tiene menos distancia de visión que el humano" -- antes el far
        // clip se aplicaba a UNA sola cámara (GetComponentInChildren). Ahora se aplica a
        // las DOS cámaras de juego (persona y perro) para que vean igual de lejos. La
        // cámara de FONDO (BackdropCamera, far=9000 para el anillo de montañas) queda
        // intacta.
        void SetGameplayFarClip(float far)
        {
            var party = Object.FindFirstObjectByType<PartyController>(FindObjectsInactive.Include);
            if (party != null)
            {
                if (party.personCam != null) party.personCam.farClipPlane = far;
                if (party.dogCam != null) party.dogCam.farClipPlane = far;
                return;
            }
            var cam = GetComponentInChildren<Camera>(); // fallback: comportamiento viejo
            if (cam != null) cam.farClipPlane = far;
        }
    }
}
