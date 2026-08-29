using UnityEditor;
using UnityEngine;

namespace DunGen.Networking.Editor
{
    /// <summary>
    /// Editor utility to create/update the BackendConnectionConfig ScriptableObject
    /// in Resources so <see cref="NetworkingBootstrap"/> can load it at runtime.
    /// </summary>
    public static class CreateNetworkingAssets
    {
        private const string ResourcesPath = "Assets/DunGenMMORPGEngine/projects/5/Assets/Code/Networking/Resources";
        private const string AssetPath = ResourcesPath + "/DunGenNetworkingConfig.asset";

        [MenuItem("DunGen/Networking/Create or Select Backend Config")]
        public static void CreateOrSelectConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<BackendConnectionConfig>(AssetPath);
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                Debug.Log($"[DunGen.Networking] Existing config selected: {AssetPath}");
                return;
            }

            System.IO.Directory.CreateDirectory(ResourcesPath);

            var asset = ScriptableObject.CreateInstance<BackendConnectionConfig>();
            AssetDatabase.CreateAsset(asset, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            Debug.Log($"[DunGen.Networking] Created config at {AssetPath}. " +
                      "Set authoritativeBaseUrl and adminApiKey in the Inspector.");
        }
    }
}
