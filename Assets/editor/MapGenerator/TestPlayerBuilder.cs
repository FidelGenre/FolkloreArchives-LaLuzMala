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

            // Cuerpo REAL (mismo modelo PSX que en online) para tener conciencia del
            // cuerpo: al mirar abajo te ves torso y piernas. Si falta el fbx, cae a cápsula.
            NetworkBuilder.BuildPersonVisual(player.transform);
            player.AddComponent<FolkloreArchives.HumanWalkAnim>(); // brazos/piernas al caminar

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
            // El menú de opciones (Esc) ahora va en el objeto NET (NetworkBuilder), que
            // NO se desactiva en online — así Esc abre el menú también en co-op.

            // conciencia del cuerpo en solo: te ocultás cabeza/cuello y acercás el near-clip
            var bodyView = player.AddComponent<FolkloreArchives.FirstPersonBodyView>();
            bodyView.cam = cam;

            // DayNightController: Tab cicla Día → Atardecer → Noche en Play mode
            var dnc = player.AddComponent<FolkloreArchives.DayNightController>();
            var moonGO = GameObject.Find("Moon");
            dnc.sun     = moonGO != null ? moonGO.GetComponent<Light>() : null;
            dnc.terrain = t;
            dnc.daySkybox   = EnvironmentBuilder.DaySkybox();   // AllSky si está, si no procedural
            dnc.duskSkybox  = EnvironmentBuilder.DuskSkybox();  // atardecer (Deep Dusk)
            dnc.nightSkybox = EnvironmentBuilder.NightSkybox();

            // ── PERRO + party (modo Solo: persona 1ª persona, perro te sigue; G alterna) ──
            BuildDogAndParty(player, camGO, spawnXZ, t);
        }

        // Spawnea el perro (modelo PS1 real) con CharacterController, su cámara de 3ª
        // persona (apagada al inicio) y el DogController en modo Follow. Cuelga el
        // PartyController en la persona para poder alternar el control con G.
        static void BuildDogAndParty(GameObject player, GameObject personCamGO, Vector2 playerXZ, Terrain t)
        {
            const string glbPath = "Assets/ExternalAssets/Dog/PS1_Dog.glb";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
            if (prefab == null)
            {
                Debug.LogWarning("Perro: no encontré/importé " + glbPath +
                    " (¿instalaste glTFast y le diste foco a Unity para importar?). Sigo sin perro.");
                return;
            }

            // raíz del perro con su collider/controller
            var dog = new GameObject("DOG");
            dog.transform.SetParent(player.transform.parent); // hermano del jugador, bajo FOLKLORE_MAP
            Vector2 dogXZ = playerXZ + new Vector2(1.6f, -1.2f);            // al lado y un poco atrás
            dog.transform.position = BuilderUtils.Ground(t, dogXZ.x, dogXZ.y) + Vector3.up * 0.2f;
            dog.transform.rotation = player.transform.rotation;

            var dcc = dog.AddComponent<CharacterController>();
            dcc.height = 1.1f; dcc.radius = 0.35f; dcc.center = new Vector3(0f, 0.55f, 0f);

            // modelo visual (glTFast). El PS1 Dog viene ENORME a escala 1 (glb en otra
            // unidad), así que NO uso un número fijo: mido su altura real y lo escalo a
            // DogTargetHeight. Gira 180° en Y porque su "adelante" apunta al revés que el
            // controlador (por eso se veía de espaldas al seguir).
            const float DogTargetHeight = 1.4f;   // alto al lomo, en metros (owner: el doble)
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            model.name = "Model";
            model.transform.SetParent(dog.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            model.transform.localScale = Vector3.one;

            var rends = model.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                float h = Mathf.Max(0.0001f, b.size.y);
                float s = DogTargetHeight / h;                       // factor para llegar al alto objetivo
                model.transform.localScale = Vector3.one * s;
                // reapoyar las patas en y=0 (según dónde quedó la base tras escalar)
                Bounds b2 = model.GetComponentInChildren<Renderer>().bounds;
                var all = model.GetComponentsInChildren<Renderer>();
                for (int i = 0; i < all.Length; i++) b2.Encapsulate(all[i].bounds);
                float bottomLocal = b2.min.y - dog.transform.position.y;
                model.transform.localPosition = new Vector3(0f, -bottomLocal - 0.06f, 0f); // -0.06: apoya patas sin hundir
                Debug.Log($"Rufus: alto nativo {h:0.00} → escala {s:0.000} (objetivo {DogTargetHeight} m).");
            }
            else Debug.LogWarning("Rufus: el modelo no tiene Renderers para medir — queda a escala 1.");

            var dogCtrl = dog.AddComponent<FolkloreArchives.DogController>();
            dogCtrl.followTarget = player.transform;
            dogCtrl.mode = FolkloreArchives.DogController.Mode.Follow;
            dog.AddComponent<FolkloreArchives.DogWalkAnim>(); // patas se mueven al caminar

            // cámara 1ª persona del perro: en el HOCICO mirando adelante (igual que
            // online). Como el modelo está girado 180°, la cabeza queda en +Z. Calculo
            // ese borde con los bounds, pero con el perro en origen+identidad para que
            // los bounds (que son en mundo) coincidan con el espacio LOCAL del perro.
            float dEyeY = 0.9f, dNoseZ = 0.6f;
            {
                var savedPos = dog.transform.position; var savedRot = dog.transform.rotation;
                dog.transform.position = Vector3.zero; dog.transform.rotation = Quaternion.identity;
                var drends = model.GetComponentsInChildren<Renderer>();
                if (drends.Length > 0)
                {
                    Bounds mb = drends[0].bounds;
                    for (int i = 1; i < drends.Length; i++) mb.Encapsulate(drends[i].bounds);
                    dEyeY = mb.max.y * 0.62f;     // ojos ≈ 62% del alto
                    dNoseZ = mb.max.z + 0.08f;    // justo delante del hocico
                }
                dog.transform.position = savedPos; dog.transform.rotation = savedRot;
            }
            var dogCamGO = new GameObject("DogCamera");
            dogCamGO.transform.SetParent(dog.transform);
            dogCamGO.transform.localPosition = new Vector3(0f, dEyeY, dNoseZ);
            dogCamGO.transform.localRotation = Quaternion.identity;
            var dogCam = dogCamGO.AddComponent<Camera>();
            dogCam.tag = "MainCamera";
            dogCam.clearFlags = CameraClearFlags.Skybox;
            dogCam.farClipPlane = MapLayout.CameraFarClip;
            dogCamGO.AddComponent<AudioListener>();
            var dogCamData = dogCam.GetUniversalAdditionalCameraData();
            dogCamData.renderPostProcessing = true;
            dogCamData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            dogCamData.antialiasingQuality = AntialiasingQuality.High;
            dogCamGO.AddComponent<FolkloreArchives.VhsPostFx>();
            dogCamGO.SetActive(false); // arranca controlando la persona

            var party = player.AddComponent<FolkloreArchives.PartyController>();
            party.person    = player.GetComponent<FolkloreArchives.MapExplorer>();
            party.dog       = dogCtrl;
            party.personCam = personCamGO.GetComponent<Camera>();
            party.dogCam    = dogCam;
        }
    }
}
