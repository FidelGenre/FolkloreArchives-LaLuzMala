// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  ChickenCoopBuilder.cs — animales/props de la granja (casa de la vieja). Owner los
//  fue pidiendo: gallinero + 4 gallinas, después chancho + caballo.
//  Gallinero: "Chicken Coop (Free)" by wolfgar74 (Sketchfab, CC-BY).
//  Gallinas: "PS1 Chicken" by honungsbi8. Chancho: "PS1 Pig". Caballo: "Cavalo PS1".
//  Todos GLB. Ver ASSET_CREDITS.md.
//
//  Los dos son GLB (malla + textura + material embebidos → Unity los importa a URP
//  solos, no hace falta cablear material). Se instancian bajo FOLKLORE_MAP con NOMBRE
//  ÚNICO y ANTES de ApplySavedLayout (se llama desde HouseBuilder.BuildBarn, que corre
//  antes) → quedan cubiertos por "Save Map Layout": el owner los mueve/rota/borra a
//  mano y persisten al regenerar (mismo criterio que la PC/silla de la YPF).
//
//  Ubicación base cerca de la granja (OldLadyHouseCenter). El owner la ajusta a mano.
// ============================================================
using UnityEditor;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class ChickenCoopBuilder
    {
        const string CoopGlb    = "Assets/ExternalAssets/ChickenFarm/chicken_coop_free.glb";
        const string ChickenGlb = "Assets/ExternalAssets/ChickenFarm/ps1_chicken.glb";
        const string PigGlb     = "Assets/ExternalAssets/ChickenFarm/ps1_pig.glb";
        const string HorseGlb   = "Assets/ExternalAssets/ChickenFarm/cavalo_ps1.glb";
        const float CoopHeight    = 2.2f;   // alto objetivo del gallinero (m)
        const float ChickenHeight = 0.38f;  // alto objetivo de cada gallina (m)
        const float PigHeight     = 0.85f;  // alto objetivo del chancho (m)
        const float HorseHeight   = 1.6f;   // alto objetivo del caballo (m)
        const float CoopYaw       = 20f;    // orientación base del gallinero (el owner la ajusta)
        // El GLB del caballo viene ACOSTADO de costado (su alto real 1.71 está en X, no en Y).
        // Lo paro rotando sobre Z. Si saliera PATAS ARRIBA, cambiar -90 → 90 (roll opuesto).
        const float HorseRollDeg  = -90f;

        // Posiciones base cerca de la granja (movibles con Save Map Layout).
        static readonly Vector2 CoopSpot  = new Vector2(195f, 170f);
        static readonly Vector2 PigSpot   = new Vector2(191f, 174f);
        static readonly Vector2 HorseSpot = new Vector2(201f, 173f);

        public static void Build(Transform parent, Terrain terrain)
        {
            var coopPrefab    = AssetDatabase.LoadAssetAtPath<GameObject>(CoopGlb);
            var chickenPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChickenGlb);
            if (coopPrefab == null && chickenPrefab == null)
            {
                Debug.LogWarning("[Gallinero] no encontré los GLB en Assets/ExternalAssets/ChickenFarm/ " +
                                 "(¿Unity terminó de importarlos?). No pongo el gallinero.");
                return;
            }

            var group = BuilderUtils.Group(parent, "ChickenCoop", Vector3.zero);

            // clips legacy en loop (uno por especie) para animar cada animal
            var chickenClip = chickenPrefab != null ? MakeLegacyClip(ChickenGlb, "anim_chicken") : null;

            if (coopPrefab != null)
                Place(group, coopPrefab, "Coop", terrain, CoopSpot, CoopYaw, CoopHeight, addCollider: true);

            // 4 gallinas alrededor del gallinero: offsets/yaws FIJOS (deterministas, no random)
            // para que su posición sea estable entre Generates hasta que el owner las mueva.
            if (chickenPrefab != null)
            {
                Vector2[] offs = { new Vector2( 2.2f, -1.4f), new Vector2(-1.8f, -2.2f),
                                   new Vector2( 1.0f,  2.4f), new Vector2(-2.4f,  0.9f) };
                float[]   yaws = { 35f, 160f, 250f, 300f };
                for (int i = 0; i < 4; i++)
                {
                    var ch = Place(group, chickenPrefab, "Chicken_" + i, terrain, CoopSpot + offs[i], yaws[i], ChickenHeight, false);
                    AttachAnim(ch, chickenClip, i * 0.5f);   // desfasaje: no picotean todas iguales
                }
            }

            // Chancho + caballo (mismos assets de granja). Van como hermanos sueltos bajo el mapa
            // (nombres únicos "Pig"/"Horse") → también los cubre Save Map Layout. El caballo se
            // para con HorseRollDeg (el GLB viene acostado).
            var pigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PigGlb);
            if (pigPrefab != null)
            {
                var pig = Place(parent, pigPrefab, "Pig", terrain, PigSpot, 200f, PigHeight, false);
                AttachAnim(pig, MakeLegacyClip(PigGlb, "anim_pig"), 0f);
            }
            // Caballo: ESTÁTICO a propósito. Al reproducir su clip como legacy desaparecía en Play
            // (su animación mueve la raíz/huesos y lo saca de cuadro / rompe el skinning). Se deja
            // quieto (sin AttachAnim). Los 6 clips del GLB siguen importados por si algún día se
            // arma bien con un Animator/controller propio.
            var horsePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HorseGlb);
            if (horsePrefab != null)
                Place(parent, horsePrefab, "Horse", terrain, HorseSpot, 210f, HorseHeight, false,
                      new Vector3(0f, 0f, HorseRollDeg));

            Debug.Log("<color=lime>[Gallinero] gallinero + 4 gallinas + chancho + caballo puestos en la granja. " +
                      "Reacomodalos a mano y Tools ▸ Folklore Archives ▸ Save Map Layout (guardan; ocultar = borrar).</color>");
        }

        // Instancia el GLB, lo escala por ALTURA a targetH, lo apoya con la base en el piso
        // centrado en xz, y le aplica yaw (preservando la rotación de import).
        static GameObject Place(Transform parent, GameObject prefab, string name, Terrain terrain,
                                Vector2 xz, float yaw, float targetH, bool addCollider, Vector3 extraEuler = default)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            inst.name = name;
            inst.transform.localScale = Vector3.one;
            Quaternion r0 = inst.transform.rotation;                 // rotación de import del GLB
            // extraEuler: enderezado por-modelo ANTES del yaw (ej. parar el caballo, que viene
            // acostado). Se aplica antes de medir, así el escalado por ALTURA usa el alto ya parado.
            inst.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(extraEuler) * r0;

            Bounds b = PropBounds(inst);
            if (b.size.y > 0.001f) inst.transform.localScale = Vector3.one * (targetH / b.size.y);

            float gy = terrain != null
                ? terrain.SampleHeight(new Vector3(xz.x, 0f, xz.y)) + terrain.transform.position.y
                : 20f;
            inst.transform.position = new Vector3(xz.x, gy, xz.y);
            b = PropBounds(inst);
            inst.transform.position += new Vector3(0f, gy - b.min.y, 0f); // base en el piso

            if (addCollider) AddColliders(inst);
            return inst;
        }

        // MeshCollider por pieza con malla (entorno fijo → no-convexo) para que no se atraviese.
        static void AddColliders(GameObject inst)
        {
            foreach (var mf in inst.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || mf.GetComponent<Collider>() != null) continue;
                mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            }
        }

        static Bounds PropBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        // ── Animaciones ───────────────────────────────────────────────────────
        // Los GLB traen clips (chicken 1, pig 1, horse 6). Para reproducirlos sin un
        // AnimatorController por animal, saco una COPIA LEGACY del clip (asset en Generated/)
        // y la toca el componente runtime LoopClipAnim. Prefiere un clip "idle" si existe.
        static AnimationClip MakeLegacyClip(string glbPath, string cacheName)
        {
            AnimationClip src = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(glbPath))
            {
                if (!(o is AnimationClip c)) continue;
                if (c.name.StartsWith("__preview")) continue;
                if (src == null) src = c;
                if (c.name.ToLowerInvariant().Contains("idle")) { src = c; break; } // idle preferido
            }
            if (src == null)
            {
                Debug.LogWarning("[Gallinero] no encontré AnimationClip en " + glbPath +
                                 " (¿el import del GLB trae animación?). Ese animal queda estático.");
                return null;
            }
            string path = MapLayout.GeneratedFolder + "/" + cacheName + ".anim";
            var copy = Object.Instantiate(src);
            copy.legacy = true;                 // el componente Animation (legacy) lo exige
            copy.wrapMode = WrapMode.Loop;
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(copy, path);
            return copy;
        }

        // Engancha el clip legacy en loop al animal, con offset (desincroniza copias).
        static void AttachAnim(GameObject inst, AnimationClip legacyClip, float offset)
        {
            if (inst == null || legacyClip == null) return;
            // el GLB puede traer un Animator (Mecanim) → choca con Animation (legacy); lo saco.
            // El clip usa rutas relativas a donde estaba el Animator (raíz del modelo).
            var existing = inst.GetComponentInChildren<Animator>(true);
            var targetGO = existing != null ? existing.gameObject : inst;
            foreach (var an in inst.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(an);
            var comp = targetGO.AddComponent<FolkloreArchives.LoopClipAnim>();
            comp.clip = legacyClip;
            comp.startOffset = offset;
        }
    }
}
