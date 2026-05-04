using System;
using System.Collections.Generic;
using MusicTogether.DancingBall.Data;
using UnityEngine.UIElements;

namespace MusicTogether.DancingBall.EditorTool.UIManager
{
    public static class BlockDisplacementUIFactory
    {
        private static readonly Dictionary<Type, Func<VisualElement, IBlockDisplacementUIManager>> s_Creators = new();
        private static bool s_Initialized;

        private static void EnsureInitialized()
        {
            if (s_Initialized) return;
            Register<ClassicBlockDisplacementData>(container =>
            {
#if UNITY_EDITOR
                var tree = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                    ClassicBlockDisplacementUIManager.UxmlPath);
                var root = tree.CloneTree();
                container.Add(root);
                return new ClassicBlockDisplacementUIManager(root);
#else
                return null;
#endif
            });
            s_Initialized = true;
        }

        public static void Register<T>(Func<VisualElement, IBlockDisplacementUIManager> factory)
            where T : IBlockDisplacementData
        {
            s_Creators[typeof(T)] = factory;
        }

        public static IBlockDisplacementUIManager Create(VisualElement container, IBlockDisplacementData data)
        {
            EnsureInitialized();
            if (container == null || data == null) return null;
            if (s_Creators.TryGetValue(data.GetType(), out var factory))
                return factory(container);
            return null;
        }

        public static bool HasCreator(IBlockDisplacementData data)
        {
            EnsureInitialized();
            return data != null && s_Creators.ContainsKey(data.GetType());
        }
    }
}
