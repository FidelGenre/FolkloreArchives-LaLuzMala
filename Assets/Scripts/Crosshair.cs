// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  Crosshair.cs — círculo fijo en el centro de la cámara para apuntar
//  interacciones (owner: "como en el fears" -- Fears to Fathom usa un
//  punto/anillo sutil siempre visible en el medio de la pantalla).
//  Vive en la CÁMARA (no en MapExplorer/PlayerVehicleInteractor) para
//  que siga dibujándose en todos los modos -- MapExplorer se
//  deshabilita al manejar, pero la cámara nunca.
// ============================================================
using UnityEngine;

namespace FolkloreArchives
{
    public class Crosshair : MonoBehaviour
    {
        public float diameter = 8f;
        public float thickness = 1.5f;
        public Color color = new Color(1f, 1f, 1f, 0.65f);

        Texture2D ringTex;

        void Awake()
        {
            int size = Mathf.CeilToInt(diameter) + 4;
            ringTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            ringTex.filterMode = FilterMode.Bilinear;
            ringTex.wrapMode = TextureWrapMode.Clamp;

            float r = diameter * 0.5f;
            Vector2 c = new Vector2(size * 0.5f, size * 0.5f);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                    float edge = Mathf.Abs(d - r);
                    float a = Mathf.InverseLerp(thickness * 0.5f + 1f, thickness * 0.5f, edge);
                    pixels[y * size + x] = new Color(color.r, color.g, color.b, color.a * Mathf.Clamp01(a));
                }
            }
            ringTex.SetPixels(pixels);
            ringTex.Apply();
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || ringTex == null) return;
            float size = ringTex.width;
            var rect = new Rect(Screen.width * 0.5f - size * 0.5f, Screen.height * 0.5f - size * 0.5f, size, size);
            GUI.DrawTexture(rect, ringTex);
        }
    }
}
