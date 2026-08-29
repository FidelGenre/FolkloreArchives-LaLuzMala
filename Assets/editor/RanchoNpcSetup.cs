// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  RanchoNpcSetup.cs — pone al VIEJO (esposo de la vieja) parado en
//  la puerta de la LETRINA del rancho, DESACTIVADO. La secuencia de
//  la misión lo activa cuando el jugador toca la puerta (susto).
//
//  Construye el modelo del MISMO pack que la vieja ("Characters PSX"
//  de Elbolilloduro, CC0): Character_32 (canoso, sweater gris + camisa
//  + jeans). Mismo tratamiento que OldLadyNpcBuilder: material URP/Lit
//  propio con la textura en filtro Point, escala a altura objetivo,
//  pies en el piso.
//
//  USO: poné la letrina donde va y corré
//       Folklore ▸ Poner viejo del rancho en la letrina.
// ============================================================
using UnityEngine;
using UnityEditor;

namespace FolkloreArchives.MapGen
{
    public static class RanchoNpcSetup
    {
        const string Fbx = "Assets/ExternalAssets/OldManNPC/Character_32.fbx";
        const string Tex = "Assets/ExternalAssets/OldManNPC/Character_32.png";
        const float  TargetHeight = 2.15f;  // hombre adulto (la vieja es 2.0; los amigos 2.2)
        const string SheepObj = "Assets/ExternalAssets/Sheep/sheep.obj";
        const string SheepTex = "Assets/ExternalAssets/Sheep/sheep_tex.jpg";
        const float  SheepHeight = 1.6f;    // altura de la oveja (owner: "más grandes")

        // el pack "Characters PSX" usa rig Mixamo (mixamorig:*): mismos limbs que el amigo
        // "green jacket". HumanWalkAnim con esto = camina + brazos a los lados (no T-pose).
        static readonly FolkloreArchives.HumanWalkAnim.Limb[] MixamoLimbs =
        {
            new FolkloreArchives.HumanWalkAnim.Limb { bone = "mixamorig:LeftUpLeg",  phase =  1f },
            new FolkloreArchives.HumanWalkAnim.Limb { bone = "mixamorig:RightUpLeg", phase = -1f },
            new FolkloreArchives.HumanWalkAnim.Limb { bone = "mixamorig:LeftArm",    phase = -1f },
            new FolkloreArchives.HumanWalkAnim.Limb { bone = "mixamorig:RightArm",   phase =  1f },
        };

