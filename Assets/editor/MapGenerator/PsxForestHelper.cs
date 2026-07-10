// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  PsxForestHelper.cs — utilidades para el pack PSX Forest de
//  StarkCrafts. Por ahora: listar el contenido del FBX (para saber
//  qué modelos trae y poder integrarlos al ForestBuilder).
// ============================================================
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class PsxForestHelper
    {
        public const string FbxPath =
            "Assets/StarkCrafts/PSX_Forest_Level_byStarkCrafts/PSX_Forest_AssetCollection_byStarkCrafts.fbx";

        [MenuItem("Tools/Folklore Archives/Listar contenido del FBX PSX")]
        public static void ListFbx()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbx == null)
            {
                Debug.LogWarning("FBX PSX no encontrado/importado aún en " + FbxPath +
                                 " — dale foco a Unity para que importe y reintentá.");
                return;
            }
            var sb = new StringBuilder("Contenido del FBX PSX (StarkCrafts):\n");
            foreach (var mf in fbx.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                if (mesh == null) continue;
                int tris = mesh.triangles.Length / 3;
                int nCol = mesh.colors.Length;             // ← ¿tiene vertex-colors?
                var mr = mf.GetComponent<MeshRenderer>();
                string mats = "";
                if (mr != null) foreach (var m in mr.sharedMaterials)
                    mats += (m != null ? m.name + "[" + (m.shader != null ? m.shader.name : "?") + "]" : "null") + " ";
                sb.AppendLine($"  - {mf.gameObject.name}  tris≈{tris}  vertexColors={nCol}  subMesh={mesh.subMeshCount}  mats: {mats}");
            }
            // color del primer vértice del primer árbol (si hay)
            var t1 = FindT("PSX_Tree1", fbx);
            if (t1 != null && t1.colors.Length > 0) sb.AppendLine("PSX_Tree1 color[0] = " + t1.colors[0]);
            Debug.Log(sb.ToString());
        }

        static Mesh FindT(string name, GameObject fbx)
        {
            foreach (var mf in fbx.GetComponentsInChildren<MeshFilter>(true))
                if (mf.gameObject.name == name) return mf.sharedMesh;
            return null;
        }
    }
}
