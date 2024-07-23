using System;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using MattyFixes.Dependency;
using MattyFixes.Utils;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Rendering;
using Random = System.Random;

namespace MattyFixes.Patches;

[HarmonyPatch]
internal static class ItemPatches
{
    
    private static readonly HashSet<Item> ComputedItems = [];

    private static readonly Dictionary<Mesh, Mesh> ReadableMeshMap = new();

    private static readonly Dictionary<string, List<float>> ItemRotations = new()
    {
        {
            "Flashlight",
            [90f, 0f, 90f]
        },
        {
            "Jetpack",
            [45f, 0f, 0f]
        },
        {
            "Key",
            [180f, 0f, 90f]
        },
        {
            "Apparatus",
            [0f, 0f, 135f]
        },
        {
            "Pro-flashlight",
            [90f, 0f, 90f]
        },
        {
            "Shovel",
            [0f, -90f, -90f]
        },
        {
            "Stun grenade",
            [0f, 0f, 90f]
        },
        {
            "Extension ladder",
            [0f, 0f, 0f]
        },
        {
            "TZP-Inhalant",
            [0f, 0f, -90f]
        },
        {
            "Zap gun",
            [95f, 0f, 90f]
        },
        {
            "Magic 7 ball",
            [0f, 0f, 0f]
        },
        {
            "Airhorn",
            [0f, 90f, 270f]
        },
        {
            "Big bolt",
            [-21f, 0f, 0f]
        },
        {
            "Bottles",
            [-90f, 0f, 0f]
        },
        {
            "Brush",
            [90f, 0f, 0f]
        },
        {
            "Candy",
            [90f, 0f, 0f]
        },
        {
            "Chemical jug",
            [-90f, 0f, 0f]
        },
        {
            "Clown horn",
            [-90f, 0f, 0f]
        },
        {
            "Large axle",
            [7f, 0f, 0f]
        },
        {
            "Teeth",
            [-90f, 0f, 0f]
        },
        {
            "V-type engine",
            [-90f, 0f, 0f]
        },
        {
            "Plastic fish",
            [-45f, 0f, 90f]
        },
        {
            "Laser pointer",
            [0f, 0f, 0f]
        },
        {
            "Gold bar",
            [-90f, 0f, -90f]
        },
        {
            "Magnifying glass",
            [0f, 90f, -90f]
        },
        {
            "Cookie mold pan",
            [-90f, 0f, 90f]
        },
        {
            "Mug",
            [-90f, 0f, 0f]
        },
        {
            "Perfume bottle",
            [-90f, 0f, 0f]
        },
        {
            "Old phone",
            [-90f, 0f, -90f]
        },
        {
            "Jar of pickles",
            [-90f, 0f, 0f]
        },
        {
            "Pill bottle",
            [-90f, 0f, 0f]
        },
        {
            "Ring",
            [0f, -90f, 90f]
        },
        {
            "Toy robot",
            [-90f, 0f, 0f]
        },
        {
            "Rubber Ducky",
            [-90f, 0f, -90f]
        },
        {
            "Steering wheel",
            [-90f, 0f, 0f]
        },
        {
            "Toothpaste",
            [-90f, 0f, 0f]
        },
        {
            "Hive",
            [7f, 0f, 0f]
        },
        {
            "Radar-booster",
            [0f, 0f, 0f]
        },
        {
            "Shotgun",
            [180f, 0f, -5f]
        },
        {
            "Ammo",
            [0f, 0f, 90f]
        },
        {
            "Spray paint",
            [0f, 0f, 195f]
        },
        {
            "Homemade flashbang",
            [0f, 0f, 90f]
        },
        {
            "Gift",
            [-90f, 0f, 0f]
        },
        {
            "Flask",
            [25f, 0f, 0f]
        },
        {
            "Tragedy",
            [-90f, 0f, 0f]
        },
        {
            "Comedy",
            [-90f, 0f, 0f]
        },
        {
            "Whoopie cushion",
            [-90f, 0f, 0f]
        }
    };

    private static readonly HashSet<Item> BrokenMeshItems = [];

    private static readonly Dictionary<MeshFilter, Mesh> ReverseMeshMap = new();
    private static Vector3 _staticElectricityParticleOffset;

