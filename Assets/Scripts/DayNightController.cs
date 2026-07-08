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
        [Header("Skyboxes (asignados por TestPlayerBuilder)")]
        public Material daySkybox;
        public Material nightSkybox;

        [Header("Referencias de escena")]
        public Light sun;
        public Terrain terrain;

        bool _isDay = false;

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab))
                Toggle();
        }

        void Toggle() => SetDay(!_isDay);

        public void SetDay(bool day)
        {
            _isDay = day;

            if (_isDay)
            {
                if (sun != null)
                {
                    sun.intensity = 1.0f;
                    sun.color     = new Color(1f, 0.92f, 0.72f);
                    sun.shadows   = LightShadows.Hard;
                }
                if (daySkybox != null) RenderSettings.skybox = daySkybox;
                RenderSettings.ambientLight = new Color(0.30f, 0.26f, 0.32f);
                RenderSettings.fogMode  = FogMode.Linear;
                RenderSettings.fogColor = new Color(0.68f, 0.54f, 0.58f);
                Shader.SetGlobalColor("_GrassTintMul", new Color(0.34f, 0.42f, 0.20f)); // verde oscuro/quemado
            }
            else
            {
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
            }

            ApplyGraphics(); // distancias/niebla con los multiplicadores de GameSettings
        }

        // Aplica las distancias del modo actual multiplicadas por los settings del
        // jugador. Lo llama SetDay() y también GameSettings.Apply() (al cambiar opciones).
        public void ApplyGraphics()
        {
            var cam = GetComponentInChildren<Camera>();

            if (_isDay)
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
                // niebla nocturna: exp². "Más lejos" = menos densidad.
                RenderSettings.fogDensity = 0.05f / Mathf.Max(0.3f, GameSettings.FogFarMul);
                float grassDist = 15f * GameSettings.GrassDistanceMul;
                if (terrain != null)
                {
                    terrain.detailObjectDistance = grassDist;
                    terrain.treeDistance         = 55f * GameSettings.TreeDistanceMul;
                    // night: dense fog + short flashlight, billboards even closer (~22m)
                    terrain.treeBillboardDistance = 22f * GameSettings.TreeBillboardMul;
                    terrain.detailObjectDensity  = 0.28f * GameSettings.GrassDensityMul;
                    terrain.Flush(); // reconstruye pasto/árboles YA (no de a poco al cambiar preset)
                }
                if (cam != null) cam.farClipPlane = 85f * GameSettings.ViewDistanceMul;
                Shader.SetGlobalFloat("_GrassFadeEnd", grassDist);
                Shader.SetGlobalFloat("_GrassFadeStart", Mathf.Max(0f, grassDist - 4f));
            }
        }
    }
}
