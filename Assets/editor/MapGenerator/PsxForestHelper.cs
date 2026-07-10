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
                int tris = mf.sharedMesh != null ? mf.sharedMesh.triangles.Length / 3 : 0;
                var b = mf.sharedMesh != null ? mf.sharedMesh.bounds.size : Vector3.zero;
                sb.AppendLine($"  - {mf.gameObject.name}  (tris≈{tris}, alto≈{b.y:0.0})");
            }
            Debug.Log(sb.ToString());
        }
    }
}
