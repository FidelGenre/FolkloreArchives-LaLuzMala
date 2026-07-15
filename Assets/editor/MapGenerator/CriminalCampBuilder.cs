// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  CriminalCampBuilder.cs — el campamento de los ladrones
//  (MainCriminalCamp). Más siniestro que el de los protagonistas:
//  4 ranchos de chapa oxidada medio caídos alrededor de una fogata,
//  con mugre/chatarra alrededor (colchones, botellas, un auto
//  abandonado, ruedas), una mesa al costado y un baño a lo lejos.
//
//  Ranchos: geometría procedural simple (no hay modelo de chapa
//  usable — el pack de shacks es .blend) texturizada con la CHAPA del
//  pack "Shacks/Shanties" (CC0) que eligió el owner → look de la foto.
//  Props (mesa, baño, chatarra): extraídos del FBX combinado
//  "Models pack psx" (Assets/ExternalAssets/ModelsPSX/models.fbx) por
//  NOMBRE de sub-objeto (Mesa, Baño, Auto, Colchon…), remapeando sus
//  materiales a URP con filtro Point.
// ============================================================
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class CriminalCampBuilder
    {
        const string TexDir  = "Assets/ExternalAssets/CriminalCampTex/";
        const string FbxPath = "Assets/ExternalAssets/ModelsPSX/models.fbx";

        // Cantidad de objetos que se registran para persistencia (IDs 0..PersistCount-1).
        // Debe coincidir con la cantidad de Reg(...) en Build (en su orden de creación).
        // ⚠ Si agregás/sacás/reordenás objetos, actualizá esto y re-guardá el layout.
        //   4 ranchos + 1 fogata + 4×(colchón+almohada)=8 + 1 baño (con inodoro/papel/
        //   botella/tacho ADENTRO como hijos) + 9 props sueltos + 2 ruedas + (2 ollas+
        //   sartén)=3 + 2 botellas + 2 mesas + 4 sillas = 36.
        public const int PersistCount = 36;

        static void Reg(GameObject go) => CriminalCampPersistence.Register(go);

        public static void Build(Transform camp, Terrain t, Vector2 c)
        {
            CriminalCampPersistence.Begin();   // carga overrides manuales guardados (si hay)

            var cube = BuilderUtils.PrimitiveMesh(PrimitiveType.Cube);

            // Chapa PS1 (filtro Point): techo oxidado rojizo (como la foto).
            // Techo: tinte OSCURO a propósito (chapa oxidada gastada); además de mate, el
            // albedo bajo evita que la luz de la fogata lo sobre-ilumine y "prenda" en
            // blanco por el bloom.
            // Paredes: gris claro (CCCCCC) elegido por el owner en el editor y horneado acá.
            // ⚠ Es bastante claro → si de noche "prende" con el bloom, bajar este gris.
            var roofMat = ChapaMat("crim_roof",  "ShedCorrugated", new Color(0.34f,  0.17f,  0.12f),  1.4f);
            var wallMat = ChapaMat("crim_wall",  "ShedPlate",      new Color(0.761f, 0.761f, 0.761f), 1.6f);

            // Escala del campamento: TODO ×S (posiciones y tamaños) MENOS los colchones,
            // que quedan de su tamaño original (pero reubicados adentro de los ranchos ×S).
            const float S = 1.6f;

            // ── 4 ranchos de chapa (offset dx,dz desde el centro, yaw, ancho W, prof D) ──
            var defs = new (float dx, float dz, float yaw, float W, float D)[]
            {
                (-7.5f, -5f,   18f, 4.4f, 3.0f),
                ( 6.5f, -6.5f,-35f, 3.6f, 2.8f),
                (-6f,    7.5f,205f, 4.8f, 3.2f),
                ( 7.5f,  6.5f,152f, 3.8f, 2.9f),
            };
            var shacks = new (float x, float z, float yaw, float W, float D)[defs.Length];
            for (int i = 0; i < defs.Length; i++)
            {
                var d = defs[i];
                float x = c.x + d.dx * S, z = c.y + d.dz * S, W = d.W * S, D = d.D * S;
                shacks[i] = (x, z, d.yaw, W, D);
                Reg(Shack(camp, t, cube, wallMat, roofMat, i.ToString(), x, z, d.yaw, W, D, S)); // ID 0..3
            }

            // ── Fogata en el medio: MISMO asset PS1 que el campamento de los protas ──
            Reg(Fire(camp, t, c, 0.55f * S));                                                    // ID 4

            // ── Props del FBX ──
            var master = FbxMaster();
            // un COLCHÓN adentro de cada rancho: POSICIÓN escalada con el rancho (para que
            // caiga adentro), pero TAMAÑO sin escalar (0.32) como pidió el owner.
            foreach (var s in shacks)   // IDs 5..12 (colchón + almohada por rancho)
            {
                Vector3 off = Quaternion.Euler(0f, s.yaw, 0f) * new Vector3(s.W * 0.5f, 0f, s.D * 0.55f);
                Reg(FbxProp(camp, t, master, "Colchon", s.x + off.x, s.z + off.z, s.yaw, 0.32f));
                // almohada en la cabecera (más adentro del rancho) → cama usada
                Vector3 ph = Quaternion.Euler(0f, s.yaw, 0f) * new Vector3(s.W * 0.5f, 0f, s.D * 0.78f);
                Reg(FbxProp(camp, t, master, "Almohada", s.x + ph.x, s.z + ph.z, s.yaw + 4f, 0.14f));
            }
            // ── Props VARIADOS del FBX PSX (cada objeto distinto, casi nada repetido).
            //    Nota: los nombres del FBX engañan, así que se eligieron por el MATERIAL real
            //    (ej. "Mesa" usa material de estante → NO se usa; las mesas van con PS1_Table). ──
            // Baño de chapa (letrina): estructura + inodoro/papel/botella/tacho ADENTRO (hijos
            // del grupo → se mueven todos juntos). Posición/rotación/tamaño donde lo dejó el owner.
            Vector2 oc = c + new Vector2(8.86f, -23.84f);
            const float OY = 244.4f;   // yaw (donde lo dejaste; giralo a mano si querés otra orientación)
            float ow = 1.5f * S, od = 1.58f * S, oh = 2.62f * S;   // ~1.5× (el tamaño que aprobaste)
            Reg(Outhouse(camp, t, cube, wallMat, roofMat, master, oc, OY, ow, od, oh));                   // 13 baño (estructura + interior)
            Reg(FbxProp(camp, t, master, "Auto",      c.x + 14f  * S, c.y + 10f  * S, 120f, 1.50f * S)); // 14 auto abandonado
            Reg(FbxProp(camp, t, master, "Litera",    c.x + 9.5f * S, c.y + 4.0f * S, 152f, 1.65f * S)); // 15 cucheta al lado de un rancho
            Reg(FbxProp(camp, t, master, "Carro_su",  c.x - 6.8f * S, c.y - 1.5f * S,  60f, 1.00f * S)); // 16 changuito con chatarra
            Reg(FbxProp(camp, t, master, "Microondas",c.x - 4.6f * S, c.y + 4.2f * S,  30f, 0.32f * S)); // 17 microondas afanado tirado
            Reg(FbxProp(camp, t, master, "Licuadora", c.x + 5.0f * S, c.y + 3.3f * S, -10f, 0.34f * S)); // 18 licuadora cerca de la mesa
            Reg(FbxProp(camp, t, master, "Puerta",    c.x + 2.3f * S, c.y - 5.6f * S, 100f, 1.10f * S)); // 19 puerta de auto (desarme)
            Reg(FbxProp(camp, t, master, "bote",      c.x + 5.6f * S, c.y - 3.6f * S,   0f, 0.60f * S)); // 20 tacho de basura
            Reg(FbxProp(camp, t, master, "Cardboard_02", c.x - 5.6f * S, c.y - 3.0f * S, 40f, 0.40f * S)); // 21 caja de cartón
            Reg(FbxProp(camp, t, master, "Papel",     c.x + 0.6f * S, c.y - 3.4f * S,  25f, 0.06f * S)); // 22 papeles tirados
            // ruedas sueltas (chatarra de auto)
            Reg(FbxProp(camp, t, master, "Rueda",     c.x - 3.5f * S, c.y - 8.0f * S,  30f, 0.55f * S)); // 23
            Reg(FbxProp(camp, t, master, "Rueda",     c.x + 6.6f * S, c.y - 5.8f * S,  75f, 0.50f * S)); // 24
            // cocinando en la fogata: ollas + sartén
            Reg(FbxProp(camp, t, master, "Olla",      c.x + 1.1f * S, c.y + 0.9f * S,  20f, 0.24f * S)); // 25
            Reg(FbxProp(camp, t, master, "Olla",      c.x - 1.2f * S, c.y + 0.6f * S, 200f, 0.20f * S)); // 26
            Reg(FbxProp(camp, t, master, "Sarten",    c.x + 0.3f * S, c.y - 1.3f * S,  50f, 0.12f * S)); // 27
            // botellas sueltas (tomando alrededor de las mesas)
            Reg(FbxProp(camp, t, master, "Botella",   c.x - 3.6f * S, c.y - 1.9f * S,   0f, 0.30f * S)); // 28
            Reg(FbxProp(camp, t, master, "Botella",   c.x + 4.0f * S, c.y + 1.8f * S,  15f, 0.28f * S)); // 29
            if (master != null) Object.DestroyImmediate(master.gameObject);

            // mesas y sillas DE VERDAD (pack PS1: PS1_Table / PS1_Chair) — posición ×S, tamaño ×S
            Reg(PS1KitchenProp(camp, t, "PS1_Table", c.x - 3.2f * S, c.y - 1.4f * S,  20f, 0.75f * S)); // 30
            Reg(PS1KitchenProp(camp, t, "PS1_Table", c.x + 4.3f * S, c.y + 2.4f * S, -40f, 0.75f * S)); // 31
            Reg(PS1KitchenProp(camp, t, "PS1_Chair", c.x - 2.2f * S, c.y - 2.8f * S,  10f, 0.9f * S));  // 32
            Reg(PS1KitchenProp(camp, t, "PS1_Chair", c.x - 5.0f * S, c.y - 0.6f * S, 160f, 0.9f * S));  // 33
            Reg(PS1KitchenProp(camp, t, "PS1_Chair", c.x + 5.8f * S, c.y + 3.6f * S, -30f, 0.9f * S));  // 34
            Reg(PS1KitchenProp(camp, t, "PS1_Chair", c.x + 3.2f * S, c.y + 1.2f * S, 150f, 0.9f * S));  // 35

            Debug.Log("<color=lime>Campamento de los ladrones construido en MainCriminalCamp.</color>");
        }

        // ── Rancho de chapa: caja de paredes con puerta abierta al frente + dos aguas ──
        static GameObject Shack(Transform parent, Terrain t, Mesh cube, Material wallMat, Material roofMat,
                          string id, float wx, float wz, float yaw, float W, float D, float sc)
        {
            float gy = t.SampleHeight(new Vector3(wx, 0f, wz));
            var g = BuilderUtils.Group(parent, "Shack_" + id, new Vector3(wx, gy, wz));
            g.rotation = Quaternion.Euler(Random.Range(-2f, 2f), yaw, Random.Range(-3f, 3f)); // decrépito/inclinado

            // TODAS las medidas escalan por sc (antes H/T/etc. estaban hardcodeadas → el
            // rancho se hacía más ancho pero NO más alto, y se veía "igual").
            float H = 2.3f * sc, T = 0.12f * sc, ov = 0.35f * sc;
            float ridgeY = H + 1.0f * sc, doorW = 1.2f * sc, doorC = W * 0.5f, doorH = 1.9f * sc;
            var wallCI = new List<CombineInstance>();
            var roofCI = new List<CombineInstance>();

            // paredes (caja); frente en z=0 con hueco de puerta
            Box(wallCI, cube, W * 0.5f, H * 0.5f, D,          W, H, T);   // fondo (z=D)
            Box(wallCI, cube, 0f,       H * 0.5f, D * 0.5f,   T, H, D);   // izq (x=0)
            Box(wallCI, cube, W,        H * 0.5f, D * 0.5f,   T, H, D);   // der (x=W)
            float dl = doorC - doorW * 0.5f, dr = doorC + doorW * 0.5f;
            if (dl > 0.02f)     Box(wallCI, cube, dl * 0.5f,      H * 0.5f, 0f, dl,     H, T);        // frente izq
            if (W - dr > 0.02f) Box(wallCI, cube, (dr + W) * 0.5f, H * 0.5f, 0f, W - dr, H, T);       // frente der
            Box(wallCI, cube, doorC, (doorH + H) * 0.5f, 0f, doorW, H - doorH, T);                    // dintel

            // techo a dos aguas: cumbrera en X a z=D/2, faldones a z=0 y z=D + hastiales
            AddSlope(roofCI, cube, -ov,      H - 0.05f * sc, D * 0.5f, ridgeY, W * 0.5f, W + 2f * ov); // faldón frente
            AddSlope(roofCI, cube, D + ov,   H - 0.05f * sc, D * 0.5f, ridgeY, W * 0.5f, W + 2f * ov); // faldón fondo
            // hastiales: triángulos de UNA cara con normal EXPLÍCITA (±X). Antes eran de
            // doble cara con RecalculateNormals → las normales se cancelaban a cero y el
            // hastial salía fullbright/blanco (igual día y noche). Ahora se ilumina bien.
            AddGable(wallCI, 0f, 0f, D, H, D * 0.5f, ridgeY, -1f);                                    // hastial izq (normal -X)
            AddGable(wallCI, W, 0f, D, H, D * 0.5f, ridgeY, +1f);                                      // hastial der (normal +X)

            AddMesh(g, "Shack_" + id + "_Walls", wallCI, wallMat, true);
            AddMesh(g, "Shack_" + id + "_Roof",  roofCI, roofMat, false);
            return g.gameObject;
        }

        // ── Baño de chapa (letrina): caja chica y alta de chapa, techo de una sola agua
        //    y una puerta de chapa abierta. Misma chapa que los ranchos. El inodoro/papel/
        //    botella/tacho se colocan aparte, adentro (ver Build). ──
        static GameObject Outhouse(Transform parent, Terrain t, Mesh cube, Material wallMat, Material roofMat,
                                   Transform master, Vector2 oc, float yaw, float W, float D, float H)
        {
            float gy = t.SampleHeight(new Vector3(oc.x, 0f, oc.y));
            var g = BuilderUtils.Group(parent, "Outhouse", new Vector3(oc.x, gy, oc.y));
            g.rotation = Quaternion.Euler(Random.Range(-1f, 1f), yaw, Random.Range(-1.5f, 1.5f)); // apenas torcido

            float T = 0.07f, ov = 0.2f;                                // chapa fina, poco alero
            float doorW = W * 0.82f, doorH = H * 0.9f, doorC = W * 0.5f;
            float frontY = H + 0.3f, backY = H;                        // techo de una sola agua, más alto adelante
            var wallCI = new List<CombineInstance>();
            var roofCI = new List<CombineInstance>();

            // paredes: fondo (z=D), izq (x=0), der (x=W), frente (z=0) con hueco de puerta
            Box(wallCI, cube, W * 0.5f, H * 0.5f, D,          W, H, T);
            Box(wallCI, cube, 0f,       H * 0.5f, D * 0.5f,   T, H, D);
            Box(wallCI, cube, W,        H * 0.5f, D * 0.5f,   T, H, D);
            float dl = doorC - doorW * 0.5f, dr = doorC + doorW * 0.5f;
            if (dl > 0.02f)     Box(wallCI, cube, dl * 0.5f,       H * 0.5f, 0f, dl,     H, T);
            if (W - dr > 0.02f) Box(wallCI, cube, (dr + W) * 0.5f, H * 0.5f, 0f, W - dr, H, T);
            Box(wallCI, cube, doorC, (doorH + H) * 0.5f, 0f, doorW, H - doorH, T);   // dintel

            // techo de una sola agua (más alto adelante que atrás)
            AddSlope(roofCI, cube, D + ov, backY - 0.04f, -ov, frontY, W * 0.5f, W + 2f * ov);
            // triángulos que tapan el hueco de la pendiente a los costados (una cara, normal ±X)
            AddGable(wallCI, 0f, 0f, D, H, 0f, frontY, -1f);
            AddGable(wallCI, W,  0f, D, H, 0f, frontY, +1f);

            // puerta de chapa abierta (~100°), bisagra en el borde izq del hueco
            Quaternion dRot = Quaternion.Euler(0f, -100f, 0f);
            Vector3 dCenter = new Vector3(dl, doorH * 0.5f, 0f) + dRot * new Vector3(doorW * 0.5f, 0f, 0f);
            wallCI.Add(CI(cube, dCenter, dRot, new Vector3(doorW, doorH, T)));

            AddMesh(g, "Outhouse_Walls", wallCI, wallMat, true);
            AddMesh(g, "Outhouse_Roof",  roofCI, roofMat, false);

            // ── Adentro: inodoro + papel + botella + tacho, como HIJOS del grupo del baño
            //    (así se mueven / rotan / escalan JUNTO con la estructura). Se colocan en
            //    mundo (apoyados en el piso) y se reparentan al grupo manteniendo la pose. ──
            Inside(parent, t, g, master, oc, yaw, new Vector2(W * 0.50f, D * 0.72f), 180f, 0.95f, "Baño");    // inodoro (usa su textura)
            Inside(parent, t, g, master, oc, yaw, new Vector2(W * 0.83f, D * 0.50f),   0f, 0.12f, "Papel");   // papel
            Inside(parent, t, g, master, oc, yaw, new Vector2(W * 0.28f, D * 0.26f),   0f, 0.30f, "Botella"); // botella
            Inside(parent, t, g, master, oc, yaw, new Vector2(W * 0.24f, D * 0.80f),   0f, 0.50f, "bote");    // tacho de lata
            return g.gameObject;
        }

        // Coloca un prop del FBX en coords LOCALES de una estructura (origen esquina + yaw),
        // apoyado en el piso, y lo REPARENTA al grupo → se mueve junto con la estructura.
        static void Inside(Transform camp, Terrain t, Transform g, Transform master, Vector2 oc, float yaw,
                           Vector2 local, float extraYaw, float targetH, string name)
        {
            Vector3 w = new Vector3(oc.x, 0f, oc.y) + Quaternion.Euler(0f, yaw, 0f) * new Vector3(local.x, 0f, local.y);
            var go = FbxProp(camp, t, master, name, w.x, w.z, yaw + extraYaw, targetH);
            if (go != null) go.transform.SetParent(g, true);   // mantiene la pose de mundo
        }

        // ── Fogata: modelo PS1 del pack del campamento (Campfire_Default) + luz cálida ──
        const string CampPS1Dir = "Assets/ExternalAssets/CampsitePS1/";
        static GameObject Fire(Transform parent, Terrain t, Vector2 c, float targetH)
        {
            var g = BuilderUtils.Group(parent, "Campfire", Ground(t, c));
            float gy = g.position.y;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CampPS1Dir + "Campfire_Default.fbx");
            if (prefab != null)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                inst.name = "CampfireModel";
                inst.transform.SetParent(g, false);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(CampPS1Dir + "Textures/CampfireBake.png");
                Material fm = tex != null
                    ? Matte(BuilderUtils.MatTextured("crim_campfire", tex, Color.white, 0f))
                    : BuilderUtils.Mat("crim_campfire", new Color(0.3f, 0.22f, 0.14f));
                foreach (var r in inst.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var outM = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                    for (int i = 0; i < outM.Length; i++) outM[i] = fm;
                    r.sharedMaterials = outM;
                }
                Vector3 s0 = inst.transform.localScale;
                Bounds b = PropBounds(inst);
                if (b.size.y > 0.001f) inst.transform.localScale = s0 * (targetH / b.size.y);
                inst.transform.position = new Vector3(c.x, gy, c.y);
                b = PropBounds(inst);
                inst.transform.position += Vector3.up * (gy - b.min.y);
            }
            else Debug.LogWarning("[CrimCamp] falta Campfire_Default.fbx (fogata sin modelo).");

            // luz cálida CONTENIDA (no supera el umbral de bloom → no lava la escena)
            var l = new GameObject("CampfireLight").AddComponent<Light>();
            l.transform.SetParent(g);
            l.transform.position = new Vector3(c.x, gy + 1.2f, c.y);
            l.type = LightType.Point; l.color = new Color(1f, 0.5f, 0.18f); l.intensity = 1.5f; l.range = 15f;
            return g.gameObject;
        }

        // ── Extracción de props del FBX combinado ─────────────────────────────
        static Transform FbxMaster()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (prefab == null) { Debug.LogWarning("[CrimCamp] falta el FBX: " + FbxPath); return null; }
            var m = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            m.SetActive(true);
            return m.transform;
        }

        static GameObject FbxProp(Transform camp, Terrain t, Transform master, string objName,
                            float wx, float wz, float yaw, float targetH)
        {
            if (master == null) return null;
            Transform src = FindByName(master, objName);
            if (src == null) { Debug.LogWarning("[CrimCamp] no encontré '" + objName + "' en el FBX (relleno salteado)."); return null; }

            Quaternion wRot = src.rotation;      // orientación mundial en el master (incluye eje del import)
            var go = (GameObject)Object.Instantiate(src.gameObject);
            go.name = "CProp_" + objName;
            go.transform.SetParent(camp, false);

            // materiales del FBX → URP Lit (con su textura) + Point
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                var sm = r.sharedMaterials;
                var outM = new Material[Mathf.Max(1, sm.Length)];
                for (int i = 0; i < outM.Length; i++) outM[i] = FbxMat(i < sm.Length ? sm[i] : null);
                r.sharedMaterials = outM;
            }

            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * wRot;
            Bounds b = PropBounds(go);
            if (b.size.y > 0.001f) go.transform.localScale *= targetH / b.size.y;

            float gy = t.SampleHeight(new Vector3(wx, 0f, wz));
            go.transform.position = new Vector3(wx, gy, wz);
            b = PropBounds(go);
            go.transform.position += Vector3.up * (gy - b.min.y);
            return go;
        }

        static Transform FindByName(Transform root, string name)
        {
            foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                if (tr.name == name) return tr;
            foreach (var tr in root.GetComponentsInChildren<Transform>(true))     // fallback: prefijo
                if (tr.name.StartsWith(name)) return tr;
            return null;
        }

        static readonly Dictionary<Texture, Material> _fbxMats = new Dictionary<Texture, Material>();
        static Material FbxMat(Material src)
        {
            var tex = src != null ? src.mainTexture as Texture2D : null;
            if (tex == null) return BuilderUtils.Mat("crimprop_untex", new Color(0.45f, 0.43f, 0.40f));
            if (_fbxMats.TryGetValue(tex, out var m)) return m;
            ForcePoint(AssetDatabase.GetAssetPath(tex));
            // tinte gris (no blanco): baja el albedo de props con textura clara para que
            // no "prendan" en blanco con el bloom (baño/colchón/etc. suelen ser claros).
            m = Matte(BuilderUtils.MatTextured("crimprop_" + tex.name, tex, new Color(0.62f, 0.60f, 0.56f), 0f));
            _fbxMats[tex] = m;
            return m;
        }

        static Bounds PropBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        // ── Silla / muebles del pack de cocina PS1 (atlas compartido) ─────────
        const string PS1KitDir = "Assets/ExternalAssets/HouseFurniture_PS1/";
        static GameObject PS1KitchenProp(Transform camp, Terrain t, string fbx, float wx, float wz, float yaw, float targetH)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PS1KitDir + fbx + ".fbx");
            if (prefab == null) { Debug.LogWarning("[CrimCamp] falta " + fbx + " (mueble salteado)."); return null; }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.name = "CProp_" + fbx;
            inst.transform.SetParent(camp, false);
            var m = PS1KitMat();
            foreach (var r in inst.GetComponentsInChildren<MeshRenderer>(true))
            {
                var outM = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < outM.Length; i++) outM[i] = m;
                r.sharedMaterials = outM;
            }
            Quaternion r0 = inst.transform.localRotation; Vector3 s0 = inst.transform.localScale;
            inst.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * r0;
            Bounds b = PropBounds(inst);
            if (b.size.y > 0.001f) inst.transform.localScale = s0 * (targetH / b.size.y);
            float gy = t.SampleHeight(new Vector3(wx, 0f, wz));
            inst.transform.position = new Vector3(wx, gy, wz);
            b = PropBounds(inst);
            inst.transform.position += Vector3.up * (gy - b.min.y);
            return inst;
        }

        static Material _ps1KitMat;
        static Material PS1KitMat()
        {
            if (_ps1KitMat != null) return _ps1KitMat;
            string tp = PS1KitDir + "stove_atlas.png";
            ForcePoint(tp);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(tp);
            _ps1KitMat = tex != null
                ? Matte(BuilderUtils.MatTextured("crim_ps1kit", tex, Color.white, 0f))
                : BuilderUtils.Mat("crim_ps1kit", new Color(0.5f, 0.45f, 0.4f));
            return _ps1KitMat;
        }

        // ── Materiales / texturas ─────────────────────────────────────────────
        static Material ChapaMat(string name, string tex, Color tint, float tiling)
        {
            string path = TexDir + tex + ".png";
            ForcePoint(path);
            var t2 = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Material m = t2 != null ? BuilderUtils.MatTextured(name, t2, tint, 0f) : BuilderUtils.Mat(name, tint, 0f);
            m.mainTextureScale = new Vector2(tiling, tiling);
            if (m.HasProperty("_BaseMap")) m.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
            return Matte(m);
        }

        // Mate total: sin specular ni reflejos de entorno. La chapa es metálica y
        // espejaba el cielo/luz → se prendía en blanco con el bloom (como las carpas).
        static Material Matte(Material m)
        {
            if (m.HasProperty("_Smoothness"))             m.SetFloat("_Smoothness", 0f);
            if (m.HasProperty("_Metallic"))               m.SetFloat("_Metallic", 0f);
            if (m.HasProperty("_SpecularHighlights"))     m.SetFloat("_SpecularHighlights", 0f);
            if (m.HasProperty("_EnvironmentReflections")) m.SetFloat("_EnvironmentReflections", 0f);
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            // matar emisión por las dudas (material cacheado)
            m.DisableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            return m;
        }

        static void ForcePoint(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return;
            if (AssetImporter.GetAtPath(assetPath) is TextureImporter ti &&
                (ti.filterMode != FilterMode.Point || ti.mipmapEnabled))
            {
                ti.filterMode = FilterMode.Point;
                ti.mipmapEnabled = false;
                ti.SaveAndReimport();
            }
        }

        // ── Helpers de geometría ──────────────────────────────────────────────
        static void Box(List<CombineInstance> ci, Mesh cube, float cx, float cy, float cz, float sx, float sy, float sz)
        {
            if (sx < 0.01f || sy < 0.01f || sz < 0.01f) return;
            ci.Add(CI(cube, new Vector3(cx, cy, cz), Quaternion.identity, new Vector3(sx, sy, sz)));
        }

        // Faldón inclinado en el plano Z-Y (cumbrera en X), extruido xw en X.
        static void AddSlope(List<CombineInstance> ci, Mesh cube, float zEave, float yEave,
                             float zRidge, float yRidge, float xc, float xw)
        {
            Vector2 lo = new Vector2(zEave, yEave), hi = new Vector2(zRidge, yRidge);
            Vector2 mid = (lo + hi) * 0.5f;
            float len = Vector2.Distance(lo, hi);
            float ang = Mathf.Atan2(hi.y - lo.y, hi.x - lo.x) * Mathf.Rad2Deg;
            ci.Add(CI(cube, new Vector3(xc, mid.y, mid.x), Quaternion.Euler(-ang, 0f, 0f),
                      new Vector3(xw, 0.1f, len)));
        }

        // Hastial: triángulo PLANO en x=xc con normal EXPLÍCITA (±X), una sola cara hacia
        // afuera. (No usar RecalculateNormals con doble cara: las normales se cancelan a
        // cero y el triángulo sale fullbright/blanco, igual de día que de noche.)
        static void AddGable(List<CombineInstance> ci, float xc, float z0, float z1,
                             float baseY, float apexZ, float apexY, float normalX)
        {
            var v  = new[] { new Vector3(xc, baseY, z0), new Vector3(xc, baseY, z1), new Vector3(xc, apexY, apexZ) };
            var n  = new[] { new Vector3(normalX, 0f, 0f), new Vector3(normalX, 0f, 0f), new Vector3(normalX, 0f, 0f) };
            var uv = new[] { new Vector2(z0, baseY), new Vector2(z1, baseY), new Vector2(apexZ, apexY) };
            int[] tris = normalX > 0f ? new[] { 0, 2, 1 } : new[] { 0, 1, 2 };   // winding hacia normalX
            var m = new Mesh { vertices = v, normals = n, uv = uv, triangles = tris };
            ci.Add(new CombineInstance { mesh = m, transform = Matrix4x4.identity });
        }

        static void AddMesh(Transform g, string name, List<CombineInstance> ci, Material m, bool collider)
        {
            var go = BuilderUtils.BuildCombinedStatic(g, name, ci, m, collider);
            if (go != null)
            {
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale    = Vector3.one;
            }
        }

        static Vector3 Ground(Terrain t, Vector2 p) => BuilderUtils.Ground(t, p.x, p.y);

        static CombineInstance CI(Mesh m, Vector3 pos, Quaternion rot, Vector3 scale) =>
            new CombineInstance { mesh = m, transform = Matrix4x4.TRS(pos, rot, scale) };
    }
}
