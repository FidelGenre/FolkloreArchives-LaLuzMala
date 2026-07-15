// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  CampsiteBuilder.cs — dressing del campamento del jugador (Campsite),
//  inspirado en un camping real de la Patagonia (Lago Queñi):
//  fogata central, TRONCOS-asiento caídos alrededor, pila de leña,
//  CARPAS atrás, mesa. SIN autos (a pedido del owner).
//
//  Carpas + fogata + bolsas de dormir = modelos PS1 reales del pack CC0
//  "Retro/Demolished Campground Environment" de 3Dexter3D
//  (Assets/ExternalAssets/CampsitePS1/). El pack trae texturas pero los
//  .mtl apuntan a rutas absolutas del autor, así que se asignan los
//  materiales por código (una URP por textura, filtro Point + mate).
//  Los troncos-asiento, la leña y la mesa son geometría procedural
//  texturizada (quedaron bien y el pack no las trae).
//  Se llama desde LandmarkBuilder.
// ============================================================
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class CampsiteBuilder
    {
        const string CampDir    = "Assets/ExternalAssets/CampsitePS1/";
        const string CampTexDir = CampDir + "Textures/";

        public static void Build(Transform camp, Terrain t, Vector2 c)
        {
            _regId = 0;   // reinicia el contador del layout horneado

            var bark     = MatTex("camp_bark",     BarkTex(),     new Color(0.52f, 0.37f, 0.23f), 3f);
            var barkGrey = MatTex("camp_bark_grey",BarkTex(),     new Color(0.60f, 0.55f, 0.48f), 3f); // tronco pelado/seco
            var charcoal = MatTex("camp_charcoal", CharcoalTex(), Color.white,                    1f);
            var fire     = BuilderUtils.Mat("camp_fire", new Color(1f, 0.55f, 0.18f), 3.5f);           // brasa emisiva

            // El ORDEN define el ID estable (0..8) de cada objeto para guardar/restaurar
            // ediciones manuales (Tools > Save Campsite Layout). No reordenar sin re-guardar.
            Reg(FirePit(camp, t, c, charcoal, fire));                                        // 0 fogata
            Reg(HLog(camp, bark,     Ground(t, c.x,        c.y - 2.5f), 3.4f, 0.36f, 90f));  // 1 tronco sur
            Reg(HLog(camp, barkGrey, Ground(t, c.x - 2.6f, c.y + 0.1f), 3.0f, 0.34f,  0f));  // 2 tronco oeste
            Reg(HLog(camp, bark,     Ground(t, c.x + 2.6f, c.y - 0.2f), 3.0f, 0.34f,  8f));  // 3 tronco este
            Reg(Firewood(camp, t, bark, c.x - 3.2f, c.y - 2.6f));                            // 4 leña

            var poles = Tex("Poles");
            Reg(PS1Tent(camp, t, "Tents_Orange",   Tex("Tent_Orange"),   poles, c.x - 3.9f, c.y + 5.2f, -18f, 1.45f)); // 5
            Reg(PS1Tent(camp, t, "Tents_Green",    Tex("Tent_Green"),    poles, c.x + 0.2f, c.y + 6.1f,   6f, 1.45f)); // 6
            Reg(PS1Tent(camp, t, "Tents_DarkBlue", Tex("Tent_DarkBlue"), poles, c.x + 4.3f, c.y + 4.9f,  22f, 1.45f)); // 7

            Reg(PicnicTable(camp, t, bark, c.x + 5.6f, c.y - 0.6f, 90f));                     // 8 mesa
        }

        // La persistencia del campamento de los protagonistas se sacó (menú Save/Clear).
        // Para NO perder el layout que el owner ajustó a mano, esas transforms se
        // HORNEARON acá: Reg() le aplica a cada objeto (en su orden de creación = su ID)
        // la posición/rotación/escala local final que tenía guardada. Es fijo, sin JSON.
        // ⚠ Si reordenás/agregás objetos arriba, actualizá esta tabla en el mismo orden.
        static int _regId;
        static readonly (Vector3 pos, Vector3 euler, Vector3 scale)[] BakedLayout =
        {
            // 0 Campfire
            (new Vector3(0f, 0f, 0f),               new Vector3(0f, 0f, 0f),               new Vector3(1f, 1f, 1f)),
            // 1 LogSeat sur
            (new Vector3(0f, 0.3602352f, -2.5f),    new Vector3(0f, 0f, 270f),             new Vector3(0.72f, 1.7f, 0.72f)),
            // 2 LogSeat oeste
            (new Vector3(-2.600006f, 0.3376131f, 0.1000061f), new Vector3(90f, 0f, 0f),    new Vector3(0.68f, 1.5f, 0.68f)),
            // 3 LogSeat este
            (new Vector3(2.64f, 0.3402357f, 0.1f),  new Vector3(82.00004f, 277.9999f, 269.9999f), new Vector3(0.68f, 2.1135f, 0.68f)),
            // 4 Firewood
            (new Vector3(-3.200012f, 0.000123f, -2.600006f), new Vector3(0f, 0f, 0f),      new Vector3(1f, 1f, 1f)),
            // 5 Tents_Orange
            (new Vector3(-5.33f, 0.5432062f, 4.98f),new Vector3(0f, 349.8486f, 0f),        new Vector3(2f, 2f, 2f)),
            // 6 Tents_Green
            (new Vector3(4.220001f, 0.5453339f, 5.940002f), new Vector3(0f, 65.82005f, 0f),new Vector3(2f, 2f, 2f)),
            // 7 Tents_DarkBlue
            (new Vector3(2.25f, 0.5453339f, -7.12f),new Vector3(0f, 191.0853f, 0f),        new Vector3(2f, 2f, 2f)),
            // 8 PicnicTable
            (new Vector3(6.84f, 0.0002356f, -1.57f),new Vector3(0f, 99.76956f, 0f),        new Vector3(1.5f, 1.5f, 1.5f)),
        };

        static void Reg(GameObject go)
        {
            int id = _regId++;
            if (go == null || id >= BakedLayout.Length) return;
            var b = BakedLayout[id];
            go.transform.localPosition    = b.pos;
            go.transform.localEulerAngles = b.euler;
            go.transform.localScale       = b.scale;
        }

        // ── Fogata: modelo PS1 + disco de ceniza + brasa emisiva + luz ────────
        static GameObject FirePit(Transform camp, Terrain t, Vector2 c, Material charcoal, Material fire)
        {
            // grupo "Campfire": se mantiene el nombre porque "tocarla = muerte" (guion)
            var g = BuilderUtils.Group(camp, "Campfire", Ground(t, c));
            float gy = g.position.y;

            // disco de ceniza/carbón bajo la fogata (suelo quemado)
            BuilderUtils.Prim(PrimitiveType.Cylinder, "Ash", g,
                new Vector3(c.x, gy + 0.02f, c.y), new Vector3(2.2f, 0.04f, 2.2f), charcoal);

            // modelo PS1 de la fogata (leños + piedras horneados en un atlas), a la
            // escala fija que eligió el owner (150) — el modelo es hijo del grupo, así
            // que su tamaño no lo guarda la persistencia (que guarda el grupo id 0).
            PS1Prop(g, t, "Campfire_Default", new[] { Tex("CampfireBake") }, c.x, c.y, 0f, 0f,
                    fixedScale: new Vector3(150f, 150f, 150f));

            // brasa emisiva (glow) + luz cálida
            BuilderUtils.Prim(PrimitiveType.Sphere, "Ember", g,
                new Vector3(c.x, gy + 0.28f, c.y), new Vector3(0.7f, 0.45f, 0.7f), fire);
            var l = new GameObject("CampfireLight").AddComponent<Light>();
            l.transform.SetParent(g);
            l.transform.position = new Vector3(c.x, gy + 0.9f, c.y);
            l.type = LightType.Point;
            l.color = new Color(1f, 0.55f, 0.2f);
            l.intensity = 2.6f;
            l.range = 14f;

            // fuego low-poly estilo PS1 (partículas billboard, textura crunchy + aditivo)
            AddFireParticles(g, new Vector3(c.x, gy + 0.1f, c.y));
            return g.gameObject;
        }

        // ── Partículas de fuego PS1 (billboards que suben, crunchy + aditivo) ──
        static void AddFireParticles(Transform parent, Vector3 worldPos)
        {
            var go = new GameObject("FireParticles");
            go.transform.SetParent(parent);
            go.transform.position = worldPos;
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.loop = true;
            main.startLifetime  = new ParticleSystem.MinMaxCurve(0.45f, 0.95f);
            main.startSpeed     = new ParticleSystem.MinMaxCurve(0.7f, 1.4f);
            main.startSize      = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
            main.startColor     = new Color(1f, 0.8f, 0.35f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.gravityModifier = -0.04f;   // suben un poco
            main.maxParticles   = 40;

            var em = ps.emission;
            em.rateOverTime = 22f;

            var sh = ps.shape;
            sh.enabled   = true;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle     = 13f;
            sh.radius    = 0.28f;
            sh.rotation  = new Vector3(-90f, 0f, 0f);   // cono apuntando hacia arriba (+Y)

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.95f, 0.5f),  0f),
                    new GradientColorKey(new Color(1f, 0.55f, 0.15f), 0.45f),
                    new GradientColorKey(new Color(0.7f, 0.14f, 0.03f), 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.18f),
                    new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f),
                });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 0.55f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.15f)));

            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.material   = FireParticleMat();
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            rend.sortingFudge = -3f;
        }

        static Material FireParticleMat()
        {
            string path = MapLayout.GeneratedFolder + "/mat_camp_fireparticle.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var tex = FireParticleTex();
            if (m == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (sh == null) sh = Shader.Find("Sprites/Default");
                m = new Material(sh);
                AssetDatabase.CreateAsset(m, path);
            }
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            m.mainTexture = tex;
            m.color = Color.white;
            // transparente + aditivo (glow de fuego)
            if (m.HasProperty("_Surface"))  m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend"))    m.SetFloat("_Blend", 2f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            if (m.HasProperty("_ZWrite"))   m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3100;
            return m;
        }

        // Textura de llama PS1: radial naranja→transparente, alpha cuantizado (crunch).
        static Texture2D FireParticleTex() => MakeTex("camp_fireparticle", 32, (x, y) =>
        {
            float dx = (x + 0.5f) / 32f - 0.5f, dy = (y + 0.5f) / 32f - 0.5f;
            float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;      // 0 centro .. 1 borde
            float a = Mathf.Clamp01(1f - d); a *= a;
            a = Mathf.Round(a * 4f) / 4f;                      // escalones PS1
            return new Color(1f, 0.72f, 0.32f, a);
        });

        // ── Coloca un modelo PS1 del pack (prop de una pieza, p.ej. la fogata):
        //    asigna una URP por textura a cada submalla y lo asienta en el piso.
        //    texBySub: 1 textura = a todas las submallas; N = por índice de submalla.
        static void PS1Prop(Transform parent, Terrain t, string fbx, Texture2D[] texBySub,
                            float wx, float wz, float yaw, float targetH, Vector3 fixedScale = default)
        {
            var inst = InstProp(parent, fbx);
            if (inst == null) return;
            foreach (var r in inst.GetComponentsInChildren<MeshRenderer>(true))
            {
                int n = Mathf.Max(1, r.sharedMaterials.Length);
                var outM = new Material[n];
                for (int i = 0; i < n; i++)
                    outM[i] = CampTexMat(texBySub[Mathf.Min(i, texBySub.Length - 1)]);
                r.sharedMaterials = outM;
            }
            SeatProp(inst, t, wx, wz, yaw, targetH, fixedScale);
        }

        // ── Carpa PS1: cada FBX del pack trae 5 carpas → recorta a UNA (1 base +
        //    la barra de soporte más cercana), textura la lona vs los palos por
        //    nombre de sub-objeto, y la asienta. ──
        static GameObject PS1Tent(Transform parent, Terrain t, string fbx, Texture2D bodyTex,
                            Texture2D poleTex, float wx, float wz, float yaw, float targetH)
        {
            var inst = InstProp(parent, fbx);
            if (inst == null) return null;
            CropToOneTent(inst);
            var bodyM = CampTexMat(bodyTex);
            var poleM = CampTexMat(poleTex);
            foreach (var r in inst.GetComponentsInChildren<MeshRenderer>(true))
            {
                var m = IsBar(r.name) ? poleM : bodyM;
                var outM = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < outM.Length; i++) outM[i] = m;
                r.sharedMaterials = outM;
            }
            SeatProp(inst, t, wx, wz, yaw, targetH);
            return inst;
        }

        static GameObject InstProp(Transform parent, string fbx)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CampDir + fbx + ".fbx");
            if (prefab == null) { Debug.LogWarning("[Campsite] falta modelo PS1: " + fbx); return null; }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.name = fbx;
            inst.transform.SetParent(parent, false);   // conserva la transform de import
            return inst;
        }

        // Escala a la altura objetivo (preservando la rotación/escala de import) + yaw,
        // y asienta la base en el piso.
        static void SeatProp(GameObject inst, Terrain t, float wx, float wz, float yaw, float targetH,
                             Vector3 fixedScale = default)
        {
            Quaternion r0 = inst.transform.localRotation;
            Vector3    s0 = inst.transform.localScale;
            inst.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * r0;
            if (fixedScale != default(Vector3))
                inst.transform.localScale = fixedScale;                    // escala fija (a mano)
            else
            {
                Bounds bb = PropBounds(inst);
                if (bb.size.y > 0.001f) inst.transform.localScale = s0 * (targetH / bb.size.y);
            }

            float gy = t.SampleHeight(new Vector3(wx, 0f, wz));
            inst.transform.position = new Vector3(wx, gy, wz);
            Bounds b = PropBounds(inst);
            inst.transform.position += Vector3.up * (gy - b.min.y);
        }

        static bool IsBar(string n) =>
            n.IndexOf("Support", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("Bar", System.StringComparison.OrdinalIgnoreCase) >= 0;

        // Deja una sola carpa: la 1ª base (lona) + la barra de soporte más cercana,
        // destruye el resto, y recentra en XZ al origen del root para poder ubicarla.
        static void CropToOneTent(GameObject inst)
        {
            var rends = new List<MeshRenderer>(inst.GetComponentsInChildren<MeshRenderer>(true));
            if (rends.Count <= 2) return;   // ya es una sola pieza (o body+bar)

            MeshRenderer keepBase = null;
            foreach (var r in rends) if (!IsBar(r.name)) { keepBase = r; break; }
            if (keepBase == null) keepBase = rends[0];

            Vector3 bc = keepBase.bounds.center;
            MeshRenderer keepBar = null; float best = float.MaxValue;
            foreach (var r in rends)
            {
                if (r == keepBase || !IsBar(r.name)) continue;
                float d = (r.bounds.center - bc).sqrMagnitude;
                if (d < best) { best = d; keepBar = r; }
            }

            var keep = new HashSet<Transform> { keepBase.transform };
            if (keepBar != null) keep.Add(keepBar.transform);
            foreach (var r in rends)
                if (!keep.Contains(r.transform)) Object.DestroyImmediate(r.gameObject);

            // recentrar en XZ: llevar el centro de la carpa al XZ del root
            Bounds bb = keepBase.bounds;
            if (keepBar != null) bb.Encapsulate(keepBar.bounds);
            Vector3 rootPos = inst.transform.position;
            Vector3 shift = new Vector3(bb.center.x - rootPos.x, 0f, bb.center.z - rootPos.z);
            keepBase.transform.position -= shift;
            if (keepBar != null) keepBar.transform.position -= shift;
        }

        static Bounds PropBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        // Un material URP por textura del pack: Point + mate (sin specular ni reflejos,
        // así las lonas no arman el halo blanco de noche). Cacheado por textura.
        static readonly Dictionary<Texture2D, Material> _campMats = new Dictionary<Texture2D, Material>();
        static Material CampTexMat(Texture2D tex)
        {
            if (tex == null) return BuilderUtils.Mat("campps1_missing", new Color(0.6f, 0.55f, 0.5f));
            if (_campMats.TryGetValue(tex, out var cached)) return cached;
            ForcePoint(tex);
            var m = BuilderUtils.MatTextured("campps1_" + tex.name, tex, Color.white, 0f);
            if (m.HasProperty("_Smoothness"))             m.SetFloat("_Smoothness", 0f);
            if (m.HasProperty("_SpecularHighlights"))     m.SetFloat("_SpecularHighlights", 0f);
            if (m.HasProperty("_EnvironmentReflections")) m.SetFloat("_EnvironmentReflections", 0f);
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            _campMats[tex] = m;
            return m;
        }

        static Texture2D Tex(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>(CampTexDir + n + ".png");

        // Fuerza el import de una textura del pack a Point + sin mipmaps (crunch PS1).
        static void ForcePoint(Texture2D tex)
        {
            string p = AssetDatabase.GetAssetPath(tex);
            if (AssetImporter.GetAtPath(p) is TextureImporter ti &&
                (ti.filterMode != FilterMode.Point || ti.mipmapEnabled))
            {
                ti.filterMode = FilterMode.Point;
                ti.mipmapEnabled = false;
                ti.SaveAndReimport();
            }
        }

        // ── Un tronco horizontal (cilindro) apoyado en el piso ────────────────
        static GameObject HLog(Transform parent, Material mat, Vector3 ground, float len, float r, float yawDeg)
        {
            Vector3 dir = Quaternion.Euler(0f, yawDeg, 0f) * Vector3.forward;
            var go = BuilderUtils.Prim(PrimitiveType.Cylinder, "LogSeat", parent,
                ground + Vector3.up * r, new Vector3(r * 2f, len * 0.5f, r * 2f), mat);
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir);
            return go;
        }

        // ── Pila de leña ──────────────────────────────────────────────────────
        static GameObject Firewood(Transform parent, Terrain t, Material mat, float wx, float wz)
        {
            float gy = t.SampleHeight(new Vector3(wx, 0f, wz));
            var g = BuilderUtils.Group(parent, "Firewood", new Vector3(wx, gy, wz));
            for (int i = 0; i < 3; i++)
                StackLog(g, mat, wx - 0.35f + i * 0.35f, gy + 0.13f, wz, 1.3f, 0.13f, 90f);
            for (int i = 0; i < 2; i++)
                StackLog(g, mat, wx - 0.17f + i * 0.35f, gy + 0.39f, wz, 1.3f, 0.13f, 90f);
            StackLog(g, mat, wx + 0.5f, gy + 0.06f, wz + 0.6f, 1.0f, 0.05f, 60f);
            StackLog(g, mat, wx - 0.5f, gy + 0.06f, wz - 0.5f, 0.9f, 0.05f, 110f);
            return g.gameObject;
        }

        static void StackLog(Transform parent, Material mat, float x, float y, float z,
                             float len, float r, float yawDeg)
        {
            Vector3 dir = Quaternion.Euler(0f, yawDeg, 0f) * Vector3.forward;
            var go = BuilderUtils.Prim(PrimitiveType.Cylinder, "Log", parent,
                new Vector3(x, y, z), new Vector3(r * 2f, len * 0.5f, r * 2f), mat);
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir);
        }

        // ── Mesa de camping rústica (tablón + 2 bancos + patas) ───────────────
        static GameObject PicnicTable(Transform parent, Terrain t, Material mat, float wx, float wz, float yaw)
        {
            float gy = t.SampleHeight(new Vector3(wx, 0f, wz));
            var g = BuilderUtils.Group(parent, "PicnicTable", new Vector3(wx, gy, wz));
            g.rotation = Quaternion.Euler(0f, yaw, 0f);
            Quaternion q = g.rotation;
            void Box(Vector3 local, Vector3 scale)
            {
                var go = BuilderUtils.Prim(PrimitiveType.Cube, "Part", g, g.position + q * local, scale, mat);
                go.transform.rotation = q;
            }
            Box(new Vector3(0f, 0.74f, 0f),      new Vector3(1.7f, 0.08f, 0.8f));   // tablón
            Box(new Vector3(0f, 0.44f, 0.62f),   new Vector3(1.7f, 0.06f, 0.28f));  // banco +
            Box(new Vector3(0f, 0.44f, -0.62f),  new Vector3(1.7f, 0.06f, 0.28f));  // banco -
            foreach (float sx in new[] { -0.75f, 0.75f })
                foreach (float sz in new[] { -0.32f, 0.32f })
                    Box(new Vector3(sx, 0.37f, sz), new Vector3(0.08f, 0.74f, 0.08f)); // patas
            return g.gameObject;
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        static Vector3 Ground(Terrain t, Vector2 p) => BuilderUtils.Ground(t, p.x, p.y);
        static Vector3 Ground(Terrain t, float x, float z) => BuilderUtils.Ground(t, x, z);

        static Material MatTex(string name, Texture2D tex, Color tint, float tiling)
        {
            var m = BuilderUtils.MatTextured(name, tex, tint, 0f);
            m.mainTextureScale = new Vector2(tiling, tiling);
            if (m.HasProperty("_BaseMap")) m.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
            // mate: sin specular ni reflejos (evita brillos raros de noche)
            if (m.HasProperty("_Smoothness"))             m.SetFloat("_Smoothness", 0f);
            if (m.HasProperty("_SpecularHighlights"))     m.SetFloat("_SpecularHighlights", 0f);
            if (m.HasProperty("_EnvironmentReflections")) m.SetFloat("_EnvironmentReflections", 0f);
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            return m;
        }

        // ── Texturas procedurales low-res + filtro Point (crunch PS1) ─────────
        static Texture2D MakeTex(string name, int S, System.Func<int, int, Color> f)
        {
            string path = MapLayout.GeneratedFolder + "/tex_" + name + ".asset";
            var ex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (ex != null) return ex;
            var tx = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                    px[y * S + x] = f(x, y);
            tx.SetPixels(px); tx.Apply();
            AssetDatabase.CreateAsset(tx, path);
            return tx;
        }

        static Texture2D BarkTex() => MakeTex("camp_bark", 64, (x, y) =>
        {
            float groove = Mathf.Sin(x * 0.9f) * 0.5f + 0.5f;
            float n = Mathf.PerlinNoise(x * 0.35f, y * 0.13f);
            float g = 0.62f + 0.20f * (n - 0.5f);
            g *= 0.72f + 0.28f * groove;
            if ((x * 7 + y) % 23 == 0) g *= 0.7f;
            return new Color(Mathf.Clamp01(g), Mathf.Clamp01(g), Mathf.Clamp01(g), 1f);
        });

        static Texture2D CharcoalTex() => MakeTex("camp_charcoal", 64, (x, y) =>
        {
            float n = Mathf.PerlinNoise(x * 0.4f + 11f, y * 0.4f + 5f);
            float g = 0.06f + 0.14f * n;
            if (n > 0.86f) return new Color(0.5f + g, 0.18f + g, 0.05f, 1f); // brasa
            return new Color(g, g * 0.95f, g * 0.9f, 1f);
        });
    }
}
