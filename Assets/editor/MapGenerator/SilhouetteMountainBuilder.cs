// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  SilhouetteMountainBuilder.cs — montañas de FONDO "no reales":
//  una banda dentada procedural (silueta plana/pintada) alrededor
//  del horizonte, en la capa "Backdrop", que la cámara de fondo
//  dibuja SOBRE el cielo de AllSky sin niebla. Dos anillos para
//  dar profundidad atmosférica. Baratísimo (un mesh por anillo).
// ============================================================
using System.Collections.Generic;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class SilhouetteMountainBuilder
    {
        public static void Build(Transform parent)
        {
            int layer = LayerMask.NameToLayer("Backdrop");
            if (layer < 0) layer = 6;

            // anillo lejano (más alto/oscuro/azulado) primero, luego el cercano encima.
            BuildRing(parent, layer, "SilhouetteFar",  2100f, 240f, 620f, new Color(0.12f, 0.14f, 0.20f), 64, 91.3f, -40f);
            BuildRing(parent, layer, "SilhouetteNear", 1450f, 150f, 430f, new Color(0.19f, 0.21f, 0.27f), 80, 13.7f, -30f);
        }

        static void BuildRing(Transform parent, int layer, string name, float radius,
                              float minH, float maxH, Color col, int segs, float noiseSeed, float baseY)
        {
            float cx = MapLayout.MapSizeX * 0.5f, cz = MapLayout.MapSize * 0.5f;
            var verts = new List<Vector3>();
            var tris  = new List<int>();

            for (int i = 0; i <= segs; i++)
            {
                float a  = (i / (float)segs) * Mathf.PI * 2f;
                float px = cx + Mathf.Cos(a) * radius;
                float pz = cz + Mathf.Sin(a) * radius;
                // altura dentada con dos octavas de Perlin → picos irregulares
                float n1 = Mathf.PerlinNoise(i * 0.33f + noiseSeed, 3.1f);
                float n2 = Mathf.PerlinNoise(i * 0.91f + noiseSeed, 7.7f);
                float h  = Mathf.Lerp(minH, maxH, n1) * (0.6f + 0.4f * n2);
                verts.Add(new Vector3(px, baseY, pz));        // base
                verts.Add(new Vector3(px, baseY + h, pz));    // pico
            }
            for (int i = 0; i < segs; i++)
            {
                int b = i * 2;
                tris.Add(b);     tris.Add(b + 1); tris.Add(b + 2);
                tris.Add(b + 2); tris.Add(b + 1); tris.Add(b + 3);
            }

            var mesh = new Mesh { name = name + "Mesh" };
            mesh.SetVertices(verts); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();

            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.layer = layer;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            // UNLIT plano (look pintado, no iluminación 3D), doble cara.
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.isStatic = true;
        }
    }
}