    internal static Vector3 FixPlacement(Vector3 hitPoint, Transform shelfTransform, GrabbableObject heldObject)
    {
        var renderer = shelfTransform.gameObject.GetComponent<Renderer>();
        var bounds = renderer?.bounds;

        var yOffset = bounds.HasValue ? bounds.Value.extents.y : shelfTransform.localScale.z / 2f;
        hitPoint.y = shelfTransform.position.y + yOffset + heldObject.itemProperties.verticalOffset;
        return hitPoint;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlaceableObjectsSurface), nameof(PlaceableObjectsSurface.itemPlacementPosition))]
    private static bool ItemPlacementPositionPatch(PlaceableObjectsSurface __instance, ref Vector3 __result,
        Transform gameplayCamera, GrabbableObject heldObject)
    {
        if (!MattyFixes.PluginConfig.ItemClipping.Enabled.Value)
            return true;

        //only tweak if we're placing inside the CupBoard
        if (__instance.transform.parent?.parent != CupBoardFix.GetCloset().gameObject.transform)
            return true;

        try
        {
            if (Physics.Raycast(gameplayCamera.position, gameplayCamera.forward, out var val, 7f,
                    1073744640, (QueryTriggerInteraction)1))
            {
                var hitPoint = __instance.placeableBounds.ClosestPoint(val.point);
                __result = FixPlacement(hitPoint, __instance.transform, heldObject);
                return false;
            }

            __result = Vector3.zero;
            return false;
        }
        catch (Exception ex)
        {
            MattyFixes.Log.LogError($"Exception while finding the Cupboard Placement {ex}");
            return true;
        }
    }

    private static void UpdateItemRotation(string modName, Item item)
    {
        if (!MattyFixes.PluginConfig.ItemClipping.ItemRotations.TryGetValue(item, out var configEntry))
        {
            var ogRotation = item.restingRotation;
            configEntry = MattyFixes.Instance.Config.Bind(
                $"ItemClipping.Rotations{(modName != null ? "." : "")}{modName}",
                item.itemName
                    .Replace('\n', ' ')
                    .Replace('\t', ' ')
                    .Replace("\\", "")
                    .Replace("\'", "")
                    .Replace("[", "")
                    .Replace("]", ""),
                $"{ogRotation.x.ToString(CultureInfo.InvariantCulture)},{item.floorYOffset.ToString(CultureInfo.InvariantCulture)},{ogRotation.z.ToString(CultureInfo.InvariantCulture)}",
                "Comma separated Vector3 rotation");
            MattyFixes.PluginConfig.ItemClipping.ItemRotations[item] = configEntry;
            configEntry.SettingChanged += (sender, args) => { UpdateItemRotation(modName, item); };
            if (LethalConfigProxy.Enabled)
                LethalConfigProxy.AddConfig(configEntry);
        }

        var rotation = configEntry.Value.Split(",");

        if (rotation.Length != 3)
            return;
        
        item.restingRotation.Set(
            float.Parse(rotation[0], CultureInfo.InvariantCulture),
            float.Parse(rotation[1], CultureInfo.InvariantCulture),
            float.Parse(rotation[2], CultureInfo.InvariantCulture));

        item.floorYOffset = (int)Math.Round(item.restingRotation.y);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.Awake))]
    [HarmonyPriority(0)]
    private static void AwakePatch(StartOfRound __instance, bool __runOriginal)
    {
        if (AsyncLoggerProxy.Enabled)
            AsyncLoggerProxy.WriteEvent(MattyFixes.NAME, "StartOfRound.Awake", "Post");

        if (!MattyFixes.PluginConfig.ItemClipping.Enabled.Value || !__runOriginal)
            return;

        using (DictionaryPool<Item, string>.Get(out var itemDict))
        {
            if (LethalLibProxy.Enabled)
                LethalLibProxy.GetModdedItems(in itemDict);

            if (LethalLevelLoaderProxy.Enabled)
                LethalLevelLoaderProxy.GetModdedItems(in itemDict);

            foreach (var itemType in __instance.allItemsList.itemsList) itemDict.TryAdd(itemType, null);

            foreach (var (item, mod) in itemDict)
                try
                {
                    if (item.spawnPrefab == null)
                        continue;

                    item.spawnPrefab.transform.CacheChildVertexes(logWarningCallback: MattyFixes.Log.LogWarning,
                        logDebugCallback: MattyFixes.PluginConfig.Debug.Verbose.Value ? MattyFixes.Log.LogDebug : null);

                    if (ItemRotations.TryGetValue(item.itemName, out var value))
                        item.restingRotation.Set(value[0], value[1], value[2]);

                    UpdateItemRotation(mod, item);
                }
                catch (Exception ex)
                {
                    MattyFixes.Log.LogError($"{mod}{(mod != null ? "." : "")}{item.itemName} crashed badly ! {ex}");
                }
        }


        MattyFixes.PluginConfig.RemoveOrphans();

        if (AsyncLoggerProxy.Enabled)
            AsyncLoggerProxy.WriteEvent(MattyFixes.NAME, "StartOfRound.Awake", "Finished");
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(NetworkBehaviour), nameof(NetworkBehaviour.OnNetworkSpawn))]
    [HarmonyPriority(20)]
    private static void SpawnPostfix(NetworkBehaviour __instance)
    {
        if (__instance is not GrabbableObject grabbable)
            return;

        if (grabbable is ClipboardItem ||
            (grabbable is PhysicsProp && grabbable.itemProperties.itemName == "Sticky note"))
            return;

        if (StartOfRound.Instance.localPlayerController != null)
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

            if (MattyFixes.PluginConfig.Compatibility.GeneralImprovements.FixItemsLoadingSameRotation.Value || 
                MattyFixes.PluginConfig.Compatibility.SmartItemSaving.SaveItemRotation.Value)
                grabbable.floorYRot = (int)Math.Floor(grabbable.transform.eulerAngles.y - 90f - grabbable.itemProperties.floorYOffset);

            grabbable.transform.rotation = Quaternion.Euler(
                grabbable.itemProperties.restingRotation.x,
                grabbable.floorYRot == -1 ? grabbable.transform.eulerAngles.y :
                grabbable.floorYRot + grabbable.itemProperties.floorYOffset + 90f,
                grabbable.itemProperties.restingRotation.z);
        }
        catch (Exception ex)
        {
            MattyFixes.Log.LogError($"Exception while setting rotation :{ex}");
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NetworkBehaviour), nameof(NetworkBehaviour.OnNetworkSpawn))]
    [HarmonyPriority(900)]
    private static void StartPrefix(NetworkBehaviour __instance)
    {

        if (__instance is not GrabbableObject grabbable)
            return;

        var itemType = grabbable.itemProperties;

        if (!ComputedItems.Add(itemType))
            return;

        if (itemType.isConductiveMetal && 
            MattyFixes.PluginConfig.ReadableMeshes.Enabled.Value &&
            MattyFixes.PluginConfig.ReadableMeshes.FixLightning.Value && 
            !MattyFixes.PluginConfig.LightingParticle.Enabled.Value)
            try
            {
                if (itemType.spawnPrefab != null)
                {
                    MakeMeshReadable(itemType.spawnPrefab);
                }
            }
            catch (Exception ex)
            {
                MattyFixes.Log.LogError($"{itemType.itemName} Failed to mark prefab Mesh Readable! {ex}");
                BrokenMeshItems.Add(itemType);
                MattyFixes.Log.LogWarning($"{itemType.itemName} Added to the ignored Meshes!");
            }

        if (!MattyFixes.PluginConfig.ItemClipping.Enabled.Value)
            return;
        
        try
        {
            if (!MattyFixes.PluginConfig.ItemClipping.ManualOffsetMap.TryGetValue(itemType.itemName,
                    out var offset))
            {
                var targetObject = itemType.spawnPrefab;
                if (targetObject == null)
                    targetObject = __instance.gameObject;

                var prefabGrabbable = targetObject.GetComponent<GrabbableObject>();

                if (prefabGrabbable.TryGetVerticalOffset(out offset, MattyFixes.Log.LogWarning,
                        MattyFixes.PluginConfig.Debug.Verbose.Value ? MattyFixes.Log.LogDebug : null))
                    offset += +MattyFixes.PluginConfig.ItemClipping.VerticalOffset.Value;
                else
                    offset = itemType.verticalOffset;
            }

            itemType.verticalOffset = offset;

            MattyFixes.Log.LogDebug($"{itemType.itemName} new offset is {itemType.verticalOffset}");
        }
        catch (Exception ex)
        {
            MattyFixes.Log.LogError($"{itemType.itemName} Failed to compute vertical offset! {ex}");
        }

    }

    private static void MakeMeshReadable(GameObject go, bool updateOriginal = false,
        Dictionary<MeshFilter, Mesh> reverseMap = null)
    {
        var renderer = go.GetComponent<MeshFilter>();
        var filters = renderer is not null ? [renderer] : go.GetComponentsInChildren<MeshFilter>();

        foreach (var meshFilter in filters)
        {
            var mesh = meshFilter.sharedMesh;

            if (mesh.isReadable) 
                continue;
            
            if (!ReadableMeshMap.TryGetValue(mesh, out var readableMesh))
                readableMesh = MakeReadableMeshCopy(mesh);
            ReadableMeshMap[mesh] = readableMesh;
            if (updateOriginal)
                meshFilter.sharedMesh = readableMesh;
            if (reverseMap != null)
                reverseMap[meshFilter] = mesh;
        }
    }

    private static void ApplyMeshMap(GameObject go, Dictionary<MeshFilter, Mesh> meshMap)
    {
        var renderer = go.GetComponent<MeshFilter>();
        var filters = renderer is not null ? [renderer] : go.GetComponentsInChildren<MeshFilter>();

        foreach (var meshFilter in filters)
            if (meshMap.TryGetValue(meshFilter, out var newmesh))
                meshFilter.sharedMesh = newmesh;
    }

    private static Mesh MakeReadableMeshCopy(Mesh nonReadableMesh)
    {
        var meshCopy = new Mesh();
        meshCopy.indexFormat = nonReadableMesh.indexFormat;

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
        [HarmonyPostfix]
        [HarmonyPatch(typeof(StormyWeather), nameof(StormyWeather.SetStaticElectricityWarning))]
        private static void ChangeParticleShape(StormyWeather __instance, NetworkObject warningObject)
        {
            try
            {
                var shapeModule = __instance.staticElectricityParticle.shape;
                if (MattyFixes.PluginConfig.LightingParticle.Enabled.Value)
                {
                    shapeModule.shapeType = ParticleSystemShapeType.Sphere;
                    shapeModule.radiusThickness = 0.01f;
                    if (!warningObject.gameObject.TryGetRadius(out var minRadius, out var maxRadius,
                            MattyFixes.Log.LogWarning,
                            MattyFixes.PluginConfig.Debug.Verbose.Value ? MattyFixes.Log.LogDebug : null))
                        return;

                    shapeModule.radius = maxRadius;
                    shapeModule.radiusThickness = 1 - minRadius / maxRadius;

                    warningObject.gameObject.TryGetWorldCentroid(out var centroid, MattyFixes.Log.LogWarning,
                        MattyFixes.PluginConfig.Debug.Verbose.Value ? MattyFixes.Log.LogDebug : null);
                    _staticElectricityParticleOffset =
                        centroid - warningObject.transform.position + Vector3.up * 0.5f;
                }
                else
                {
                    var grabbable = warningObject.gameObject.GetComponent<GrabbableObject>();
                    if (MattyFixes.PluginConfig.ReadableMeshes.Enabled.Value &&
                        MattyFixes.PluginConfig.ReadableMeshes.FixLightning.Value &&
                        !BrokenMeshItems.Contains(grabbable.itemProperties))
                        try
                        {
                            MakeMeshReadable(warningObject.gameObject, true, ReverseMeshMap);
                        }
                        catch (Exception ex)
                        {
                            MattyFixes.Log.LogError(
                                $"{grabbable.itemProperties.itemName} Failed to mark prefab Mesh Readable! {ex}");
                            BrokenMeshItems.Add(grabbable.itemProperties);
                            MattyFixes.Log.LogWarning(
                                $"{grabbable.itemProperties.itemName} Added to the ignored Meshes!");
                        }
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
            if (__instance.setStaticToObject == null || !useTargetedObject)
                return;

            if (MattyFixes.PluginConfig.ReadableMeshes.Enabled.Value)
                ApplyMeshMap(__instance.setStaticToObject, ReverseMeshMap);

            ReverseMeshMap.Clear();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(StormyWeather), nameof(StormyWeather.Update))]
        private static void SetCorrectParticlePosition(StormyWeather __instance)
        {
            if (__instance.setStaticToObject == null)
                return;

            if (MattyFixes.PluginConfig.LightingParticle.Enabled.Value)
                __instance.staticElectricityParticle.transform.position += _staticElectricityParticleOffset;
        }
    }
}