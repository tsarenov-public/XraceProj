using System.Collections.Generic;
using UnityEngine;

// A display car deliberately contains no vehicle components. Shared meshes/materials remain assets;
// all transforms, skin bones and LOD references point to this new visual hierarchy.
public static class GaragePreviewBuilder
{
    public static GameObject Create(GameObject prefab, Transform parent, string name, HideFlags flags = HideFlags.None)
    {
        if (prefab == null || parent == null) return null;
        GameObject preview = new GameObject(name) { hideFlags = flags };
        preview.transform.hideFlags = flags;
        preview.SetActive(false);
        preview.transform.SetParent(parent, false);
        var transforms = new Dictionary<Transform, Transform>();
        var renderers = new Dictionary<Renderer, Renderer>();
        CopyHierarchy(prefab.transform, preview.transform, transforms, flags, true);

        foreach (KeyValuePair<Transform, Transform> pair in transforms)
        {
            MeshFilter filter = pair.Key.GetComponent<MeshFilter>();
            if (filter != null)
            {
                MeshFilter copy = pair.Value.gameObject.AddComponent<MeshFilter>();
                copy.hideFlags = flags;
                copy.sharedMesh = filter.sharedMesh;
            }
            MeshRenderer mesh = pair.Key.GetComponent<MeshRenderer>();
            if (mesh != null)
            {
                MeshRenderer copy = pair.Value.gameObject.AddComponent<MeshRenderer>();
                CopyRenderer(mesh, copy, transforms, flags);
                copy.additionalVertexStreams = mesh.additionalVertexStreams;
                renderers.Add(mesh, copy);
            }
            SkinnedMeshRenderer skin = pair.Key.GetComponent<SkinnedMeshRenderer>();
            if (skin != null)
            {
                SkinnedMeshRenderer copy = pair.Value.gameObject.AddComponent<SkinnedMeshRenderer>();
                CopyRenderer(skin, copy, transforms, flags);
                copy.sharedMesh = skin.sharedMesh;
                copy.localBounds = skin.localBounds;
                copy.quality = skin.quality;
                copy.updateWhenOffscreen = skin.updateWhenOffscreen;
                copy.rootBone = Remap(skin.rootBone, transforms);
                Transform[] bones = skin.bones;
                for (int i = 0; i < bones.Length; i++) bones[i] = Remap(bones[i], transforms);
                copy.bones = bones;
                if (skin.sharedMesh != null)
                    for (int i = 0; i < skin.sharedMesh.blendShapeCount; i++)
                        copy.SetBlendShapeWeight(i, skin.GetBlendShapeWeight(i));
                renderers.Add(skin, copy);
            }
        }

        foreach (KeyValuePair<Transform, Transform> pair in transforms)
        {
            LODGroup source = pair.Key.GetComponent<LODGroup>();
            if (source == null) continue;
            LODGroup copy = pair.Value.gameObject.AddComponent<LODGroup>();
            copy.hideFlags = flags;
            copy.enabled = source.enabled;
            copy.fadeMode = source.fadeMode;
            copy.animateCrossFading = source.animateCrossFading;
            LOD[] lods = source.GetLODs();
            for (int i = 0; i < lods.Length; i++)
            {
                var mapped = new List<Renderer>(lods[i].renderers.Length);
                foreach (Renderer renderer in lods[i].renderers)
                    if (renderer != null && renderers.TryGetValue(renderer, out Renderer target)) mapped.Add(target);
                lods[i].renderers = mapped.ToArray();
            }
            copy.SetLODs(lods);
            copy.localReferencePoint = source.localReferencePoint;
            copy.size = source.size;
        }
        preview.SetActive(true);
        return preview;
    }

    private static void CopyHierarchy(Transform source, Transform parent,
        Dictionary<Transform, Transform> transforms, HideFlags flags, bool isRoot)
    {
        var gameObject = new GameObject(source.name) { layer = source.gameObject.layer, hideFlags = flags };
        Transform copy = gameObject.transform;
        copy.hideFlags = flags;
        copy.SetParent(parent, false);
        copy.localPosition = isRoot ? Vector3.zero : source.localPosition;
        copy.localRotation = isRoot ? Quaternion.identity : source.localRotation;
        copy.localScale = source.localScale;
        gameObject.SetActive(isRoot || source.gameObject.activeSelf);
        transforms.Add(source, copy);
        for (int i = 0; i < source.childCount; i++) CopyHierarchy(source.GetChild(i), copy, transforms, flags, false);
    }

    private static void CopyRenderer(Renderer source, Renderer target,
        Dictionary<Transform, Transform> transforms, HideFlags flags)
    {
        target.hideFlags = flags;
        target.sharedMaterials = source.sharedMaterials;
        target.enabled = source.enabled;
        target.shadowCastingMode = source.shadowCastingMode;
        target.receiveShadows = source.receiveShadows;
        target.lightProbeUsage = source.lightProbeUsage;
        target.reflectionProbeUsage = source.reflectionProbeUsage;
        target.probeAnchor = Remap(source.probeAnchor, transforms);
        target.renderingLayerMask = source.renderingLayerMask;
        target.allowOcclusionWhenDynamic = source.allowOcclusionWhenDynamic;
        target.sortingLayerID = source.sortingLayerID;
        target.sortingOrder = source.sortingOrder;
    }

    private static Transform Remap(Transform source, Dictionary<Transform, Transform> transforms)
        => source != null && transforms.TryGetValue(source, out Transform target) ? target : null;
}
