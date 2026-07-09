// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  VhsPostFx.cs — look "video de celular berreta de los 2000"
//  (NO VHS): imagen blanda/borrosa por lente barata, LEVEMENTE
//  distorsionada, con algo de ruido y viñeta suave. Colores
//  casi normales (no lavados a blanco, sin scanlines ni RGB split).
//
//  La blandura/baja-res la da el renderScale del PC_RPAsset (0.65).
//  El RGB split + scanlines VHS quedaron apagados en PC_Renderer.
//  Backup del look anterior: _ConfigBackups/vhs_2026-07-07/RESTORE.txt
// ============================================================
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FolkloreArchives
{
    public class VhsPostFx : MonoBehaviour
    {
        void Start()
        {
            VolumeProfile profile = null;
            foreach (var v in FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                if (v.isGlobal && v.profile != null) { profile = v.profile; break; }
            }
            if (profile == null)
            {
                var go = new GameObject("VHS_PostFX_Volume");
                var vol = go.AddComponent<Volume>();
                vol.isGlobal = true;
                vol.priority = 100f;
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                vol.sharedProfile = profile;
            }

            // ── Split toning CÁLIDO (FtF viejo/amarillento): altas ámbar, sombras
            //    apenas cálidas → tinte de video viejo.
            var split = AddOrGet<SplitToning>(profile);
            split.balance.Override(12f);
            split.shadows.Override(new Color(0.42f, 0.40f, 0.34f));
            split.highlights.Override(new Color(0.85f, 0.72f, 0.42f));

            // ── Distorsión de lente leve (lente barata de celular) ───────────────
            //  Es el efecto principal que pediste: "levemente distorsionada".
            //  intensity positivo = barril (imagen abombada). Si se ve pellizcada
            //  hacia adentro, poné el valor en negativo.
            var lens = AddOrGet<LensDistortion>(profile);
            lens.intensity.Override(0.22f);
            lens.scale.Override(1.0f);

            // ── Aberración cromática SUTIL solo en los bordes (radial, no RGB split)
            AddOrGet<ChromaticAberration>(profile).intensity.Override(0.24f); // fringe VHS un toque más

            // ── Motion blur sutil (toque cinematográfico, sin marear) ────────────
            var mb = AddOrGet<MotionBlur>(profile);
            mb.mode.Override(MotionBlurMode.CameraOnly);
            mb.intensity.Override(0.18f);
            mb.quality.Override(MotionBlurQuality.Medium);

            // ── Bloom apagado (el halo era lo que más lavaba a blanco) ───────────
            var bloom = AddOrGet<Bloom>(profile);
            bloom.threshold.Override(1.1f);   // FtF: glow suave (el "compositor pass with bloom")
            bloom.intensity.Override(0.22f);
            bloom.scatter.Override(0.6f);
            bloom.tint.Override(new Color(1f, 0.93f, 0.78f)); // halo cálido

            // ── Color: bajo la exposición y COMPRIMO las altas (contraste negativo)
            //    para que el cielo/niebla dejen de reventar a blanco.
            var color = AddOrGet<ColorAdjustments>(profile);
            color.saturation.Override(-12f);       // más lavado/viejo
            color.contrast.Override(-4f);
            color.postExposure.Override(-0.42f);   // oscuro pero navegable (equilibrio)
            color.colorFilter.Override(new Color(1.0f, 0.93f, 0.78f)); // tinte ÁMBAR/amarillento

            // ── Tirar los BLANCOS hacia abajo específicamente (cielo/niebla) sin
            //    oscurecer tanto las sombras: baja la ganancia de las altas.
            var lgg = AddOrGet<LiftGammaGain>(profile);
            lgg.lift.Override(new Vector4(1f, 1f, 1f, 0f));    // sin lift (ya no hay posterizado que aplaste)
            lgg.gamma.Override(new Vector4(1f, 1f, 1f, 0f));
            lgg.gain.Override(new Vector4(1f, 1f, 1f, -0.18f));  // altas un poco más bajas

            // ── Balance de blancos apenas cálido (casi neutro) ───────────────────
            var wb = AddOrGet<WhiteBalance>(profile);
            wb.temperature.Override(22f);  // cálido → amarillo/viejo
            wb.tint.Override(6f);          // levísimo verde → VHS "enfermizo"

            // ── Ruido fino de sensor barato (no el grano grueso de cinta) ─────────
            var grain = AddOrGet<FilmGrain>(profile);
            grain.type.Override(FilmGrainLookup.Thin1);   // grano fino y sutil (no estática)
            grain.intensity.Override(0.18f);
            grain.response.Override(0.75f);

            // ── Viñeta (un poco más marcada para el look viejo) ──────────────────
            var vig = AddOrGet<Vignette>(profile);
            vig.intensity.Override(0.3f);
            vig.smoothness.Override(0.7f);

            Debug.Log("<color=cyan>Look Fears-to-Fathom (VHS cálido/amarillento) aplicado.</color> " +
                      "Post Processing ON en el Game view.");
        }

        static T AddOrGet<T>(VolumeProfile p) where T : VolumeComponent
        {
            if (p.TryGet<T>(out var existing)) { existing.active = true; return existing; }
            return p.Add<T>(true);
        }

        static void Disable<T>(VolumeProfile p) where T : VolumeComponent
        {
            if (p.TryGet<T>(out var existing)) existing.active = false;
        }
    }
}
