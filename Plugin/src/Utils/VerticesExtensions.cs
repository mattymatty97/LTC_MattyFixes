using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using MattyFixes.Patches;
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
    public static bool TryGetVerticalOffset(this GrabbableObject target, out float offset,
        Action<string> logWarningCallback = null,
        Action<string> logDebugCallback = null)
    {
        string Logfunc(List<Vector3> vertices)
        {
            return TryGetVerticalOffset(vertices, out var min) ? $"min {min}" : "";
        }

        var transform = target.transform;
        var localMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(
            target.itemProperties.restingRotation.x, target.itemProperties.floorYOffset + 90f,
            target.itemProperties.restingRotation.z), transform.localScale);
        var vertices = ListPool<Vector3>.Get();

        transform.GetChildVertexes(vertices, localMatrix,
            logFunc: Logfunc, logWarningCallback: logWarningCallback,
            logDebugCallback: logDebugCallback);
        var retcode = TryGetVerticalOffset(vertices, out offset);
        ListPool<Vector3>.Release(vertices);
        return retcode;
    }


    //GameObject EXTENSIONS
    public static bool TryGetLocalCentroid(this GameObject target, out Vector3 centroid,
        Action<string> logWarningCallback = null,
        Action<string> logDebugCallback = null)
    {
        string Logfunc(List<Vector3> vertices)
        {
            return TryGetCentroid(vertices, out var centroid) ? $"centroid {centroid}" : "";
        }

        var transform = target.transform;
        var localMatrix = Matrix4x4.TRS(transform.localPosition, transform.localRotation, transform.localScale);
        var vertices = ListPool<Vector3>.Get();

        transform.GetChildVertexes(vertices, localMatrix, logFunc: Logfunc, logWarningCallback: logWarningCallback,
            logDebugCallback: logDebugCallback);
        var retcode = TryGetCentroid(vertices, out centroid);
        ListPool<Vector3>.Release(vertices);
        return retcode;
    }

    public static bool TryGetWorldCentroid(this GameObject target, out Vector3 centroid,
        Action<string> logWarningCallback = null,
        Action<string> logDebugCallback = null)
    {
        string Logfunc(List<Vector3> vertices)
        {
            return TryGetCentroid(vertices, out var centroid) ? $"centroid {centroid}" : "";
        }

        var transform = target.transform;
        var vertices = ListPool<Vector3>.Get();

        transform.GetChildVertexes(vertices, Matrix4x4.identity, logFunc: Logfunc,
            logWarningCallback: logWarningCallback,
            logDebugCallback: logDebugCallback);

        using var nativeVertices = vertices.ToNativeArray(AllocatorManager.Temp);
        transform.TransformPoints(nativeVertices);

        vertices.Clear();
        vertices.AddRange(nativeVertices);

        var retcode = TryGetCentroid(vertices, out centroid);
        ListPool<Vector3>.Release(vertices);
        return retcode;
    }

    public static bool TryGetRadius(this GameObject target, out float minRadius, out float maxRadius,
        Action<string> logWarningCallback = null,
        Action<string> logDebugCallback = null)
    {
        string Logfunc(List<Vector3> vertices)
        {
            if (vertices.Count == 0)
                return "";
            TryGetCentroid(vertices, out var centroid);
            TryGetRadius(vertices, out var minRadius, out var maxRadius);
            return $"centroid {centroid} minR {minRadius} maxR {maxRadius}";
        }

        var transform = target.transform;
        var localMatrix = Matrix4x4.TRS(transform.localPosition, transform.localRotation, transform.localScale);
        var vertices = ListPool<Vector3>.Get();

        transform.GetChildVertexes(vertices, localMatrix, logFunc: Logfunc, logWarningCallback: logWarningCallback,
            logDebugCallback: logDebugCallback);

        var retcode = TryGetRadius(vertices, out minRadius, out maxRadius);
        ListPool<Vector3>.Release(vertices);
        return retcode;
    }

    //Transform Extensions
    // ReSharper disable once MemberCanBePrivate.Global
    public static void GetChildVertexes(this Transform target, List<Vector3> outVertices,
        Matrix4x4 localMatrix = default,
        string path = "", Func<List<Vector3>, string> logFunc = null, Action<string> logWarningCallback = null,
        Action<string> logDebugCallback = null)
    {
        logDebugCallback?.Invoke($"Processing {path}/{target.name}");

        if (target.TryGetComponent<ScanNodeProperties>(out _))
        {
            logDebugCallback?.Invoke($"Skipping {path}/{target.name}!");
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
                                logWarningCallback?.Invoke($"{renderer.GetType()} in {path} is missing a mesh");
                                continue;
                            }

                            if (VerticesCache.TryGetValue(mesh, out var cached))
                            {
                                rVertices.AddRange(cached);
                            }
                            else
                            {
                                var tmpMesh = new Mesh();

                                skinnedMeshRenderer.BakeMesh(tmpMesh, true);

                                if (tmpMesh.isReadable)
                                    tmpMesh.GetVertices(rVertices);
                                else
                                    tmpMesh.GetNonReadableVertices(rVertices);

                                VerticesCache[mesh] = rVertices.ToArray();

                                Object.Destroy(tmpMesh);
                            }

                            break;
                        }
                        case MeshRenderer:
                        {
                            var filter = renderer.GetComponent<MeshFilter>();
                            if (filter == null)
                            {
                                logWarningCallback?.Invoke(
                                    $"{renderer.GetType()} in {path} is missing a MeshFilter");
                                continue;
                            }

                            var mesh = filter.sharedMesh;

                            if (mesh == null)
                            {
                                logWarningCallback?.Invoke($"{filter.GetType()} in {path} is missing a mesh");
                                continue;
                            }

                            if (VerticesCache.TryGetValue(mesh, out var cached))
                            {
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

                    logDebugCallback?.Invoke(
                        $"Processing {path}/{target.name} renderer {renderer.GetType().Name} {logFunc?.Invoke(rVertices)}");

                    vertices.AddRange(rVertices);
                }

            foreach (Transform child in target.transform)
            {
                if (!child.gameObject.activeSelf)
                    continue;

                var childMatrix = Matrix4x4.TRS(child.localPosition, child.localRotation, child.localScale);

                var childVertices = ListPool<Vector3>.Get();
                GetChildVertexes(child, childVertices, childMatrix, path + "/" + target.name, logFunc,
                    logWarningCallback, logDebugCallback);

                vertices.AddRange(childVertices);
                ListPool<Vector3>.Release(childVertices);
            }

            outVertices.AddRange(vertices.Select(localMatrix.MultiplyPoint3x4));
        }

        logDebugCallback?.Invoke($"Found {path}/{target.name} {logFunc?.Invoke(outVertices)}");
    }

    public static void CacheChildVertexes(this Transform target,
        string path = "", Action<string> logWarningCallback = null,
        Action<string> logDebugCallback = null)
    {
        logDebugCallback?.Invoke($"Caching {path}/{target.name}");

        if (target.TryGetComponent<ScanNodeProperties>(out _))
        {
            logDebugCallback?.Invoke($"Skipping {path}/{target.name}!");
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
                        logWarningCallback?.Invoke($"{renderer.GetType()} in {path} is missing a mesh");
                        continue;
                    }

                    if (!VerticesCache.ContainsKey(mesh))
                    {
                        var tmpMesh = new Mesh();

                        skinnedMeshRenderer.BakeMesh(tmpMesh, true);

                        if (tmpMesh.isReadable)
                            tmpMesh.CacheVertices(mesh, logWarningCallback, logDebugCallback);
                        else
                            tmpMesh.CacheNonReadableVertices(mesh, logWarningCallback, logDebugCallback);

                        Object.Destroy(tmpMesh);
                    }

                    break;
                }
                case MeshRenderer:
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null)
                    {
                        logWarningCallback?.Invoke(
                            $"{renderer.GetType()} in {path} is missing a MeshFilter");
                        continue;
                    }

                    var mesh = filter.sharedMesh;

                    if (mesh == null)
                    {
                        logWarningCallback?.Invoke($"{filter.GetType()} in {path} is missing a mesh");
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

            logDebugCallback?.Invoke(
                $"Caching {path}/{target.name} renderer {renderer.GetType().Name}");

            foreach (Transform child in target.transform)
                CacheChildVertexes(child, path + "/" + target.name,
                    logWarningCallback, logDebugCallback);
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

    private static void CacheNonReadableVertices(this Mesh nonReadableMesh, Mesh cacheKey = null, Action<string> logWarningCallback = null,
    Action<string> logDebugCallback = null)
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
            logDebugCallback?.Invoke($"Requesting vertices for {nonReadableMesh} from GPU");
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
                    logDebugCallback?.Invoke($"Cached {tmp.Count} vertices for {nonReadableMesh}");
                    VerticesCache[cacheKey != null ? cacheKey : nonReadableMesh] = tmp.ToArray();
                }

                Object.Destroy(meshCopy);
            });
        }
    }

    private static void CacheVertices(this Mesh readableMesh, Mesh cacheKey = null, Action<string> logWarningCallback = null,
        Action<string> logDebugCallback = null)
    {
        if (VerticesCache.ContainsKey(readableMesh))
            return;

        using (ListPool<Vector3>.Get(out var tmp))
        {
            readableMesh.GetVertices(tmp);
            logDebugCallback?.Invoke($"Cached {tmp.Count} vertices for {readableMesh}");
            VerticesCache[cacheKey != null ? cacheKey : readableMesh] = tmp.ToArray();
        }
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

    private static bool TryGetCentroid(List<Vector3> vertices, out Vector3 centroid)
    {
        centroid = Vector3.zero;
        if (vertices.Count == 0)
            return false;

        var sum = Vector3.zero;

        foreach (var v in vertices) sum += v;

        centroid = sum / vertices.Count;
        return true;
    }

    private static bool TryGetRadius(List<Vector3> vertices, out float minRadius, out float maxRadius)
    {
        minRadius = 0;
        maxRadius = 0;
        if (vertices.Count == 0)
            return false;

        TryGetCentroid(vertices, out var centroid);

        minRadius = float.MaxValue;
        maxRadius = float.MinValue;
        foreach (var vertex in vertices)
        {
            var tVertex = vertex - centroid;
            var magnitude = tVertex.sqrMagnitude;

            if (magnitude < minRadius)
                minRadius = magnitude;
            if (magnitude > maxRadius)
                maxRadius = magnitude;
        }

        minRadius = Mathf.Sqrt(minRadius);
        maxRadius = Mathf.Sqrt(maxRadius);

        return true;
    }
}