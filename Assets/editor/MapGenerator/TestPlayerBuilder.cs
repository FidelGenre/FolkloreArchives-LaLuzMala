// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  TestPlayerBuilder.cs — spawns a first-person test player with
//  a flashlight at the campsite so you can walk the map on Play.
//  Paste into:  Assets/Editor/MapGenerator/TestPlayerBuilder.cs
// ============================================================
using UnityEditor;
using UnityEditor.Animations;
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

            // spawn AL LADO DEL AUTO (en la ruta, pasando el túnel) para poder probar
            // el manejo sin caminar desde el campamento. Encaja con el arranque del juego.
            // (Para volver a spawnear en el campamento: usar MapLayout.Campsite como antes.)
            float carX = MapLayout.TunnelEntranceX + 22f;
            Vector2 spawnXZ = new Vector2(carX, MapLayout.PavedRouteZAt(carX) + 3.5f);
            player.transform.position = new Vector3(spawnXZ.x, MapLayout.RoadSurfaceHeight + 0.2f, spawnXZ.y);
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

        // Spawnea el perro (modelo PS1 real) con CharacterController, su cámara de 3ª
        // persona (apagada al inicio) y el DogController en modo Follow. Cuelga el
        // PartyController en la persona para poder alternar el control con G.
        static void BuildDogAndParty(GameObject player, GameObject personCamGO, Vector2 playerXZ, Terrain t)
        {
            const string glbPath = "Assets/ExternalAssets/DogAnim/Dog.fbx";  // perro riggeado + animado (WildPoly3D, CC-BY)
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
            if (prefab == null)
            {
                Debug.LogWarning("Perro: no encontré/importé " + glbPath +
                    " (dale FOCO a Unity para que importe el FBX). Sigo sin perro.");
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
            model.transform.localRotation = Quaternion.identity; // el perro nuevo ya mira a +Z (adelante)
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

            // Animator con las animaciones reales (Idle/Walk/Run/Lie) del FBX.
            var dogAnim = model.GetComponentInChildren<Animator>();
            if (dogAnim == null) dogAnim = model.AddComponent<Animator>();
            dogAnim.runtimeAnimatorController = BuildDogAnimator(glbPath);
            dogAnim.applyRootMotion = false;   // el CharacterController mueve; la animación solo anima
            dogAnim.speed = 0.4f;              // bien lento (cola calma)
            // (sin PsxAnimator: animación suave/limpia como el humano)

            // El perro usa VERTEX COLORS (no textura): con el shader LowPolyVertexColor
            // salen ojos/nariz/boca/lengua/pelo tal cual el modelo original, mate.
            var vcShader = Shader.Find("Folklore/LowPolyVertexColor");
            Material dogMat;
            if (vcShader != null)
            {
                string mp = MapLayout.GeneratedFolder + "/mat_dog_vc.mat";
                dogMat = AssetDatabase.LoadAssetAtPath<Material>(mp);
                if (dogMat == null) { dogMat = new Material(vcShader); AssetDatabase.CreateAsset(dogMat, mp); }
                dogMat.shader = vcShader;
                if (dogMat.HasProperty("_BaseColor")) dogMat.SetColor("_BaseColor", Color.white);
                if (dogMat.HasProperty("_BaseMap"))
                {
                    dogMat.SetTexture("_BaseMap", DogGrungeTex());          // grunge que te gustó (textura de piel)
                    dogMat.SetTextureScale("_BaseMap", new Vector2(3f, 3f));
                }
                // cuerpo PLANO marrón (el de antes) + detalles (boca/dientes/lengua) por vertex-color
                if (dogMat.HasProperty("_FlatBodyOn")) dogMat.SetFloat("_FlatBodyOn", 1f);
                if (dogMat.HasProperty("_FlatColor")) dogMat.SetColor("_FlatColor", new Color(0.46f, 0.33f, 0.21f)); // marrón (compensado por el grunge)
                if (dogMat.HasProperty("_BodyRef")) dogMat.SetColor("_BodyRef", new Color(0.72f, 0.60f, 0.46f));     // tan del cuerpo a reemplazar
                if (dogMat.HasProperty("_DetailSharp")) dogMat.SetFloat("_DetailSharp", 4f);
                if (dogMat.HasProperty("_PsxSnap")) dogMat.SetFloat("_PsxSnap", 0f);
                if (dogMat.HasProperty("_PsxColorLevels")) dogMat.SetFloat("_PsxColorLevels", 0f);
            }
            else dogMat = MatteMat("dog_fur", new Color(0.34f, 0.25f, 0.17f)); // fallback si falta el shader
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++) ms[i] = dogMat;
                r.sharedMaterials = ms;
            }
            Debug.Log($"<color=cyan>[Dog] shader vertex-color {(vcShader != null ? "OK" : "NO (fallback color plano)")}</color>");

            // OJOS + NARIZ procedurales (esferas oscuras). La nariz va BAJADA y adelantada
            // al hocico (el hueso "nose" está muy alto → si no, quedaba arriba y doble).
            var darkBall = MatteMat("dog_dark", new Color(0.03f, 0.03f, 0.03f));
            foreach (var bone in model.GetComponentsInChildren<Transform>(true))
            {
                string bn = bone.name.ToLower();
                if (bn.Contains("end") || bn.Contains("target") || bn.Contains("pole")) continue;
                if (bn.Contains("eye")) AddBall(bone, 0.05f, darkBall);
                else if (bn.Contains("nose"))
                {
                    var nb = AddBall(bone, 0.06f, darkBall);
                    nb.transform.position = bone.position - model.transform.up * 0.04f + model.transform.forward * 0.045f;
                }
            }

            var dogCtrl = dog.AddComponent<FolkloreArchives.DogController>();
            dogCtrl.followTarget = player.transform;
            dogCtrl.mode = FolkloreArchives.DogController.Mode.Follow;
            // (ya no se usa DogWalkAnim: el perro tiene animaciones reales)

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

        // Crea un AnimatorController con estados Idle/Walk/Run/Lie usando los clips del
        // FBX (matchea por nombre). Pone los clips en LOOP (si no, se congelan). El
        // DogController hace animator.CrossFade("Idle"/"Walk"/"Run"/"Lie").
        static AnimatorController BuildDogAnimator(string fbxPath)
        {
            // asegurar loop en todos los clips del FBX
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer != null)
            {
                var anims = importer.clipAnimations;
                if (anims == null || anims.Length == 0) anims = importer.defaultClipAnimations;
                bool changed = false;
                for (int i = 0; i < anims.Length; i++)
                    if (!anims[i].loopTime) { anims[i].loopTime = true; changed = true; }
                if (changed) { importer.clipAnimations = anims; importer.SaveAndReimport(); }
            }

            var clips = new System.Collections.Generic.List<AnimationClip>();
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                if (a is AnimationClip c && !c.name.StartsWith("__preview")) clips.Add(c);

            System.Func<string[], AnimationClip> find = keys =>
            {
                foreach (var k in keys)
                    foreach (var c in clips)
                        if (c.name.ToLower().Contains(k)) return c;
                return clips.Count > 0 ? clips[0] : null;
            };

            string path = MapLayout.GeneratedFolder + "/DogAnimator.controller";
            AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            var sm = ctrl.layers[0].stateMachine;
            var idle = sm.AddState("Idle"); idle.motion = find(new[] { "idle" });
            var walk = sm.AddState("Walk"); walk.motion = find(new[] { "walk" });
            var run  = sm.AddState("Run");  run.motion  = find(new[] { "run" });
            var lie  = sm.AddState("Lie");  lie.motion  = find(new[] { "lie", "lay", "sit", "sleep" });
            sm.defaultState = idle;
            Debug.Log($"<color=cyan>[Dog] Animator: {clips.Count} clips. Idle={ClipName(idle)} Walk={ClipName(walk)} Run={ClipName(run)} Lie={ClipName(lie)}</color>");
            return ctrl;
        }

        static string ClipName(UnityEditor.Animations.AnimatorState s) => s.motion != null ? s.motion.name : "(none)";

        // esfera oscura (ojo/nariz) colgada de un hueso, de ~worldSize metros reales
        // (compenso la escala del hueso para que no dependa del escalado del perro).
        static GameObject AddBall(Transform bone, float worldSize, Material m)
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            s.name = "Ball_" + bone.name;
            Object.DestroyImmediate(s.GetComponent<Collider>());
            s.transform.SetParent(bone, false);
            s.transform.localPosition = Vector3.zero;
            Vector3 ls = bone.lossyScale;
            s.transform.localScale = new Vector3(
                worldSize / Mathf.Max(0.0001f, ls.x),
                worldSize / Mathf.Max(0.0001f, ls.y),
                worldSize / Mathf.Max(0.0001f, ls.z));
            s.GetComponent<Renderer>().sharedMaterial = m;
            return s;
        }

        // Textura de GRUNGE procedural (grano/mugre) que multiplica los vertex colors del
        // perro para romper el color plano y que se integre con el entorno sucio/PSX.
        // filterMode Point + baja resolución = crocante estilo PS1.
        static Texture2D DogGrungeTex()
        {
            string path = MapLayout.GeneratedFolder + "/tex_dog_grunge.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            var rnd = new System.Random(1234);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float n = Mathf.PerlinNoise(x * 0.09f, y * 0.09f) * 0.55f
                            + Mathf.PerlinNoise(x * 0.33f + 7f, y * 0.33f + 3f) * 0.30f
                            + (float)rnd.NextDouble() * 0.15f;
                    float v = Mathf.Lerp(0.55f, 1.0f, Mathf.Clamp01(n)); // mancha, sin ennegrecer de más
                    tex.SetPixel(x, y, new Color(v, v, v));
                }
            tex.Apply();
            AssetDatabase.CreateAsset(tex, path);
            return tex;
        }

        // material URP MATE (sin brillo ni metal) — evita el look plástico.
        static Material MatteMat(string name, Color c)
        {
            var m = BuilderUtils.Mat(name, c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 0f);
            return m;
        }
    }
}
