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
        // XZ del punto de spawn (al lado del auto, sobre la ruta pasando el túnel).
        // Menos offset lateral que antes (2.0 en vez de 3.5) para no salirse del asfalto.
        public static Vector2 SpawnXZ()
        {
            float carX = MapLayout.TunnelEntranceX + 22f;
            return new Vector2(carX, MapLayout.PavedRouteZAt(carX) + 2.0f);
        }

        // Posición de spawn apoyada SOBRE el terreno real (muestreado con SampleHeight)
        // o sobre la ruta si ésta está por encima, con un pequeño margen para que la
        // gravedad lo asiente parado. Así nunca aparece enterrado aunque el terreno
        // haya cambiado de altura desde el último Generate.
        public static Vector3 SpawnPos(Terrain t)
        {
            Vector2 xz = SpawnXZ();
            float terrainY = t != null
                ? t.SampleHeight(new Vector3(xz.x, 0f, xz.y)) + t.transform.position.y
                : MapLayout.RoadSurfaceHeight;
            float groundY = Mathf.Max(MapLayout.RoadSurfaceHeight, terrainY);
            return new Vector3(xz.x, groundY + 0.5f, xz.y);
        }

        // Botón para REUBICAR el spawn sobre el terreno actual SIN regenerar el mapa
        // (así no se borra el galpón ni nada puesto a mano). Corrige TEST_PLAYER (y el
        // perro) muestreando la altura real del terreno. Después: guardar la escena.
        [MenuItem("Tools/Folklore Archives/Reubicar Spawn sobre el terreno")]
        public static void RelocateSpawn()
        {
            var player = GameObject.Find("TEST_PLAYER");
            if (player == null) { Debug.LogError("[Spawn] No encontré TEST_PLAYER en la escena."); return; }
            var t = Terrain.activeTerrain;
            if (t == null) { Debug.LogError("[Spawn] No hay Terrain activo en la escena."); return; }

            Vector3 pos = SpawnPos(t);
            player.transform.position = pos;
            player.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            // re-apoyar el perro al lado, si está
            var dog = GameObject.Find("DOG");
            if (dog != null)
            {
                Vector2 dxz = SpawnXZ() + new Vector2(1.6f, -1.2f);
                float dy = t.SampleHeight(new Vector3(dxz.x, 0f, dxz.y)) + t.transform.position.y;
                dog.transform.position = new Vector3(dxz.x, dy + 0.2f, dxz.y);
            }

            EditorUtility.SetDirty(player);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(player.scene);
            Debug.Log($"<color=lime>[Spawn] Reubicado en {pos} (terreno muestreado). Guardá la escena (Ctrl+S).</color>");
            Selection.activeGameObject = player;
        }

        public static void Build(Transform parent, Terrain t)
        {
            // disable any existing cameras
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                c.gameObject.SetActive(false);

            var player = new GameObject("TEST_PLAYER");
            player.transform.SetParent(parent);

            // spawn AL LADO DEL AUTO (en la ruta, pasando el túnel) para poder probar
            // el manejo sin caminar desde el campamento. Encaja con el arranque del juego.
            // (Para volver a spawnear en el campamento: usar MapLayout.Campsite como antes.)
            // La Y se MUESTREA del terreno (como el perro) — antes usaba una altura fija
            // (RoadSurfaceHeight) y si el terreno estaba más alto ahí, spawneabas enterrado.
            Vector2 spawnXZ = SpawnXZ();
            player.transform.position = SpawnPos(t);
            player.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // mirando hacia el auto

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
            camGO.AddComponent<FolkloreArchives.Crosshair>();  // owner: "un circulito en medio de la camara ... como en el fears"
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
            player.AddComponent<FolkloreArchives.PlayerVehicleInteractor>(); // subir/bajar del auto con E
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

        // Mide una malla leyendo sus VÉRTICES CRUDOS (no Renderer.bounds ni Mesh.bounds:
        // los dos vienen cacheados en cero para el glb del perro -- típico de mallas
        // importadas por glTF/glTFast sin que nadie llame RecalculateBounds()). Esto no
        // puede estar cacheado en cero bajo ningún escenario: solo mira posiciones reales.
        static Bounds? MeasureRawVertexBounds(GameObject root)
        {
            Bounds? measured = null;
            void EncapsulatePoint(Vector3 p)
            {
                if (measured == null) measured = new Bounds(p, Vector3.zero);
                else { var m = measured.Value; m.Encapsulate(p); measured = m; }
            }
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.sharedMesh == null) continue;
                foreach (var v in smr.sharedMesh.vertices) EncapsulatePoint(v);
            }
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                foreach (var v in mf.sharedMesh.vertices) EncapsulatePoint(v);
            }
            return measured;
        }

        // Spawnea el perro (modelo PS1) con CharacterController, su cámara de 3ª persona
        // (apagada al inicio) y el DogController en modo Follow. Cuelga el PartyController
        // en la persona para poder alternar el control con G.
        static void BuildDogAndParty(GameObject player, GameObject personCamGO, Vector2 playerXZ, Terrain t)
        {
            // owner: "usa lo mismo que usabas en el anterior pero con este asset" -- vuelve
            // a ser una función independiente (sin depender de NetworkBuilder), igual que
            // el perro riggeado de antes, solo que apuntando al glb del PS1 Dog.
            const string glbPath = "Assets/ExternalAssets/Dog/PS1_Dog.glb";
            // owner: "en multiplayer esta bien pero en singleplayer no" -- con el mismo
            // código, la única diferencia real es CUÁNDO se toca el asset por primera vez
            // en este Generate (TestPlayerBuilder corre ANTES que NetworkBuilder.EnsureNet
            // en MapGenerator.Generate()). Fuerza un reimport ANTES de cargarlo, por si el
            // primer acceso al glb en la sesión trae datos de malla todavía no resueltos.
            AssetDatabase.ImportAsset(glbPath, ImportAssetOptions.ForceUpdate);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
            if (prefab == null)
            {
                Debug.LogWarning("Perro: no encontré/importé " + glbPath + ". Sigo sin perro.");
                return;
            }

            var dog = new GameObject("DOG");
            dog.transform.SetParent(player.transform.parent); // hermano del jugador, bajo FOLKLORE_MAP
            Vector2 dogXZ = playerXZ + new Vector2(1.6f, -1.2f);            // al lado y un poco atrás
            // owner: "el perro esta con la mitad del cuerpo bajo tierra" -- mismo bug que
            // ya se había arreglado en CarBuilder/SpawnPos: cerca de este punto (lado
            // este, junto al auto) el terreno CRUDO muestreado queda más bajo que la ruta
            // pavimentada real (otra malla encima) -- BuilderUtils.Ground a secas lo
            // hacía nacer medio hundido, y un CharacterController no se "expulsa" solo de
            // un overlap inicial. Pisa el mayor entre terreno real y la ruta, igual que
            // el spawn del jugador.
            Vector3 dogGround = BuilderUtils.Ground(t, dogXZ.x, dogXZ.y);
            dogGround.y = Mathf.Max(MapLayout.RoadSurfaceHeight, dogGround.y);
            dog.transform.position = dogGround + Vector3.up * 0.2f;
            dog.transform.rotation = player.transform.rotation;

            var dcc = dog.AddComponent<CharacterController>();
            dcc.height = 1.1f; dcc.radius = 0.35f; dcc.center = new Vector3(0f, 0.55f, 0f);

            const float DogTargetHeight = 1.4f;
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            model.name = "Model";
            model.transform.SetParent(dog.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // su "adelante" apunta al revés que el controlador
            model.transform.localScale = Vector3.one;

            var measured = MeasureRawVertexBounds(model);
            if (measured.HasValue && measured.Value.size.y > 0.01f)
            {
                float h = measured.Value.size.y;
                float s = DogTargetHeight / h;
                model.transform.localScale = Vector3.one * s;
                // el modelo sigue en localPosition=0 acá, así que su Y mundial == la del
                // perro; una rotación en Y no cambia el valor Y de los bounds.
                // owner: "sigue bajo tierra" -- el cálculo puro (bottomLocal, sin más)
                // seguía dejando las patas metidas; el propio owner lo subió a mano en el
                // editor hasta que se vio bien (Model Position Y quedó en 0.576) -- se usa
                // ese valor calibrado como el "extra" fijo por encima del cálculo, así
                // sobrevive a cualquier regenerado futuro.
                const float ManualLiftCalibrated = 0.567f; // 0.576 (verificado a mano) - ~0.009 (bottomLocal actual)
                float bottomLocal = measured.Value.min.y * s;
                model.transform.localPosition = new Vector3(0f, -bottomLocal + ManualLiftCalibrated, 0f);
                Debug.Log($"Rufus: alto nativo {h:0.000} → escala {s:0.0000} (objetivo {DogTargetHeight} m).");
            }
            else Debug.LogWarning("Rufus: el modelo no tiene mallas para medir — queda a escala 1.");

            var dogCtrl = dog.AddComponent<FolkloreArchives.DogController>();
            dogCtrl.followTarget = player.transform;
            dogCtrl.mode = FolkloreArchives.DogController.Mode.Follow;
            dog.AddComponent<FolkloreArchives.DogWalkAnim>(); // patas se mueven al caminar (el PS1 no trae clips)

            // cámara 1ª persona del perro: en el HOCICO mirando adelante. Como el modelo
            // está girado 180°, la cabeza queda en +Z. "measured" está en espacio CRUDO
            // del mesh (sin el giro de 180° todavía aplicado) -- ese giro invierte el
            // signo de Z (post = -crudo), así que el lado "adelante" (post-giro, +Z) es
            // el que en crudo era -min.z, no max.z.
            float dEyeY = 0.62f * (DogTargetHeight - 0.06f); // ojos ≈ 62% del alto real (s*size.y == DogTargetHeight por construcción)
            float dNoseZ = 0.6f;
            if (measured.HasValue)
            {
                float s = model.transform.localScale.y;
                dNoseZ = (-measured.Value.min.z) * s + 0.08f;   // justo delante del hocico
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

            // owner: "al cambiar con la g al perro no me da la opcion de subirme siendo
            // perro" -- en modo SOLO (sin host) el DOG nunca tuvo este componente, solo
            // se le agregaba al jugador humano (línea de arriba, `player.AddComponent
            // <PlayerVehicleInteractor>`). El prefab de RED (NetworkBuilder.cs) sí lo
            // tiene para el perro (`canOpenDoors = false`) -- mismo criterio acá.
            var dogInteractor = dog.AddComponent<FolkloreArchives.PlayerVehicleInteractor>();
            dogInteractor.canOpenDoors = false;
            // owner: "no veo al perro desde la camara del humano y no veo al humano
            // desde la camara del perro" -- layer propio para el perro, no compartido
            // con la persona (ver PlayerVehicleInteractor.selfHiddenLayerName).
            dogInteractor.selfHiddenLayerName = FolkloreArchives.MapGen.LayerSetup.SelfHiddenLayerDog;

            var party = player.AddComponent<FolkloreArchives.PartyController>();
            party.person    = player.GetComponent<FolkloreArchives.MapExplorer>();
            party.dog       = dogCtrl;
            party.personCam = personCamGO.GetComponent<Camera>();
            party.dogCam    = dogCam;
        }

    }
}
