using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MattyFixes.Dependency;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

namespace MattyFixes.Patches
{
    [HarmonyPatch]
    internal static class ItemPatches
    {
        private static readonly HashSet<Item> ComputedItems = [];

        private static readonly HashSet<Item> ReadableObjects = [];

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

        internal static Vector3 FixPlacement(Vector3 hitPoint, Transform shelfTransform, GrabbableObject heldObject)
        {
            hitPoint.y = shelfTransform.position.y + shelfTransform.localScale.z / 2f;
            return hitPoint + Vector3.up * (heldObject.itemProperties.verticalOffset -
                                            MattyFixes.PluginConfig.ItemClipping.VerticalOffset.Value);
        }


        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlaceableObjectsSurface), nameof(PlaceableObjectsSurface.itemPlacementPosition))]
        private static bool ItemPlacementPositionPatch(PlaceableObjectsSurface __instance, ref Vector3 __result,
            Transform gameplayCamera, GrabbableObject heldObject)
        {
            if (!MattyFixes.PluginConfig.ItemClipping.Enabled.Value)
                return true;

            try
            {
                if (Physics.Raycast(gameplayCamera.position, gameplayCamera.forward, out var val, 7f,
                        StartOfRound.Instance.collidersAndRoomMask, (QueryTriggerInteraction)1))
                {
                    var bounds = __instance.placeableBounds.bounds;

                    if (bounds.Contains(val.point))
                    {
                        __result = FixPlacement(val.point, __instance.transform, heldObject);
                        return false;
                    }

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

        private static readonly HashSet<Item> BrokenMeshItems = [];

        private static void UpdateItemRotation(Item item)
        {
            if (!MattyFixes.PluginConfig.ItemClipping.ItemRotations.TryGetValue(item, out var configEntry))
            {
                var ogRotation = item.restingRotation;
                configEntry = MattyFixes.INSTANCE.Config.Bind($"ItemClipping.Rotations", 
                    item.itemName
                        .Replace('\n',' ')
                        .Replace('\t', ' ')
                        .Replace("\\" , "")
                        .Replace("\'","")
                        .Replace("[","")
                        .Replace("]",""), 
                    $"{ogRotation.x},{ogRotation.y},{ogRotation.z}", 
                    "Comma separated Vector3 rotation");
                MattyFixes.PluginConfig.ItemClipping.ItemRotations[item] = configEntry;
                configEntry.SettingChanged += (sender, args) =>
                {
                    UpdateItemRotation(item);
                };
                if (LethalConfigProxy.Enabled)
                    LethalConfigProxy.AddConfig(configEntry, false);
            }

            var rotation = configEntry.Value.Split(",");
            
            if (rotation.Length == 3)
                item.restingRotation.Set(
                    float.Parse(rotation[0]),
                    float.Parse(rotation[1]),
                    float.Parse(rotation[2]));

        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.Awake))]
        [HarmonyPriority(0)]
        private static void AwakePatch(StartOfRound __instance, bool __runOriginal)
        {
            if (AsyncLoggerProxy.Enabled)
                AsyncLoggerProxy.WriteEvent(MattyFixes.NAME, "StartOfRound.Awake", $"Post");
            
            if (!MattyFixes.PluginConfig.ItemClipping.Enabled.Value || !__runOriginal)
                return;

            foreach (var itemType in __instance.allItemsList.itemsList)
            {
                try
                {
                    if (itemType.spawnPrefab == null)
                        continue;

                    if (ItemRotations.TryGetValue(itemType.itemName, out List<float> value))
                        itemType.restingRotation.Set(value[0], value[1], value[2]);
                    
                    UpdateItemRotation(itemType);
                }
                catch (Exception ex)
                {
                    MattyFixes.Log.LogError($"{itemType.itemName} crashed badly ! {ex}");
                }
            }
            MattyFixes.PluginConfig.RemoveOrphans();
            
            if (AsyncLoggerProxy.Enabled)
                AsyncLoggerProxy.WriteEvent(MattyFixes.NAME, "StartOfRound.Awake", $"Finished");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(NetworkBehaviour), nameof(NetworkBehaviour.OnNetworkSpawn))]
        [HarmonyPriority(20)]
        private static void SpawnPostfix(NetworkBehaviour __instance)
        {

            if (!(__instance is GrabbableObject grabbable))
                return;
            
            if (!StartOfRound.Instance.shipInnerRoomBounds.bounds.Contains(__instance.transform.position))
                return;
            
            try
            {

                if (MattyFixes.PluginConfig.Radar.RemoveOnShip.Value)
                {
                    grabbable.isInElevator = true;
                    grabbable.isInShipRoom = true;
                }
                
                
                if (!MattyFixes.PluginConfig.ItemClipping.RotateOnSpawn.Value)
                    return;
                
                if (grabbable is ClipboardItem || (grabbable is PhysicsProp && grabbable.itemProperties.itemName == "Sticky note"))
                    return;
                
                grabbable.transform.rotation = Quaternion.Euler(
                    grabbable.itemProperties.restingRotation.x,
                    grabbable.floorYRot == -1
                        ? grabbable.transform.eulerAngles.y
                        : grabbable.floorYRot + grabbable.itemProperties.floorYOffset + 90f,
                    grabbable.itemProperties.restingRotation.z);
            }
            catch (Exception ex)
            {
                MattyFixes.Log.LogError($"Exception while setting rotation :{ex}");
            }
        }

        private static List<Vector3> GetChildVertexes(Transform target)
        {
            List<Vector3> vertices = [];
            var renderers = target.GetComponents<Renderer>();
            
            //TODO: remove log
            MattyFixes.Log.LogDebug($"Processing {target.parent?.name}.{target.name}");

            foreach (var renderer in renderers.Where(r => r.enabled))
            {
                List<Vector3> rVertices = [];
                
                //TODO: remove log
                MattyFixes.Log.LogDebug($"Processing {target.parent?.name}.{target.name} renderer {renderer.GetType().Name}");
                
                switch (renderer)
                {
                    case SkinnedMeshRenderer skinnedMeshRenderer:
                    {
                        var mesh = skinnedMeshRenderer.sharedMesh;
                        if (mesh.isReadable)
                            mesh.GetVertices(rVertices);
                        else
                            rVertices = GetNonReadableVertices(mesh);
                        break;
                    }
                    case MeshRenderer:
                    {
                        var filter = renderer.GetComponent<MeshFilter>();
                        var mesh = filter.sharedMesh;
                        if (mesh.isReadable)
                            mesh.GetVertices(rVertices);
                        else
                            rVertices = GetNonReadableVertices(mesh);
                        break;
                    }
                    case ParticleSystemRenderer:
                        break;
                    default:
                    {
                        var bounds = renderer.bounds;
                        rVertices.Add(bounds.min);
                        rVertices.Add(new Vector3(bounds.min.x,bounds.min.y, bounds.max.z));
                        rVertices.Add(new Vector3(bounds.min.x,bounds.max.y, bounds.max.z));
                        rVertices.Add(new Vector3(bounds.max.x,bounds.min.y, bounds.max.z));
                        rVertices.Add(new Vector3(bounds.max.x,bounds.min.y, bounds.min.z));
                        rVertices.Add(new Vector3(bounds.max.x,bounds.max.y, bounds.min.z));
                        rVertices.Add(bounds.max);
                        break;
                    }
                }

                float? rMin = rVertices.Count > 0 ? rVertices.Min(v => v.y) : null;
                
                //TODO: remove log
                MattyFixes.Log.LogDebug($"Processing {target.parent?.name}.{target.name} renderer {renderer.GetType().Name} min {rMin}");

                vertices.AddRange(rVertices);
            }

            foreach (Transform child in target.transform)
            {
                vertices.AddRange(GetChildVertexes(child));
            }

            var tmp = vertices.Select(target.TransformVector).ToList();
            float? min = tmp.Count > 0 ? tmp.Min(v => v.y) : null;
            MattyFixes.Log.LogDebug($"Processing {target.name} min {min}");
            
            return tmp;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GrabbableObject), nameof(GrabbableObject.Start))]
        [HarmonyPriority(9999)]
        private static void StartPrefix(GrabbableObject __instance)
        {
            if (!MattyFixes.PluginConfig.ItemClipping.Enabled.Value)
                return;
            
            var itemType = __instance.itemProperties;
            
            if (ComputedItems.Contains(itemType))
                return;

            if (itemType.isConductiveMetal)
            {
                try
                {
                    if (itemType.spawnPrefab != null)
                    {
                        MakeMeshReadable(itemType.spawnPrefab);

                        ReadableObjects.Add(itemType);
                    }
                }
                catch (Exception ex)
                {
                    MattyFixes.Log.LogError($"{itemType.itemName} Failed to mark prefab Mesh Readable! {ex}");
                    BrokenMeshItems.Add(itemType);
                    MattyFixes.Log.LogWarning($"{itemType.itemName} Added to the ignored Meshes!");
                }
            }

            try
            {
                if (!MattyFixes.PluginConfig.ItemClipping.ManualOffsetMap.TryGetValue(itemType.itemName,
                        out var offset))
                {

                    var targetObject = itemType.spawnPrefab;

                    if (targetObject == null)
                        targetObject = __instance.gameObject;

                    var memRotation = targetObject.transform.rotation;

                    targetObject.transform.rotation = Quaternion.Euler(itemType.restingRotation);

                    var vertices = GetChildVertexes(targetObject.transform);

                    targetObject.transform.rotation = memRotation;
                    offset = vertices.Count > 0 ? vertices.Min(v => v.y) : itemType.verticalOffset;
                }

                itemType.verticalOffset = offset + MattyFixes.PluginConfig.ItemClipping.VerticalOffset.Value;

                MattyFixes.Log.LogDebug($"{itemType.itemName} new offset is {itemType.verticalOffset}");
            }
            catch (Exception ex)
            {
                MattyFixes.Log.LogError($"{itemType.itemName} Failed to compute vertical offset! {ex}");
            }

            ComputedItems.Add(__instance.itemProperties);
        }


        private static Bounds? CalculateRendererBounds(GameObject go)
        {
            var oRenderer = go.GetComponent<Renderer>();
            Renderer[] renderers = oRenderer != null ? [oRenderer] : go.GetComponentsInChildren<Renderer>();

            Bounds? bounds = null;

            foreach (var renderer in renderers.Where(r => r.gameObject.activeSelf && r.enabled))
            {
                var rBounds = renderer.bounds;
                
                if (renderer is ParticleSystemRenderer)
                    continue;
                
                if (rBounds.size == Vector3.zero)
                    continue;
                
                if (AsyncLoggerProxy.Enabled)
                    AsyncLoggerProxy.WriteData(MattyFixes.NAME, "Bounds", $"{go.name}({go.GetInstanceID()}),{renderer.gameObject.name} rBounds was {rBounds}");

                if (bounds.HasValue){
                    var b = bounds.Value;
                    b.Encapsulate(rBounds);
                    bounds = b;
                }
                else
                    bounds = rBounds;
                
                if (AsyncLoggerProxy.Enabled)
                    AsyncLoggerProxy.WriteData(MattyFixes.NAME, "Bounds", $"{go.name}({go.GetInstanceID()}) Bounds is {bounds.Value}");
            }

            return bounds;
        }

        private static void MakeMeshReadable(GameObject go, bool updateOriginal = false,
            Dictionary<MeshFilter, Mesh> reverseMap = null)
        {
            var renderer = go.GetComponent<MeshFilter>();
            var filters = renderer is not null ? [renderer] : go.GetComponentsInChildren<MeshFilter>();

            foreach (var meshFilter in filters)
            {
                var mesh = meshFilter.mesh;

                if (!mesh.isReadable)
                {
                    if (!ReadableMeshMap.TryGetValue(mesh, out var readableMesh))
                        readableMesh = MakeReadableMeshCopy(mesh);
                    ReadableMeshMap[mesh] = readableMesh;
                    if (updateOriginal)
                        meshFilter.mesh = readableMesh;
                    if (reverseMap != null)
                        reverseMap[meshFilter] = mesh;
                }
            }
        }

        private static void ApplyMeshMap(GameObject go, Dictionary<MeshFilter, Mesh> meshMap)
        {
            var renderer = go.GetComponent<MeshFilter>();
            var filters = renderer is not null ? [renderer] : go.GetComponentsInChildren<MeshFilter>();

            foreach (var meshFilter in filters)
            {
                var mesh = meshFilter.mesh;

                if (meshMap.TryGetValue(meshFilter, out var newmesh))
                {
                    meshFilter.mesh = newmesh;
                }
            }
        }

        private static Mesh MakeReadableMeshCopy(Mesh nonReadableMesh)
        {
            Mesh meshCopy = new Mesh();
            meshCopy.indexFormat = nonReadableMesh.indexFormat;

            // Handle vertices
            nonReadableMesh.vertexBufferTarget = GraphicsBuffer.Target.Vertex;
            if (nonReadableMesh.vertexBufferCount > 0)
            {
                GraphicsBuffer verticesBuffer = nonReadableMesh.GetVertexBuffer(0);
                int totalSize = verticesBuffer.stride * verticesBuffer.count;
                byte[] data = new byte[totalSize];
                verticesBuffer.GetData(data);
                meshCopy.SetVertexBufferParams(nonReadableMesh.vertexCount, nonReadableMesh.GetVertexAttributes());
                meshCopy.SetVertexBufferData(data, 0, 0, totalSize);
                verticesBuffer.Release();
            }


            // Handle triangles
            nonReadableMesh.indexBufferTarget = GraphicsBuffer.Target.Index;
            meshCopy.subMeshCount = nonReadableMesh.subMeshCount;
            GraphicsBuffer indexesBuffer = nonReadableMesh.GetIndexBuffer();
            int tot = indexesBuffer.stride * indexesBuffer.count;
            byte[] indexesData = new byte[tot];
            indexesBuffer.GetData(indexesData);
            meshCopy.SetIndexBufferParams(indexesBuffer.count, nonReadableMesh.indexFormat);
            meshCopy.SetIndexBufferData(indexesData, 0, 0, tot);
            indexesBuffer.Release();

            // Restore submesh structure
            uint currentIndexOffset = 0;
            for (int i = 0; i < meshCopy.subMeshCount; i++)
            {
                uint subMeshIndexCount = nonReadableMesh.GetIndexCount(i);
                meshCopy.SetSubMesh(i, new SubMeshDescriptor((int)currentIndexOffset, (int)subMeshIndexCount));
                currentIndexOffset += subMeshIndexCount;
            }

            // Recalculate normals and bounds
            meshCopy.RecalculateNormals();
            meshCopy.RecalculateBounds();

            meshCopy.name = $"Readable {nonReadableMesh.name}";
            return meshCopy;
        }

        private static List<Vector3> GetNonReadableVertices(Mesh nonReadableMesh)
        {
            Mesh meshCopy = new Mesh();
            meshCopy.indexFormat = nonReadableMesh.indexFormat;

            // Handle vertices
            nonReadableMesh.vertexBufferTarget = GraphicsBuffer.Target.Vertex;
            if (nonReadableMesh.vertexBufferCount > 0)
            {
                GraphicsBuffer verticesBuffer = nonReadableMesh.GetVertexBuffer(0);
                int totalSize = verticesBuffer.stride * verticesBuffer.count;
                byte[] data = new byte[totalSize];
                verticesBuffer.GetData(data);
                meshCopy.SetVertexBufferParams(nonReadableMesh.vertexCount, nonReadableMesh.GetVertexAttributes());
                meshCopy.SetVertexBufferData(data, 0, 0, totalSize);
                verticesBuffer.Release();
            }

            var vertices = new List<Vector3>();
            meshCopy.GetVertices(vertices);
            return vertices;
        }
        
        private static readonly Dictionary<MeshFilter, Mesh> ReverseMeshMap = new Dictionary<MeshFilter, Mesh>();

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
                        Bounds? bounds = CalculateRendererBounds(warningObject.gameObject);

                        shapeModule.shapeType = ParticleSystemShapeType.Sphere;
                        shapeModule.radiusThickness = 0.01f;
                        if (!bounds.HasValue)
                            return;
                        var extents = bounds.Value.extents;
                        shapeModule.radius = Math.Max(extents.x, Math.Max(extents.y, extents.z));
                    }
                    else
                    {
                        var grabbable = warningObject.gameObject.GetComponent<GrabbableObject>();
                        if (MattyFixes.PluginConfig.ReadableMeshes.Enabled.Value && MattyFixes.PluginConfig.ReadableMeshes.FixLignting.Value &&
                            !BrokenMeshItems.Contains(grabbable.itemProperties))
                        {
                            try
                            {
                                MakeMeshReadable(warningObject.gameObject, true, ReverseMeshMap);
                            }
                            catch (Exception ex)
                            {
                                MattyFixes.Log.LogError(
                                    $"{grabbable.itemProperties.itemName} Failed to mark prefab Mesh Readable! {ex}");
                                BrokenMeshItems.Add(grabbable.itemProperties);
                                MattyFixes.Log.LogWarning($"{grabbable.itemProperties.itemName} Added to the ignored Meshes!");
                            }
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
                {
                    var bounds = CalculateRendererBounds(__instance.setStaticToObject);
                    if (!bounds.HasValue)
                        return;

                    __instance.staticElectricityParticle.transform.position = bounds.Value.center + Vector3.up * 0.5f;
                }
            }
        }
    }
}