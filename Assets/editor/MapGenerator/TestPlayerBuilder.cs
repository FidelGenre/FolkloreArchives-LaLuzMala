// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  TestPlayerBuilder.cs — spawns a first-person test player with
//  a flashlight at the campsite so you can walk the map on Play.
//  Paste into:  Assets/Editor/MapGenerator/TestPlayerBuilder.cs
// ============================================================
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FolkloreArchives.MapGen
{
    public static class TestPlayerBuilder
    {
        public static void Build(Transform parent, Terrain t)
        {
            // disable any existing cameras
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                c.gameObject.SetActive(false);

            var player = new GameObject("TEST_PLAYER");
            player.transform.SetParent(parent);

            // spawn ON the dirt road a little back from the campsite, FACING down the
            // road, so the two-track wheel ruts are right in front of the player
            // instead of behind them (that's why the road was "never visible").
            Vector2 roadEnd = MapLayout.Campsite;
            Vector2 roadPrev = MapLayout.DirtRoad[MapLayout.DirtRoad.Length - 2];
            Vector2 dir = (roadPrev - roadEnd).normalized;
            Vector2 spawnXZ = roadEnd + dir * 12f;
            player.transform.position = BuilderUtils.Ground(t, spawnXZ.x, spawnXZ.y) + Vector3.up * 0.2f;
            player.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg, 0f);

            var cc = player.AddComponent<CharacterController>();
            cc.height = 2.4f;                        // jugador de 2.40 m
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 1.2f, 0f);   // centro = altura/2 (pies en el suelo)

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(player.transform);
            body.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            body.transform.localScale = new Vector3(0.7f, 1.2f, 0.7f); // capsule primitive = 2u alto → 2.4 m
            Object.DestroyImmediate(body.GetComponent<Collider>());

            var camGO = new GameObject("Camera");
            camGO.transform.SetParent(player.transform);
            camGO.transform.localPosition = new Vector3(0f, 2.30f, 0f); // ojos a 2.30
            var cam = camGO.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.Skybox; // dusk sky so trees silhouette against it
            cam.backgroundColor = new Color(0.05f, 0.08f, 0.15f); // dusk-blue fallback
            cam.farClipPlane = MapLayout.CameraFarClip;
            camGO.AddComponent<AudioListener>();

            // enable URP post-processing on this camera so the VHS grade/chromatic-
            // aberration/grain/lens effects actually render
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            // SMAA: limpia los bordes dentados (árboles/pasto/render scale 0.8) sin el
            // smearing de TAA. Buena relación calidad/costo.
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camData.antialiasingQuality = AntialiasingQuality.High;

            camGO.AddComponent<FolkloreArchives.VhsPostFx>();  // FtF-style filmic grade: grain + bloom + subtle CA + vignette
            // no scanlines - Fears to Fathom is a clean sharp image, not a scanline CRT look
            // (La cámara de fondo para montañas-sobre-el-cielo se quitó: en URP rompía el
            //  skybox/día-noche. El fondo de montañas correcto es un SKYBOX con montañas.)

            // Flashlight: warm YELLOW beam (Fears-to-Fathom lantern look). More
            // saturated than before because the VHS grade (desaturate + teal shadows)
            // was washing the yellow out toward white.
            var flashGO = new GameObject("Flashlight");
            flashGO.transform.SetParent(camGO.transform);
            flashGO.transform.localPosition = new Vector3(0.25f, -0.2f, 0.1f);
            var flashlight = flashGO.AddComponent<Light>();
            flashlight.type = LightType.Spot;
            flashlight.range = MapLayout.FlashlightRange;
            flashlight.spotAngle = MapLayout.FlashlightSpotAngle;
            flashlight.intensity = 28f;
            flashlight.color = new Color(1f, 0.78f, 0.38f); // warm amber-yellow
            flashlight.shadows = LightShadows.None; // shadows from this were a real cost too; skip them

            player.AddComponent<FolkloreArchives.MapExplorer>();
            player.AddComponent<FolkloreArchives.SettingsMenu>(); // menú de opciones (Esc): motion blur, AA, render scale

            // DayNightController: toggle Tab en Play mode
            var dnc = player.AddComponent<FolkloreArchives.DayNightController>();
            var moonGO = GameObject.Find("Moon");
            dnc.sun     = moonGO != null ? moonGO.GetComponent<Light>() : null;
            dnc.terrain = t;
            dnc.daySkybox   = EnvironmentBuilder.DaySkybox();   // AllSky si está, si no procedural
            dnc.nightSkybox = EnvironmentBuilder.NightSkybox();
        }
    }
}
