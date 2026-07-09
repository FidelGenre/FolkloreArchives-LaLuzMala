// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  BackdropCamera.cs — cámara de FONDO para el anillo de montañas.
//  Dibuja SOLO las montañas (capa "Backdrop") detrás del mundo,
//  con distancia enorme y SIN niebla, así se ven en el horizonte
//  sobre el cielo aunque la niebla del juego tape lo cercano.
//  La cámara principal (padre) dibuja el mundo encima con su niebla.
//  Va en un GameObject HIJO de la cámara principal.
// ============================================================
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FolkloreArchives
{
    [RequireComponent(typeof(Camera))]
    public class BackdropCamera : MonoBehaviour
    {
        Camera bg, main;
        bool fogWas, ready;

        void Start()
        {
            bg = GetComponent<Camera>();
            main = transform.parent != null ? transform.parent.GetComponent<Camera>() : null;
            if (bg == null || main == null) { enabled = false; return; }

            int layer = LayerMask.NameToLayer("Backdrop");
            if (layer < 0) layer = 6;
            int mask = 1 << layer;

            // FONDO: solo montañas, limpia con skybox, lejísimo, se dibuja PRIMERO.
            bg.clearFlags   = CameraClearFlags.Skybox;
            bg.cullingMask  = mask;
            bg.nearClipPlane = 1f;
            bg.farClipPlane  = 9000f;
            bg.depth        = main.depth - 1; // antes que la principal
            bg.tag          = "Untagged";
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            var bgData = bg.GetUniversalAdditionalCameraData();
            if (bgData != null) { bgData.renderType = CameraRenderType.Base; bgData.renderPostProcessing = false; }

            // PRINCIPAL: NO dibuja las montañas y NO limpia el color (deja ver el fondo).
            main.cullingMask &= ~mask;
            main.clearFlags   = CameraClearFlags.Depth;

            ready = true;
        }

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBegin;
            RenderPipelineManager.endCameraRendering   += OnEnd;
        }
        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBegin;
            RenderPipelineManager.endCameraRendering   -= OnEnd;
        }

        // niebla OFF solo mientras se dibuja la cámara de fondo, ON de nuevo después.
        void OnBegin(ScriptableRenderContext ctx, Camera cam)
        {
            if (ready && cam == bg) { fogWas = RenderSettings.fog; RenderSettings.fog = false; }
        }
        void OnEnd(ScriptableRenderContext ctx, Camera cam)
        {
            if (ready && cam == bg) RenderSettings.fog = fogWas;
        }
    }
}
