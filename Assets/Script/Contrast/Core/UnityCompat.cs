using System.Collections.Generic;
using UnityEngine;

namespace Contrast.Core
{
    /// <summary>
    /// Cross-version compatibility helpers supporting both Unity 2019.4 LTS (C# 7.3)
    /// and Unity 2022.2+ / Unity 6 without missing APIs.
    /// </summary>
    public static class UnityCompat
    {
        public static T FindAny<T>(bool includeInactive = false) where T : UnityEngine.Object
        {
#if UNITY_2022_2_OR_NEWER
            return UnityEngine.Object.FindAnyObjectByType<T>(
                includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);
#elif UNITY_2020_1_OR_NEWER
            return UnityEngine.Object.FindObjectOfType<T>(includeInactive);
#else
            T active = UnityEngine.Object.FindObjectOfType<T>();
            if (active != null || !includeInactive)
                return active;

            T[] all = Resources.FindObjectsOfTypeAll<T>();
            for (int i = 0; i < all.Length; i++)
            {
                T item = all[i];
                if (item == null)
                    continue;
                Component comp = item as Component;
                if (comp != null && comp.gameObject.scene.IsValid())
                    return item;
            }
            return null;
#endif
        }

        public static T[] FindAll<T>(bool includeInactive = false) where T : UnityEngine.Object
        {
#if UNITY_6000_0_OR_NEWER
            return UnityEngine.Object.FindObjectsByType<T>(
                includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);
#elif UNITY_2022_2_OR_NEWER
            return UnityEngine.Object.FindObjectsByType<T>(
                includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
#elif UNITY_2020_1_OR_NEWER
            return UnityEngine.Object.FindObjectsOfType<T>(includeInactive);
#else
            if (!includeInactive)
                return UnityEngine.Object.FindObjectsOfType<T>();

            T[] all = Resources.FindObjectsOfTypeAll<T>();
            List<T> sceneItems = new List<T>(all.Length);
            for (int i = 0; i < all.Length; i++)
            {
                T item = all[i];
                if (item == null)
                    continue;
                Component comp = item as Component;
                if (comp != null && comp.gameObject.scene.IsValid())
                    sceneItems.Add(item);
            }
            return sceneItems.ToArray();
#endif
        }

        public static Font GetBuiltinFont(int fallbackSize = 16)
        {
            Font font = null;
            try
            {
#if UNITY_2022_2_OR_NEWER
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
            }
            catch
            {
            }

            if (font == null)
            {
                try
                {
                    font = Font.CreateDynamicFontFromOSFont("Arial", fallbackSize);
                }
                catch
                {
                }
            }

            return font;
        }
    }
}
