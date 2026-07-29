// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  NetworkBuilder.cs — infraestructura de red en la escena:
//   - root "NET" (persiste entre regenerados)
//   - NetworkManager + UnityTransport + NetworkBootstrap (UI/código)
//   - NetGameSpawner (spawnea persona/perro según la elección)
//   - prefabs de PERSONA y PERRO en red (owner-aware)
//  Idempotente: se puede llamar en cada Generate.
// ============================================================
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using WASDSound;

namespace FolkloreArchives.MapGen
{
    public static class NetworkBuilder
    {
        const string PersonPrefabPath = "Assets/_FolkloreArchives/Generated/NetPerson.prefab";

        // owner: "sonidos... pisadas" -- mismo pack/criterio que TestPlayerBuilder.
        const string FootstepBundlePath = "Assets/ExternalAssets/WASDFootstepSFX/Assets/Free Bundle.asset";
        static void AddFootsteps(GameObject go)
        {
            var mgr = AssetDatabase.LoadAssetAtPath<WASDFootstepManager>(FootstepBundlePath);
            if (mgr == null) { Debug.LogWarning("NetworkBuilder: no encontré " + FootstepBundlePath + " -- ¿falta importar el pack WASD?"); return; }
            var src = go.AddComponent<WASDFootstepSource>();
            var so = new SerializedObject(src);
            so.FindProperty("footsteps").objectReferenceValue = mgr;
            so.FindProperty("volume").floatValue = 0.5f; // owner: "bajalos un poco" -- default del pack era 0.8
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        const string DogPrefabPath    = "Assets/_FolkloreArchives/Generated/NetDog.prefab";
        const string DogGlb           = "Assets/ExternalAssets/Dog/PS1_Dog.glb";

        public static void EnsureNet()
        {
            // owner: "el perro no deberia verse a si mismo" -- asegura que existan los
            // layers que PlayerVehicleInteractor usa para excluir el propio modelo de
            // su propia cámara (ver LayerSetup.cs). Uno para la persona y OTRO aparte
            // para el perro -- si compartieran el mismo, cuando los dos están sentados
            // a la vez cada uno terminaría ocultando también al otro.
            LayerSetup.EnsureLayer(LayerSetup.SelfHiddenLayer);
            LayerSetup.EnsureLayer(LayerSetup.SelfHiddenLayerDog);

            var net = GameObject.Find("NET");
            if (net == null) net = new GameObject("NET");

            if (net.GetComponent<FolkloreArchives.Net.NetworkBootstrap>() == null)
                net.AddComponent<FolkloreArchives.Net.NetworkBootstrap>();

            // Menú de opciones (Esc): acá en NET (siempre activo) para que funcione
            // tanto en single-player como en online (donde el player se desactiva).
            if (net.GetComponent<FolkloreArchives.SettingsMenu>() == null)
                net.AddComponent<FolkloreArchives.SettingsMenu>();

            var nm = net.GetComponent<NetworkManager>();
            if (nm == null) nm = net.AddComponent<NetworkManager>();
            var utp = net.GetComponent<UnityTransport>();
            if (utp == null) utp = net.AddComponent<UnityTransport>();

            if (nm.NetworkConfig == null) nm.NetworkConfig = new NetworkConfig();
            nm.LogLevel = LogLevel.Error;   // menos spam de NGO en la consola
            nm.NetworkConfig.NetworkTransport = utp;
            nm.NetworkConfig.PlayerPrefab = null;          // spawn manual por elección
            nm.NetworkConfig.ConnectionApproval = true;    // cada cliente manda su elección

            var person = BuildPersonPrefab();
            var dog = BuildDogPrefab();

            var spawner = net.GetComponent<FolkloreArchives.Net.NetGameSpawner>();
            if (spawner == null) spawner = net.AddComponent<FolkloreArchives.Net.NetGameSpawner>();
            spawner.personPrefab = person;
            spawner.dogPrefab = dog;

            EditorUtility.SetDirty(nm);
            EditorUtility.SetDirty(spawner);
        }

        // ── PERSONA en red: rig 1ª persona (cámara + linterna + MapExplorer) ──
        static GameObject BuildPersonPrefab()
        {
            var root = new GameObject("NetPerson");
            root.AddComponent<NetworkObject>();
            root.AddComponent<FolkloreArchives.Net.OwnerNetworkTransform>();
            root.AddComponent<FolkloreArchives.Net.NetOwnerGate>();
            var cc = root.AddComponent<CharacterController>();
            cc.height = 2.4f; cc.radius = 0.35f; cc.center = new Vector3(0f, 1.2f, 0f);
            var explorer = root.AddComponent<FolkloreArchives.MapExplorer>();
            explorer.enabled = false; // el gate lo prende para el dueño
            // owner: "no aumento la velocidad" -- flySpeed es un campo público; un
            // GameObject YA generado en una escena anterior se queda con el valor
            // viejo aunque cambie el default en el código (mismo bug que
            // CarAutoDrive.cruiseSpeedKmh, ver DEV_LOG) -- asignado EXPLÍCITO acá para
            // que quede claro que este número se hornea y necesita Regenerar.
            explorer.flySpeed = 30f;
            AddFootsteps(root);
            // owner: "no me deja interactuar con las cosas... abrir puertas ni las
            // opciones me salen ni nada" -- faltaba este componente entero en el
            // personaje de red (subir/bajar del auto, abrir/cerrar puertas, y el texto
            // "[E] ..." en pantalla salen todos de acá).
            var interactor = root.AddComponent<FolkloreArchives.PlayerVehicleInteractor>();
            interactor.enabled = false; // el gate lo prende para el dueño

            // cuerpo = modelo humano PSX (lo que ve el compañero). Si el FBX no está,
            // cae a una cápsula.
            BuildPersonVisual(root.transform);
            root.AddComponent<FolkloreArchives.NetCrouchSync>();  // replica el agachado al compañero
            root.AddComponent<FolkloreArchives.HumanWalkAnim>();  // brazos/piernas al caminar

            var camGO = new GameObject("Camera");
            camGO.transform.SetParent(root.transform);
            camGO.transform.localPosition = new Vector3(0f, 2.3f, 0f);
            var cam = camGO.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.farClipPlane = MapLayout.CameraFarClip;
            camGO.AddComponent<AudioListener>();
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camData.antialiasingQuality = AntialiasingQuality.High;
            camGO.AddComponent<FolkloreArchives.VhsPostFx>();
            camGO.AddComponent<FolkloreArchives.Crosshair>();  // owner: "necesito el crosshair en ambos pjs"

            var flashGO = new GameObject("Flashlight");
            flashGO.transform.SetParent(camGO.transform);
            flashGO.transform.localPosition = new Vector3(0.25f, -0.2f, 0.1f);
            var fl = flashGO.AddComponent<Light>();
            fl.type = LightType.Spot; fl.range = MapLayout.FlashlightRange;
            fl.spotAngle = MapLayout.FlashlightSpotAngle; fl.intensity = 28f;
            fl.color = new Color(1f, 0.78f, 0.38f); fl.shadows = LightShadows.None;

            camGO.SetActive(false); // gate lo prende para el dueño

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PersonPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        const string CharFbx = "Assets/ExternalAssets/Player/SimpleCharacterPSX.fbx";
        const string CharTex = "Assets/ExternalAssets/Player/character_256.png";

        // Instancia el modelo humano PSX, lo escala a ~2.3 m, le apoya los pies en y=0
        // y le pone la textura 256 con filtro Point (look PSX). Fallback: cápsula.
        public static void BuildPersonVisual(Transform parent)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CharFbx);
            if (fbx == null)
            {
                Debug.LogWarning("NetPerson: no encontré " + CharFbx + " — uso cápsula.");
                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Body"; body.transform.SetParent(parent);
                body.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                body.transform.localScale = new Vector3(0.7f, 1.2f, 0.7f);
                Object.DestroyImmediate(body.GetComponent<Collider>());
                return;
            }

            var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            model.name = "Model";
            model.transform.SetParent(parent);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            const float target = 2.3f; // alto ≈ jugador (CC = 2.4)
            var rends = model.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                float h = Mathf.Max(0.0001f, b.size.y);
                model.transform.localScale = Vector3.one * (target / h);
                Bounds b2 = model.GetComponentInChildren<Renderer>().bounds;
                foreach (var r in model.GetComponentsInChildren<Renderer>()) b2.Encapsulate(r.bounds);
                // -0.1: planta los pies. El origen del jugador (fondo del CharacterController)
                // queda un poco por encima del suelo real (skinWidth + margen), así que sin
                // este ajuste el modelo flota. Baja el modelo para que las suelas toquen.
                model.transform.localPosition = new Vector3(0f, -(b2.min.y - parent.position.y) - 0.1f, 0f);

                // material PSX. SIEMPRE creo un material URP (aunque falte la textura),
                // porque si no, las mallas se quedan con los materiales "Standard" del FBX
                // que en URP se ven MAGENTA (shader incompatible).
                var tex = LoadCharTex();
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                if (tex != null && mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                else if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.82f, 0.68f, 0.55f)); // piel de respaldo
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f);
                string matPath = "Assets/Settings/PSX_Character.mat";
                AssetDatabase.DeleteAsset(matPath);
                AssetDatabase.CreateAsset(mat, matPath);
                // pinto TODAS las ranuras de material de cada renderer (no solo la 0),
                // si no las sub-mallas restantes quedan magenta.
                foreach (var r in rends)
                {
                    var arr = new Material[r.sharedMaterials.Length];
                    for (int k = 0; k < arr.Length; k++) arr[k] = mat;
                    r.sharedMaterials = arr;
                }
            }
        }

        static Texture2D LoadCharTex()
        {
            var imp = AssetImporter.GetAtPath(CharTex) as TextureImporter;
            if (imp != null && imp.filterMode != FilterMode.Point)
            {
                imp.filterMode = FilterMode.Point;   // pixelado PSX
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(CharTex);
        }

        // ── PERRO en red: modelo PS1 + DogController + cámara 3ª persona ──
        static GameObject BuildDogPrefab()
        {
            var root = new GameObject("NetDog");
            root.AddComponent<NetworkObject>();
            root.AddComponent<FolkloreArchives.Net.OwnerNetworkTransform>();
            root.AddComponent<FolkloreArchives.Net.NetOwnerGate>();
            var cc = root.AddComponent<CharacterController>();
            cc.height = 1.1f; cc.radius = 0.35f; cc.center = new Vector3(0f, 0.55f, 0f);
            var dogCtrl = root.AddComponent<FolkloreArchives.DogController>();
            dogCtrl.enabled = false;
            dogCtrl.flySpeed = 30f; // ver nota en MapExplorer más arriba -- horneado explícito
            root.AddComponent<FolkloreArchives.DogWalkAnim>(); // patas se mueven al caminar

            // owner: "necesito que el perro pueda subirse pero no abrir las puertas" --
            // se puede sentar (si alguien más ya abrió la puerta) y bajarse, pero no
            // puede abrir/cerrar puertas él mismo.
            var dogInteractor = root.AddComponent<FolkloreArchives.PlayerVehicleInteractor>();
            dogInteractor.canOpenDoors = false;
            dogInteractor.selfHiddenLayerName = LayerSetup.SelfHiddenLayerDog; // layer propio, no compartido con la persona
            dogInteractor.enabled = false; // el gate lo prende para el dueño

            BuildDogVisual(root.transform);

            // 1ª persona: la cámara va en el HOCICO mirando adelante. Como el modelo
            // está girado 180°, su cabeza queda en el frente (+Z); calculo ese borde con
            // los bounds y coloco la cámara justo ahí (si no, veías el cuerpo = 3ª persona).
            float eyeY = 0.9f, noseZ = 0.6f;
            {
                var rends = root.GetComponentsInChildren<Renderer>();
                if (rends.Length > 0)
                {
                    Bounds mb = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++) mb.Encapsulate(rends[i].bounds);
                    eyeY = mb.max.y * 0.62f;      // altura de los ojos ≈ 62% del alto
                    noseZ = mb.max.z + 0.08f;     // justo delante del hocico
                }
            }
            var camGO = new GameObject("Camera");
            camGO.transform.SetParent(root.transform);
            camGO.transform.localPosition = new Vector3(0f, eyeY, noseZ);
            camGO.transform.localRotation = Quaternion.identity;
            var cam = camGO.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.farClipPlane = MapLayout.CameraFarClip;
            camGO.AddComponent<AudioListener>();
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camData.antialiasingQuality = AntialiasingQuality.High;
            camGO.AddComponent<FolkloreArchives.VhsPostFx>();
            camGO.AddComponent<FolkloreArchives.Crosshair>();  // owner: "necesito el crosshair en ambos pjs"
            camGO.SetActive(false);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, DogPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // Carga el glb, lo escala a ~1.4 m y lo gira 180° (mismo criterio que el single-player).
        // owner: "usa el mismo que esta en multiplayer" -- TestPlayerBuilder (modo Solo)
        // llama a ESTE mismo método en vez de tener su propia copia, para que no puedan
        // volver a divergir entre los dos modos.
        public static void BuildDogVisual(Transform parent)
        {
            var glb = AssetDatabase.LoadAssetAtPath<GameObject>(DogGlb);
            if (glb == null) { Debug.LogWarning("NetDog: no encontré " + DogGlb + " — perro sin modelo."); return; }
            const float target = 1.4f;
            var model = (GameObject)PrefabUtility.InstantiatePrefab(glb);
            model.name = "Model";
            model.transform.SetParent(parent);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            model.transform.localScale = Vector3.one;

            // owner: "el perro esta gigante" -- ya se probó Renderer.bounds (caché roto
            // en 0,0,0 confirmado en el Inspector) y Mesh.bounds (¡TAMBIÉN venía en cero
            // para este glb -- típico de mallas importadas por glTF/glTFast sin que nadie
            // llame RecalculateBounds()!). Nada de esto se puede confiar acá: se leen los
            // VÉRTICES CRUDOS de cada malla y se calcula el min/max a mano. Esto no puede
            // estar cacheado en cero bajo ningún escenario porque no usa NINGÚN campo de
            // bounds, solo la posición real de cada vértice.
            Bounds? measured = null;
            void EncapsulatePoint(Vector3 p)
            {
                if (measured == null) measured = new Bounds(p, Vector3.zero);
                else { var m = measured.Value; m.Encapsulate(p); measured = m; }
            }
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.sharedMesh == null) continue;
                foreach (var v in smr.sharedMesh.vertices) EncapsulatePoint(v);
            }
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                foreach (var v in mf.sharedMesh.vertices) EncapsulatePoint(v);
            }

            if (measured.HasValue)
            {
                float h = Mathf.Max(0.0001f, measured.Value.size.y);
                float s = target / h;
                model.transform.localScale = Vector3.one * s;
                // el modelo todavía está en localPosition=0 acá, así que su Y mundial ==
                // la del padre; una rotación en Y no cambia el valor Y de los bounds, así
                // que "measured" (medido a escala 1, sin rotar) sigue siendo válido.
                float bottomLocal = measured.Value.min.y * s;
                model.transform.localPosition = new Vector3(0f, -bottomLocal - 0.06f, 0f); // -0.06: apoya patas sin hundir
                Debug.Log($"[NetDog] alto nativo {h:0.000} → escala {s:0.0000} (objetivo {target}m).");
            }
            else Debug.LogWarning("NetDog: el modelo no tiene mallas para medir — queda a escala 1.");
        }
    }
}
