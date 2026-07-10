// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  DayNightController.cs — togglea día/noche en runtime (Tab).
//  SetDay() maneja el "modo" (sol, cielo, color, tinte del pasto);
//  ApplyGraphics() aplica las DISTANCIAS (niebla, pasto, árboles,
//  cámara) partiendo de los valores base por modo y multiplicándolos
//  por los settings gráficos del jugador (GameSettings).
// ============================================================
using UnityEngine;

namespace FolkloreArchives
{
    public class DayNightController : MonoBehaviour
    {
        // Tres momentos del día. Tab cicla Día → Atardecer → Noche → Día.
        public enum Phase { Day, Dusk, Night }

        [Header("Skyboxes (asignados por TestPlayerBuilder)")]
        public Material daySkybox;
        public Material duskSkybox;
        public Material nightSkybox;

        [Header("Referencias de escena")]
        public Light sun;
        public Terrain terrain;

        Phase _phase = Phase.Day;   // el juego arranca de día

        // Compatibilidad: el resto del código (MapGenerator) sigue pensando en día/noche.
        public bool IsDay => _phase == Phase.Day;

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

        void Toggle() => SetPhase((Phase)(((int)_phase + 1) % 3));

        // Wrapper viejo: SetDay(true) = día, SetDay(false) = noche.
        public void SetDay(bool day) => SetPhase(day ? Phase.Day : Phase.Night);

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

        // Aplica las distancias del modo actual multiplicadas por los settings del
        // jugador. Lo llama SetDay() y también GameSettings.Apply() (al cambiar opciones).
        public void ApplyGraphics()
        {
            var cam = GetComponentInChildren<Camera>();

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
                if (cam != null) cam.farClipPlane = 140f * GameSettings.ViewDistanceMul;
                Shader.SetGlobalFloat("_GrassFadeEnd", grassDist);
                Shader.SetGlobalFloat("_GrassFadeStart", Mathf.Max(0f, grassDist - 4f));
            }
            else
            {
                // Atardecer y noche comparten niebla exp². El atardecer va a mitad de
                // camino: menos densa y más distancia de vista que la noche cerrada
                // (si no, en el atardecer no se ven ni las montañas del skybox).
                bool dusk = _phase == Phase.Dusk;

                // niebla: exp². "Más lejos" = menos densidad.
                RenderSettings.fogDensity = (dusk ? 0.018f : 0.05f) / Mathf.Max(0.3f, GameSettings.FogFarMul);
                float grassDist = (dusk ? 32f : 15f) * GameSettings.GrassDistanceMul;
                if (terrain != null)
                {
                    terrain.detailObjectDistance = grassDist;
                    terrain.treeDistance         = (dusk ? 85f : 55f) * GameSettings.TreeDistanceMul;
                    // night: dense fog + short flashlight, billboards even closer (~22m)
                    terrain.treeBillboardDistance = (dusk ? 30f : 22f) * GameSettings.TreeBillboardMul;
                    terrain.detailObjectDensity  = (dusk ? 0.24f : 0.28f) * GameSettings.GrassDensityMul;
                    terrain.Flush(); // reconstruye pasto/árboles YA (no de a poco al cambiar preset)
                }
                if (cam != null) cam.farClipPlane = (dusk ? 120f : 85f) * GameSettings.ViewDistanceMul;
                Shader.SetGlobalFloat("_GrassFadeEnd", grassDist);
                Shader.SetGlobalFloat("_GrassFadeStart", Mathf.Max(0f, grassDist - 4f));
            }
        }
    }
}
