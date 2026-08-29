using System.IO;
using DunGen.Testing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DunGen.Testing.Editor
{
    public static class GameplayAutomationLauncher
    {
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
        private const string SampleAutomationScriptPath = "Assets/DunGenMMORPGEngine/projects/5/Assets/Code/Testing/Resources/SampleGameplayAutomationScript.asset";
        private const string BootstrapObjectName = "DunGen Gameplay Automation";

        [MenuItem("DunGen/Testing/Create Sample Automation Script")]
        public static void CreateSampleAutomationScript()
        {
            var dir = Path.GetDirectoryName(SampleAutomationScriptPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir!);

            var existing = AssetDatabase.LoadAssetAtPath<GameplayAutomationScript>(SampleAutomationScriptPath);
            if (existing != null)
            {
                Debug.Log($"[DunGen.Testing] Asset already exists at '{SampleAutomationScriptPath}'.");
                EditorGUIUtility.PingObject(existing);
                return;
            }

            var asset = ScriptableObject.CreateInstance<GameplayAutomationScript>();
            asset.Seed = 12345;
            asset.MaxTurns = 12;
            asset.StopWhenGameOver = true;

            AssetDatabase.CreateAsset(asset, SampleAutomationScriptPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[DunGen.Testing] Created sample automation script at '{SampleAutomationScriptPath}'.");
            EditorGUIUtility.PingObject(asset);
        }

        [MenuItem("DunGen/Testing/Run Sample Gameplay Automation")]
        public static void RunSampleGameplayAutomation()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var automationScript = AssetDatabase.LoadAssetAtPath<GameplayAutomationScript>(SampleAutomationScriptPath);
            if (automationScript == null)
            {
                Debug.Log($"[DunGen.Testing] Auto-creating missing sample automation script.");
                CreateSampleAutomationScript();
                automationScript = AssetDatabase.LoadAssetAtPath<GameplayAutomationScript>(SampleAutomationScriptPath);
            }

            var scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
            var bootstrap = FindOrCreateBootstrap();
            bootstrap.Configure(automationScript, true);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeObject = bootstrap.gameObject;
            EditorGUIUtility.PingObject(bootstrap.gameObject);

            if (!EditorApplication.isPlaying)
                EditorApplication.isPlaying = true;
        }

        private static GameplayAutomationBootstrap FindOrCreateBootstrap()
        {
            var existing = Object.FindAnyObjectByType<GameplayAutomationBootstrap>();
            if (existing != null)
                return existing;

            var gameObject = GameObject.Find(BootstrapObjectName) ?? new GameObject(BootstrapObjectName);
            var bootstrap = gameObject.GetComponent<GameplayAutomationBootstrap>();
            if (bootstrap == null)
                bootstrap = gameObject.AddComponent<GameplayAutomationBootstrap>();

            return bootstrap;
        }
    }
}