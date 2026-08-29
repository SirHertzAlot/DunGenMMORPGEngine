using System;
using System.Collections.Generic;
using UnityEngine;

namespace DunGen.ECS.Models
{
    public interface IModelAssetProvider
    {
        GameObject LoadPrefab(ModelAssetEntry asset);
    }

    public sealed class EmptyModelAssetProvider : IModelAssetProvider
    {
        public GameObject LoadPrefab(ModelAssetEntry asset)
        {
            return null;
        }
    }

    public sealed class VisualSpawnPoolEntry : MonoBehaviour
    {
        public CharacterArchetype Archetype;
        public string VariantName;
        public string SourceModelName;
        public List<string> PartNames = new();
    }

    public sealed class VisualModelPartInstance : MonoBehaviour
    {
        public string AssetName;
        public string RelativePath;
        public string SourceFolder;
        public string Category;
        public bool UsedPrefabAsset;
    }

    public sealed class VisualModelBuildResult
    {
        public GameObject Root;
        public readonly List<string> MissingParts = new();
        public int InstantiatedPartCount;
        public int MetadataPartCount;

        public bool IsComplete => MissingParts.Count == 0;
    }

    public static class VisualModelBuildSystem
    {
        public static VisualModelBuildResult BuildRecipe(
            ModularCharacterRecipe recipe,
            ModelAssetManifest manifest,
            IModelAssetProvider assetProvider = null,
            Transform parent = null,
            bool active = false)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));

            assetProvider ??= new EmptyModelAssetProvider();

            var result = new VisualModelBuildResult();
            var root = new GameObject(recipe.VariantName);
            root.transform.SetParent(parent, false);
            root.SetActive(active);
            result.Root = root;

            var poolEntry = root.AddComponent<VisualSpawnPoolEntry>();
            poolEntry.Archetype = recipe.Archetype;
            poolEntry.VariantName = recipe.VariantName;
            poolEntry.SourceModelName = PolygonFantasyHeroAssetSource.PrimaryModularCharactersFbx;

            for (int i = 0; i < recipe.RequiredObjectNames.Length; i++)
            {
                string partName = recipe.RequiredObjectNames[i];
                poolEntry.PartNames.Add(partName);

                if (!manifest.TryGetAsset(partName, out var asset))
                {
                    result.MissingParts.Add(partName);
                    continue;
                }

                var prefab = assetProvider.LoadPrefab(asset);
                GameObject partObject = prefab != null
                    ? UnityEngine.Object.Instantiate(prefab, root.transform)
                    : new GameObject(asset.name);

                partObject.name = asset.name;
                if (partObject.transform.parent != root.transform)
                    partObject.transform.SetParent(root.transform, false);

                var partInstance = partObject.GetComponent<VisualModelPartInstance>();
                if (partInstance == null)
                    partInstance = partObject.AddComponent<VisualModelPartInstance>();

                partInstance.AssetName = asset.name;
                partInstance.RelativePath = asset.relativePath;
                partInstance.SourceFolder = asset.sourceFolder;
                partInstance.Category = asset.category;
                partInstance.UsedPrefabAsset = prefab != null;

                if (prefab != null)
                    result.InstantiatedPartCount++;
                else
                    result.MetadataPartCount++;
            }

            return result;
        }
    }

    public sealed class VisualSpawnPool
    {
        private readonly Dictionary<CharacterArchetype, Queue<GameObject>> _pooledVisuals = new();
        private readonly Transform _root;

        public VisualSpawnPool(Transform root = null)
        {
            if (root == null)
            {
                var rootObject = new GameObject("Visual Spawn Pool");
                root = rootObject.transform;
            }

            _root = root;
        }

        public Transform Root => _root;

        public int Count(CharacterArchetype archetype)
        {
            return _pooledVisuals.TryGetValue(archetype, out var queue) ? queue.Count : 0;
        }

        public void Prewarm(
            ModelAssetManifest manifest,
            IEnumerable<CharacterArchetype> archetypes,
            int instancesPerArchetype,
            IModelAssetProvider assetProvider = null)
        {
            if (instancesPerArchetype < 0)
                throw new ArgumentOutOfRangeException(nameof(instancesPerArchetype));

            foreach (var archetype in archetypes)
            {
                for (int i = 0; i < instancesPerArchetype; i++)
                {
                    var result = VisualModelBuildSystem.BuildRecipe(
                        VisualModelCatalog.GetRecipe(archetype),
                        manifest,
                        assetProvider,
                        _root,
                        false);

                    if (!result.IsComplete)
                        throw new InvalidOperationException($"{archetype} visual recipe is missing: {string.Join(", ", result.MissingParts)}");

                    Return(archetype, result.Root);
                }
            }
        }

        public bool TryTake(CharacterArchetype archetype, out GameObject visual)
        {
            visual = null;
            if (!_pooledVisuals.TryGetValue(archetype, out var queue) || queue.Count == 0)
                return false;

            visual = queue.Dequeue();
            visual.SetActive(true);
            return true;
        }

        public void Return(CharacterArchetype archetype, GameObject visual)
        {
            if (visual == null)
                return;

            visual.SetActive(false);
            visual.transform.SetParent(_root, false);

            if (!_pooledVisuals.TryGetValue(archetype, out var queue))
            {
                queue = new Queue<GameObject>();
                _pooledVisuals.Add(archetype, queue);
            }

            queue.Enqueue(visual);
        }
    }
}
