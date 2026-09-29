// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  SkillCheck.cs — minigame tipo "skill check" de Dead by Daylight
//  (owner: "tienen que tocar la E mientras gira el coso y van y vuelven
//  el coso y si erran hay que empezar de nuevo"). Un anillo con una zona
//  marcada; una aguja recorre el anillo yendo y volviendo. Apretás E
//  cuando la aguja está adentro de la zona = acierto. Errás = el progreso
//  vuelve a CERO. Cada acierto la zona se achica y la aguja acelera.
//
//  Uso desde una corrutina:  yield return SkillCheck.Run("TIRAR", 5);
//  (no termina hasta que el jugador complete todos los aciertos seguidos)
// ============================================================
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FolkloreArchives
{
    public class SkillCheck : MonoBehaviour
    {
        public static IEnumerator Run(string label, int hitsNeeded = 5)
        {
            var go = new GameObject("SkillCheck");
            var sc = go.AddComponent<SkillCheck>();
            sc._label = label;
            sc._need = Mathf.Max(1, hitsNeeded);
            while (sc != null && !sc._done) yield return null;
            // un respiro para que se vea el anillo completo en verde antes de cerrarlo
            yield return new WaitForSeconds(0.35f);
            if (go != null) Destroy(go);
        }

        const int   TexSize   = 256;
        const float RingOuter = 0.48f;   // radios relativos al tamaño de la textura
        const float RingInner = 0.40f;
        const float ProgOuter = 0.37f;   // anillo interior = progreso (verde, como la referencia)
        const float ProgInner = 0.33f;
        const float EdgeMargin = 12f;    // la zona nunca toca los extremos (0/360) donde rebota la aguja

        string _label;
        int _need, _hits;
        bool _done;

        float _angle = 0f;     // 0 = arriba, sentido horario, en grados; va y vuelve entre 0 y 360
        float _dir = 1f;
        float _speed;
        float _zoneStart, _zoneSize;

        float _flash;          // >0 = anillo teñido (verde acierto / rojo error)
        Color _flashCol;
        Texture2D _tex;
        bool _dirty = true;
        GUIStyle _labelStyle, _keyStyle;

        void Start() { NewZone(); }

        void OnDestroy() { if (_tex != null) Destroy(_tex); }

        void NewZone()
        {
            float t = _need > 1 ? _hits / (float)(_need - 1) : 0f;
            _zoneSize = Mathf.Lerp(55f, 30f, t);
            _speed    = Mathf.Lerp(170f, 300f, t);
            // la zona nueva va lejos de donde está la aguja ahora (si no, sería un acierto regalado)
            for (int i = 0; i < 20; i++)
            {
                _zoneStart = Random.Range(EdgeMargin, 360f - EdgeMargin - _zoneSize);
                float center = _zoneStart + _zoneSize * 0.5f;
                if (Mathf.Abs(center - _angle) > 90f) break;
            }
            _dirty = true;
        }

        bool InZone(float a) => a >= _zoneStart && a <= _zoneStart + _zoneSize;

        void Update()
        {
            if (_done) return;

            _angle += _dir * _speed * Time.deltaTime;
            if (_angle >= 360f) { _angle = 720f - _angle; _dir = -1f; }
            if (_angle <= 0f)   { _angle = -_angle;       _dir =  1f; }
            if (_flash > 0f) _flash -= Time.deltaTime;

            var kb = Keyboard.current;
            if (kb == null || !kb[Key.E].wasPressedThisFrame) return;

            if (InZone(_angle))
            {
                _hits++;
                _flash = 0.25f; _flashCol = new Color(0.3f, 1f, 0.3f);
                if (_hits >= _need) { _done = true; _dirty = true; return; }
            }
            else
            {
                _hits = 0;   // erraste: se empieza de nuevo
                _flash = 0.45f; _flashCol = new Color(1f, 0.25f, 0.2f);
            }
            NewZone();
        }

        // arma la textura del anillo (fondo + zona + progreso). Solo cuando cambia algo.
        void Rebuild()
        {
            if (_tex == null)
            {
                _tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false);
                _tex.wrapMode = TextureWrapMode.Clamp;
                _tex.filterMode = FilterMode.Bilinear;
            }
            var px = new Color32[TexSize * TexSize];
            float c = (TexSize - 1) * 0.5f;
            float progAngle = 360f * _hits / _need;
            Color ringCol = new Color(0.12f, 0.12f, 0.12f, 0.75f);
            Color zoneCol = new Color(0.95f, 0.95f, 0.95f, 0.95f);
            Color progCol = new Color(0.2f, 0.62f, 0.2f, 0.95f);
            Color progBg  = new Color(0.05f, 0.05f, 0.05f, 0.5f);

            for (int y = 0; y < TexSize; y++)
            for (int x = 0; x < TexSize; x++)
            {
                float dx = x - c, dy = y - c;   // y de la textura crece hacia ARRIBA en pantalla
                float r = Mathf.Sqrt(dx * dx + dy * dy) / TexSize;
                float a = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg;   // 0 = arriba, horario
                if (a < 0f) a += 360f;

                Color col = Color.clear;
                if (r >= RingInner && r <= RingOuter)
                {
                    col = (!_done && InZone(a)) ? zoneCol : (_done ? progCol : ringCol);
                    col.a *= Edge(r, RingInner, RingOuter);
                }
                else if (r >= ProgInner && r <= ProgOuter)
                {
                    col = a <= progAngle ? progCol : progBg;
                    col.a *= Edge(r, ProgInner, ProgOuter);
                }
                px[y * TexSize + x] = col;
            }
            _tex.SetPixels32(px);
            _tex.Apply(false);
            _dirty = false;
        }

        // borde suavizado (~1px) para que el anillo no quede serruchado
        static float Edge(float r, float inner, float outer)
        {
            float d = Mathf.Min(r - inner, outer - r) * TexSize;
            return Mathf.Clamp01(d);
        }

        void OnGUI()
        {
            if (_dirty) Rebuild();

            float size = Mathf.Round(Screen.height * 0.24f);
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var rect = new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size);

            Color prev = GUI.color;
            if (_flash > 0f) GUI.color = Color.Lerp(Color.white, _flashCol, 0.8f);
            GUI.DrawTexture(rect, _tex);
            GUI.color = prev;

            // aguja: de un poco adentro del anillo hasta el borde de afuera
            if (!_done)
            {
                Matrix4x4 m = GUI.matrix;
                GUIUtility.RotateAroundPivot(_angle, center);
                float len = size * (RingOuter - RingInner + 0.05f);
                GUI.color = new Color(0.9f, 0.1f, 0.1f);
                GUI.DrawTexture(new Rect(center.x - 2f, center.y - size * RingOuter - 2f, 4f, len), Texture2D.whiteTexture);
                GUI.color = prev;
                GUI.matrix = m;
            }

            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                _keyStyle   = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            }
            _labelStyle.fontSize = Mathf.Max(14, Mathf.RoundToInt(size * 0.13f));
            _keyStyle.fontSize   = Mathf.Max(11, Mathf.RoundToInt(size * 0.07f));
            _labelStyle.normal.textColor = Color.white;
            _keyStyle.normal.textColor = new Color(1f, 1f, 1f, 0.75f);
            GUI.Label(new Rect(center.x - size * 0.5f, center.y - size * 0.12f, size, size * 0.16f), _label, _labelStyle);
            GUI.Label(new Rect(center.x - size * 0.5f, center.y + size * 0.05f, size, size * 0.1f), $"[E]  {_hits}/{_need}", _keyStyle);
        }
    }
}
