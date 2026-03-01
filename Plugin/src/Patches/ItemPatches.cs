using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using MattyFixes.Dependency;
using MattyFixes.Interfaces;
using MattyFixes.Utils;
using MattyFixes.Utils.IL;
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
    
    // ReSharper disable function SuspiciousTypeConversion.Global
    private static bool TryUpdateItemRotation(Item item)
    {
        if (((IInjectedItem)item).MattyFixes_IsRegistered)
            return false;
        
        ((IInjectedItem)item).MattyFixes_IsRegistered = true;
        
        if (!MattyFixes.PluginConfig.ItemClipping.ItemRotations.TryGetValue(item, out var rotationConfig))
        {
            var itemPath = item.GetPath();
            
            var itemSection = Path.GetDirectoryName(itemPath) ?? "Unknown";
            var itemName = ItemCategory.SanitizeForConfig(Path.GetFileName(itemPath));

            itemSection = itemSection.Replace(Path.AltDirectorySeparatorChar, '|');
            itemSection = ItemCategory.SanitizeForConfig(itemSection);
            
            var ogRotation = item.restingRotation;
            ogRotation.y = item.floorYOffset;

            var vanillaDefault =
                $"{ogRotation.x.ToString(CultureInfo.InvariantCulture)},{ogRotation.y.ToString(CultureInfo.InvariantCulture)},{ogRotation.z.ToString(CultureInfo.InvariantCulture)}";

            var defValue = "default";

            if (ItemRotations.TryGetValue(itemPath, out var value))
            {
                defValue = $"{value[0]},{value[1]},{value[2]}";
            }

            rotationConfig = new MattyFixes.ItemRotationConfig(
                ogRotation,
                MattyFixes.Instance.Config.Bind(
                    itemSection,
                    itemName,
                    defValue,
                    $"Comma separated Vector3 rotation\nvanilla default = '{vanillaDefault}'")
            );

            MattyFixes.PluginConfig.ItemClipping.ItemRotations[item] = rotationConfig;
            rotationConfig.Config.SettingChanged += (_, _) => { TryUpdateItemRotation(item); };
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

        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Item), "Awake")]
    private static void OnNewItem(Item __instance)
    {
        if (!MenuManagerPatch.GameHasLoaded)
        {
            ((IInjectedItem)__instance).MattyFixes_ItemType = ItemCategory.ItemType.Vanilla;
            ((IInjectedItem)__instance).MattyFixes_Path = __instance.ComputePath("Vanilla");
        }
        TryUpdateItemRotation(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.Start))]
    private static void RegisterItems(StartOfRound __instance, bool __runOriginal)
    {
        if (!MattyFixes.PluginConfig.ItemClipping.Enabled.Value || !__runOriginal)
            return;

        foreach (var item in __instance.allItemsList.itemsList)
        {
            try
            {
                TryUpdateItemRotation(item);
            }
            catch (Exception ex)
            {
                MattyFixes.Log.LogError($"{item.GetPath()} crashed badly ! {ex}");
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
            
            TryUpdateItemRotation(itemType);

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
                        CacheReadableMeshes(itemType.spawnPrefab);
                    }
                }
                catch (Exception ex)
                {
                    var key = itemType.GetPath();
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
                MattyFixes.Log.LogError($"Exception while setting rotation of {grabbable.itemProperties.GetPath()} :{ex}");
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


    private static Mesh GetReadableMesh(Mesh original, out bool wasReadable)
    {
        wasReadable = true;
        
        if (original.isReadable)
            return original;
        
        wasReadable = false;

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

            GetReadableMesh(mesh, out _);
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
        private static (Vector3 position, Vector3 rotation, Vector3 scale)? OriginalOffsets = null;
        
        [HarmonyTranspiler]
        [HarmonyPatch(typeof(StormyWeather), nameof(StormyWeather.SetStaticElectricityWarning))]
        private static IEnumerable<CodeInstruction> SetStaticElectricityWarning(IEnumerable<CodeInstruction> instructions,
            ILGenerator ilGenerator)
        {
            var codes = instructions.ToList();
            
            var staticElectricityParticleField = typeof(StormyWeather).GetField(nameof(StormyWeather.staticElectricityParticle), AccessTools.all);
            var setTimeMethod = typeof(ParticleSystem).GetProperty(nameof(ParticleSystem.time), AccessTools.all)?.GetSetMethod();
            var playMethod = typeof(ParticleSystem).GetMethod(nameof(ParticleSystem.Play), 0, []);

            var changeMethod = typeof(StormyWeatherPatch).GetMethod(nameof(ChangeParticleShape), AccessTools.all);
            
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
                var matrix = Matrix4x4.TRS(Vector3.zero, warningObject.transform.rotation,
                    warningObject.transform.lossyScale);

                var particleSystem = __instance.staticElectricityParticle;
                var shapeModule = particleSystem.shape;
                OriginalOffsets = (shapeModule.position, shapeModule.rotation, shapeModule.scale);
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

                    if (!bounds.HasValue) 
                        return;
                    
                    var (_, radius) = vertexes.GetFarthestPoint(bounds.Value.center);

                    shapeModule.radius = radius;

                    shapeModule.position = bounds.Value.center + Vector3.up * 0.5f;
                }
                else
                {
                    var grabbable = warningObject.gameObject.GetComponent<GrabbableObject>();
                    if (!MattyFixes.PluginConfig.ReadableMeshes.Enabled.Value ||
                        !MattyFixes.PluginConfig.ReadableMeshes.FixLightning.Value ||
                        BrokenMeshItems.Contains(grabbable.itemProperties)) 
                        return;
                    
                    try
                    {
                        var rendererGo = shapeModule.meshRenderer.gameObject;
                        if (!rendererGo.TryGetComponent<MeshFilter>(out var meshFilter))
                            return;
                                    
                        var readableMesh = GetReadableMesh(meshFilter.sharedMesh, out var wasReadable);
                        if (wasReadable)
                            return;
                        
                        shapeModule.shapeType     = ParticleSystemShapeType.Mesh;
                        shapeModule.mesh          = readableMesh;
                        shapeModule.meshRenderer  = null;
                        shapeModule.position      = warningObject.transform.InverseTransformPoint(rendererGo.transform.position);
                        shapeModule.rotation      = (Quaternion.Inverse(particleSystem.transform.rotation) 
                                                     * rendererGo.transform.rotation).eulerAngles;
                        var meshWorldScale = rendererGo.transform.lossyScale;
                        var psWorldScale = particleSystem.transform.lossyScale;

                        shapeModule.scale = new Vector3(
                            meshWorldScale.x / psWorldScale.x,
                            meshWorldScale.y / psWorldScale.y,
                            meshWorldScale.z / psWorldScale.z);
                    }
                    catch (Exception ex)
                    {
                        var item = grabbable.itemProperties;
                        var key = item.GetPath();
                        MattyFixes.Log.LogError($"{key} Failed to make prefab Mesh Readable! {ex}");
                        BrokenMeshItems.Add(item);
                        MattyFixes.Log.LogWarning($"{key} Added to the ignored Meshes!");
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

            var shapeModule = __instance.staticElectricityParticle.shape;
            shapeModule.shapeType = ParticleSystemShapeType.MeshRenderer;
            
            if (!OriginalOffsets.HasValue)
                return;

            shapeModule.position = OriginalOffsets.Value.position;
            shapeModule.rotation = OriginalOffsets.Value.rotation;
            shapeModule.scale    = OriginalOffsets.Value.scale;
            OriginalOffsets      = null;
        }
    }

    private static readonly Dictionary<string, List<float>> ItemRotations = new()
    {
        {
            "Vanilla/Flashlight",
            [90f, 0f, 90f]
        },
        {
            "Vanilla/Jetpack",
            [45f, 0f, 0f]
        },
        {
            "Vanilla/Key",
            [180f, 0f, 90f]
        },
        {
            "Vanilla/Apparatus",
            [0f, 0f, 135f]
        },
        {
            "Vanilla/Pro-flashlight",
            [90f, 0f, 90f]
        },
        {
            "Vanilla/Shovel",
            [0f, 0f, -90f]
        },
        {
            "Vanilla/Stun grenade",
            [0f, 0f, 90f]
        },
        {
            "Vanilla/Extension ladder",
            [0f, 90f, 0f]
        },
        {
            "Vanilla/TZP-Inhalant",
            [0f, 0f, -90f]
        },
        {
            "Vanilla/Zap gun",
            [95f, 0f, 90f]
        },
        {
            "Vanilla/Magic 7 ball",
            [0f, 0f, 0f]
        },
        {
            "Vanilla/Airhorn",
            [0f, -90f, 270f]
        },
        {
            "Vanilla/Big bolt",
            [-21f, 0f, 0f]
        },
        {
            "Vanilla/Bottles",
            [-90f, 0f, 0f]
        },
        {
            "Vanilla/Brush",
            [90f, 180f, 0f]
        },
        {
            "Vanilla/Candy",
            [90f, -135f, 0f]
        },
        {
            "Vanilla/Cash register",
            [-90f, -90f, 40f]
        },
        {
            "Vanilla/Chemical jug",
            [-90f, 0f, 0f]
        },
        {
            "Vanilla/Clown horn",
            [-90f, -30f, 0f]
        },
        {
            "Vanilla/Large axle",
            [7f, 180f, 0f]
        },
        {
            "Vanilla/Teeth",
            [-90f, 0f, 0f]
        },
        {
            "Vanilla/Dust pan",
            [-90f, 180f, 0f]
        },
        {
            "Vanilla/Egg beater",
            [90f, 180f, 0f]
        },
        {
            "Vanilla/V-type engine",
            [-90f, 0f, 0f]
        },
        {
            "Vanilla/Plastic fish",
            [-45f, 0f, 90f]
        },
        {
            "Vanilla/Laser pointer",
            [0f, 0f, 0f]
        },
        {
            "Vanilla/Gold bar",
            [-90f, 0f, -90f]
        },
        {
            "Vanilla/Hairdryer",
            [0f, -90f, -90f]
        },
        {
            "Vanilla/Magnifying glass",
            [0f, -45f, -90f]
        },
        {
            "Vanilla/Cookie mold pan",
            [-90f, 0f, 90f]
        },
        {
            "Vanilla/Mug",
            [-90f, 0f, 0f]
        },
        {
            "Vanilla/Perfume bottle",
            [-90f, 0f, 0f]
        },
        {
            "Vanilla/Old phone",
            [-90f, 180f, -90f]
        },
        {
            "Vanilla/Jar of pickles",
            [-90f, 0f, 0f]
        },
        {
            "Vanilla/Pill bottle",
            [-90f, 0f, 0f]
        },
        {
            "Vanilla/Remote",
            [-90f, 180f, 0f]
        },
        {
            "Vanilla/Ring",
            [0f, -90f, 90f]
        },
        {
            "Vanilla/Toy robot",
            [-90f, 0f, 0f]
        },
        {
            "Vanilla/Rubber Ducky",
            [-90f, 0f, 90f]
        },
        {
            "Vanilla/Steering wheel",
            [-90f, 0f, 0f]
        },
        {
            "Vanilla/Toothpaste",
            [-90f, 0f, 0f]
        },
        {
            "Vanilla/Hive",
            [7f, 0f, 0f]
        },
        {
            "Vanilla/Radar-booster",
            [0f, 0f, 0f]
        },
        {
            "Vanilla/Shotgun",
            [180f, 90f, -5f]
        },
        {
            "Vanilla/Ammo",
            [0f, 0f, 90f]
        },
        {
            "Vanilla/Spray paint",
            [0f, 0f, 195f]
        },
        {
            "Vanilla/Homemade flashbang",
            [0f, 0f, 90f]
        },
        {
            "Vanilla/Gift",
            [-90f, 0f, 0f]
        },
        {
            "Vanilla/Flask",
            [25f, 0f, 0f]
        },
        {
            "Vanilla/Tragedy",
            [-90f, 90f, 0f]
        },
        {
            "Vanilla/Comedy",
            [-90f, 90f, 0f]
        },
        {
            "Vanilla/Whoopie cushion",
            [-90f, 180f, 0f]
        },
        {
            "Vanilla/Zed Dog",
            [0f, -90f, 0f]
        }
    };
}
