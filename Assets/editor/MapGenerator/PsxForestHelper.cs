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

        // Diagnóstico ya cumplido (las texturas se extrajeron a PSX_ExtractedTex).
        // Se saca del menú; se puede reactivar descomentando el [MenuItem].
        // [MenuItem("Tools/Folklore Archives/Listar contenido del FBX PSX")]
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
                {
                    string texName = "-";
                    if (m != null && m.HasProperty("_MainTex") && m.mainTexture != null) texName = m.mainTexture.name;
                    else if (m != null && m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null) texName = m.GetTexture("_BaseMap").name;
                    mats += (m != null ? m.name + "[" + (m.shader != null ? m.shader.name : "?") + " tex:" + texName + "]" : "null") + " ";
                }
                var sz = mesh.bounds.size;
                sb.AppendLine($"  - {mf.gameObject.name}  bounds=({sz.x:0.000},{sz.y:0.000},{sz.z:0.000})  tris≈{tris}  vColors={nCol}  subMesh={mesh.subMeshCount}  mats: {mats}");
            }
            // Sub-assets embebidos del FBX (texturas/materiales dentro del .fbx)
            sb.AppendLine("\nSub-assets embebidos en el FBX:");
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
            {
                if (obj is Texture tex) sb.AppendLine($"  [Texture] {tex.name}  ({tex.width}x{tex.height})");
                else if (obj is Material mat) sb.AppendLine($"  [Material] {mat.name}  shader={(mat.shader != null ? mat.shader.name : "?")}");
            }
            // color del primer vértice del primer árbol (si hay)
            var t1 = FindT("PSX_Tree1", fbx);
            if (t1 != null && t1.colors.Length > 0) sb.AppendLine("PSX_Tree1 color[0] = " + t1.colors[0]);
            Debug.Log(sb.ToString());

            string outPath = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(Application.dataPath), "psx_fbx_report.txt");
            System.IO.File.WriteAllText(outPath, sb.ToString());
            Debug.Log("Reporte PSX escrito en: " + outPath);
        }

        static Mesh FindT(string name, GameObject fbx)
        {
            foreach (var mf in fbx.GetComponentsInChildren<MeshFilter>(true))
                if (mf.gameObject.name == name) return mf.sharedMesh;
            return null;
        }
    }
}
