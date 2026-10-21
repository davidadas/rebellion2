using System;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_6000_4_OR_NEWER
using ResourceId = System.UInt64;
#else
using ResourceId = System.Int32;
#endif

namespace TND.Common.URP
{
    // A customized version of RenderGraphResourcePool from SRP core
    public class RTHandlePool
    {
        private static int s_currentStaleResourceCount = 0;
        // Keep stale resources alive for 3 frames
        private static int s_staleResourceLifetime = 3;
        // Store max 32 rtHandles
        private static int s_staleResourceMaxCapacity = 32;

        // Dictionary tracks resources by hash and stores resources with same hash in a List (list instead of a stack because we need to be able to remove stale allocations, potentially in the middle of the stack).
        // The list needs to be sorted otherwise you could get inconsistent resource usage from one frame to another.
        private readonly Dictionary<int, SortedList<ResourceId, (RTHandle resource, int frameIndex)>> _resourcePool = new();
        private readonly List<ResourceId> _removeList = new(32); // Used to remove stale resources as there is no RemoveAll on SortedLists

        /// <summary>
        /// Controls the resource pool's max stale resource capacity.
        /// Increasing the capacity may have a negative impact on the memory usage.
        /// Increasing the capacity may reduce the runtime RTHandle realloc cost in multi view/multi camera setup.
        /// Setting capacity will purge the current pool. It is recommended to setup the capacity upfront and not changing it during the runtime.
        /// Setting capacity won't do anything if new capacity is the same to the current capacity.
        /// </summary>
        public int StaleResourceCapacity
        {
            get => s_staleResourceMaxCapacity;
            set
            {
                if (s_staleResourceMaxCapacity != value)
                {
                    s_staleResourceMaxCapacity = value;
                    Cleanup();
                }
            }
        }

        // Add no longer used resouce to pool
        // Return true if resource is added to pool successfully, return false otherwise.
        public bool AddResourceToPool(RTHandle resource, int currentFrameIndex)
        {
            return AddResourceToPool(CreateTextureDescriptor(resource), resource, currentFrameIndex);
        }

        public bool AddResourceToPool(in RenderTextureDescriptor texDesc, RTHandle resource, int currentFrameIndex)
        {
            if (s_currentStaleResourceCount >= s_staleResourceMaxCapacity)
                return false;

            int hashCode = GetHashCodeWithNameHash(texDesc, resource.name);

            if (!_resourcePool.TryGetValue(hashCode, out var list))
            {
                // Init list with max capacity to avoid runtime GC.Alloc when calling list.Add(resize list)
                list = new SortedList<ResourceId, (RTHandle resource, int frameIndex)>(s_staleResourceMaxCapacity);
                _resourcePool.Add(hashCode, list);
            }

#if UNITY_6000_4_OR_NEWER
            ResourceId resourceId = resource.GetUniqueID();
#else
            ResourceId resourceId = resource.GetInstanceID();
#endif

            list.Add(resourceId, (resource, currentFrameIndex));
            s_currentStaleResourceCount++;

            return true;
        }

        // Get resource from the pool using TextureDesc as key
        // Return true if resource successfully retried resource from the pool, return false otherwise.
        public bool TryGetResource(in RenderTextureDescriptor texDesc, string name, out RTHandle resource, bool usepool = true)
        {
            int hashCode = GetHashCodeWithNameHash(texDesc, name);
            if (usepool && _resourcePool.TryGetValue(hashCode, out SortedList<ResourceId, (RTHandle resource, int frameIndex)> list) && list.Count > 0)
            {
                resource = list.Values[list.Count - 1].resource;
                list.RemoveAt(list.Count - 1); // O(1) since it's the last element.
                s_currentStaleResourceCount--;
                return true;
            }

            resource = null;
            return false;
        }

        // Release all resources in pool.
        public void Cleanup()
        {
            foreach (var kvp in _resourcePool)
            {
                foreach (var res in kvp.Value)
                {
                    res.Value.resource.Release();
                }
            }
            _resourcePool.Clear();

            s_currentStaleResourceCount = 0;
        }

        private static bool ShouldReleaseResource(int lastUsedFrameIndex, int currentFrameIndex)
        {
            // We need to have a delay of a few frames before releasing resources for good.
            // Indeed, when having multiple off-screen cameras, they are rendered in a separate SRP render call and thus with a different frame index than main camera
            // This causes texture to be deallocated/reallocated every frame if the two cameras don't need the same buffers.
            return (lastUsedFrameIndex + s_staleResourceLifetime) < currentFrameIndex;
        }

