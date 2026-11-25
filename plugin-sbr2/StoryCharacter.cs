using System;
using System.Collections.Generic;
using HarmonyLib;

namespace SBR2QoL
{
    [HarmonyPatch(typeof(StoryManager), nameof(StoryManager.getStoryCharacterId))]
    internal static class StoryCharacterPatch
    {
        private static void Postfix(int joinPlayerIndex,
            ref CharacterDataManager.CharacterID __result)
        {
            if (joinPlayerIndex != 0) return;

            string key = Plugin.StoryCharacterKey.Value;
            if (string.IsNullOrEmpty(key)) return;
            key = key.Trim();
            if (key.Length == 0) return;

            var manager = SingletonBase<CharacterDataManager>.getInstance();
            if (manager == null) return;

            try
            {
                var id = manager.getCharacterId(key);
                if (!EqualityComparer<CharacterDataManager.CharacterID>.Default
                    .Equals(id, default))
                    __result = id;
                else
                    Plugin.Logger.LogWarning("Unknown story character key: " + key);
            }
            catch (Exception e)
            {
                Plugin.Logger.LogWarning("Story character '" + key +
                    "' failed: " + e.Message);
            }
        }
    }
}
