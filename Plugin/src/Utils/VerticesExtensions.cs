using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MattyFixes.Utils;

public static class VerticesExtensions
{
    private static readonly Dictionary<Mesh, Vector3[]> VerticesCache = new();

    //GrabbableObject EXTENSIONS
    public static bool TryGetVerticalOffset(this GrabbableObject target, out float offset, Matrix4x4? overrideMatrix = null)
    {
        string Logfunc(List<Vector3> vertices)
        {
            return TryGetBounds(vertices, out var bounds) ? $"{bounds} Min {bounds.min} Max {bounds.max}" : "";
        }

        var transform = target.transform;
        var localMatrix = overrideMatrix ?? Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(
            target.itemProperties.restingRotation.x, target.itemProperties.floorYOffset + 90f,
            target.itemProperties.restingRotation.z), transform.localScale);

        var vertices = ListPool<Vector3>.Get();

        transform.GetChildVertexes(vertices, localMatrix, logFunc: Logfunc);
        var retcode = TryGetBounds(vertices, out var bounds);
        offset = -bounds.min.y;
        ListPool<Vector3>.Release(vertices);
        return retcode;
    }


    //GameObject EXTENSIONS
    public static bool TryGetBounds(this GameObject target, out Bounds bounds, Matrix4x4? overrideMatrix = null)
    {
        string Logfunc(List<Vector3> vertices)
        {
            return TryGetBounds(vertices, out var bounds) ? $"{bounds} Min {bounds.min} Max {bounds.max}" : "";
        }

        var transform = target.transform;
        var vertices = ListPool<Vector3>.Get();

        var localMatrix = overrideMatrix ?? Matrix4x4.identity;

        transform.GetChildVertexes(vertices, localMatrix, logFunc: Logfunc);
        var retcode = TryGetBounds(vertices, out bounds);
        ListPool<Vector3>.Release(vertices);
        return retcode;
    }

    public static bool TryGetWorldBounds(this GameObject target, out Bounds bounds)
    {
        string Logfunc(List<Vector3> vertices)
        {
            return TryGetBounds(vertices, out var bounds) ? $"{bounds} Min {bounds.min} Max {bounds.max}" : "";
        }

        var transform = target.transform;
        var vertices = ListPool<Vector3>.Get();
        
        var localMatrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
        transform.GetChildVertexes(vertices, localMatrix, logFunc: Logfunc);

        var retcode = TryGetBounds(vertices, out bounds);
        ListPool<Vector3>.Release(vertices);
        return retcode;
    }

    public static bool TryGetRadius(this GameObject target, out float radius)
    {
        string Logfunc(List<Vector3> vertices)
        {
            return TryGetBounds(vertices, out var bounds) ? $"{bounds} Min {bounds.min} Max {bounds.max}" : "";
        }

        var transform = target.transform;
        var localMatrix = Matrix4x4.TRS(Vector3.zero, transform.rotation, transform.lossyScale);
        var vertices = ListPool<Vector3>.Get();

        transform.GetChildVertexes(vertices, localMatrix, logFunc: Logfunc);

        var retcode = TryGetBounds(vertices, out var bounds);

        radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
        
        ListPool<Vector3>.Release(vertices);
        return retcode;
    }

    //Transform Extensions
    // ReSharper disable once MemberCanBePrivate.Global
    public static void GetChildVertexes(this Transform target, List<Vector3> outVertices,
        Matrix4x4 localMatrix = default,
        string path = "", Func<List<Vector3>, string> logFunc = null)
    {
        
        MattyFixes.VerboseMeshLog(LogLevel.Debug, () => $"Processing {path}/{target.name}");

        if (target.TryGetComponent<ScanNodeProperties>(out _))
        {
            MattyFixes.VerboseMeshLog(LogLevel.Debug, () => $"Skipping {path}/{target.name}!");
            return;
        }

        using var pooledRenderers = ListPool<Renderer>.Get(out var renderers);
        target.GetComponents(renderers);

        using (ListPool<Vector3>.Get(out var vertices))
        {
            foreach (var renderer in renderers.Where(r => r.enabled))
                using (ListPool<Vector3>.Get(out var rVertices))
                {
                    switch (renderer)
                    {
                        case SkinnedMeshRenderer skinnedMeshRenderer:
                        {
                            var mesh = skinnedMeshRenderer.sharedMesh;
                            if (mesh == null)
                            {
                                MattyFixes.VerboseMeshLog(LogLevel.Warning, () => $"{renderer.GetType()} in {path} is missing a mesh");
                                continue;
                            }

                            if (VerticesCache.TryGetValue(mesh, out var cached))
                            {
                                MattyFixes.VerboseMeshLog(LogLevel.Debug, () => $"Cache hit {path}/{target.name} renderer {renderer.GetType().Name}");
                                rVertices.AddRange(cached);
                            }
                            else
                            {
                                var matrix = Matrix4x4.TRS(target.localPosition, target.localRotation, target.localScale).inverse;
                                using (ListPool<Vector3>.Get(out var tmpVertices))
                                {
                                    var tmpMesh = new Mesh();

                                    skinnedMeshRenderer.BakeMesh(tmpMesh, true);

                                    if (tmpMesh.isReadable)
                                        tmpMesh.GetVertices(tmpVertices);
                                    else
                                        tmpMesh.GetNonReadableVertices(tmpVertices);
                                    
                                    rVertices.AddRange(tmpVertices.Select(matrix.MultiplyPoint3x4));
                                    
                                    Object.Destroy(tmpMesh);
                                }
                                VerticesCache[mesh] = rVertices.ToArray();
                            }

                            break;
                        }
                        case MeshRenderer:
                        {
                            var filter = renderer.GetComponent<MeshFilter>();
                            if (filter == null)
                            {
                                MattyFixes.VerboseMeshLog(LogLevel.Warning, () => $"{renderer.GetType()} in {path} is missing a MeshFilter");
                                continue;
                            }

                            var mesh = filter.sharedMesh;

                            if (mesh == null)
                            {
                                MattyFixes.VerboseMeshLog(LogLevel.Warning, () => $"{renderer.GetType()} in {path} is missing a mesh");
                                continue;
                            }

                            if (VerticesCache.TryGetValue(mesh, out var cached))
                            {
                                MattyFixes.VerboseMeshLog(LogLevel.Debug, () => $"Cache hit {path}/{target.name} renderer {renderer.GetType().Name}");
                                rVertices.AddRange(cached);
                            }
                            else
                            {
                                if (mesh.isReadable)
                                    mesh.GetVertices(rVertices);
                                else
                                    mesh.GetNonReadableVertices(rVertices);

                                VerticesCache[mesh] = rVertices.ToArray();
                            }

                            break;
                        }
                        case ParticleSystemRenderer:
                            break;
                        default:
                        {
                            var bounds = renderer.bounds;
                            rVertices.Add(bounds.min);
                            rVertices.Add(new Vector3(bounds.min.x, bounds.min.y, bounds.max.z));
                            rVertices.Add(new Vector3(bounds.min.x, bounds.max.y, bounds.max.z));
                            rVertices.Add(new Vector3(bounds.max.x, bounds.min.y, bounds.max.z));
                            rVertices.Add(new Vector3(bounds.max.x, bounds.min.y, bounds.min.z));
                            rVertices.Add(new Vector3(bounds.max.x, bounds.max.y, bounds.min.z));
                            rVertices.Add(bounds.max);
                            break;
                        }
                    }

                    MattyFixes.VerboseMeshLog(LogLevel.Debug, () => $"Processing {path}/{target.name} renderer {renderer.GetType().Name} {logFunc?.Invoke(rVertices)}");
                    
                    vertices.AddRange(rVertices);
                }

            foreach (Transform child in target.transform)
            {
                if (!child.gameObject.activeSelf)
                    continue;

                var childMatrix = Matrix4x4.TRS(child.localPosition, child.localRotation, child.localScale);

                var childVertices = ListPool<Vector3>.Get();
                GetChildVertexes(child, childVertices, childMatrix, path + "/" + target.name, logFunc);

                vertices.AddRange(childVertices);
                ListPool<Vector3>.Release(childVertices);
            }

            outVertices.AddRange(vertices.Select(localMatrix.MultiplyPoint3x4));
        }

        MattyFixes.VerboseMeshLog(LogLevel.Debug, () => $"Found {path}/{target.name} {logFunc?.Invoke(outVertices)}");
    }

    public static void CacheChildVertexes(this Transform target, string path = "")
    {
        MattyFixes.VerboseMeshLog(LogLevel.Info, () => $"Caching {path}/{target.name}");

        if (target.TryGetComponent<ScanNodeProperties>(out _))
        {
            MattyFixes.VerboseMeshLog(LogLevel.Debug, () => $"Skipping {path}/{target.name}!");
            return;
        }

        using var pooledRenderers = ListPool<Renderer>.Get(out var renderers);
        target.GetComponents(renderers);

        foreach (var renderer in renderers.Where(r => r.enabled))
        {
            switch (renderer)
            {
                case SkinnedMeshRenderer skinnedMeshRenderer:
                {
                    var mesh = skinnedMeshRenderer.sharedMesh;
                    if (mesh == null)
                    {
                        MattyFixes.VerboseMeshLog(LogLevel.Warning, () => $"{renderer.GetType()} in {path} is missing a mesh");
                        continue;
                    }

                    if (!VerticesCache.ContainsKey(mesh))
                    {
                        var tmpMesh = new Mesh();

                        skinnedMeshRenderer.BakeMesh(tmpMesh, true);

                        if (tmpMesh.isReadable)
                            tmpMesh.CacheVertices(mesh);
                        else
                            tmpMesh.CacheNonReadableVertices(mesh);

                        Object.Destroy(tmpMesh);
                    }

                    break;
                }
                case MeshRenderer:
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null)
                    {
                        MattyFixes.VerboseMeshLog(LogLevel.Warning, () => $"{renderer.GetType()} in {path} is missing a MeshFilter");
                        continue;
                    }

                    var mesh = filter.sharedMesh;

                    if (mesh == null)
                    {
                        MattyFixes.VerboseMeshLog(LogLevel.Warning, () => $"{renderer.GetType()} in {path} is missing a mesh");
                        continue;
                    }

                    if (!VerticesCache.ContainsKey(mesh))
                    {
                        if (mesh.isReadable)
                            mesh.CacheVertices();
                        else
                            mesh.CacheNonReadableVertices();
                    }

                    break;
                }
                case ParticleSystemRenderer:
                    break;
            }

            
            MattyFixes.VerboseMeshLog(LogLevel.Debug, () => $"Caching {path}/{target.name} renderer {renderer.GetType().Name}");

            foreach (Transform child in target.transform)
                CacheChildVertexes(child, path + "/" + target.name);
        }
    }


    //INTERNAL

    private static void GetNonReadableVertices(this Mesh nonReadableMesh, List<Vector3> vertices)
    {
       
        Mesh meshCopy = new();

        // Handle vertices
        nonReadableMesh.vertexBufferTarget = GraphicsBuffer.Target.Vertex;
        if (nonReadableMesh.vertexBufferCount > 0)
        {
            var verticesBuffer = nonReadableMesh.GetVertexBuffer(0);
            var totalSize = verticesBuffer.stride * verticesBuffer.count;

            var data = ArrayPool<byte>.Shared.Rent(totalSize);
            verticesBuffer.GetData(data, 0, 0, totalSize);

            var vertexAttributeCount = nonReadableMesh.vertexAttributeCount;
            var vertexAttributes = new NativeArray<VertexAttributeDescriptor>(vertexAttributeCount, Allocator.Temp);
            for (var i = 0; i < vertexAttributeCount; i++) vertexAttributes[i] = nonReadableMesh.GetVertexAttribute(i);

            meshCopy.SetVertexBufferParams(nonReadableMesh.vertexCount, vertexAttributes);
            meshCopy.SetVertexBufferData(data, 0, 0, totalSize);

            ArrayPool<byte>.Shared.Return(data);
            vertexAttributes.Dispose();
            verticesBuffer.Dispose();
        }

        meshCopy.GetVertices(vertices);

        Object.Destroy(meshCopy);
    }

    private static void CacheNonReadableVertices(this Mesh nonReadableMesh, Mesh cacheKey = null)
    {
        if (VerticesCache.ContainsKey(nonReadableMesh))
            return;

        // Handle vertices
        nonReadableMesh.vertexBufferTarget = GraphicsBuffer.Target.Vertex;
        if (nonReadableMesh.vertexBufferCount > 0)
        {
            var verticesBuffer = nonReadableMesh.GetVertexBuffer(0);
            var totalSize = verticesBuffer.stride * verticesBuffer.count;
            var attributes = nonReadableMesh.GetVertexAttributes();
            var count = nonReadableMesh.vertexCount;
            
            MattyFixes.VerboseMeshLog(LogLevel.Message, () => $"Requesting vertices for {nonReadableMesh} from GPU");
            AsyncGPUReadback.Request(verticesBuffer, totalSize, 0, request =>
            {
                verticesBuffer.Release();
                if (VerticesCache.ContainsKey(nonReadableMesh))
                    return;

                Mesh meshCopy = new();
                var data = request.GetData<byte>();
                meshCopy.SetVertexBufferParams(count, attributes);
                meshCopy.SetVertexBufferData(data, 0, 0, totalSize);

                using (ListPool<Vector3>.Get(out var tmp))
                {
                    meshCopy.GetVertices(tmp);
                    MattyFixes.VerboseMeshLog(LogLevel.Info, () => $"Cached {tmp.Count} vertices for {nonReadableMesh}");
                    VerticesCache[cacheKey != null ? cacheKey : nonReadableMesh] = tmp.ToArray();
                }

                Object.Destroy(meshCopy);
            });
        }
    }

    private static void CacheVertices(this Mesh readableMesh, Mesh cacheKey = null)
    {
        if (VerticesCache.ContainsKey(readableMesh))
            return;

        using (ListPool<Vector3>.Get(out var tmp))
        {
            readableMesh.GetVertices(tmp);
            MattyFixes.VerboseMeshLog(LogLevel.Info, () => $"Cached {tmp.Count} vertices for {readableMesh}");
            VerticesCache[cacheKey != null ? cacheKey : readableMesh] = tmp.ToArray();
        }
    }

    private static bool TryGetBounds(List<Vector3> vertices, out Bounds bounds)
    {
        bounds = new Bounds();
        if (vertices.Count == 0)
            return false;

        foreach (var v in vertices)
            bounds.Encapsulate(v);

        return true;
    }
    
    private static bool TryGetVerticalOffset(List<Vector3> vertices, out float offset)
    {
        offset = 0;
        if (vertices.Count == 0)
            return false;

        var minOffset = float.MaxValue;

        foreach (var v in vertices)
            if (v.y < minOffset)
                minOffset = v.y;

        offset = -minOffset;
        return true;
    }
    
}