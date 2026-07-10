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
        public const string MatPath = "Assets/Settings/MountainSkybox.mat";
        const string TexPath = "Assets/Settings/MountainSkybox_Tex.asset";

        // Cielo BASE: el equirect de AllSky Free que usa el juego de día. Las montañas
        // se pintan ENCIMA de este cielo, así no lo reemplazan (antes el baker pintaba
        // su propio degradé y por eso pisaba al AllSky).
        const string BaseSkyPath = "Assets/AllSkyFree/Epic_GloriousPink/Epic_GloriousPink_EquiRect.png";

        // ── colores tuneables ──
        static readonly Color MtnFar     = new Color(0.40f, 0.42f, 0.55f); // montañas lejanas (azuladas, perspectiva atmosférica)
        static readonly Color MtnNear    = new Color(0.20f, 0.21f, 0.31f); // montañas cercanas (más oscuras)
        static readonly Color Ground     = new Color(0.10f, 0.10f, 0.14f); // bajo el horizonte
        const float FarHaze  = 0.55f;   // cuánto se mezcla la cadena lejana con el cielo (bruma)
        const float NearHaze = 0.18f;   // la cercana casi no se mezcla

        [MenuItem("Tools/Folklore Archives/Generar Skybox de Montañas")]
        public static void Bake()
        {
            var baseSky = LoadReadable(BaseSkyPath);
            if (baseSky == null)
            {
                Debug.LogError("SkyboxMountainBaker: no pude leer el cielo base " + BaseSkyPath);
                return;
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
                    // dos cadenas de montañas (altura del horizonte por columna)
                    float far  = 0.5f + 0.02f + Mtn(u, 2.0f, 11f) * 0.055f; // ← subí el 0.055 = más altas
                    float near = 0.5f + 0.00f + Mtn(u, 3.6f, 47f) * 0.085f;

                    Color c;
                    if (v >= far && v >= near)      c = sky;                                    // cielo AllSky intacto
                    else if (v >= near)             c = Color.Lerp(MtnFar,  sky, FarHaze);      // cadena lejana (con bruma del cielo)
                    else if (v >= 0.5f)             c = Color.Lerp(MtnNear, sky, NearHaze);     // cadena cercana
                    else                            c = Color.Lerp(MtnNear, Ground, (0.5f - v) / 0.5f); // bajo el horizonte

                    c.a = 1f;
                    px[y * W + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.wrapModeU = TextureWrapMode.Repeat;   // envuelve horizontal (seamless)
            tex.wrapModeV = TextureWrapMode.Clamp;
            tex.Apply(false, false);

            // guardar textura como asset
            AssetDatabase.DeleteAsset(TexPath);
            AssetDatabase.CreateAsset(tex, TexPath);

            // material Skybox/Panoramic (equirect, Latitude-Longitude)
            var mat = new Material(Shader.Find("Skybox/Panoramic"));
            mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_Mapping")) mat.SetFloat("_Mapping", 1);   // 1 = Latitude Longitude Layout
            if (mat.HasProperty("_ImageType")) mat.SetFloat("_ImageType", 0); // 360
            if (mat.HasProperty("_Exposure")) mat.SetFloat("_Exposure", 1f);
            AssetDatabase.DeleteAsset(MatPath);
            AssetDatabase.CreateAsset(mat, MatPath);
            AssetDatabase.SaveAssets();

            RenderSettings.skybox = mat; // aplicar ya para previsualizar
            Debug.Log("<color=lime>Skybox de montañas generado</color> en " + MatPath +
                      ". Regenerá el mapa (o ya está aplicado). Tuneá alturas/colores en SkyboxMountainBaker.cs.");
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
