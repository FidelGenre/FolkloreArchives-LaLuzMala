// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  VhsOverlay.cs — STEADY VHS scanlines (no flicker). The moving
//  grain / chromatic aberration / colour grade live in VhsPostFx;
//  this just adds fixed CRT scanlines on top for the strong VHS
//  tape look, without the distracting flickering the owner disliked.
// ============================================================
using UnityEngine;

namespace FolkloreArchives
{
    public class VhsOverlay : MonoBehaviour
    {
        [Range(0f, 1f)] public float scanlineStrength = 0.28f;
        public int scanlinePeriod = 3; // pixels per scanline pair

        Texture2D _scanline;

        void Awake()
        {
            // a small vertical strip: dark line then clear lines, tiled down the screen
            int h = Mathf.Max(2, scanlinePeriod);
            _scanline = new Texture2D(1, h, TextureFormat.RGBA32, false)
            { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
            for (int y = 0; y < h; y++)
                _scanline.SetPixel(0, y, new Color(0f, 0f, 0f, y == 0 ? 1f : 0f));
            _scanline.Apply();
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            float sw = Screen.width, sh = Screen.height;

            // steady scanlines (no per-frame randomness -> no flicker)
            GUI.color = new Color(1f, 1f, 1f, scanlineStrength);
            GUI.DrawTextureWithTexCoords(new Rect(0, 0, sw, sh), _scanline,
                new Rect(0f, 0f, 1f, sh / _scanline.height));
            GUI.color = Color.white;
        }
    }
}
