// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  YpfNpcPlacer.cs — owner: "ponelos vos a ambos" / reponerlos SIN regenerar.
//  Menú: Tools > Folklore Archives > Colocar NPCs YPF (playero + viejo).
//  Usa la misma lógica que YpfNpcBuilder (coords a mano, 2.4m, collider +
//  FriendWander + HumanWalkAnim en Richard) pero los mete en la ESCENA ACTUAL
//  bajo un objeto "YPF_NPCs" (fuera del map root, así sobrevive a un Generate).
//  Limpia los que ya estén para no duplicar. NO regenera nada.
// ============================================================
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class YpfNpcPlacer
    {
        [MenuItem("Tools/Folklore Archives/Colocar NPCs YPF (playero + viejo)")]
        static void Place()
        {
            var parent = GameObject.Find("YPF_NPCs");
            if (parent == null)
            {
                parent = new GameObject("YPF_NPCs");
                Undo.RegisterCreatedObjectUndo(parent, "YPF NPCs");
            }
            else
            {
                // limpiar los existentes para no duplicar
                for (int i = parent.transform.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(parent.transform.GetChild(i).gameObject);
            }

            YpfNpcBuilder.Build(parent.transform); // playero + viejo, 2.4m + movilidad, pies al piso

            EditorSceneManager.MarkSceneDirty(parent.scene);
            Selection.activeGameObject = parent;
            Debug.Log("<color=lime>[YpfNpc] Colocados en la escena (2.4m + movilidad). Guardá la escena (Ctrl+S). " +
                      "No hace falta regenerar; sobreviven a un Generate.</color>");
        }

        // owner: "hay un solo playero y quiero que esté en la silla" -- sienta a Richard en
        // la silla de oficina que YA está en la escena (la que acomodaste a mano), SIN
        // regenerar. Usa la silla seleccionada si es una; si no, busca "OfficeChair_YPF".
        [MenuItem("Tools/Folklore Archives/Sentar Playero en la silla YPF")]
        static void SeatPlayero()
        {
            GameObject chair = null;
            // 1) si seleccionaste la silla (o un mesh hijo de ella), subo hasta la raíz OfficeChair_YPF
            var sel = Selection.activeGameObject;
            if (sel != null)
            {
                var t = sel.transform;
                while (t != null && chair == null)
                {
                    if (t.name == "OfficeChair_YPF") chair = t.gameObject;
                    t = t.parent;
                }
                // fallback: cualquier objeto seleccionado que sea "chair" y no tenga un padre chair
                if (chair == null && sel.name.ToLowerInvariant().Contains("chair")) chair = sel;
            }
            // 2) si no, busco la silla por nombre en la escena
            if (chair == null)
            {
                foreach (var tr in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                    if (tr.name == "OfficeChair_YPF") { chair = tr.gameObject; break; }
            }
            if (chair == null)
            {
                Debug.LogWarning("[YpfNpc] No encontré la silla. Seleccioná la silla de oficina en la jerarquía y volvé a correr esto (o fijate que se llame 'OfficeChair_YPF').");
                return;
            }

            var go = YpfNpcBuilder.SeatPlayeroOnChair(chair);
            if (go != null)
            {
                Undo.RegisterCreatedObjectUndo(go, "Sentar Playero");
                EditorSceneManager.MarkSceneDirty(go.scene);
                Selection.activeGameObject = go;
                Debug.Log("<color=lime>[YpfNpc] Playero sentado en la silla (emparentado, la sigue al moverla). " +
                          "En PLAY se le corrigen los brazos (abajo) y los muslos doblados; en el editor lo ves en T hasta darle Play. " +
                          "Guardá (Ctrl+S).</color>");
            }
        }
    }
}
