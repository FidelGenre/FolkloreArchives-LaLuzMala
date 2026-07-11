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

namespace FolkloreArchives.MapGen
{
    public static class NetworkBuilder
    {
        const string PersonPrefabPath = "Assets/_FolkloreArchives/Generated/NetPerson.prefab";
        const string DogPrefabPath    = "Assets/_FolkloreArchives/Generated/NetDog.prefab";
        const string DogGlb           = "Assets/ExternalAssets/Dog/PS1_Dog.glb";

        public static void EnsureNet()
        {
            var net = GameObject.Find("NET");
            if (net == null) net = new GameObject("NET");

            if (net.GetComponent<FolkloreArchives.Net.NetworkBootstrap>() == null)
                net.AddComponent<FolkloreArchives.Net.NetworkBootstrap>();

            var nm = net.GetComponent<NetworkManager>();
            if (nm == null) nm = net.AddComponent<NetworkManager>();
            var utp = net.GetComponent<UnityTransport>();
            if (utp == null) utp = net.AddComponent<UnityTransport>();

            if (nm.NetworkConfig == null) nm.NetworkConfig = new NetworkConfig();
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

            // cuerpo = modelo humano PSX (lo que ve el compañero). Si el FBX no está,
            // cae a una cápsula.
            BuildPersonVisual(root.transform);
            root.AddComponent<FolkloreArchives.HumanWalkAnim>(); // brazos/piernas al caminar

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
        static void BuildPersonVisual(Transform parent)
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
                model.transform.localPosition = new Vector3(0f, -(b2.min.y - parent.position.y), 0f);

                // material PSX con la textura del pack
                var tex = LoadCharTex();
                if (tex != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f);
                    string matPath = "Assets/Settings/PSX_Character.mat";
                    AssetDatabase.DeleteAsset(matPath);
                    AssetDatabase.CreateAsset(mat, matPath);
                    foreach (var r in rends) r.sharedMaterial = mat;
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
            root.AddComponent<FolkloreArchives.DogWalkAnim>(); // patas se mueven al caminar

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
            camGO.SetActive(false);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, DogPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // Carga el glb, lo escala a ~1.4 m y lo gira 180° (mismo criterio que el single-player).
        static void BuildDogVisual(Transform parent)
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
            var rends = model.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                float h = Mathf.Max(0.0001f, b.size.y);
                model.transform.localScale = Vector3.one * (target / h);
                Bounds b2 = model.GetComponentInChildren<Renderer>().bounds;
                foreach (var r in model.GetComponentsInChildren<Renderer>()) b2.Encapsulate(r.bounds);
                // -0.25: los bounds del skinned-mesh vienen inflados hacia abajo y lo
                // dejaban levitando; bajo el modelo para apoyar las patas en el piso.
                model.transform.localPosition = new Vector3(0f, -(b2.min.y - parent.position.y) - 0.25f, 0f);
            }
        }
    }
}
