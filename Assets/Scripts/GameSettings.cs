// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  GameSettings.cs — configuración gráfica completa con presets
//  (Baja/Media/Alta/Ultra/Personalizado) y opciones individuales.
//  Persiste con PlayerPrefs y se aplica en vivo. La UI (SettingsMenu)
//  la lee/escribe. Las distancias de niebla/pasto/árboles se aplican
//  como MULTIPLICADORES a través del DayNightController (que maneja
//  los valores base por modo día/noche).
// ============================================================
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FolkloreArchives
{
    public enum QualityPreset { Baja = 0, Media = 1, Alta = 2, Ultra = 3, Personalizado = 4 }

    public static class GameSettings
    {
        public static QualityPreset Preset = QualityPreset.Alta;

        // ---- opciones individuales ----
        public static float RenderScale = 0.8f;      // 0.5 .. 1.0
        public static int Antialiasing = 2;          // 0=Off 1=FXAA 2=SMAA 3=TAA
        public static bool MotionBlur = true;
        public static bool Bloom = false;
        public static bool FilmGrain = true;
        public static int TextureQuality = 0;        // 0=Full 1=Media 2=Baja (mip limit)
        public static int ShadowQuality = 2;         // 0=Off 1=Baja 2=Alta
        public static bool Vsync = false;
        public static int Msaa = 0;                  // 0=Off 1=2x 2=4x (MSAA en el URP asset)
        public static bool Ssao = false;             // oclusión ambiental (renderer feature)
        public static int FpsCap = 0;                // 0=Sin límite 1=30 2=60 3=120 4=144
        public static float Fov = 70f;               // campo de visión de la cámara (60..100)
        public static float TreeBillboardMul = 1f;   // multiplica la distancia billboard (35m día / 22m noche)

        // multiplicadores (1 = default). Los aplica DayNightController.
        public static float FogNearMul = 1f;         // niebla cercana (dónde empieza)
        public static float FogFarMul = 1f;          // niebla lejana (hasta dónde ves)
        public static float GrassDensityMul = 1f;
        public static float GrassDistanceMul = 1f;
        public static float TreeDistanceMul = 1f;
        public static float ViewDistanceMul = 1f;    // distancia de cámara (far clip)

        // ---- presets ----
        public static void ApplyPreset(QualityPreset p)
        {
            Preset = p;
            switch (p)
            {
                case QualityPreset.Baja:
                    RenderScale = 0.60f; Antialiasing = 0; MotionBlur = false; Bloom = false; FilmGrain = false;
                    TextureQuality = 2; ShadowQuality = 0; Msaa = 0; Ssao = false; TreeBillboardMul = 0.7f;
                    FogNearMul = 0.8f; FogFarMul = 0.6f; GrassDensityMul = 0.4f; GrassDistanceMul = 0.6f; TreeDistanceMul = 0.55f; ViewDistanceMul = 0.7f;
                    break;
                case QualityPreset.Media:
                    RenderScale = 0.75f; Antialiasing = 1; MotionBlur = false; Bloom = false; FilmGrain = true;
                    TextureQuality = 1; ShadowQuality = 1; Msaa = 0; Ssao = false; TreeBillboardMul = 0.85f;
                    FogNearMul = 0.9f; FogFarMul = 0.85f; GrassDensityMul = 0.7f; GrassDistanceMul = 0.8f; TreeDistanceMul = 0.8f; ViewDistanceMul = 0.85f;
                    break;
                case QualityPreset.Alta:
                    RenderScale = 0.85f; Antialiasing = 2; MotionBlur = true; Bloom = false; FilmGrain = true;
                    TextureQuality = 0; ShadowQuality = 2; Msaa = 0; Ssao = false; TreeBillboardMul = 1f;
                    FogNearMul = 1f; FogFarMul = 1f; GrassDensityMul = 1f; GrassDistanceMul = 1f; TreeDistanceMul = 1f; ViewDistanceMul = 1f;
                    break;
                case QualityPreset.Ultra:
                    RenderScale = 1.0f; Antialiasing = 2; MotionBlur = true; Bloom = true; FilmGrain = true;
                    TextureQuality = 0; ShadowQuality = 2; Msaa = 2; Ssao = true; TreeBillboardMul = 1.5f;
                    FogNearMul = 1f; FogFarMul = 1.3f; GrassDensityMul = 1.2f; GrassDistanceMul = 1.3f; TreeDistanceMul = 1.3f; ViewDistanceMul = 1.3f;
                    break;
            }
        }

        // ---- persistencia ----
        public static void Load()
        {
            Preset = (QualityPreset)PlayerPrefs.GetInt("gfx_preset", (int)QualityPreset.Alta);
            // preferencias independientes del preset (los presets no las cambian)
            Fov    = PlayerPrefs.GetFloat("gfx_fov", 70f);
            FpsCap = PlayerPrefs.GetInt("gfx_fpscap", 0);
            if (Preset != QualityPreset.Personalizado) { ApplyPreset(Preset); return; }
            RenderScale     = PlayerPrefs.GetFloat("gfx_rs", 0.8f);
            Antialiasing    = PlayerPrefs.GetInt("gfx_aa", 2);
            MotionBlur      = PlayerPrefs.GetInt("gfx_mb", 1) == 1;
            Bloom           = PlayerPrefs.GetInt("gfx_bloom", 0) == 1;
            FilmGrain       = PlayerPrefs.GetInt("gfx_grain", 1) == 1;
            TextureQuality  = PlayerPrefs.GetInt("gfx_tex", 0);
            ShadowQuality   = PlayerPrefs.GetInt("gfx_shadow", 2);
            Vsync           = PlayerPrefs.GetInt("gfx_vsync", 0) == 1;
            Msaa            = PlayerPrefs.GetInt("gfx_msaa", 0);
            Ssao            = PlayerPrefs.GetInt("gfx_ssao", 0) == 1;
            TreeBillboardMul= PlayerPrefs.GetFloat("gfx_treebb", 1f);
            FogNearMul      = PlayerPrefs.GetFloat("gfx_fognear", 1f);
            FogFarMul       = PlayerPrefs.GetFloat("gfx_fogfar", 1f);
            GrassDensityMul = PlayerPrefs.GetFloat("gfx_grassdens", 1f);
            GrassDistanceMul= PlayerPrefs.GetFloat("gfx_grassdist", 1f);
            TreeDistanceMul = PlayerPrefs.GetFloat("gfx_treedist", 1f);
            ViewDistanceMul = PlayerPrefs.GetFloat("gfx_view", 1f);
        }

        public static void Save()
        {
            PlayerPrefs.SetInt("gfx_preset", (int)Preset);
            PlayerPrefs.SetFloat("gfx_rs", RenderScale);
            PlayerPrefs.SetInt("gfx_aa", Antialiasing);
            PlayerPrefs.SetInt("gfx_mb", MotionBlur ? 1 : 0);
            PlayerPrefs.SetInt("gfx_bloom", Bloom ? 1 : 0);
            PlayerPrefs.SetInt("gfx_grain", FilmGrain ? 1 : 0);
            PlayerPrefs.SetInt("gfx_tex", TextureQuality);
            PlayerPrefs.SetInt("gfx_shadow", ShadowQuality);
            PlayerPrefs.SetInt("gfx_vsync", Vsync ? 1 : 0);
            PlayerPrefs.SetInt("gfx_msaa", Msaa);
            PlayerPrefs.SetInt("gfx_ssao", Ssao ? 1 : 0);
            PlayerPrefs.SetInt("gfx_fpscap", FpsCap);
            PlayerPrefs.SetFloat("gfx_fov", Fov);
            PlayerPrefs.SetFloat("gfx_treebb", TreeBillboardMul);
            PlayerPrefs.SetFloat("gfx_fognear", FogNearMul);
            PlayerPrefs.SetFloat("gfx_fogfar", FogFarMul);
            PlayerPrefs.SetFloat("gfx_grassdens", GrassDensityMul);
            PlayerPrefs.SetFloat("gfx_grassdist", GrassDistanceMul);
            PlayerPrefs.SetFloat("gfx_treedist", TreeDistanceMul);
            PlayerPrefs.SetFloat("gfx_view", ViewDistanceMul);
            PlayerPrefs.Save();
        }

        // ---- aplicar a los sistemas ----
        public static void Apply()
        {
            // Render scale
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.renderScale = Mathf.Clamp(RenderScale, 0.5f, 1f);
                urp.shadowDistance = ShadowQuality == 0 ? 0f : (ShadowQuality == 1 ? 18f : 45f);
                // MSAA en runtime (mismo patrón que renderScale: modifica el asset en
                // memoria, NO reimporta el archivo → no rompe referencias).
                urp.msaaSampleCount = Msaa == 2 ? 4 : (Msaa == 1 ? 2 : 1);
            }

            // SSAO (renderer feature) on/off en runtime
            SetSsao(Ssao);

            // Antialiasing + FOV en la cámara principal
            var cam = Camera.main;
            if (cam != null)
            {
                var d = cam.GetUniversalAdditionalCameraData();
                d.antialiasing = Antialiasing switch
                {
                    1 => AntialiasingMode.FastApproximateAntialiasing,
                    2 => AntialiasingMode.SubpixelMorphologicalAntiAliasing,
                    3 => AntialiasingMode.TemporalAntiAliasing,
                    _ => AntialiasingMode.None
                };
                d.antialiasingQuality = AntialiasingQuality.High;
                cam.fieldOfView = Mathf.Clamp(Fov, 60f, 100f);
            }

            // Post-procesado (motion blur / bloom / grain) en el/los Volume globales
            foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                if (v.profile == null) continue;
                if (v.profile.TryGet<MotionBlur>(out var mb)) mb.active = MotionBlur;
                if (v.profile.TryGet<Bloom>(out var bl)) bl.active = Bloom;
                if (v.profile.TryGet<FilmGrain>(out var fg)) fg.active = FilmGrain;
            }

            // Texturas (mip limit): 0=full, 1=media, 2=baja. Solo lo tocamos si CAMBIÓ:
            // cambiar el mip limit fuerza a Unity a recargar todas las texturas (stall),
            // así que evitamos ese recargo cuando ajustás otra opción cualquiera.
            int mip = Mathf.Clamp(TextureQuality, 0, 3);
            if (QualitySettings.globalTextureMipmapLimit != mip)
                QualitySettings.globalTextureMipmapLimit = mip;

            // Vsync + límite de FPS (vSync manda; si está ON, el cap se ignora)
            QualitySettings.vSyncCount = Vsync ? 1 : 0;
            Application.targetFrameRate = Vsync ? -1 : FpsCapValue();

            // Niebla / pasto / árboles / vista → los aplica el DayNightController
            // (que tiene los valores base por modo día/noche) con los multiplicadores.
            // Ese ApplyGraphics hace terrain.Flush() para que el pasto/árboles se
            // reconstruyan al instante (y no aparezcan de a poco al cambiar preset).
            var dnc = Object.FindFirstObjectByType<DayNightController>();
            if (dnc != null) dnc.ApplyGraphics();
        }

        static int FpsCapValue()
        {
            switch (FpsCap) { case 1: return 30; case 2: return 60; case 3: return 120; case 4: return 144; default: return -1; }
        }

        // Activa/desactiva la renderer feature de SSAO en runtime (por reflexión, porque
        // el URP asset no expone la lista de features públicamente). Modifica el objeto
        // en memoria, no el archivo → seguro. Silencioso si no encuentra la feature.
        static void SetSsao(bool on)
        {
            try
            {
                if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)) return;
                var f = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (f == null) return;
                if (!(f.GetValue(urp) is ScriptableRendererData[] list)) return;
                foreach (var data in list)
                {
                    if (data == null) continue;
                    foreach (var feat in data.rendererFeatures)
                    {
                        if (feat != null && feat.GetType().Name == "ScreenSpaceAmbientOcclusion")
                            feat.SetActive(on);
                    }
                }
            }
            catch { /* SSAO no disponible en este renderer: se ignora */ }
        }
    }
}