        // Release resources that are not used in last couple frames.
        public void PurgeUnusedResources(int currentFrameIndex)
        {
            // Update the frame index for the lambda. Static because we don't want to capture.
            _removeList.Clear();

            foreach (var kvp in _resourcePool)
            {
                // WARNING: No foreach here. Sorted list GetEnumerator generates garbage...
                var list = kvp.Value;
                var keys = list.Keys;
                var values = list.Values;
                for (int i = 0; i < list.Count; ++i)
                {
                    var value = values[i];
                    if (ShouldReleaseResource(value.frameIndex, currentFrameIndex))
                    {
                        value.resource.Release();
                        _removeList.Add(keys[i]);
                        s_currentStaleResourceCount--;
                    }
                }

                foreach (var key in _removeList)
                    list.Remove(key);
            }
        }

        public void LogDebugInfo()
        {
            var sb = new StringBuilder();
            sb.AppendFormat("RTHandlePool for frame {0}, Total stale resources {1}", Time.frameCount, s_currentStaleResourceCount);
            sb.AppendLine();

            foreach (var kvp in _resourcePool)
            {
                var list = kvp.Value;
                var keys = list.Keys;
                var values = list.Values;
                for (int i = 0; i < list.Count; ++i)
                {
                    var value = values[i];
                    sb.AppendFormat("Resource in pool: Name {0} Last active frame index {1} Size {2} x {3} x {4}",
                        value.resource.name,
                        value.frameIndex,
                        value.resource.rt.descriptor.width,
                        value.resource.rt.descriptor.height,
                        value.resource.rt.descriptor.volumeDepth
                        );
                    sb.AppendLine();
                }
            }

            Debug.Log(sb);
        }

        // NOTE: Only allow reusing resource with the same name.
        // This is because some URP code uses texture name as key to bind input texture (GBUFFER_2). Different name will result in URP bind texture to different shader input slot.
        // Ideally if URP code uses shaderPropertyID(instead of name string), we can relax the restriction here.
        private static int GetHashCodeWithNameHash(in RenderTextureDescriptor desc, string name)
        {
            const uint prime = 16777619;
            const uint offsetBasis = 2166136261;

            // HashFNV1A32 without relying on the Core RP internal struct
            uint hash = offsetBasis;
            unchecked
            {
                hash = (hash ^ (uint)desc.width) * prime;
                hash = (hash ^ (uint)desc.height) * prime;
                hash = (hash ^ (uint)desc.volumeDepth) * prime;
                hash = (hash ^ (uint)desc.graphicsFormat) * prime;
                hash = (hash ^ (uint)desc.depthStencilFormat) * prime;
                hash = (hash ^ (uint)desc.dimension) * prime;
                hash = (hash ^ (uint)desc.memoryless) * prime;
                hash = (hash ^ (uint)desc.vrUsage) * prime;
                hash = (hash ^ (desc.enableRandomWrite ? 1u : 0u)) * prime;
                hash = (hash ^ (desc.useMipMap ? 1u : 0u)) * prime;
                hash = (hash ^ (desc.autoGenerateMips ? 1u : 0u)) * prime;
                hash = (hash ^ (desc.bindMS ? 1u : 0u)) * prime;
                hash = (hash ^ (desc.useDynamicScale ? 1u : 0u)) * prime;
            }

            return (int)hash * 23 + name.GetHashCode();
        }

        public static RenderTextureDescriptor CreateTextureDescriptor(RTHandle rtHandle)
        {
            return new RenderTextureDescriptor
            {
                width = rtHandle.rt.width,
                height = rtHandle.rt.height,
                volumeDepth = rtHandle.rt.volumeDepth,
                graphicsFormat = rtHandle.rt.graphicsFormat,
                depthStencilFormat = rtHandle.rt.depthStencilFormat,
                dimension = rtHandle.rt.dimension,
                memoryless = rtHandle.rt.memorylessMode,
                vrUsage = rtHandle.rt.vrUsage,
                enableRandomWrite = rtHandle.rt.enableRandomWrite,
                useMipMap = rtHandle.rt.useMipMap,
                autoGenerateMips = rtHandle.rt.autoGenerateMips,
                bindMS = rtHandle.rt.bindTextureMS,
                useDynamicScale = rtHandle.rt.useDynamicScale,
            };
        }
    }
}
