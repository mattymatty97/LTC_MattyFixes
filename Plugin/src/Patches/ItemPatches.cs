using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using HarmonyLib;
using MattyFixes.Dependency;
using RuntimeIcons.Utils;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using VertexLibrary;

namespace MattyFixes.Patches;

[HarmonyPatch]
internal static class ItemPatches
{
    private static readonly HashSet<Item> ComputedItems = [];

    private static readonly Dictionary<Mesh, Mesh> ReadableMeshMap = new();

    private static readonly HashSet<Item> BrokenMeshItems = [];

    private static readonly Dictionary<MeshFilter, Mesh> ReverseMeshMap = new();
    private static Vector3 _staticElectricityParticleOffset;


    private static void UpdateItemRotation(Item item, string itemPath = null)
    {
        if (!MattyFixes.PluginConfig.ItemClipping.ItemRotations.TryGetValue(item, out var rotationConfig))
        {
            itemPath ??= ItemCategory.GetPathForItem(item);
            
            var itemSection = Path.GetDirectoryName(itemPath) ?? "";
            var itemName = ItemCategory.SanitizeForConfig(Path.GetFileName(itemPath) ?? item.itemName);

            itemSection = itemSection.Replace(Path.DirectorySeparatorChar, '|');
            itemSection = ItemCategory.SanitizeForConfig(itemSection);
            
            var ogRotation = item.restingRotation;
            ogRotation.y = item.floorYOffset;

            var vanillaDefault =
                $"{ogRotation.x.ToString(CultureInfo.InvariantCulture)},{ogRotation.y.ToString(CultureInfo.InvariantCulture)},{ogRotation.z.ToString(CultureInfo.InvariantCulture)}";

            rotationConfig = new MattyFixes.ItemRotationConfig(
                ogRotation,
                MattyFixes.Instance.Config.Bind(
                    itemSection,
                    itemName,
                    "default",
                    $"Comma separated Vector3 rotation\nvanilla default = '{vanillaDefault}'")
            );

            MattyFixes.PluginConfig.ItemClipping.ItemRotations[item] = rotationConfig;
            rotationConfig.Config.SettingChanged += (sender, args) => { UpdateItemRotation(item, itemPath); };
            if (LethalConfigProxy.Enabled)
                LethalConfigProxy.AddConfig(rotationConfig.Config);
        }

        var parsedRotation = rotationConfig.Original;

        var rotation = rotationConfig.Config.Value.Split(",");

        if (rotation.Length == 3)
            if (float.TryParse(rotation[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x))
                if (float.TryParse(rotation[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
                    if (float.TryParse(rotation[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
                        parsedRotation = new Vector3(x, y, z);

        item.restingRotation = parsedRotation;

        item.floorYOffset = (int)Math.Round(parsedRotation.y);
    }


    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.Start))]
    private static void RegisterItems(StartOfRound __instance, bool __runOriginal)
    {
        if (!MattyFixes.PluginConfig.ItemClipping.Enabled.Value || !__runOriginal)
            return;

        foreach (var item in __instance.allItemsList.itemsList)
        {
            var modTag = ItemCategory.GetTagForItem(item);
            var key = ItemCategory.GetPathForTag(modTag, item);
            key = key.Replace(Path.DirectorySeparatorChar, '/');

            try
            {

                if (modTag.Item1 == "Vanilla" && ItemRotations.TryGetValue(item.itemName, out var value))
                {
                    item.restingRotation.Set(value[0], value[1], value[2]);
                    item.floorYOffset = (int)Math.Round(value[1]);
                }

                UpdateItemRotation(item, ItemCategory.GetPathForTag(modTag, item));
            }
            catch (Exception ex)
            {
                MattyFixes.Log.LogError(
                    $"{key} crashed badly ! {ex}");
            }
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
                    var key = ItemCategory.GetPathForItem(itemType);
                    key = key.Replace(Path.DirectorySeparatorChar, '/');
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

                grabbable.floorYRot =
                    (int)Math.Floor(grabbable.transform.eulerAngles.y - 90f - grabbable.itemProperties.floorYOffset);

                grabbable.transform.rotation = Quaternion.Euler(
                    grabbable.itemProperties.restingRotation.x,
                    grabbable.transform.eulerAngles.y,
                    grabbable.itemProperties.restingRotation.z);
            }
            catch (Exception ex)
            {            
                var key = ItemCategory.GetPathForItem(grabbable.itemProperties);
                key = key.Replace(Path.DirectorySeparatorChar, '/');
                MattyFixes.Log.LogError($"Exception while setting rotation of {key} :{ex}");
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
                var matrix = Matrix4x4.TRS(Vector3.zero, warningObject.transform.rotation,
                    warningObject.transform.lossyScale);

                var shapeModule = __instance.staticElectricityParticle.shape;
                if (MattyFixes.PluginConfig.LightingParticle.Enabled.Value)
                {
                    shapeModule.shapeType = ParticleSystemShapeType.Sphere;
                    shapeModule.radiusThickness = 0.01f;

                    var executionOptions = new ExecutionOptions()
                    {
                        VertexCache = VertexesExtensions.GlobalPartialCache,
                        CullingMask = MattyFixes.VisibleLayerMask,
                        LogHandler = MattyFixes.VerboseMeshLog,
                        OverrideMatrix = matrix
                    };

                    var vertexes = warningObject.transform.GetVertexes(executionOptions);

                    var bounds = vertexes.GetBounds();

                    if (bounds.HasValue)
                    {
                        var (_, radius) = vertexes.GetFarthestPoint(bounds.Value.center);

                        shapeModule.radius = radius;

                        _staticElectricityParticleOffset = bounds.Value.center + Vector3.up * 0.5f;
                    }
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
                            var key = ItemCategory.GetPathForItem(grabbable.itemProperties);
                            key = key.Replace(Path.DirectorySeparatorChar, '/');
                            MattyFixes.Log.LogError(
                                $"{key} Failed to mark prefab Mesh Readable! {ex}");
                            BrokenMeshItems.Add(grabbable.itemProperties);
                            MattyFixes.Log.LogWarning(
                                $"{key} Added to the ignored Meshes!");
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
            [0f, 0f, -90f]
        },
        {
            "Stun grenade",
            [0f, 0f, 90f]
        },
        {
            "Extension ladder",
            [0f, 90f, 0f]
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
            [0f, -90f, 270f]
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
            [90f, 180f, 0f]
        },
        {
            "Candy",
            [90f, -135f, 0f]
        },
        {
            "Cash register",
            [-90f, -90f, 40f]
        },
        {
            "Chemical jug",
            [-90f, 0f, 0f]
        },
        {
            "Clown horn",
            [-90f, -30f, 0f]
        },
        {
            "Large axle",
            [7f, 180f, 0f]
        },
        {
            "Teeth",
            [-90f, 0f, 0f]
        },
        {
            "Dust pan",
            [-90f, 180f, 0f]
        },
        {
            "Egg beater",
            [90f, 180f, 0f]
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
            "Hairdryer",
            [0f, -90f, -90f]
        },
        {
            "Magnifying glass",
            [0f, -45f, -90f]
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
            [-90f, 180f, -90f]
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
            "Remote",
            [-90f, 180f, 0f]
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
            [-90f, 0f, 90f]
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
            [180f, 90f, -5f]
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
            [-90f, 90f, 0f]
        },
        {
            "Comedy",
            [-90f, 90f, 0f]
        },
        {
            "Whoopie cushion",
            [-90f, 180f, 0f]
        },
        {
            "Zed Dog",
            [0f, -90f, 0f]
        }
    };
}