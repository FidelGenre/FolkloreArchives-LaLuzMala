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
        //   "Cold Sunset"  → cielo AZUL con nubes y sol bajo = nuestro DÍA
        //   "Deep Dusk"    → techo de nubes + resplandor cálido en el horizonte = ATARDECER
        //   "Cold Night"   → azul oscuro = NOCHE
        // El equirect de Deep Dusk es MUY oscuro (por eso antes salía un rojo apagado);
        // se compensa con DuskExposure en el material del skybox.
        const string BaseSkyDay   = "Assets/AllSkyFree/Cold Sunset/Cold Sunset Equirect.png";
        const string BaseSkyDusk  = "Assets/AllSkyFree/Deep Dusk/Deep Dusk Equirect.png";
        const string BaseSkyNight = "Assets/AllSkyFree/Cold Night/Cold Night Equirect.png";

        // Exposición del material Skybox/Panoramic por fase (1 = tal cual el archivo).
        const float DayExposure   = 1.0f;
        const float DuskExposure  = 1.9f;   // levanta el Deep Dusk, que viene casi negro
        const float NightExposure = 1.0f;

        // ── colores tuneables ──
        // DÍA: montañas azuladas por perspectiva atmosférica.
        static readonly Color MtnFar   = new Color(0.40f, 0.42f, 0.55f); // cadena lejana
        static readonly Color MtnNear  = new Color(0.20f, 0.21f, 0.31f); // cadena cercana (más oscura)
        static readonly Color Ground   = new Color(0.10f, 0.10f, 0.14f); // bajo el horizonte
        const float FarHaze  = 0.55f;   // cuánto se mezcla la cadena lejana con el cielo (bruma)
        const float NearHaze = 0.18f;   // la cercana casi no se mezcla

        // ATARDECER: siluetas a contraluz contra el resplandor del horizonte. OJO: estos
        // valores son PRE-exposición — el material multiplica todo por DuskExposure
        // (1.9), así que en pantalla el "far" termina en ~0.30, no en 0.16.
        static readonly Color DuskMtnFar  = new Color(0.16f, 0.135f, 0.185f);
        static readonly Color DuskMtnNear = new Color(0.070f, 0.058f, 0.090f);
        static readonly Color DuskGround  = new Color(0.028f, 0.023f, 0.036f);
        const float DuskFarHaze  = 0.45f;
        const float DuskNearHaze = 0.14f;

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
        const float FarBase  = 0.030f, FarAmp  = 0.095f;  // cadena lejana → cima ≈ 0.625 (~22.5°)
        const float NearBase = 0.000f, NearAmp = 0.130f;  // cadena cercana → cima ≈ 0.630 (~23.4°)

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
            Debug.Log($"<color=lime>Skybox de montañas generado</color> — día: {(day ? "OK" : "FALLÓ")}, " +
                      $"atardecer: {(dusk ? "OK" : "FALLÓ")}, noche: {(night ? "OK" : "FALLÓ")}. " +
                      "Ahora regenerá el mapa. Tuneá alturas/colores en SkyboxMountainBaker.cs.");
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
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false, false);
            var px = new Color[W * H];

            for (int y = 0; y < H; y++)
            {
                float v = y / (float)(H - 1);                 // 0 = abajo, 1 = arriba
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)(W - 1);             // longitud (envuelve)
                    Color sky = baseSky.GetPixelBilinear(u, v);   // ← cielo REAL de AllSky
                    // dos cadenas de montañas (altura del horizonte por columna).
                    // MISMO ruido/semilla que el de día → las montañas coinciden entre
                    // día y noche (si no, al amanecer "saltarían" de lugar).
                    float far  = 0.5f + FarBase  + Mtn(u, 2.0f, 11f) * FarAmp;
                    float near = 0.5f + NearBase + Mtn(u, 3.6f, 47f) * NearAmp;

                    Color c;
                    if (v >= far && v >= near)      c = sky;                                    // cielo AllSky intacto
                    else if (v >= near)             c = Color.Lerp(mtnFar,  sky, farHaze);      // cadena lejana (con bruma del cielo)
                    else if (v >= 0.5f)             c = Color.Lerp(mtnNear, sky, nearHaze);     // cadena cercana
                    else                            c = Color.Lerp(mtnNear, ground, (0.5f - v) / 0.5f); // bajo el horizonte

                    c.a = 1f;
                    px[y * W + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.wrapModeU = TextureWrapMode.Repeat;   // envuelve horizontal (seamless)
            tex.wrapModeV = TextureWrapMode.Clamp;
            tex.Apply(false, false);

            // guardar textura como asset
            AssetDatabase.DeleteAsset(texPath);
            AssetDatabase.CreateAsset(tex, texPath);

            // material Skybox/Panoramic (equirect, Latitude-Longitude)
            var mat = new Material(Shader.Find("Skybox/Panoramic"));
            mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_Mapping")) mat.SetFloat("_Mapping", 1);   // 1 = Latitude Longitude Layout
            if (mat.HasProperty("_ImageType")) mat.SetFloat("_ImageType", 0); // 360
            if (mat.HasProperty("_Exposure")) mat.SetFloat("_Exposure", exposure);
            AssetDatabase.DeleteAsset(matPath);
            AssetDatabase.CreateAsset(mat, matPath);

            if (applyNow) RenderSettings.skybox = mat; // previsualizar el de día
            return true;
        }

        // Carga una textura asegurándose de que se puedan leer sus píxeles
        // (GetPixelBilinear falla si el importer tiene isReadable = false).
        static Texture2D LoadReadable(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null && !imp.isReadable)
            {
                imp.isReadable = true;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
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