        [MenuItem("Folklore/Poner viejo del rancho en la letrina")]
        static void PlaceOldMan()
        {
            AssetDatabase.Refresh();

            var door = FindByName("letrina.007");           // puerta
            var body = FindByName("letrina.006");           // cuerpo (para saber hacia dónde es "afuera")
            if (door == null)
            {
                EditorUtility.DisplayDialog("Rancho", "No encontré 'letrina.007' (la puerta) en la escena.", "OK");
                return;
            }

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            if (fbx == null)
            {
                EditorUtility.DisplayDialog("Rancho", "No encontré " + Fbx + " (¿lo importó Unity ya?).", "OK");
                return;
            }

            // dirección "hacia afuera": del cuerpo de la letrina hacia la puerta
            Vector3 doorPos = door.position;
            Vector3 outward = body != null ? (doorPos - body.position) : door.forward;
            outward.y = 0f;
            if (outward.sqrMagnitude < 1e-4f) outward = Vector3.forward;
            outward.Normalize();

            // si ya había uno, lo saco (idempotente)
            var prev = FindByName("RanchoViejo");
            if (prev != null) Object.DestroyImmediate(prev.gameObject);

            // raíz + modelo (igual criterio que OldLadyNpcBuilder)
            var go = new GameObject("RanchoViejo");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            model.name = "Model";
            model.transform.SetParent(go.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            var rends = model.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) { Object.DestroyImmediate(go); EditorUtility.DisplayDialog("Rancho", "El FBX del viejo no tiene Renderers.", "OK"); return; }

            // escala a la altura objetivo
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            float h = Mathf.Max(0.0001f, b.size.y);
            model.transform.localScale = Vector3.one * (TargetHeight / h);

            // replanta los pies en la base de la raíz (los bounds cambiaron con la escala)
            Bounds b2 = rends[0].bounds;
            foreach (var r in model.GetComponentsInChildren<Renderer>()) b2.Encapsulate(r.bounds);
            model.transform.localPosition = new Vector3(0f, -(b2.min.y - go.transform.position.y), 0f);

            // material URP propio (si no, el FBX trae Standard = magenta en URP)
            var tex = LoadPointTex(Tex);
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (tex != null && mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f);
            mat = BuilderUtils.SaveMaterialStable(mat, "Assets/Settings/PSX_RanchoViejo.mat");
            foreach (var r in rends)
            {
                var arr = new Material[r.sharedMaterials.Length];
                for (int k = 0; k < arr.Length; k++) arr[k] = mat;
                r.sharedMaterials = arr;
            }

            // colisión (cápsula parada) + animación procedural (camina + brazos abajo), igual amigos
            var col = go.AddComponent<CapsuleCollider>();
            col.height = TargetHeight; col.radius = TargetHeight * 0.16f; col.center = new Vector3(0f, TargetHeight * 0.5f, 0f);
            var anim = go.AddComponent<FolkloreArchives.HumanWalkAnim>();
            anim.limbs = MixamoLimbs;

            // la VIEJA (ya en la escena) también necesita la misma movilidad/pose
            EnsureMobility(FindByName("OldLady_Storyteller"));

            // pos: justo afuera de la puerta, apoyado en el piso (raycast al terreno)
            Vector3 p = doorPos + outward * 0.6f;
            if (Physics.Raycast(p + Vector3.up * 5f, Vector3.down, out var hit, 30f)) p.y = hit.point.y;
            else p.y = doorPos.y;
            go.transform.position = p;
            go.transform.rotation = Quaternion.LookRotation(outward, Vector3.up); // mira afuera (al jugador)

            go.SetActive(false);   // aparece cuando tocás la puerta (lo activa la secuencia)

            Undo.RegisterCreatedObjectUndo(go, "Poner viejo del rancho");
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
            Debug.Log("[Rancho] 'RanchoViejo' (Character_32) puesto DESACTIVADO en " + p +
                      ", mirando afuera de la letrina. Lo activa la secuencia al tocar la puerta.");
        }

        // agrega HumanWalkAnim (camina + brazos a los lados) si no lo tiene ya
        static void EnsureMobility(Transform npc)
        {
            if (npc == null) return;
            if (npc.GetComponent<FolkloreArchives.HumanWalkAnim>() == null)
            {
                var a = npc.gameObject.AddComponent<FolkloreArchives.HumanWalkAnim>();
                a.limbs = MixamoLimbs;
                Debug.Log("[Rancho] HumanWalkAnim agregado a " + npc.name);
            }
        }

        // Arma una TRANQUERA abrible a partir del Cube.184 seleccionado (que es combined mesh y
        // no se puede rotar). Lee su AABB/material, crea una réplica-plank con bisagra en un
        // extremo (eje Y) + CorralGate, y DESACTIVA el original.
        [MenuItem("Folklore/Armar tranquera del corral (abrible)")]
        static void BuildGate()
        {
            var sel = Selection.activeGameObject;
            if (sel == null) { EditorUtility.DisplayDialog("Tranquera", "Seleccioná primero la puerta del corral (Cube.184).", "OK"); return; }
            var rend = sel.GetComponent<Renderer>();
            if (rend == null) { EditorUtility.DisplayDialog("Tranquera", "El objeto seleccionado no tiene Renderer.", "OK"); return; }

            Bounds wb = rend.bounds;                 // AABB en mundo (la puerta)
            Vector3 c = wb.center, s = wb.size;
            bool longX = s.x >= s.z;                 // eje largo horizontal = a lo largo de la tranquera
            float length = longX ? s.x : s.z;
            Vector3 longDir = longX ? Vector3.right : Vector3.forward;
            Vector3 hinge = c - longDir * (length * 0.5f);   // bisagra en un EXTREMO

            var mat = rend.sharedMaterial;

            var prev = FindByName("TranqueraCorral");
            if (prev != null) Object.DestroyImmediate(prev.gameObject);

            var pivot = new GameObject("TranqueraCorral");
            pivot.transform.position = hinge;
            pivot.transform.rotation = Quaternion.identity;

            var plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plank.name = "Plank";
            plank.transform.SetParent(pivot.transform, true);
            plank.transform.position = c;
            plank.transform.rotation = Quaternion.identity;
            plank.transform.localScale = s;          // pivot con escala 1 -> tamaño mundo = s
            if (mat != null) plank.GetComponent<Renderer>().sharedMaterial = mat;

            pivot.AddComponent<FolkloreArchives.CorralGate>();

            sel.SetActive(false);   // ocultamos la puerta combined original

            Undo.RegisterCreatedObjectUndo(pivot, "Armar tranquera");
            Selection.activeGameObject = pivot;
            EditorGUIUtility.PingObject(pivot);
            Debug.Log("[Rancho] 'TranqueraCorral' armada (bisagra en un extremo, eje Y). Original " +
                      sel.name + " desactivado. Ajustá openDeg (+/-) si abre para el lado equivocado.");
        }

