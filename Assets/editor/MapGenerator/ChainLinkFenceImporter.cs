// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  ChainLinkFenceImporter.cs — configura las texturas del asset PSX Modular
//  Chain-Link Fence (DanglingBat, itch.io) al importarlas (SIN botón):
//   · filtro Point (pixelado PS1) + mipmaps.
//   · la malla (chainlink_diffuse+alpha) = alpha is transparency.
//   · la textura *_normal = tipo Normal map.
//  Los GLB se convirtieron a FBX con Blender (Assets/ExternalAssets/ChainLinkFence/
//  Models). Los materiales + la colocación del cerco los arma AreaPoiBuilder (YPF).
// ============================================================
using UnityEditor;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public class ChainLinkFenceImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            var p = assetPath.Replace('\\', '/');
            if (!p.Contains("/ChainLinkFence/Textures/")) return;
            var ti = (TextureImporter)assetImporter;
            if (p.Contains("_normal"))
            {
                ti.textureType = TextureImporterType.NormalMap;
            }
            else
            {
                ti.textureType = TextureImporterType.Default;
                ti.alphaIsTransparency = true; // la malla tiene alfa; evita halos en el recorte
            }
            ti.filterMode = FilterMode.Point;  // look PS1
            ti.mipmapEnabled = true;
        }
    }
}
