#define ENABLE_PROFILER
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Pool;

namespace MattyFixes.Utils;

public static class VerticesExtensions
{
    private static readonly ProfilerMarker s_VertexProfiler = new("MattyFixes.VerticesExtensions.GetRecursiveVertex");

    //GrabbableObject EXTENSIONS
    public static bool TryGetVerticalOffset(this GrabbableObject target, out float offset,
        Action<string> logWarningCallback = null,
        Action<string> logDebugCallback = null)
    {
        string Logfunc(List<Vector3> vertices) => TryGetVerticalOffset(vertices, out var min) ? $"min {min}" : "";
        var transform = target.transform;
        var localMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(
            target.itemProperties.restingRotation.x, (150 + target.itemProperties.floorYOffset) + 90f,
            target.itemProperties.restingRotation.z), transform.localScale);
        var vertices = transform.GetChildVertexes(localMatrix,
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
        string Logfunc(List<Vector3> vertices) =>
            TryGetCentroid(vertices, out var centroid) ? $"centroid {centroid}" : "";

        var transform = target.transform;
        var localMatrix = Matrix4x4.TRS(transform.localPosition, transform.localRotation, transform.localScale);
        var vertices = transform.GetChildVertexes(localMatrix, logFunc: Logfunc, logWarningCallback: logWarningCallback,
            logDebugCallback: logDebugCallback);
        var retcode = TryGetCentroid(vertices, out centroid);
        ListPool<Vector3>.Release(vertices);
        return retcode;
    }

    public static bool TryGetWorldCentroid(this GameObject target, out Vector3 centroid,
        Action<string> logWarningCallback = null,
        Action<string> logDebugCallback = null)
    {
        string Logfunc(List<Vector3> vertices) =>
            TryGetCentroid(vertices, out var centroid) ? $"centroid {centroid}" : "";

        var transform = target.transform;
        var localMatrix = Matrix4x4.identity;
        var vertices = transform.GetChildVertexes(localMatrix, logFunc: Logfunc, logWarningCallback: logWarningCallback,
            logDebugCallback: logDebugCallback);

        vertices = vertices.Select(v => transform.TransformPoint(v)).ToList();
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
        var vertices = transform.GetChildVertexes(localMatrix, logFunc: Logfunc, logWarningCallback: logWarningCallback,
            logDebugCallback: logDebugCallback);
        
        var retcode = TryGetRadius(vertices, out minRadius, out maxRadius);
        ListPool<Vector3>.Release(vertices);
        return retcode;
    }

    //Transform Extensions
    public static List<Vector3> GetChildVertexes(this Transform target, Matrix4x4 localMatrix = default,
        string path = "", Func<List<Vector3>, string> logFunc = null, Action<string> logWarningCallback = null,
        Action<string> logDebugCallback = null)
    {
        using (s_VertexProfiler.Auto())
        {
            var outVertices = ListPool<Vector3>.Get();
            
            var renderers = target.GetComponents<Renderer>();

            logDebugCallback?.Invoke($"Processing {path}/{target.name}");

            if (target.TryGetComponent<ScanNodeProperties>(out _))
            {
                logDebugCallback?.Invoke($"Skipping {path}/{target.name}!");
                return outVertices;
            }
            
            using (CollectionPool<List<Vector3>, Vector3>.Get(out List<Vector3> vertices))
            {
                foreach (var renderer in renderers.Where(r => r.enabled))
                {
                    using (CollectionPool<List<Vector3>, Vector3>.Get(out List<Vector3> rVertices))
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

                                var tmpMesh = new Mesh();

                                skinnedMeshRenderer.BakeMesh(tmpMesh, true);

                                if (tmpMesh.isReadable)
                                    tmpMesh.GetVertices(rVertices);
                                else
                                    tmpMesh.GetNonReadableVertices(rVertices);
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
                                    logWarningCallback?.Invoke($"{renderer.GetType()} in {path} is missing a mesh");
                                    continue;
                                }

                                if (mesh.isReadable)
                                    mesh.GetVertices(rVertices);
                                else
                                    mesh.GetNonReadableVertices(rVertices);
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
                }

                foreach (Transform child in target.transform)
                {
                    if (!child.gameObject.activeSelf)
                        continue;
                    var childMatrix = Matrix4x4.TRS(child.localPosition, child.localRotation, child.localScale);
                    var childVertices = GetChildVertexes(child, childMatrix, path + "/" + target.name, logFunc,
                        logWarningCallback, logDebugCallback);
                    vertices.AddRange(childVertices);
                    ListPool<Vector3>.Release(childVertices);
                }

                foreach (var vertex in vertices)
                {
                    outVertices.Add(localMatrix.MultiplyPoint3x4(vertex));
                }
            }

            logDebugCallback?.Invoke($"Found {path}/{target.name} {logFunc?.Invoke(outVertices)}");
            s_VertexProfiler.End();
            return outVertices;
        }
    }


    //INTERNAL

    private static void GetNonReadableVertices(this Mesh nonReadableMesh, List<Vector3> vertices)
    {
        Mesh meshCopy = new()
        {
            indexFormat = nonReadableMesh.indexFormat
        };

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

        meshCopy.GetVertices(vertices);
    }

    private static bool TryGetVerticalOffset(List<Vector3> vertices, out float offset)
    {
        offset = 0;
        if (vertices.Count == 0)
            return false;

        offset = -vertices.Min(v => v.y);
        return true;
    }

    private static bool TryGetCentroid(List<Vector3> vertices, out Vector3 centroid)
    {
        centroid = Vector3.zero;
        if (vertices.Count == 0)
            return false;

        centroid = vertices.Aggregate(Vector3.zero, (agg, v) => agg + v, agg => agg / vertices.Count);
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
            var magnitude = tVertex.magnitude;
            if (magnitude < minRadius)
                minRadius = magnitude;
            if (magnitude > maxRadius)
                maxRadius = magnitude;
        }
        return true;
    }
}