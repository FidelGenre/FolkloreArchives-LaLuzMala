// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  SkyboxMountainBaker.cs — genera POR CÓDIGO un skybox panorámico
//  con SILUETAS DE MONTAÑAS + cielo de atardecer. Un skybox de
//  verdad: se ve siempre en el horizonte (día), con niebla, sin 2ª
//  cámara, sin romper el cielo, sin costo de FPS.
//  Menú: Tools > Folklore Archives > Generar Skybox de Montañas.
//  Después regenerá el mapa (EnvironmentBuilder.DaySkybox lo usa).
// ============================================================
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class SkyboxMountainBaker
    {
        // rutas ESTABLES (fuera de Generated) para que sobrevivan al regenerar
        public const string MatPath      = "Assets/Settings/MountainSkybox.mat";        // DÍA
        public const string DuskMatPath  = "Assets/Settings/MountainSkyboxDusk.mat";    // ATARDECER
        public const string NightMatPath = "Assets/Settings/MountainSkyboxNight.mat";   // NOCHE
        const string TexPath      = "Assets/Settings/MountainSkybox_Tex.asset";
        const string DuskTexPath  = "Assets/Settings/MountainSkyboxDusk_Tex.asset";
        const string NightTexPath = "Assets/Settings/MountainSkyboxNight_Tex.asset";

        // Cielos BASE: los equirect de AllSky Free que usa el juego. Las montañas se
        // pintan ENCIMA de estos cielos, así no los reemplazan (antes el baker pintaba
        // su propio degradé y por eso pisaba al AllSky).
        // OJO con los nombres del pack, engañan:
        //   "Epic_BlueSunset"  → azul profundo, cúmulos, sol chico bajo    = nuestro DÍA
        //   "Cold Sunset"      → azul con nubes deshilachadas y sol grande = ATARDECER
        //   "Cold Night"       → azul oscuro                               = NOCHE
        // Descartados: "Deep Dusk" y "Epic_GloriousPink" (naranja/rojo) y "Overcast Low"
        // (el gris no tiene color propio: el grade VHS ámbar se lo comía y salía naranja).
        const string BaseSkyDay   = "Assets/AllSkyFree/Epic_BlueSunset/Epic_BlueSunset_EquiRect_flat.png";
        const string BaseSkyDusk  = "Assets/AllSkyFree/Cold Sunset/Cold Sunset Equirect.png";
        const string BaseSkyNight = "Assets/AllSkyFree/Cold Night/Cold Night Equirect.png";

        // Exposición del material Skybox/Panoramic por fase (1 = tal cual el archivo).
        // Cada valor va atado a SU imagen, así que al intercambiar los cielos se
        // intercambian también: el Cold Sunset necesita 1.15, el BlueSunset 1.0.
        const float DayExposure   = 1.0f;
        const float DuskExposure  = 1.15f;
        const float NightExposure = 1.0f;

        // ── colores tuneables ──
        // DÍA: montañas azuladas por perspectiva atmosférica. Son valores PRE-exposición
        // (el material multiplica todo por DayExposure).
        static readonly Color MtnFar   = new Color(0.36f, 0.38f, 0.50f); // cadena lejana
        static readonly Color MtnNear  = new Color(0.18f, 0.19f, 0.28f); // cadena cercana (más oscura)
        static readonly Color Ground   = new Color(0.090f, 0.090f, 0.125f); // bajo el horizonte
        const float FarHaze  = 0.66f;   // cuánto se mezcla la cadena lejana con el cielo (bruma)
        const float NearHaze = 0.10f;   // la cercana casi no se mezcla → silueta oscura

        // ATARDECER (Cold Sunset, cielo azul con sol bajo): montañas azuladas por
        // perspectiva atmosférica, un poco más oscuras que las del día.
        static readonly Color DuskMtnFar  = new Color(0.34f, 0.36f, 0.48f);
        static readonly Color DuskMtnNear = new Color(0.16f, 0.17f, 0.26f);
        static readonly Color DuskGround  = new Color(0.075f, 0.075f, 0.105f);
        const float DuskFarHaze  = 0.62f;
        const float DuskNearHaze = 0.11f;

        // NOCHE: siluetas casi negras, apenas azuladas. Menos bruma (de noche el aire
        // no dispersa luz), si no las montañas se "comen" las estrellas del cielo.
        static readonly Color NightMtnFar  = new Color(0.055f, 0.065f, 0.105f);
        static readonly Color NightMtnNear = new Color(0.022f, 0.026f, 0.045f);
        static readonly Color NightGround  = new Color(0.010f, 0.012f, 0.020f);
        const float NightFarHaze  = 0.22f;
        const float NightNearHaze = 0.06f;

        // ── ALTURA de las cadenas (en fracción de la vertical del equirect) ──
        // v = 0.5 es el horizonte (0° de elevación); v = 1.0 es el cenit (90°).
        // O sea: elevación_máx ≈ (base + amplitud) * 180°. Con los valores de abajo las
        // cimas llegan a ~23°, que es lo que hace falta para que ASOMEN por encima de
        // los pinos (un pino de 12m a 20m tapa ~31°, así que con 15° no se veían).
        // UNA sola cadena. Antes eran dos (lejana + cercana) y, por más que las separara
        // en altura y bruma, la de atrás se seguía leyendo como un calco de la de
        // adelante en vez de como profundidad.
        const float RidgeBase = 0.020f, RidgeAmp = 0.135f;  // cima ≈ 0.655 (~28° de elevación)

        // Llamado por Generate: NO re-hornea si los 3 materiales ya existen (ahorra
        // ~48 MB de texturas por regeneración). Para forzar el rebuild, el menú.
        public static void BakeCached()
        {
            bool haveAll =
                AssetDatabase.LoadAssetAtPath<Material>(MatPath) != null &&
                AssetDatabase.LoadAssetAtPath<Material>(DuskMatPath) != null &&
                AssetDatabase.LoadAssetAtPath<Material>(NightMatPath) != null;
            if (haveAll) return;   // ya está horneado, no rehacer
            Bake();
        }

        [MenuItem("Tools/Folklore Archives/Generar Skybox de Montañas")]
        public static void Bake()
        {
            bool day = BakeOne(BaseSkyDay, MatPath, TexPath,
                               MtnFar, MtnNear, Ground, FarHaze, NearHaze, DayExposure, applyNow: true);
            bool dusk = BakeOne(BaseSkyDusk, DuskMatPath, DuskTexPath,
                                DuskMtnFar, DuskMtnNear, DuskGround, DuskFarHaze, DuskNearHaze, DuskExposure, applyNow: false);
            bool night = BakeOne(BaseSkyNight, NightMatPath, NightTexPath,
                                 NightMtnFar, NightMtnNear, NightGround, NightFarHaze, NightNearHaze, NightExposure, applyNow: false);
            AssetDatabase.SaveAssets();

            // Si ya hay un mapa generado en la escena, reapuntar sus skyboxes a los
            // materiales recién horneados. Cuando el baker se corre desde el menú
            // (con el mapa ya hecho) esto es lo que evita que Tab quede sin cielos.
            var dnc = Object.FindFirstObjectByType<FolkloreArchives.DayNightController>();
            if (dnc != null)
            {
                dnc.daySkybox   = EnvironmentBuilder.DaySkybox();
                dnc.duskSkybox  = EnvironmentBuilder.DuskSkybox();
                dnc.nightSkybox = EnvironmentBuilder.NightSkybox();
                EditorUtility.SetDirty(dnc);
            }

            if (day && dusk && night)
                Debug.Log("<color=lime>Skybox de montañas: día OK, atardecer OK, noche OK.</color>");
            else
                Debug.LogError($"SKYBOX DE MONTAÑAS FALLÓ — día: {(day ? "OK" : "FALLÓ")}, " +
                               $"atardecer: {(dusk ? "OK" : "FALLÓ")}, noche: {(night ? "OK" : "FALLÓ")}. " +
                               "El mapa va a usar los cielos de AllSky pelados, SIN montañas.");
        }

        // Hornea UN skybox: cielo base equirect + dos cadenas de montañas encima.
        static bool BakeOne(string basePath, string matPath, string texPath,
                            Color mtnFar, Color mtnNear, Color ground,
                            float farHaze, float nearHaze, float exposure, bool applyNow)
        {
            var baseSky = LoadReadable(basePath);
            if (baseSky == null)
            {
                Debug.LogError("SkyboxMountainBaker: no pude leer el cielo base " + basePath);
                return false;
            }

            const int W = 2048, H = 1024;
            // Reusar el asset de textura si ya existe (NO borrarlo): el DayNightController
            // de la escena guarda referencias a estos assets, y borrarlos se las deja en
            // null → Tab dejaba de cambiar el cielo.
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            bool newTex = tex == null || tex.width != W || tex.height != H;
            if (newTex) tex = new Texture2D(W, H, TextureFormat.RGBA32, false, false);
            var px = new Color[W * H];

            for (int y = 0; y < H; y++)
            {
                float v = y / (float)(H - 1);                 // 0 = abajo, 1 = arriba
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)(W - 1);             // longitud (envuelve)
                    Color sky = baseSky.GetPixelBilinear(u, v);   // ← cielo REAL de AllSky
                    // UNA sola cadena (el dueño veía la segunda como un calco encima).
                    // MISMO ruido/semilla en las 3 fases → las montañas no "saltan" de
                    // lugar al pasar de día a atardecer a noche.
                    float ridge = 0.5f + RidgeBase + Mtn(u, 2.4f, 11f) * RidgeAmp;

                    Color c;
                    if (v >= ridge)      c = sky;                                          // cielo AllSky intacto
                    else if (v >= 0.5f)  c = Color.Lerp(mtnNear, sky, nearHaze);           // la montaña
                    else                 c = Color.Lerp(mtnNear, ground, (0.5f - v) / 0.5f); // bajo el horizonte

                    c.a = 1f;
                    px[y * W + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.wrapModeU = TextureWrapMode.Repeat;   // envuelve horizontal (seamless)
            tex.wrapModeV = TextureWrapMode.Clamp;
            tex.Apply(false, false);
            if (newTex) AssetDatabase.CreateAsset(tex, texPath);
            else        EditorUtility.SetDirty(tex);   // mismo asset, píxeles nuevos

            // material Skybox/Panoramic (equirect, Latitude-Longitude). Se REUSA el
            // asset si ya existe: borrarlo dejaba en null las referencias que guarda el
            // DayNightController de la escena, y Tab dejaba de cambiar el cielo.
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Skybox/Panoramic"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.shader = Shader.Find("Skybox/Panoramic");
            mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_Mapping")) mat.SetFloat("_Mapping", 1);   // 1 = Latitude Longitude Layout
            if (mat.HasProperty("_ImageType")) mat.SetFloat("_ImageType", 0); // 360
            if (mat.HasProperty("_Exposure")) mat.SetFloat("_Exposure", exposure);
            EditorUtility.SetDirty(mat);

            if (applyNow) RenderSettings.skybox = mat; // previsualizar el de día
            return true;
        }

        // Carga una textura asegurándose de que se pueda leer como Texture2D.
        // OJO: los "*Equirect.png" de AllSky vienen importados como CUBEMAP
        // (textureShape = Cube). Con esa forma, LoadAssetAtPath<Texture2D> devuelve
        // null y el horneado abortaba sin que se notara. Hay que forzarlos a Texture2D
        // (y a readable, o GetPixelBilinear falla). Solo rompe los "* Equirect.mat" del
        // pack, que no usamos.
        static Texture2D LoadReadable(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null)
            {
                Debug.LogError("SkyboxMountainBaker: no hay TextureImporter en " + path);
                return null;
            }
            bool dirty = false;
            if (imp.textureShape != TextureImporterShape.Texture2D) { imp.textureShape = TextureImporterShape.Texture2D; dirty = true; }
            if (!imp.isReadable) { imp.isReadable = true; dirty = true; }
            if (dirty) imp.SaveAndReimport();

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) Debug.LogError("SkyboxMountainBaker: no pude cargar como Texture2D " + path);
            return tex;
        }

        // altura de montaña por columna (0..1), sin costuras (muestreo en círculo) + 3 octavas
        static float Mtn(float u, float freq, float seed)
        {
            float ang = u * Mathf.PI * 2f;
            float x = Mathf.Cos(ang) * freq + seed;
            float y = Mathf.Sin(ang) * freq + seed;
            return Mathf.PerlinNoise(x, y) * 0.6f
                 + Mathf.PerlinNoise(x * 2.3f + 5f, y * 2.3f + 5f) * 0.3f
                 + Mathf.PerlinNoise(x * 4.7f + 9f, y * 4.7f + 9f) * 0.1f;
        }
    }
}
