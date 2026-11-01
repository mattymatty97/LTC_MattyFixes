using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx.Bootstrap;
using HarmonyLib;
using MattyFixes.Interfaces;
using MattyFixes.Utils;
using MattyFixes.Utils.IL;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Rendering;
using VertexLibrary;

namespace MattyFixes.Patches;

[HarmonyPatch]
internal static class ItemPatches
{
    private static readonly FieldInfo ChainloaderDoneField = typeof(Chainloader).GetField("_loaded", AccessTools.all);

    private static readonly Func<bool> IsChainloaderDone = (Func<bool>)ChainloaderDoneField.FastGetter();

    private static readonly HashSet<Item> ComputedItems = [];

    private static readonly Dictionary<Mesh, Mesh> ReadableMeshMap = new();

    private static readonly HashSet<Item> BrokenMeshItems = [];

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Item), "Awake")]
    private static void OnNewItem(Item __instance)
    {
        if (!IsChainloaderDone())
            return;

        if (!MenuManagerPatch.GameHasLoaded)
        {
            ((IInjectedItem)__instance).MattyFixes_ItemCategory = new ItemCategory.ItemCategoryHolder(
                __instance,
                ItemCategory.ItemType.Vanilla,
                ItemCategory.ItemLibrary.Vanilla,
                ItemCategory.ComputePath(ItemCategory.ItemLibrary.Vanilla, __instance.itemName ?? __instance.name)
            );
        }
    }

    [HarmonyPatch(typeof(NetworkBehaviour), nameof(NetworkBehaviour.OnNetworkSpawn))]
    internal static class NetworkSpawnPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(900)]
        private static void Prefix(NetworkBehaviour __instance)
        {
            if (__instance is not GrabbableObject grabbable)
                return;

            var itemType = grabbable.itemProperties;

            if (!ComputedItems.Add(itemType))
                return;

            if (!itemType.isConductiveMetal || !MattyFixes.PluginConfig.ReadableMeshes.Enabled.Value)
                return;

            try
            {
                if (itemType.spawnPrefab != null)
                {
                    CacheReadableMeshes(itemType.spawnPrefab);
                }
            }
            catch (Exception ex)
            {
                var key = itemType.MattyFixes_Identifier;
                MattyFixes.Log.LogError($"{key} Failed to mark prefab Mesh Readable! {ex}");
                BrokenMeshItems.Add(itemType);
                MattyFixes.Log.LogWarning($"{key} Added to the ignored Meshes!");
            }
        }

        [HarmonyPostfix]
        [HarmonyPriority(-900)]
        private static void Postfix(NetworkBehaviour __instance)
        {
            if (__instance is not GrabbableObject grabbable)
                return;

            if (grabbable is ClipboardItem ||
                (grabbable is PhysicsProp && grabbable.itemProperties.itemName == "Sticky note"))
                return;

            if (StartOfRound.Instance.localPlayerController && !StartOfRoundPatch.IsInitializingGame)
                return;


            try
            {
                grabbable.isInElevator = true;
                grabbable.isInShipRoom = true;
                if (grabbable is LungProp lungProp)
                {
                    lungProp.isLungDocked = false;
                    lungProp.isLungPowered = false;
                    lungProp.isLungDockedInElevator = false;
                    lungProp.GetComponent<AudioSource>()?.Stop();
                }

                if (!MattyFixes.PluginConfig.ItemClipping.RotateOnSpawn.Value)
                    return;

                var restingRotation = grabbable.MattyFixes_RestingRotation;
                var floorYOffset = grabbable.MattyFixes_FloorYOffset;

                grabbable.floorYRot = (int)Math.Floor(grabbable.transform.eulerAngles.y - 90f - floorYOffset);

                grabbable.transform.rotation = Quaternion.Euler(restingRotation.x, grabbable.transform.eulerAngles.y, restingRotation.z);
            }
            catch (Exception ex)
            {
                MattyFixes.Log.LogError($"Exception while setting rotation of {grabbable.itemProperties.MattyFixes_Identifier} :{ex}");
            }
        }
    }


    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.LoadUnlockables))]
    private static void CorrectlyPlaceAllUnlockables(StartOfRound __instance)
    {
        if (!MattyFixes.PluginConfig.ItemClipping.Enabled.Value)
            return;

        foreach (var placeableObject in UnityEngine.Object.FindObjectsOfType<AutoParentToShip>()) placeableObject.MoveToOffset();

        Physics.SyncTransforms();
    }


    private static Mesh GetReadableMesh(Mesh original)
    {
        if (original.isReadable)
            return original;

        if (ReadableMeshMap.TryGetValue(original, out var readableMesh))
            return readableMesh;

        readableMesh = MakeReadableMeshCopy(original);
        ReadableMeshMap[original] = readableMesh;

        return readableMesh;
    }

    private static void CacheReadableMeshes(GameObject go)
    {
        var renderer = go.GetComponent<MeshFilter>();
        var filters = renderer is not null ? [renderer] : go.GetComponentsInChildren<MeshFilter>();

        foreach (var meshFilter in filters)
        {
            var mesh = meshFilter.sharedMesh;

            GetReadableMesh(mesh);
        }
    }

    private static Mesh MakeReadableMeshCopy(Mesh nonReadableMesh)
    {
        var meshCopy = new Mesh
        {
            indexFormat = nonReadableMesh.indexFormat
        };

        // Handle vertices
        nonReadableMesh.vertexBufferTarget = GraphicsBuffer.Target.Vertex;
        if (nonReadableMesh.vertexBufferCount > 0)
        {
            var verticesBuffer = nonReadableMesh.GetVertexBuffer(0);
            var totalSize = verticesBuffer.stride * verticesBuffer.count;
            var data = new byte[totalSize];
            verticesBuffer.GetData(data);
            meshCopy.SetVertexBufferParams(nonReadableMesh.vertexCount, nonReadableMesh.GetVertexAttributes());
            meshCopy.SetVertexBufferData(data, 0, 0, totalSize);
            verticesBuffer.Release();
        }


        // Handle triangles
        nonReadableMesh.indexBufferTarget = GraphicsBuffer.Target.Index;
        meshCopy.subMeshCount = nonReadableMesh.subMeshCount;
        var indexesBuffer = nonReadableMesh.GetIndexBuffer();
        var tot = indexesBuffer.stride * indexesBuffer.count;
        var indexesData = new byte[tot];
        indexesBuffer.GetData(indexesData);
        meshCopy.SetIndexBufferParams(indexesBuffer.count, nonReadableMesh.indexFormat);
        meshCopy.SetIndexBufferData(indexesData, 0, 0, tot);
        indexesBuffer.Release();

        // Restore submesh structure
        uint currentIndexOffset = 0;
        for (var i = 0; i < meshCopy.subMeshCount; i++)
        {
            var subMeshIndexCount = nonReadableMesh.GetIndexCount(i);
            meshCopy.SetSubMesh(i, new SubMeshDescriptor((int)currentIndexOffset, (int)subMeshIndexCount));
            currentIndexOffset += subMeshIndexCount;
        }

        // Recalculate normals and bounds
        meshCopy.RecalculateNormals();
        meshCopy.RecalculateBounds();

        meshCopy.name = $"Readable {nonReadableMesh.name}";
        return meshCopy;
    }

    [HarmonyPatch]
    internal class StormyWeatherPatch
    {
        private static (ParentConstraint parent, ScaleConstraint scale) _staticElectricityConstraints;
        private static (Vector3 position, Vector3 rotation, Vector3 scale) _originalOffsets;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(StormyWeather), nameof(IInjectedStormyWeather.Start))]
        private static void Setup(StormyWeather __instance)
        {
            if (MattyFixes.PluginConfig.Particles.Lightning.Value == MattyFixes.PluginConfig.Particles.LightningType.Vanilla)
                return;

            var particleSystem = __instance.staticElectricityParticle;
            var shapeModule = particleSystem.shape;
            _originalOffsets = (shapeModule.position, shapeModule.rotation, shapeModule.scale);

            var parentConstraint = particleSystem.gameObject.AddComponent<ParentConstraint>();
            parentConstraint.weight = 1.0f;
            parentConstraint.translationAtRest = particleSystem.transform.position;
            parentConstraint.rotationAtRest    = particleSystem.transform.rotation.eulerAngles;

            var scaleConstraint = parentConstraint.gameObject.AddComponent<ScaleConstraint>();
            scaleConstraint.weight = 1.0f;
            scaleConstraint.scaleAtRest        = particleSystem.transform.lossyScale;

            _staticElectricityConstraints = (parentConstraint, scaleConstraint);
        }

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(StormyWeather), nameof(StormyWeather.SetStaticElectricityWarning))]
        private static IEnumerable<CodeInstruction> SetStaticElectricityWarning(IEnumerable<CodeInstruction> instructions,
            ILGenerator ilGenerator)
        {
            var codes = instructions.ToList();

            var staticElectricityParticleField = typeof(StormyWeather).GetField(nameof(StormyWeather.staticElectricityParticle), AccessTools.all);
            var setTimeMethod = typeof(ParticleSystem).GetProperty(nameof(ParticleSystem.time), AccessTools.all)?.GetSetMethod();
            var playMethod = typeof(ParticleSystem).GetMethod(nameof(ParticleSystem.Play), 0, []);

            var changeMethod = typeof(ItemPatches.StormyWeatherPatch).GetMethod(nameof(ChangeParticleShape), AccessTools.all);

            // = shape.meshRenderer = setStaticToObject.GetComponentInChildren<UnityEngine.MeshRenderer>();
            // + StormyWeatherPatch.ChangeParticleShape(this, warningObject);
            // = staticElectricityParticle.time = particleTime;
            // = staticElectricityParticle.Play();
            // = staticElectricityParticle.time = particleTime;
            var injector = new ILInjector(codes, ilGenerator)
                .Find(
                    ILMatcher.Ldarg(),
                    ILMatcher.Ldfld(staticElectricityParticleField),
                    ILMatcher.Ldarg(),
                    ILMatcher.Callvirt(setTimeMethod),
                    ILMatcher.Ldarg(),
                    ILMatcher.Ldfld(staticElectricityParticleField),
                    ILMatcher.Callvirt(playMethod));

            if (!injector.IsValid)
            {
                // print error
                MattyFixes.Log.LogWarning("StormyWeather.SetStaticElectricityWarning patch failed!!");
                MattyFixes.Log.LogDebug(string.Join("\n", injector.ReleaseInstructions()));
                return codes;
            }

            injector.Insert(
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Call, changeMethod));

            MattyFixes.Log.LogDebug("StormyWeather.SetStaticElectricityWarning patched!");

            return injector.ReleaseInstructions();
        }


        private static void ChangeParticleShape(StormyWeather __instance, NetworkObject warningObject)
        {
            try
            {
                var grabbable = warningObject.gameObject.GetComponent<GrabbableObject>();

                var particleSystem  = __instance.staticElectricityParticle;
                var shapeModule     = particleSystem.shape;
                var mainModule      = particleSystem.main;

                switch (MattyFixes.PluginConfig.Particles.Lightning.Value)
                {
                    case MattyFixes.PluginConfig.Particles.LightningType.Alternate:
                    {
                        var constraintSource = new ConstraintSource()
                        {
                            sourceTransform = warningObject.transform,
                            weight = 1.0f
                        };

                        mainModule.scalingMode  = ParticleSystemScalingMode.Shape;

                        _staticElectricityConstraints.parent.AddSource(constraintSource);
                        _staticElectricityConstraints.scale.AddSource(constraintSource);

                        _staticElectricityConstraints.parent.constraintActive = true;
                        _staticElectricityConstraints.scale.constraintActive = true;

                        shapeModule.shapeType = ParticleSystemShapeType.Sphere;
                        shapeModule.radiusThickness = 0.01f;

                        var executionOptions = new ExecutionOptions()
                        {
                            VertexCache = VertexesExtensions.GlobalPartialCache,
                            CullingMask = MattyFixes.VisibleLayerMask,
                            LogHandler = MattyFixes.VerboseMeshLog,
                        };

                        var vertexes = warningObject.transform.GetVertexes(executionOptions);

                        var bounds = vertexes.GetBounds();

                        if (!bounds.HasValue)
                            break;

                        var (_, radius) = vertexes.GetFarthestPoint(bounds.Value.center);

                        shapeModule.radius = radius;

                        shapeModule.position = bounds.Value.center;

                        break;
                    }
                    case MattyFixes.PluginConfig.Particles.LightningType.Shape:
                    {
                        if (ItemPatches.BrokenMeshItems.Contains(grabbable.itemProperties))
                            break;

                        try
                        {
                            var rendererGo = shapeModule.meshRenderer.gameObject;
                            if (!rendererGo.TryGetComponent<MeshFilter>(out var meshFilter))
                                break;

                            var constraintSource = new ConstraintSource()
                            {
                                sourceTransform = rendererGo.transform,
                                weight = 1.0f
                            };

                            mainModule.scalingMode  = ParticleSystemScalingMode.Shape;

                            _staticElectricityConstraints.parent.AddSource(constraintSource);
                            _staticElectricityConstraints.scale.AddSource(constraintSource);

                            _staticElectricityConstraints.parent.constraintActive = true;
                            _staticElectricityConstraints.scale.constraintActive = true;

                            var readableMesh = ItemPatches.GetReadableMesh(meshFilter.sharedMesh);

                            shapeModule.shapeType     = ParticleSystemShapeType.Mesh;
                            shapeModule.mesh          = readableMesh;
                            shapeModule.meshRenderer  = null;
                            shapeModule.position      = Vector3.zero;
                            shapeModule.rotation      = Vector3.zero;
                            shapeModule.scale         = Vector3.one;
                        }
                        catch (Exception ex)
                        {
                            var item = grabbable.itemProperties;
                            var key = item.MattyFixes_Identifier;
                            MattyFixes.Log.LogError($"{key} Failed to make prefab Mesh Readable! {ex}");
                            ItemPatches.BrokenMeshItems.Add(item);
                            MattyFixes.Log.LogWarning($"{key} Added to the ignored Meshes!");
                        }
                        break;
                    }
                    case MattyFixes.PluginConfig.Particles.LightningType.Vanilla:
                    default:
                        break;
                }

            }
            catch (Exception ex)
            {
                MattyFixes.Log.LogError(ex);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(StormyWeather), nameof(StormyWeather.LightningStrike))]
        private static void ResetMeshes(StormyWeather __instance, bool useTargetedObject)
        {
            if (MattyFixes.PluginConfig.Particles.Lightning.Value == MattyFixes.PluginConfig.Particles.LightningType.Vanilla)
                return;

            if (__instance.setStaticToObject == null || !useTargetedObject)
                return;

            _staticElectricityConstraints.parent.constraintActive = false;
            _staticElectricityConstraints.scale.constraintActive = false;
            _staticElectricityConstraints.parent.SetSources([]);
            _staticElectricityConstraints.scale.SetSources([]);

            var shapeModule = __instance.staticElectricityParticle.shape;
            var mainModule = __instance.staticElectricityParticle.main;

            mainModule.scalingMode  = ParticleSystemScalingMode.Local;
            shapeModule.shapeType   = ParticleSystemShapeType.MeshRenderer;

            shapeModule.position    = _originalOffsets.position;
            shapeModule.rotation    = _originalOffsets.rotation;
            shapeModule.scale       = _originalOffsets.scale;
        }
    }

    [HarmonyPatch]
    internal class RandomFlyParticlePatches
    {
        [HarmonyTranspiler]
        [HarmonyPatch(typeof(RandomFlyParticle), nameof(RandomFlyParticle.InitializeAfterPositioning))]
        private static IEnumerable<CodeInstruction> InitializeAfterPositioning(
            IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator)
        {
            var codes = instructions.ToList();

            var setMeshRenderer = typeof(ParticleSystem.ShapeModule).GetProperty(nameof(ParticleSystem.ShapeModule.meshRenderer), AccessTools.all)?.GetSetMethod();
            var changeShapeMethod =
                typeof(ItemPatches.RandomFlyParticlePatches).GetMethod(nameof(ChangeParticleShape), AccessTools.all);

            // = {
            // =     gameObject = UnityEngine.Object.Instantiate<GameObject>(this.badFlyPrefab, this.transform.position, Quaternion.identity, this.transform);
            // -     gameObject.GetComponentsInChildren<ParticleSystem>()[1].shape.meshRenderer = this.gameObject.GetComponent<MeshRenderer>();
            // +     RandomFlyParticlePatches.ChangeParticleShape(gameObject.GetComponentsInChildren<ParticleSystem>()[1].shape, this.gameObject.GetComponent<MeshRenderer>());
            // = }
            var injector = new ILInjector(codes, ilGenerator)
                .Find(ILMatcher.Call(setMeshRenderer));

            if (!injector.IsValid)
            {
                // print error
                MattyFixes.Log.LogWarning("RandomFlyParticle.InitializeAfterPositioning patch failed!!");
                MattyFixes.Log.LogDebug(string.Join("\n", injector.ReleaseInstructions()));
                return codes;
            }

            injector
                .ReplaceLastMatch(
                    new CodeInstruction(OpCodes.Call, changeShapeMethod)
                );

            MattyFixes.Log.LogDebug("RandomFlyParticle.InitializeAfterPositioning patched!");

            return injector.ReleaseInstructions();
        }

        private static void ChangeParticleShape(ref ParticleSystem.ShapeModule shapeModule, MeshRenderer meshRenderer)
        {
            var particleSystem = shapeModule.m_ParticleSystem;

            if (!MattyFixes.PluginConfig.Particles.Flies.Value)
            {
                shapeModule.meshRenderer = meshRenderer;
                return;
            }

            var mainModule         = particleSystem.main;
            mainModule.scalingMode = ParticleSystemScalingMode.Shape;

            particleSystem.transform.localPosition = -particleSystem.transform.parent.localPosition;
            particleSystem.transform.localRotation = Quaternion.Inverse(particleSystem.transform.parent.localRotation);

            var rendererGo = meshRenderer.gameObject;
            if (!rendererGo.TryGetComponent<MeshFilter>(out var meshFilter))
                return;

            var readableMesh = ItemPatches.GetReadableMesh(meshFilter.sharedMesh);

            shapeModule.shapeType     = ParticleSystemShapeType.Mesh;
            shapeModule.meshShapeType = ParticleSystemMeshShapeType.Triangle;
            shapeModule.mesh          = readableMesh;
            shapeModule.meshRenderer  = null;
            shapeModule.position      = Vector3.zero;
            shapeModule.rotation      = Vector3.zero;
            shapeModule.scale         = Vector3.one;
        }
    }
}
