// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  SettingsMenu.cs — menú de pausa/opciones in-game (Esc).
//  Presets (Baja/Media/Alta/Ultra/Personalizado) + todas las
//  opciones gráficas individuales, con scroll. Se aplican en vivo
//  y se guardan. Pausa el juego mientras está abierto.
// ============================================================
using UnityEngine;
using UnityEngine.InputSystem;

namespace FolkloreArchives
{
    public class SettingsMenu : MonoBehaviour
    {
        public static bool IsOpen;   // MapExplorer lo lee para frenar el control del jugador

        Vector2 _scroll;
        GUIStyle _title, _label, _labelC, _btn, _btnSel, _btnMini;

        void Start()
        {
            GameSettings.Load();
            GameSettings.Apply();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Toggle();
        }

        void Toggle()
        {
            IsOpen = !IsOpen;
            Time.timeScale = IsOpen ? 0f : 1f;
            Cursor.lockState = IsOpen ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = IsOpen;
        }

        void OnGUI()
        {
            if (!IsOpen) return;
            EnsureStyles();

            float w = 480f, h = 470f;
            float x = (Screen.width - w) * 0.5f, y = (Screen.height - h) * 0.5f;

            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = new Color(0.08f, 0.09f, 0.11f, 0.97f);
            GUI.DrawTexture(new Rect(x - 22, y - 22, w + 44, h + 44), Texture2D.whiteTexture);
            GUI.color = prev;

            GUILayout.BeginArea(new Rect(x, y, w, h));
            GUILayout.Label("OPCIONES GRÁFICAS", _title);

            // ---- presets ----
            GUILayout.BeginHorizontal();
            PresetBtn(QualityPreset.Baja); PresetBtn(QualityPreset.Media);
            PresetBtn(QualityPreset.Alta); PresetBtn(QualityPreset.Ultra);
            GUILayout.EndHorizontal();
            GUILayout.Label(GameSettings.Preset == QualityPreset.Personalizado ? "Preset: Personalizado" : "Preset: " + GameSettings.Preset, _labelC);
            GUILayout.Space(6);

            // ---- opciones individuales (scroll) ----
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(300));
            bool ch = false;
            ch |= Slider("Resolución interna", ref GameSettings.RenderScale, 0.5f, 1f, "");
            ch |= Cycle("Antialiasing", ref GameSettings.Antialiasing, new[] { "Off", "FXAA", "SMAA", "TAA" });
            ch |= Cycle("MSAA", ref GameSettings.Msaa, new[] { "Off", "2x", "4x" });
            ch |= Toggle("SSAO (oclusión ambiental)", ref GameSettings.Ssao);
            ch |= Toggle("Motion Blur", ref GameSettings.MotionBlur);
            ch |= Toggle("Bloom", ref GameSettings.Bloom);
            ch |= Toggle("Grano de película", ref GameSettings.FilmGrain);
            ch |= Cycle("Texturas", ref GameSettings.TextureQuality, new[] { "Full", "Media", "Baja" });
            ch |= Cycle("Sombras", ref GameSettings.ShadowQuality, new[] { "Off", "Baja", "Alta" });
            ch |= Toggle("V-Sync", ref GameSettings.Vsync);
            ch |= Cycle("Límite de FPS", ref GameSettings.FpsCap, new[] { "Sin límite", "30", "60", "120", "144" });
            ch |= Slider("Campo de visión (FOV)", ref GameSettings.Fov, 60f, 100f, "°");
            GUILayout.Space(6);
            ch |= Slider("Distancia billboard árboles", ref GameSettings.TreeBillboardMul, 0.4f, 2f, "x");
            ch |= Slider("Niebla cercana", ref GameSettings.FogNearMul, 0.5f, 1.5f, "x");
            ch |= Slider("Niebla lejana", ref GameSettings.FogFarMul, 0.5f, 1.5f, "x");
            ch |= Slider("Densidad de pasto", ref GameSettings.GrassDensityMul, 0.2f, 1.5f, "x");
            ch |= Slider("Distancia de pasto", ref GameSettings.GrassDistanceMul, 0.4f, 1.5f, "x");
            ch |= Slider("Distancia de árboles", ref GameSettings.TreeDistanceMul, 0.4f, 1.5f, "x");
            ch |= Slider("Distancia de vista", ref GameSettings.ViewDistanceMul, 0.5f, 1.5f, "x");
            GUILayout.EndScrollView();

            if (ch)
            {
                GameSettings.Preset = QualityPreset.Personalizado;
                GameSettings.Apply();
                GameSettings.Save();
            }

            GUILayout.Space(8);
            if (GUILayout.Button("Volver al juego", _btn, GUILayout.Height(38))) Toggle();
            GUILayout.EndArea();
        }

        void PresetBtn(QualityPreset p)
        {
            var style = GameSettings.Preset == p ? _btnSel : _btn;
            if (GUILayout.Button(p.ToString(), style, GUILayout.Height(32)))
            {
                GameSettings.ApplyPreset(p);
                GameSettings.Apply();
                GameSettings.Save();
            }
        }

        bool Slider(string label, ref float val, float min, float max, string suffix)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _label, GUILayout.Width(190));
            float nv = GUILayout.HorizontalSlider(val, min, max, GUILayout.Width(160));
            GUILayout.Label(nv.ToString("0.00") + suffix, _labelC, GUILayout.Width(55));
            GUILayout.EndHorizontal();
            if (!Mathf.Approximately(nv, val)) { val = nv; return true; }
            return false;
        }

        bool Cycle(string label, ref int val, string[] opts)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _label, GUILayout.Width(190));
            int nv = val;
            if (GUILayout.Button("◄", _btnMini, GUILayout.Width(30))) nv = (val - 1 + opts.Length) % opts.Length;
            GUILayout.Label(opts[Mathf.Clamp(val, 0, opts.Length - 1)], _labelC, GUILayout.Width(95));
            if (GUILayout.Button("►", _btnMini, GUILayout.Width(30))) nv = (val + 1) % opts.Length;
            GUILayout.EndHorizontal();
            if (nv != val) { val = nv; return true; }
            return false;
        }

        bool Toggle(string label, ref bool val)
        {
            bool nv = GUILayout.Toggle(val, "  " + label, _label, GUILayout.Height(26));
            if (nv != val) { val = nv; return true; }
            return false;
        }

        void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _title.normal.textColor = new Color(0.85f, 0.78f, 0.55f);
            _label = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            _label.normal.textColor = new Color(0.9f, 0.88f, 0.82f);
            _labelC = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter };
            _btn = new GUIStyle(GUI.skin.button) { fontSize = 14 };
            _btnSel = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold };
            _btnSel.normal.textColor = new Color(0.95f, 0.82f, 0.35f);
            _btnMini = new GUIStyle(GUI.skin.button) { fontSize = 13 };
        }
    }
}