        // pone N ovejas (sheep.obj) en un cluster cerca de la tranquera. La secuencia las
        // mueve al pastizal cuando abrís la tranquera. Grupo "Ovejas" con hijos "Oveja_i".
        [MenuItem("Folklore/Poner ovejas en el corral")]
        static void PlaceSheep()
        {
            AssetDatabase.Refresh();
            var obj = AssetDatabase.LoadAssetAtPath<GameObject>(SheepObj);
            if (obj == null) { EditorUtility.DisplayDialog("Ovejas", "No encontré " + SheepObj + " (¿lo importó Unity?).", "OK"); return; }

            var prev = FindByName("Ovejas");
            if (prev != null) Object.DestroyImmediate(prev.gameObject);
            var group = new GameObject("Ovejas");

            // punto de spawn del rebaño (owner TEST_PLAYER, adentro del corral)
            Vector3 baseP = new Vector3(108.905f, 26.75688f, 154.3876f);

            // material URP con la textura (Point = PSX)
            var tex = LoadPointTex(SheepTex);
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (tex != null && mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f);
            mat = BuilderUtils.SaveMaterialStable(mat, "Assets/Settings/PSX_Oveja.mat");

            const int N = 4;
            for (int i = 0; i < N; i++)
            {
                var s = BuildSheep(obj, mat, i);
                s.transform.SetParent(group.transform);
                float ang = i * 90f * Mathf.Deg2Rad;
                Vector3 p = baseP + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (1.0f + i * 0.25f);
                if (Physics.Raycast(p + Vector3.up * 5f, Vector3.down, out var hit, 30f)) p.y = hit.point.y;
                s.transform.position = p;
                s.transform.rotation = Quaternion.Euler(0f, i * 63f, 0f);
            }
            Undo.RegisterCreatedObjectUndo(group, "Poner ovejas");
            Selection.activeGameObject = group;
            EditorGUIUtility.PingObject(group);
            Debug.Log("[Rancho] " + N + " ovejas puestas en 'Ovejas' cerca del corral. Movelas si hace falta.");
        }

        static GameObject BuildSheep(GameObject obj, Material mat, int idx)
        {
            var go = new GameObject("Oveja_" + idx);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(obj);
            model.name = "Model";
            model.transform.SetParent(go.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            var rends = model.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                float h = Mathf.Max(0.0001f, b.size.y);
                model.transform.localScale = Vector3.one * (SheepHeight / h);
                Bounds b2 = rends[0].bounds;
                foreach (var r in model.GetComponentsInChildren<Renderer>()) b2.Encapsulate(r.bounds);
                model.transform.localPosition = new Vector3(0f, -(b2.min.y - go.transform.position.y), 0f);
                foreach (var r in rends)
                {
                    var arr = new Material[r.sharedMaterials.Length];
                    for (int k = 0; k < arr.Length; k++) arr[k] = mat;
                    r.sharedMaterials = arr;
                }
            }
            var col = go.AddComponent<CapsuleCollider>();
            col.height = SheepHeight; col.radius = SheepHeight * 0.4f; col.center = new Vector3(0f, SheepHeight * 0.5f, 0f);
            return go;
        }

        static Texture2D LoadPointTex(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null && imp.filterMode != FilterMode.Point)
            {
                imp.filterMode = FilterMode.Point;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Transform FindByName(string name)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t != null && t.name == name) return t;
            return null;
        }
    }
}
