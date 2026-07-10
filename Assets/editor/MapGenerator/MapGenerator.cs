// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  MapGenerator.cs — entry point. Adds the menu:
//  Tools > Folklore Archives > Generate Greybox Map
//  Paste into:  Assets/Editor/MapGenerator/MapGenerator.cs
// ============================================================
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FolkloreArchives.MapGen
{
    // Dibuja un botón fijo en el corner superior-derecho del Scene View para
    // cambiar entre día y noche sin ir al menú. Se registra automáticamente al
    // abrir Unity (InitializeOnLoad).
    [InitializeOnLoad]
    public static class DayNightSceneButton
    {
        static bool _isDay = false;

        static DayNightSceneButton()
        {
            SceneView.duringSceneGui += Draw;
        }

        static void Draw(SceneView sv)
        {
            // Solo mostrar si el mapa está generado
            if (GameObject.Find(MapLayout.RootName) == null) return;

            Handles.BeginGUI();
            float w = 140f, h = 28f, margin = 8f;
            var rect = new Rect(sv.position.width - w - margin, margin, w, h);

            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };

            string label = _isDay ? "☽  Pasar a Noche" : "☀  Pasar a Día";
            if (GUI.Button(rect, label, style))
            {
                MapGenerator.ToggleDayNight();
                _isDay = !_isDay;
            }

            // Second button below: fog on/off (mirrors the day/night one)
            var fogRect = new Rect(sv.position.width - w - margin, margin + h + 4f, w, h);
            string fogLabel = RenderSettings.fog ? "🌫  Niebla: ON" : "🌫  Niebla: OFF";
            if (GUI.Button(fogRect, fogLabel, style))
                MapGenerator.ToggleFog();

            Handles.EndGUI();
        }
    }

    public static class MapGenerator
    {
        [MenuItem("Tools/Folklore Archives/Generate Greybox Map")]
        public static void Generate()
        {
            Random.InitState(MapLayout.Seed);
            BuilderUtils.EnsureFolders();
            DeleteMap();

            var root = new GameObject(MapLayout.RootName);

            // Hornea los 3 skybox (día/atardecer/noche) con las montañas ANTES de que
            // EnvironmentBuilder los busque. Antes era un menú aparte y era un pie de
            // fuego: si no lo corrías, el mapa se generaba con los .mat viejos y no
            // había ninguna señal de que estabas viendo un cielo de hace dos versiones.
            SkyboxMountainBaker.Bake();

            Terrain terrain = TerrainBuilder.Build(root.transform);
            EnvironmentBuilder.Build(root.transform);
            EnvironmentBuilder.BuildDaySky(); // pre-genera mat_daysky.mat para el DayNightController
            ForestBuilder.Build(root.transform, terrain);
            RoadsideBuilder.Build(root.transform, terrain); // guardrail + lake on the road's south side
            BridgeBuilder.Build(root.transform, terrain);   // steel-girder bridge over the water crossing
            TunnelBuilder.Build(root.transform, terrain);   // west-end drivable tunnel (game start)
            LandmarkBuilder.Build(root.transform, terrain);
            // Montañas de fondo: desactivadas por ahora. El método de "cámara de fondo"
            // rompía el skybox/día-noche en URP. El camino correcto es un SKYBOX con
            // montañas (mantiene cielo + montañas, funciona con niebla, sin 2ª cámara).
            // SilhouetteMountainBuilder.Build(root.transform);
            // MountainRingBuilder.Build(root.transform, terrain); // montañas 3D reales HQP
            HouseBuilder.Build(root.transform, terrain);     // casa de la vieja (OldLadyRanch) — Fase 1: cáscara + valla
            StoryTriggerBuilder.Build(root.transform, terrain);
            TestPlayerBuilder.Build(root.transform, terrain);

            // Red (co-op): NET con NetworkManager + transporte + panel de conexión +
            // prefab de jugador de prueba. Idempotente; persiste entre regenerados.
            NetworkBuilder.EnsureNet();

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene()); // salva el .unity para que el Build incluya el mapa
            Selection.activeGameObject = root;
            Debug.Log("<color=lime>LA LUZ MALA map generated. Press Play: WASD + mouse, Shift = run, F = flashlight.</color>");
        }

        [MenuItem("Tools/Folklore Archives/Delete Map")]
        public static void DeleteMap()
        {
            var old = GameObject.Find(MapLayout.RootName);
            if (old != null) Object.DestroyImmediate(old);
        }

        // Switches between DAY (to inspect the map with full light, no fog)
        // and NIGHT (the real game mood). Day is only a preview tool.
        // Shortcut: Ctrl+Shift+D
        [MenuItem("Tools/Folklore Archives/Toggle Day-Night Preview %#d")]
        public static void ToggleDayNight()
        {
            var moon = GameObject.Find("Moon");
            if (moon == null)
            {
                Debug.LogWarning("Generate the map first (Tools > Folklore Archives > Generate Greybox Map).");
                return;
            }
            var light = moon.GetComponent<Light>();
            bool toDay = light.intensity < 0.8f;
            var dnc = Object.FindFirstObjectByType<FolkloreArchives.DayNightController>();
            if (toDay)
            {
                light.intensity = 1.0f;  // un poco menos de sol directo
                light.color = new Color(1f, 0.92f, 0.72f);
                light.shadows = LightShadows.Soft;
                RenderSettings.skybox = EnvironmentBuilder.DaySkybox(); // AllSky si está
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.30f, 0.26f, 0.32f); // más oscuro — menos sobreexpuesto
                RenderSettings.fog              = true;
                RenderSettings.fogMode          = FogMode.Linear;
                RenderSettings.fogStartDistance = MapLayout.DayFogStart;
                RenderSettings.fogEndDistance   = MapLayout.DayFogEnd;
                RenderSettings.fogColor         = MapLayout.DayFogColor;
                var t = Terrain.activeTerrain;
                if (t != null) { t.detailObjectDistance = MapLayout.DayDetailRenderDistance; t.treeDistance = MapLayout.DayTreeRenderDistance; t.detailObjectDensity = 0.20f; }
                ForestBuilder.SetGrassFadeGlobals(MapLayout.DayDetailRenderDistance);
                Shader.SetGlobalColor("_GrassTintMul", MapLayout.GrassDayTint);
                var cam = Camera.main;
                if (cam != null) cam.farClipPlane = MapLayout.DayCameraFarClip;
                if (dnc != null) dnc.SetDay(true);
                Debug.Log("<color=yellow>DAY. Toggle again to restore night.</color>");
            }
            else
            {
                light.intensity = MapLayout.MoonIntensity;
                light.color = new Color(0.42f, 0.52f, 0.78f);
                light.shadows = LightShadows.Hard;
                RenderSettings.skybox = EnvironmentBuilder.NightSkybox(); // AllSky si está
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.016f, 0.026f, 0.052f);
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogDensity = MapLayout.FogDensity;
                RenderSettings.fogColor = new Color(0.035f, 0.055f, 0.105f);
                var t = Terrain.activeTerrain;
                if (t != null) { t.detailObjectDistance = MapLayout.DetailRenderDistance; t.treeDistance = MapLayout.TreeRenderDistance; t.detailObjectDensity = MapLayout.DetailDensity; }
                ForestBuilder.SetGrassFadeGlobals(MapLayout.DetailRenderDistance);
                Shader.SetGlobalColor("_GrassTintMul", Color.white); // noche: sin cambio
                var cam = Camera.main;
                if (cam != null) cam.farClipPlane = MapLayout.CameraFarClip;
                if (dnc != null) dnc.SetDay(false);
                Debug.Log("<color=cyan>NIGHT restored.</color>");
            }
        }

        // Toggles scene fog on/off, for inspecting the map without the murk
        // (mirrors the day/night preview). Only flips RenderSettings.fog; the
        // density/color/mode are left as the current day or night preset, so
        // turning fog back ON restores whatever mood was active.
        // Shortcut: Ctrl+Shift+F
        [MenuItem("Tools/Folklore Archives/Toggle Fog %#f")]
        public static void ToggleFog()
        {
            RenderSettings.fog = !RenderSettings.fog;
            Debug.Log(RenderSettings.fog
                ? "<color=cyan>Fog ON.</color>"
                : "<color=yellow>Fog OFF — toggle again to restore.</color>");
            SceneView.RepaintAll();
        }
    }
}
