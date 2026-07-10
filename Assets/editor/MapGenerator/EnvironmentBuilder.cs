// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  EnvironmentBuilder.cs — night atmosphere (moon, fog, ambient)
//  and the river water plane.
//  Paste into:  Assets/Editor/MapGenerator/EnvironmentBuilder.cs
// ============================================================
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FolkloreArchives.MapGen
{
    public static class EnvironmentBuilder
    {
        public static void Build(Transform parent)
        {
            BuildWater(parent);
            SetupNight(parent);
            // niebla del agua DESACTIVADA (owner: quitar la niebla del río y el lago).
            // Para reactivarla, descomentar estas dos líneas.
            // BuildWaterMist(parent, new Vector3(805f, 15f, 500f), new Vector3(70f, 14f, 1080f), 34f);        // río
            // BuildWaterMist(parent, new Vector3(MapLayout.CentralLakeCenter.x, MapLayout.CentralLakeLevel + 6f, MapLayout.CentralLakeCenter.y),
            //                new Vector3(MapLayout.CentralLakeRadius * 2f + 40f, 14f, MapLayout.CentralLakeRadius * 2f + 40f), 30f); // lago central
        }

        // Sistema de partículas de niebla flotando bajo sobre el agua. Muchas partículas
        // grandes, translúcidas y lentas: de frente (mirando cruzar el agua) se
        // acumulan y tapan la orilla de enfrente; desde arriba casi no se notan.
        static Material _mistMat;
        static void BuildWaterMist(Transform parent, Vector3 center, Vector3 box, float rate)
        {
            var go = new GameObject("WaterMist");
            go.transform.SetParent(parent);
            go.transform.position = center;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop();

            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 30f;
            main.startSpeed = 0.08f;
            main.startSize = new ParticleSystem.MinMaxCurve(20f, 40f);  // grandes + muchas + muy translúcidas = manto parejo, no discos
            main.startColor = new Color(0.82f, 0.80f, 0.84f, 0.035f);   // alpha muy bajo → se funden suave al superponerse
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.maxParticles = 2000;
            main.prewarm = true;   // que arranque ya lleno (loop on por defecto)

            var em = ps.emission; em.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;

            var vol = ps.velocityOverLifetime;
            vol.enabled = true; vol.space = ParticleSystemSimulationSpace.World;
            // las tres (x/y/z) deben estar en el MISMO modo (TwoConstants), si no
            // Unity tira "Particle Velocity curves must all be in the same mode".
            vol.x = new ParticleSystem.MinMaxCurve(-0.18f, 0.18f);
            vol.y = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f);
            vol.z = new ParticleSystem.MinMaxCurve(-0.18f, 0.18f);

            // aparecer/desaparecer suave (sin popping de partículas)
            var col = ps.colorOverLifetime; col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.material = MistMaterial();
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.alignment = ParticleSystemRenderSpace.View;
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = false;
            rend.sortingFudge = -20f; // dibujar detrás de props cercanos

            ps.Play();
        }

        static Material MistMaterial()
        {
            if (_mistMat != null) return _mistMat;
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            _mistMat = new Material(sh);
            _mistMat.SetTexture("_BaseMap", SoftBlobTexture());
            _mistMat.SetColor("_BaseColor", new Color(0.82f, 0.80f, 0.84f, 1f));
            // transparente (alpha blend), sin z-write, sin sombras
            _mistMat.SetFloat("_Surface", 1f);
            _mistMat.SetFloat("_Blend", 0f);
            _mistMat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            _mistMat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            _mistMat.SetInt("_ZWrite", 0);
            _mistMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _mistMat.renderQueue = 3100;
            return _mistMat;
        }

        static Texture2D SoftBlobTexture()
        {
            string path = MapLayout.GeneratedFolder + "/tex_mistblob.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            const int S = 64;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, true);
            Vector2 c = new Vector2(S * 0.5f, S * 0.5f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c) / (S * 0.5f);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a; // borde bien suave
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply(true);
            tex.wrapMode = TextureWrapMode.Clamp;
            AssetDatabase.CreateAsset(tex, path);
            return tex;
        }

        static void BuildWater(Transform parent)
        {
            var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.name = "River_Water";
            water.transform.SetParent(parent);
            water.transform.position = new Vector3(595f, 9.6f, 500f);  // río movido al centro (plano final del owner)
            water.transform.localScale = new Vector3(18f, 1f, 120f);
            // Share the SAME material as the lake (mat_lakewater) so the river and
            // lake read as one body of water instead of two slightly-different tones.
            var wmat = BuilderUtils.Mat("lakewater", new Color(0.05f, 0.11f, 0.16f), 0.2f);
            if (wmat.HasProperty("_Cull")) wmat.SetFloat("_Cull", 0f);
            wmat.doubleSidedGI = true;
            water.GetComponent<Renderer>().sharedMaterial = wmat;
            // was the one water/road surface in the project still casting shadows
            // for no visual benefit (matches the lake/road mesh/puddles, which
            // already disable this) - barely visible at night anyway (ShadowDistance=20).
            water.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            water.isStatic = true;
            Object.DestroyImmediate(water.GetComponent<Collider>());

            // SEGUNDO RÍO (tributario lago → río principal): agua como CINTA (mesh) que
            // SIGUE el cauce curvo. Un plano recto no cubría la curva (se desviaba ~55m)
            // y el agua quedaba al costado → no se veía. El ribbon la sigue punto a punto.
            {
                var r2 = MapLayout.River2;
                var verts = new System.Collections.Generic.List<Vector3>();
                var tris  = new System.Collections.Generic.List<int>();
                const float halfW = 22f, wy2 = 9.8f;
                for (int i = 0; i < r2.Length; i++)
                {
                    Vector2 fwd = (i < r2.Length - 1) ? r2[i + 1] - r2[i] : r2[i] - r2[i - 1];
                    if (fwd.sqrMagnitude < 1e-4f) fwd = Vector2.up;
                    fwd.Normalize();
                    Vector2 perp = new Vector2(-fwd.y, fwd.x) * halfW;
                    Vector2 lft = r2[i] + perp, rgt = r2[i] - perp;
                    verts.Add(new Vector3(lft.x, wy2, lft.y));
                    verts.Add(new Vector3(rgt.x, wy2, rgt.y));
                }
                for (int i = 0; i < r2.Length - 1; i++)
                {
                    int a = i * 2;
                    tris.Add(a); tris.Add(a + 2); tris.Add(a + 1);
                    tris.Add(a + 1); tris.Add(a + 2); tris.Add(a + 3);
                }
                var m2 = new Mesh { name = "River2_WaterMesh" };
                m2.SetVertices(verts); m2.SetTriangles(tris, 0);
                m2.RecalculateNormals(); m2.RecalculateBounds();
                var water2 = new GameObject("River2_Water");
                water2.transform.SetParent(parent);
                water2.AddComponent<MeshFilter>().sharedMesh = m2;
                var w2mat = BuilderUtils.Mat("lakewater", new Color(0.05f, 0.11f, 0.16f), 0.2f);
                if (w2mat.HasProperty("_Cull")) w2mat.SetFloat("_Cull", 0f);
                w2mat.doubleSidedGI = true;
                var mr2 = water2.AddComponent<MeshRenderer>();
                mr2.sharedMaterial = w2mat;
                mr2.shadowCastingMode = ShadowCastingMode.Off;
                water2.isStatic = true;
            }

            // LAGO GIGANTE CENTRAL (owner): plano de agua sobre la cuenca carvada.
            var lake = GameObject.CreatePrimitive(PrimitiveType.Plane);
            lake.name = "Central_Lake_Water";
            lake.transform.SetParent(parent);
            lake.transform.position = new Vector3(MapLayout.CentralLakeCenter.x, MapLayout.CentralLakeLevel, MapLayout.CentralLakeCenter.y);
            // Plane de Unity = 10x10u a escala 1 → cubrir el diámetro (radio*2) + margen
            float lakeScale = (MapLayout.CentralLakeRadius * 2f + 60f) / 10f;
            lake.transform.localScale = new Vector3(lakeScale, 1f, lakeScale);
            var lmat = BuilderUtils.Mat("lakewater", new Color(0.05f, 0.11f, 0.16f), 0.2f);
            if (lmat.HasProperty("_Cull")) lmat.SetFloat("_Cull", 0f);
            lmat.doubleSidedGI = true;
            lake.GetComponent<Renderer>().sharedMaterial = lmat;
            lake.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            lake.isStatic = true;
            Object.DestroyImmediate(lake.GetComponent<Collider>());
        }

        static void SetupNight(Transform parent)
        {
            // turn off any existing suns
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) l.gameObject.SetActive(false);

            // Fears-to-Fathom-style night: dark but NAVIGABLE. A soft blue moon lets
            // you actually see the forest silhouettes, and it casts soft shadows for
            // depth.
            // FtF Ironbark "blue hour" dusk: dark, but the sky is a deep blue that the
            // black tree silhouettes stand out against - that sky-vs-silhouette
            // contrast is the icon of the look. Moon is a soft blue rim light.
            var moon = new GameObject("Moon").AddComponent<Light>();
            moon.transform.SetParent(parent);
            moon.type = LightType.Directional;
            moon.intensity = MapLayout.MoonIntensity;
            moon.color = new Color(0.42f, 0.52f, 0.78f); // deep cool blue moonlight
            moon.shadows = LightShadows.Hard; // hard is much cheaper than soft across a dense forest
            moon.transform.eulerAngles = new Vector3(28f, -130f, 0f);

            // deep dark-blue night sky (FtF Scratch-Creek tone) so trees silhouette
            RenderSettings.skybox = NightSkybox();
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.016f, 0.026f, 0.052f); // deep dark blue base
            RenderSettings.reflectionIntensity = 0.35f; // higher so glossy puddles reflect the dark-blue sky instead of reading as black holes
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = MapLayout.FogDensity;
            // deep blue night fog: distant trees fade into a dark blue murk
            RenderSettings.fogColor = new Color(0.035f, 0.055f, 0.105f);

            // realtime shadows across a dense forest are expensive - cap them to
            // roughly where the fog makes things invisible anyway.
            // NOTE: URP ignores QualitySettings.shadowDistance - the shadow distance
            // that actually matters lives on the active UniversalRenderPipelineAsset.
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urpAsset)
                urpAsset.shadowDistance = MapLayout.ShadowDistance;
        }

        // ── Skybox: usa AllSky Free si está importado, si no el cielo procedural ──
        //  Día = Epic_GloriousPink (atardecer, pega con la niebla rosada). Noche =
        //  Cold Night (azul oscuro). Cambiá la ruta acá para probar otro cielo de AllSky.
        public static Material DaySkybox()
        {
            // HDRI de día PSX (StarkCrafts) DESACTIVADO: el dueño prefiere el cielo de
            // AllSky Free. Para volverlo a activar, descomentá:
            //   var psx = PanoramicSkybox("Assets/StarkCrafts/PSX_Daysky_HDRI/DAYSKY.hdr",
            //                             "Assets/Settings/PSX_DaySky.mat");
            //   if (psx != null) return psx;

            // 1º: skybox de montañas. Ya NO reemplaza al AllSky: el baker hornea las
            // montañas ENCIMA del equirect del cielo, así tenés las dos cosas.
            // Se genera con Tools > Folklore Archives > Generar Skybox de Montañas.
            var mtn = AssetDatabase.LoadAssetAtPath<Material>(SkyboxMountainBaker.MatPath);
            if (mtn != null) return mtn;
            // 2º: el AllSky pelado (si el baker falló). Día = "Epic_BlueSunset".
            var sky = AssetDatabase.LoadAssetAtPath<Material>("Assets/AllSkyFree/Epic_BlueSunset/Epic_BlueSunset.mat");
            if (sky != null) return sky;
            return AssetDatabase.LoadAssetAtPath<Material>(MapLayout.GeneratedFolder + "/mat_daysky.mat") ?? BuildDaySky();
        }
        // ATARDECER: "Cold Sunset" de AllSky (nubes deshilachadas, sol grande y difuso).
        // Los cielos naranjas/rojos (Deep Dusk, Epic_GloriousPink) quedaron descartados.
        public static Material DuskSkybox()
        {
            var mtn = AssetDatabase.LoadAssetAtPath<Material>(SkyboxMountainBaker.DuskMatPath);
            if (mtn != null) return mtn;
            var sky = AssetDatabase.LoadAssetAtPath<Material>("Assets/AllSkyFree/Cold Sunset/Cold Sunset.mat");
            if (sky != null) return sky;
            return DaySkybox();
        }

        public static Material NightSkybox()
        {
            // HDRI de noche PSX (StarkCrafts) DESACTIVADO: el dueño prefiere el cielo
            // nocturno de antes (AllSky Free "Cold Night").
            // Para volver al PSX de noche, descomentá estas tres líneas:
            //   var psx = PanoramicSkybox("Assets/StarkCrafts/PSX_Nightsky_HDRI/PSX_NIGHTSKY.hdr",
            //                             "Assets/Settings/PSX_NightSky.mat");
            //   if (psx != null) return psx;

            // 1º: skybox de montañas NOCTURNO (siluetas horneadas sobre el Cold Night).
            // Se genera con Tools > Folklore Archives > Generar Skybox de Montañas.
            var mtn = AssetDatabase.LoadAssetAtPath<Material>(SkyboxMountainBaker.NightMatPath);
            if (mtn != null) return mtn;
            // 2º: el Cold Night pelado (si nunca se corrió el baker).
            var sky = AssetDatabase.LoadAssetAtPath<Material>("Assets/AllSkyFree/Cold Night/Cold Night.mat");
            if (sky != null) return sky;
            var proc = AssetDatabase.LoadAssetAtPath<Material>(MapLayout.GeneratedFolder + "/mat_nightsky.mat");
            return proc != null ? proc : BuildDuskSky();
        }

        // Crea (y cachea) un material Skybox/Panoramic a partir de un HDRI equirect.
        // Devuelve null si el HDRI todavía no está importado.
        static Material PanoramicSkybox(string hdrPath, string matPath)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture>(hdrPath);
            if (tex == null) return null;
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Skybox/Panoramic"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_Mapping"))   mat.SetFloat("_Mapping", 1);   // Latitude Longitude
            if (mat.HasProperty("_ImageType")) mat.SetFloat("_ImageType", 0); // 360
            if (mat.HasProperty("_Exposure"))  mat.SetFloat("_Exposure", 1f);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        // Warm golden-hour / late-afternoon sky: FtF style — orange at the horizon,
        // blue at the zenith, volumetric cloud hints, and mountain ridges lit from the
        // front (lighter/warmer than the night silhouettes).
        public static Material BuildDaySky()
        {
            const int W = 2048, H = 512;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = "DaySky" };

            var zenith  = new Color(0.26f, 0.40f, 0.70f); // deep clear blue at top
            var mid     = new Color(0.58f, 0.68f, 0.88f); // light blue mid-sky
            var horizon = new Color(0.92f, 0.66f, 0.38f); // warm orange/gold at horizon
            var glow    = new Color(1.00f, 0.84f, 0.52f); // sun-glow band
            var mtnFar  = new Color(0.52f, 0.44f, 0.54f); // far ridge - warm purple-grey
            var mtnNear = new Color(0.30f, 0.24f, 0.32f); // near ridge - darker

            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
            {
                float v  = y / (float)(H - 1);              // 0 bottom → 1 top
                float up = Mathf.Clamp01((v - 0.5f) * 2f);
                float hz = Mathf.Clamp01(1f - Mathf.Abs(v - 0.5f) * 5f); // thin band at horizon

                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)(W - 1);
                    // sky gradient: golden horizon → light blue → deep blue
                    Color sky = Color.Lerp(horizon, mid, Mathf.Clamp01(up * 1.8f));
                    sky = Color.Lerp(sky, zenith, Mathf.Clamp01(up * up * 1.6f));
                    // sun glow along the whole horizon (panoramic warm rim)
                    sky = Color.Lerp(sky, glow, hz * 0.55f);

                    // mountain silhouettes — visible but warmly lit (not pure black)
                    if (v >= 0.45f && v < 0.68f)
                    {
                        float ang = u * Mathf.PI * 2f;
                        float cx2 = Mathf.Cos(ang), sz2 = Mathf.Sin(ang);
                        float farR = 0.5f + Ridge(cx2, sz2, 1.7f, 1.6f) * 0.14f;
                        if (v < farR) sky = Color.Lerp(sky, mtnFar, 0.55f);
                        float midR = 0.5f + Ridge(cx2 * 1.4f + 3f, sz2 * 1.4f + 3f, 2.3f, 1.8f) * 0.10f;
                        if (v < midR) sky = Color.Lerp(sky, mtnNear, 0.60f);
                    }

                    // clouds: soft Perlin blobs in the upper half
                    if (v > 0.56f && v < 0.88f)
                    {
                        float ang = u * Mathf.PI * 2f;
                        float cx2 = Mathf.Cos(ang), cz2 = Mathf.Sin(ang);
                        float c1 = Mathf.PerlinNoise(cx2 * 3.5f + 10f, cz2 * 3.5f + 10f);
                        float c2 = Mathf.PerlinNoise(cx2 * 7.0f + 5f,  cz2 * 7.0f + 5f) * 0.4f;
                        float mask = Mathf.Clamp01((c1 + c2 - 0.58f) * 2.8f);
                        var cloudCol = new Color(1.0f, 0.90f, 0.74f); // warm lit cloud undersides
                        sky = Color.Lerp(sky, cloudCol, mask * 0.42f);
                    }

                    px[y * W + x] = sky;
                }
            }
            tex.SetPixels(px);
            tex.Apply(true);
            tex.wrapModeU = TextureWrapMode.Repeat;
            tex.wrapModeV = TextureWrapMode.Clamp;

            string texPath = MapLayout.GeneratedFolder + "/tex_daysky.asset";
            AssetDatabase.DeleteAsset(texPath);
            AssetDatabase.CreateAsset(tex, texPath);

            var shader = Shader.Find("Skybox/Panoramic");
            if (shader == null) return null;
            var mat = new Material(shader) { name = "DaySky" };
            mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_Mapping"))   mat.SetFloat("_Mapping",   1f); // Latitude-Longitude
            if (mat.HasProperty("_ImageType")) mat.SetFloat("_ImageType", 0f); // 360
            if (mat.HasProperty("_Exposure"))  mat.SetFloat("_Exposure",  1.3f); // slightly brighter than night

            string matPath = MapLayout.GeneratedFolder + "/mat_daysky.mat";
            AssetDatabase.DeleteAsset(matPath);
            AssetDatabase.CreateAsset(mat, matPath);
            return mat;
        }

        // seamless (wraps around the horizon) fractal ridge height 0..1
        static float Ridge(float cx, float sz, float scale, float sharp)
        {
            float amp = 0.5f, fr = 1f, sum = 0f, norm = 0f;
            for (int o = 0; o < 4; o++)
            {
                sum += amp * Mathf.PerlinNoise(cx * scale * fr + 20f, sz * scale * fr + 20f);
                norm += amp; fr *= 2f; amp *= 0.5f;
            }
            return Mathf.Pow(Mathf.Clamp01(sum / norm), sharp);
        }

        // Deep-blue "blue hour" dusk gradient with distant MOUNTAIN silhouettes along
        // the horizon, saved as a lat-long panoramic skybox. Painting the mountains
        // into the sky (rather than as terrain) means they're always visible behind
        // the fog - the FtF "on a mountain, ridgelines in the distance" look.
        static Material BuildDuskSky()
        {
            const int W = 2048, H = 512;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = "DuskSky" };

            var zenith   = new Color(0.015f, 0.03f, 0.075f); // deep dark blue overhead
            var horizon  = new Color(0.07f, 0.11f, 0.20f);   // slightly lighter blue at the horizon
            var mtnFar   = new Color(0.05f, 0.08f, 0.14f);   // hazy far ridge
            var mtnNear  = new Color(0.02f, 0.035f, 0.07f);  // darker near ridge

            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
            {
                float v = y / (float)(H - 1);              // 0 = bottom, 1 = top
                float up = Mathf.Clamp01((v - 0.5f) * 2f);  // 0 at horizon, 1 at zenith
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)(W - 1);
                    Color sky = Color.Lerp(horizon, zenith, up * up);

                    if (v >= 0.46f && v < 0.7f)
                    {
                        float ang = u * Mathf.PI * 2f;
                        float cx = Mathf.Cos(ang), sz = Mathf.Sin(ang);

                        // soft hazy mountain ridges only (the distant FOREST is done with
                        // real trees + fog, not painted - a flat painted forest always
                        // reads as spikes/blocks, never like real trees).
                        float farR = 0.5f + Ridge(cx, sz, 1.7f, 1.6f) * 0.15f;
                        if (v < farR) sky = Color.Lerp(sky, mtnFar, 0.6f);
                        float midMtn = 0.5f + Ridge(cx * 1.4f + 3f, sz * 1.4f + 3f, 2.3f, 1.8f) * 0.11f;
                        if (v < midMtn) sky = Color.Lerp(sky, mtnNear, 0.55f);
                    }
                    px[y * W + x] = sky;
                }
            }
            tex.SetPixels(px);
            tex.Apply(true);
            tex.wrapModeU = TextureWrapMode.Repeat;
            tex.wrapModeV = TextureWrapMode.Clamp;

            string texPath = MapLayout.GeneratedFolder + "/tex_nightsky.asset";
            AssetDatabase.DeleteAsset(texPath);
            AssetDatabase.CreateAsset(tex, texPath);

            var shader = Shader.Find("Skybox/Panoramic");
            if (shader == null) return null;
            var mat = new Material(shader) { name = "NightSky" };
            mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_Mapping")) mat.SetFloat("_Mapping", 1f);   // Latitude-Longitude layout
            if (mat.HasProperty("_ImageType")) mat.SetFloat("_ImageType", 0f); // 360 degrees
            if (mat.HasProperty("_Exposure")) mat.SetFloat("_Exposure", 1f);
            if (mat.HasProperty("_Rotation")) mat.SetFloat("_Rotation", 0f);

            string matPath = MapLayout.GeneratedFolder + "/mat_nightsky.mat";
            AssetDatabase.DeleteAsset(matPath);
            AssetDatabase.CreateAsset(mat, matPath);
            return mat;
        }
    }
}
