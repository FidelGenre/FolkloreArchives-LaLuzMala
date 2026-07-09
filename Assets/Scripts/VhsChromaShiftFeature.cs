// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  VhsChromaShiftFeature.cs — ScriptableRendererFeature that runs
//  VhsChromaShift.shader as a fullscreen pass AFTER post-processing.
//  Gives a UNIFORM VHS RGB-split across the whole screen (unlike
//  URP's built-in ChromaticAberration, which is radial/edges-only),
//  plus optional scanlines and per-line jitter.
//
//  HOW TO ENABLE (one click, no manual wiring):
//    Assets/Settings/PC_Renderer  ->  Inspector  ->  Add Renderer Feature
//      ->  "Vhs Chroma Shift Feature".  Tune the sliders there.
//  (Repeat on Mobile_Renderer too if you build for that quality tier.)
// ============================================================
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

namespace FolkloreArchives
{
    public class VhsChromaShiftFeature : ScriptableRendererFeature
    {
        [Tooltip("Horizontal R/B channel offset, same across the whole screen (UV units). 0.003-0.006 is a nice VHS.")]
        [Range(0f, 0.02f)] public float chromaOffset = 0.004f;

        [Tooltip("Darkening of the scanlines. 0 = off.")]
        [Range(0f, 1f)] public float scanlineStrength = 0.12f;

        [Tooltip("Number of scanlines (higher = finer lines).")]
        [Range(200f, 2400f)] public float scanlineCount = 1400f;

        [Tooltip("Per-scanline horizontal wobble/jitter (UV units). 0 = stable image.")]
        [Range(0f, 0.01f)] public float jitter = 0.0015f;

        [Tooltip("PSX: niveles de color por canal (bandas retro). 0/1 = OFF. 24-40 = look PS1/PS2.")]
        [Range(0f, 64f)] public float posterizeLevels = 32f;

        [Tooltip("PSX: fuerza del dither Bayer (patrón de puntitos que suaviza las bandas). 0 = off.")]
        [Range(0f, 1.5f)] public float ditherStrength = 0.8f;

        [Tooltip("When to run, relative to the rest of the frame.")]
        public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;

        Material _material;
        VhsPass _pass;

        public override void Create()
        {
            var shader = Shader.Find("Hidden/Folklore/VhsChromaShift");
            if (shader == null)
            {
                Debug.LogWarning("VhsChromaShiftFeature: shader 'Hidden/Folklore/VhsChromaShift' not found - is VhsChromaShift.shader in the project?");
                return;
            }
            _material = CoreUtils.CreateEngineMaterial(shader);
            _pass = new VhsPass(_material) { renderPassEvent = injectionPoint };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_material == null) return;
            // don't run on preview/reflection cameras
            var camType = renderingData.cameraData.cameraType;
            if (camType == CameraType.Preview || camType == CameraType.Reflection) return;

            _material.SetFloat("_ChromaOffset", chromaOffset);
            _material.SetFloat("_ScanlineStrength", scanlineStrength);
            _material.SetFloat("_ScanlineCount", scanlineCount);
            _material.SetFloat("_Jitter", jitter);
            _material.SetFloat("_PosterizeLevels", posterizeLevels);
            _material.SetFloat("_DitherStrength", ditherStrength);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
        }

        class VhsPass : ScriptableRenderPass
        {
            readonly Material _mat;

            public VhsPass(Material mat)
            {
                _mat = mat;
                // we sample the camera colour while writing it, so we need URP to give
                // us a real intermediate texture (not the raw backbuffer)
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resourceData = frameData.Get<UniversalResourceData>();
                var source = resourceData.activeColorTexture;

                var destDesc = renderGraph.GetTextureDesc(source);
                destDesc.name = "VhsChromaShift";
                destDesc.clearBuffer = false;
                destDesc.depthBufferBits = 0;
                var dest = renderGraph.CreateTexture(destDesc);

                var blitParams = new RenderGraphUtils.BlitMaterialParameters(source, dest, _mat, 0);
                renderGraph.AddBlitPass(blitParams, "VHS ChromaShift");

                // make the shifted result the camera colour for whatever comes next
                resourceData.cameraColor = dest;
            }
        }
    }
}
