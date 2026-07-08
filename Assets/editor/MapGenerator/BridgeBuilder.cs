// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  BridgeBuilder.cs — a rural steel-girder bridge over the water
//  crossing, built ON TOP of the existing paved road (which is
//  already the deck + has a collider). Style ref: green plate
//  girders, white railings with diagonal stays, concrete piers.
//
//  Adds, along a span of the road:
//    • Green side girders (both road edges) + transverse beams
//    • Concrete piers (2 per bent) going down into the water + caps
//    • White railings: posts + top rail + diagonal truss stays
//
//  Follows the road's z-curve via PavedRouteZAt and matches the
//  road's asymmetric edges (south 4.5 m, north 12 m).
//
//  Tune CenterX / Span to move/resize the bridge to the crossing.
//  If the owner supplies a metal texture, swap the green material
//  in GreenMat() for a MatTextured call.
// ============================================================
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class BridgeBuilder
    {
        // ── Placement (tune to the crossing) ─────────────────────────────────
        const float CenterX = 600f;   // river crossing (río movido al centro, plano final)
        const float Span    = 120f;   // length along the road (was 90 — owner wanted bigger)

        // Road edges (match RoadsideBuilder's paved-road cross-section).
        const float SouthHalf = 4.5f;
        const float NorthHalf = 12f;

        const float DeckY     = MapLayout.RoadSurfaceHeight;  // 17
        const float GirderH   = 2.6f;    // green side-beam height (taller, more substantial)
        const float GirderD   = 0.85f;   // its thickness
        const float PierBaseY = 3f;      // bottom of piers (below the water surface)
        const float PierSize  = 1.8f;    // thicker concrete columns
        const float PierStep  = 22f;     // spacing between pier bents
        const float RailH     = 1.35f;   // white railing height
        const float PostStep  = 4f;      // railing post spacing
        const float SegStep   = 5f;      // girder / rail segment length

        public static void Build(Transform parent, Terrain terrain)
        {
            var group = BuilderUtils.Group(parent, "Bridge", Vector3.zero);

            var metalTex = MetalTex();
            var green    = MetalMat("bridge_greenmetal", metalTex, new Color(0.13f, 0.32f, 0.19f), 0.5f, 0.75f, 2.5f);
            var white    = MetalMat("bridge_white",      metalTex, new Color(0.88f, 0.88f, 0.85f), 0.45f, 0.55f, 1.5f);
            var concrete = PierMat();

            var cube = BuilderUtils.PrimitiveMesh(PrimitiveType.Cube);
            float x0 = CenterX - Span * 0.5f, x1 = CenterX + Span * 0.5f;

            float girderTop = DeckY - 0.05f;
            float girderCy  = girderTop - GirderH * 0.5f;
            float pierTop   = girderCy - GirderH * 0.5f;

            var greenCI    = new List<CombineInstance>();
            var concreteCI = new List<CombineInstance>();
            var whiteCI    = new List<CombineInstance>();

            // ── Side girders (segmented, following the road curve) ──────────
            for (float x = x0; x < x1 - 0.01f; x += SegStep)
            {
                float xm = Mathf.Min(x + SegStep, x1);
                float cx = (x + xm) * 0.5f;
                float segLen = xm - x + 0.1f;
                float zc = MapLayout.PavedRouteZAt(cx);
                greenCI.Add(CI(cube, new Vector3(cx, girderCy, zc - SouthHalf),
                    Quaternion.identity, new Vector3(segLen, GirderH, GirderD)));
                greenCI.Add(CI(cube, new Vector3(cx, girderCy, zc + NorthHalf),
                    Quaternion.identity, new Vector3(segLen, GirderH, GirderD)));
            }

            // ── Pier bents (transverse beam + 2 concrete columns + cap) ─────
            for (float x = x0; x <= x1 + 0.01f; x += PierStep)
            {
                float zc = MapLayout.PavedRouteZAt(x);
                float deckW = SouthHalf + NorthHalf;
                float zMid  = zc + (NorthHalf - SouthHalf) * 0.5f;   // centre of the deck width

                // transverse green beam under the deck spanning both girders
                greenCI.Add(CI(cube, new Vector3(x, girderCy, zMid),
                    Quaternion.identity, new Vector3(GirderD, GirderH * 0.8f, deckW)));

                // concrete cap beam just under the girders
                concreteCI.Add(CI(cube, new Vector3(x, pierTop - 0.35f, zMid),
                    Quaternion.identity, new Vector3(PierSize * 1.3f, 0.7f, deckW * 0.95f)));

                // two columns down into the water
                float pierH  = pierTop - 0.7f - PierBaseY;
                float pierCy = (pierTop - 0.7f + PierBaseY) * 0.5f;
                concreteCI.Add(CI(cube, new Vector3(x, pierCy, zc - SouthHalf + 1f),
                    Quaternion.identity, new Vector3(PierSize, pierH, PierSize)));
                concreteCI.Add(CI(cube, new Vector3(x, pierCy, zc + NorthHalf - 1f),
                    Quaternion.identity, new Vector3(PierSize, pierH, PierSize)));
            }

            // ── White railings: posts + top rail + diagonal stays, each side ─
            foreach (float half in new[] { -SouthHalf, NorthHalf })
            {
                // posts
                for (float x = x0; x <= x1 + 0.01f; x += PostStep)
                {
                    float z = MapLayout.PavedRouteZAt(x) + half;
                    whiteCI.Add(CI(cube, new Vector3(x, DeckY + RailH * 0.5f, z),
                        Quaternion.identity, new Vector3(0.16f, RailH, 0.16f)));
                }
                // top rail (segments) + diagonal stays
                for (float x = x0; x < x1 - 0.01f; x += SegStep)
                {
                    float xm = Mathf.Min(x + SegStep, x1);
                    float cx = (x + xm) * 0.5f;
                    float zc = MapLayout.PavedRouteZAt(cx) + half;
                    whiteCI.Add(CI(cube, new Vector3(cx, DeckY + RailH, zc),
                        Quaternion.identity, new Vector3(xm - x + 0.1f, 0.14f, 0.14f)));

                    // diagonal stay from deck at x to rail top at xm (truss look)
                    Vector3 a = new Vector3(x,  DeckY,         MapLayout.PavedRouteZAt(x)  + half);
                    Vector3 b = new Vector3(xm, DeckY + RailH,  MapLayout.PavedRouteZAt(xm) + half);
                    Vector3 mid = (a + b) * 0.5f;
                    Vector3 dir = (b - a);
                    float len = dir.magnitude;
                    Quaternion rot = Quaternion.FromToRotation(Vector3.right, dir.normalized);
                    whiteCI.Add(CI(cube, mid, rot, new Vector3(len, 0.08f, 0.08f)));
                }
            }

            BuilderUtils.BuildCombinedStatic(group, "BridgeGirders", greenCI,    green,    addCollider: true);
            BuilderUtils.BuildCombinedStatic(group, "BridgePiers",   concreteCI, concrete, addCollider: true);
            BuilderUtils.BuildCombinedStatic(group, "BridgeRailings", whiteCI,   white,    addCollider: true);

            BuilderUtils.MarkStaticRecursive(group);
        }

        // ── Helpers ──────────────────────────────────────────────────────────
        static CombineInstance CI(Mesh m, Vector3 pos, Quaternion rot, Vector3 scale) =>
            new CombineInstance { mesh = m, transform = Matrix4x4.TRS(pos, rot, scale) };

        // Painted-metal material: generated metal texture + tint + metallic sheen.
        static Material MetalMat(string name, Texture2D tex, Color tint,
                                 float smoothness, float metallic, float tiling)
        {
            var m = BuilderUtils.MatTextured(name, tex, tint, smoothness);
            m.mainTextureScale = new Vector2(tiling, tiling);
            if (m.HasProperty("_BaseMap")) m.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
            if (m.HasProperty("_Metallic"))   m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            return m;
        }

        // Concrete/stone piers: reuse a rock texture if present, else flat concrete.
        static Material PierMat()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/ExternalAssets/ForestPack/Texture/Rock Texture/Rock2/Rock2_2K_Color.png");
            Material m = tex != null
                ? BuilderUtils.MatTextured("bridge_concrete", tex, new Color(0.60f, 0.58f, 0.55f), 0.08f)
                : BuilderUtils.Mat("bridge_concrete", new Color(0.55f, 0.54f, 0.51f), 0f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.08f);
            return m;
        }

        // Procedural painted-metal texture: subtle vertical brushing + weathering +
        // faint horizontal panel seams. Tinted per material. Cached as an asset.
        static Texture2D MetalTex()
        {
            string path = MapLayout.GeneratedFolder + "/tex_bridgemetal.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            const int S = 256;
            var t  = new Texture2D(S, S, TextureFormat.RGBA32, true);
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float streak = (Mathf.PerlinNoise(x * 0.25f, y * 0.015f) - 0.5f) * 0.12f; // vertical brushing
                    float weath  = (Mathf.PerlinNoise(x * 0.03f + 5f, y * 0.03f + 9f) - 0.5f) * 0.16f;
                    float g = 0.74f + streak + weath;
                    if (y % 64 == 0) g *= 0.72f;  // faint horizontal panel seam
                    g = Mathf.Clamp01(g);
                    px[y * S + x] = new Color(g, g, g, 1f);
                }
            }
            t.SetPixels(px); t.Apply();
            AssetDatabase.CreateAsset(t, path);
            return t;
        }
    }
}
