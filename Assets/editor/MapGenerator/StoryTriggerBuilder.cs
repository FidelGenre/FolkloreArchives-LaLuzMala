// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  StoryTriggerBuilder.cs — invisible trigger boxes for story
//  events. Hook your act/checkpoint logic to these later.
//  Paste into:  Assets/Editor/MapGenerator/StoryTriggerBuilder.cs
// ============================================================
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class StoryTriggerBuilder
    {
        public static void Build(Transform parent, Terrain t)
        {
            var group = BuilderUtils.Group(parent, "StoryTriggers", Vector3.zero);
            Trigger(group, t, "TRIGGER_ACT1_DIRT_TURNOFF", MapLayout.DirtTurnoff, 20f);
            Trigger(group, t, "TRIGGER_ACT1_CAMPSITE", MapLayout.Campsite, 30f);
            Trigger(group, t, "TRIGGER_ACT2_RANCH", MapLayout.OldLadyRanch, 15f);
            Trigger(group, t, "TRIGGER_ACT2_HUNTING_FIELD", MapLayout.HuntingField, 30f);
            Trigger(group, t, "TRIGGER_ACT2_GRAVE", MapLayout.Grave, 10f);
            Trigger(group, t, "TRIGGER_ACT3_CRIMINAL_CAMP", MapLayout.MainCriminalCamp, 30f);
            Trigger(group, t, "TRIGGER_ACT3_RESCUE", MapLayout.HostageArea, 12f);
            Trigger(group, t, "TRIGGER_ACT4_SECONDARY_CAMP", MapLayout.SecondaryCamp, 20f);
            Trigger(group, t, "TRIGGER_ACT4_CAR",
                new Vector2(MapLayout.Campsite.x - 12f, MapLayout.Campsite.y - 16f), 10f);
        }

        static void Trigger(Transform parent, Terrain t, string name, Vector2 p, float size)
        {
            var g = BuilderUtils.Empty(parent, name, BuilderUtils.Ground(t, p) + Vector3.up * 4f);
            var box = g.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(size, 8f, size);
        }
    }
}
