using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using UABEANext4.AssetWorkspace;
using UABEANext4.Logic.Mesh;

namespace UABEANext4.Logic.Scene;

public class SceneData
{
    public List<SceneObject> RootObjects { get; } = new();
    public List<SceneObject> AllObjects { get; } = new();

    private readonly Workspace _workspace;
    private readonly Dictionary<long, SceneObject> _pathIdToObject = new();
    private readonly Action<string>? _logAction;

    public SceneData(Workspace workspace, Action<string>? logAction = null)
    {
        _workspace = workspace;
        _logAction = logAction;
    }

    private void Log(string message)
    {
        _logAction?.Invoke(message);
    }

    public void LoadFromFile(AssetsFileInstance fileInst)
    {
        if (fileInst == null)
        {
            Log("LoadFromFile: fileInst is null");
            throw new ArgumentNullException(nameof(fileInst), "File instance cannot be null");
        }

        if (fileInst.file == null)
        {
            Log("LoadFromFile: fileInst.file is null");
            throw new ArgumentException("File instance has null file", nameof(fileInst));
        }

        RootObjects.Clear();
        AllObjects.Clear();
        _pathIdToObject.Clear();

        Log($"Starting LoadFromFile for: {fileInst.name}");

        var transformInfos = fileInst.file.GetAssetsOfType(AssetClassID.Transform)
            .Concat(fileInst.file.GetAssetsOfType(AssetClassID.RectTransform))
            .ToList();

        Log($"Found {transformInfos.Count} transforms");

        var goInfos = fileInst.file.GetAssetsOfType(AssetClassID.GameObject).ToList();
        Log($"Found {goInfos.Count} GameObjects");

        var goPathIdToInfo = goInfos.ToDictionary(g => g.PathId);

        var tfmParentMap = new Dictionary<long, long>();
        var tfmToGoMap = new Dictionary<long, long>();
        var tfmChildrenMap = new Dictionary<long, List<long>>();

        foreach (var tfmInfo in transformInfos)
        {
            var tfmBf = _workspace.GetBaseField(fileInst, tfmInfo.PathId);
            if (tfmBf == null) continue;

            var parentPathId = tfmBf["m_Father"]["m_PathID"].AsLong;
            tfmParentMap[tfmInfo.PathId] = parentPathId;

            var goPtr = tfmBf["m_GameObject"];
            var goPathId = goPtr["m_PathID"].AsLong;
            tfmToGoMap[tfmInfo.PathId] = goPathId;

            var localPos = ReadVector3(tfmBf["m_LocalPosition"]);
            var localRot = ReadQuaternion(tfmBf["m_LocalRotation"]);
            var localScale = ReadVector3(tfmBf["m_LocalScale"]);

            string goName = "[Unknown]";
            AssetInst? goAsset = null;
            if (goPathIdToInfo.TryGetValue(goPathId, out var goInfo))
            {
                goAsset = _workspace.GetAssetInst(fileInst, 0, goInfo.PathId);
                var goBf = _workspace.GetBaseField(fileInst, goInfo.PathId);
                if (goBf != null)
                {
                    goName = goBf["m_Name"].AsString;
                }
            }

            var sceneObj = new SceneObject
            {
                Name = goName,
                PathId = tfmInfo.PathId,
                GameObjectAsset = goAsset,
                LocalPosition = localPos,
                LocalRotation = localRot,
                LocalScale = localScale
            };

            _pathIdToObject[tfmInfo.PathId] = sceneObj;
            AllObjects.Add(sceneObj);

            var childrenArr = tfmBf["m_Children.Array"];
            var childIds = new List<long>();
            foreach (var child in childrenArr)
            {
                childIds.Add(child["m_PathID"].AsLong);
            }
            tfmChildrenMap[tfmInfo.PathId] = childIds;
        }

        Log("Building hierarchy...");
        foreach (var kvp in _pathIdToObject)
        {
            var tfmPathId = kvp.Key;
            var sceneObj = kvp.Value;

            if (tfmParentMap.TryGetValue(tfmPathId, out var parentPathId) && parentPathId != 0)
            {
                if (_pathIdToObject.TryGetValue(parentPathId, out var parentObj))
                {
                    sceneObj.Parent = parentObj;
                    parentObj.Children.Add(sceneObj);
                }
            }
            else
            {
                RootObjects.Add(sceneObj);
            }
        }

        Log($"Built hierarchy: {AllObjects.Count} objects, {RootObjects.Count} root objects");

        Log("Loading meshes and materials...");
        var meshFilterInfos = fileInst.file.GetAssetsOfType(AssetClassID.MeshFilter).ToList();
        var meshRendererInfos = fileInst.file.GetAssetsOfType(AssetClassID.MeshRenderer).ToList();
        var meshColliderInfos = fileInst.file.GetAssetsOfType(AssetClassID.MeshCollider).ToList();
        var skinnedMeshRendererInfos = fileInst.file.GetAssetsOfType(AssetClassID.SkinnedMeshRenderer).ToList();

        Log($"Found {meshFilterInfos.Count} MeshFilters, {meshRendererInfos.Count} MeshRenderers, {meshColliderInfos.Count} MeshColliders, {skinnedMeshRendererInfos.Count} SkinnedMeshRenderers");

        var goToMeshFilter = new Dictionary<long, AssetFileInfo>();
        var goToMeshRenderer = new Dictionary<long, AssetFileInfo>();
        var goToMeshCollider = new Dictionary<long, AssetFileInfo>();
        var goToSkinnedMeshRenderer = new Dictionary<long, AssetFileInfo>();

        var batchedGameObjects = new HashSet<long>();

        foreach (var mfInfo in meshFilterInfos)
        {
            var mfBf = _workspace.GetBaseField(fileInst, mfInfo.PathId);
            if (mfBf == null) continue;

            var goPathId = mfBf["m_GameObject"]["m_PathID"].AsLong;
            goToMeshFilter[goPathId] = mfInfo;
        }

        foreach (var mrInfo in meshRendererInfos)
        {
            var mrBf = _workspace.GetBaseField(fileInst, mrInfo.PathId);
            if (mrBf == null) continue;

            var goPathId = mrBf["m_GameObject"]["m_PathID"].AsLong;
            goToMeshRenderer[goPathId] = mrInfo;

            var staticBatchInfo = mrBf["m_StaticBatchInfo"];
            if (staticBatchInfo != null && !staticBatchInfo.IsDummy)
            {
                var subMeshCount = staticBatchInfo["subMeshCount"].AsInt;
                if (subMeshCount > 0)
                {
                    batchedGameObjects.Add(goPathId);
                }
            }
        }

        foreach (var mcInfo in meshColliderInfos)
        {
            var mcBf = _workspace.GetBaseField(fileInst, mcInfo.PathId);
            if (mcBf == null) continue;

            var goPathId = mcBf["m_GameObject"]["m_PathID"].AsLong;
            goToMeshCollider[goPathId] = mcInfo;
        }

        foreach (var smrInfo in skinnedMeshRendererInfos)
        {
            var smrBf = _workspace.GetBaseField(fileInst, smrInfo.PathId);
            if (smrBf == null) continue;

            var goPathId = smrBf["m_GameObject"]["m_PathID"].AsLong;
            goToSkinnedMeshRenderer[goPathId] = smrInfo;
        }

        int meshesLoaded = 0;
        int meshLoadErrors = 0;
        int colliderMeshesLoaded = 0;
        int skinnedMeshesLoaded = 0;
        foreach (var sceneObj in AllObjects)
        {
            var goPathId = tfmToGoMap.GetValueOrDefault(sceneObj.PathId, 0);
            if (goPathId == 0) continue;

            bool meshLoaded = false;

            if (goToSkinnedMeshRenderer.TryGetValue(goPathId, out var smrInfo))
            {
                var smrBf = _workspace.GetBaseField(fileInst, smrInfo.PathId);
                if (smrBf != null)
                {
                    var meshPtr = smrBf["m_Mesh"];
                    if (meshPtr != null && !meshPtr.IsDummy)
                    {
                        var meshPathId = meshPtr["m_PathID"].AsLong;
                        var meshFileId = meshPtr["m_FileID"].AsInt;

                        if (meshPathId != 0)
                        {
                            try
                            {
                                var meshBf = _workspace.GetBaseField(fileInst, meshFileId, meshPathId);
                                if (meshBf != null)
                                {
                                    var version = fileInst.file.Metadata.UnityVersion;
                                    sceneObj.Mesh = new MeshObj(fileInst, meshBf, new UnityVersion(version));

                                    if (sceneObj.Mesh?.UVs != null && sceneObj.Mesh.UVs.Length > 0 && sceneObj.Mesh.UVs[0] != null)
                                    {
                                        sceneObj.UVs = sceneObj.Mesh.UVs[0];
                                    }
                                    if (sceneObj.Mesh?.UVs != null && sceneObj.Mesh.UVs.Length > 1 && sceneObj.Mesh.UVs[1] != null)
                                    {
                                        sceneObj.LightmapUVs = sceneObj.Mesh.UVs[1];
                                    }

                                    sceneObj.IsSkinnedMesh = true;

                                    var rootBonePtr = smrBf["m_RootBone"];
                                    if (rootBonePtr != null && !rootBonePtr.IsDummy)
                                    {
                                        var rootBonePathId = rootBonePtr["m_PathID"].AsLong;
                                        if (rootBonePathId != 0 && _pathIdToObject.TryGetValue(rootBonePathId, out var rootBoneObj))
                                        {
                                            sceneObj.RootBone = rootBoneObj;
                                            Log($"SkinnedMesh '{sceneObj.Name}' uses root bone '{rootBoneObj.Name}'");
                                        }
                                    }

                                    meshLoaded = true;
                                    skinnedMeshesLoaded++;
                                    meshesLoaded++;
                                }
                            }
                            catch (Exception ex)
                            {
                                Log($"SkinnedMeshRenderer mesh load failed for '{sceneObj.Name}': {ex.Message}");
                                meshLoadErrors++;
                            }
                        }
                    }

                    if (meshLoaded)
                    {
                        var materialsArr = smrBf["m_Materials.Array"];
                        if (materialsArr != null && !materialsArr.IsDummy && materialsArr.Children.Count > 0)
                        {
                            var matPtr = materialsArr[0];
                            var matPathId = matPtr["m_PathID"].AsLong;
                            var matFileId = matPtr["m_FileID"].AsInt;

                            if (matPathId != 0)
                            {
                                try
                                {
                                    LoadTextureFromMaterial(fileInst, matFileId, matPathId, sceneObj);
                                }
                                catch (Exception ex)
                                {
                                    Log($"Texture load failed for skinned mesh '{sceneObj.Name}': {ex.Message}");
                                }
                            }
                        }
                    }
                }
            }

            if (!meshLoaded && goToMeshCollider.TryGetValue(goPathId, out var mcInfo))
            {
                var mcBf = _workspace.GetBaseField(fileInst, mcInfo.PathId);
                if (mcBf != null)
                {
                    var meshPtr = mcBf["m_Mesh"];
                    if (meshPtr != null && !meshPtr.IsDummy)
                    {
                        var meshPathId = meshPtr["m_PathID"].AsLong;
                        var meshFileId = meshPtr["m_FileID"].AsInt;

                        if (meshPathId != 0)
                        {
                            try
                            {
                                var meshBf = _workspace.GetBaseField(fileInst, meshFileId, meshPathId);
                                if (meshBf != null)
                                {
                                    var version = fileInst.file.Metadata.UnityVersion;
                                    sceneObj.Mesh = new MeshObj(fileInst, meshBf, new UnityVersion(version));

                                    if (sceneObj.Mesh?.UVs != null && sceneObj.Mesh.UVs.Length > 0 && sceneObj.Mesh.UVs[0] != null)
                                    {
                                        sceneObj.UVs = sceneObj.Mesh.UVs[0];
                                    }
                                    if (sceneObj.Mesh?.UVs != null && sceneObj.Mesh.UVs.Length > 1 && sceneObj.Mesh.UVs[1] != null)
                                    {
                                        sceneObj.LightmapUVs = sceneObj.Mesh.UVs[1];
                                    }
                                    meshLoaded = true;
                                    colliderMeshesLoaded++;
                                    meshesLoaded++;
                                }
                            }
                            catch (Exception ex)
                            {
                                Log($"MeshCollider mesh load failed for '{sceneObj.Name}': {ex.Message}");
                                meshLoadErrors++;
                            }
                        }
                    }
                }
            }

            if (!meshLoaded && goToMeshFilter.TryGetValue(goPathId, out var mfInfo))
            {
                var isBatched = batchedGameObjects.Contains(goPathId);

                if (!isBatched)
                {
                    var mfBf = _workspace.GetBaseField(fileInst, mfInfo.PathId);
                    if (mfBf != null)
                    {
                        var meshPtr = mfBf["m_Mesh"];
                        if (meshPtr != null && !meshPtr.IsDummy)
                        {
                            var meshPathId = meshPtr["m_PathID"].AsLong;
                            var meshFileId = meshPtr["m_FileID"].AsInt;

                            if (meshPathId != 0)
                            {
                                try
                                {
                                    var meshBf = _workspace.GetBaseField(fileInst, meshFileId, meshPathId);
                                    if (meshBf != null)
                                    {
                                        var version = fileInst.file.Metadata.UnityVersion;
                                        sceneObj.Mesh = new MeshObj(fileInst, meshBf, new UnityVersion(version));

                                        if (sceneObj.Mesh?.UVs != null && sceneObj.Mesh.UVs.Length > 0 && sceneObj.Mesh.UVs[0] != null)
                                        {
                                            sceneObj.UVs = sceneObj.Mesh.UVs[0];
                                        }
                                        if (sceneObj.Mesh?.UVs != null && sceneObj.Mesh.UVs.Length > 1 && sceneObj.Mesh.UVs[1] != null)
                                        {
                                            sceneObj.LightmapUVs = sceneObj.Mesh.UVs[1];
                                        }
                                        meshLoaded = true;
                                        meshesLoaded++;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log($"MeshFilter mesh load failed for '{sceneObj.Name}': {ex.Message}");
                                    meshLoadErrors++;
                                }
                            }
                        }
                    }
                }
            }

            if (goToMeshRenderer.TryGetValue(goPathId, out var mrInfo))
            {
                var mrBf = _workspace.GetBaseField(fileInst, mrInfo.PathId);
                if (mrBf == null) continue;

                var materialsArr = mrBf["m_Materials.Array"];
                if (materialsArr != null && !materialsArr.IsDummy && materialsArr.Children.Count > 0)
                {
                    var matPtr = materialsArr[0];
                    var matPathId = matPtr["m_PathID"].AsLong;
                    var matFileId = matPtr["m_FileID"].AsInt;

                    if (matPathId != 0)
                    {
                        try
                        {
                            LoadTextureFromMaterial(fileInst, matFileId, matPathId, sceneObj);
                        }
                        catch (Exception ex)
                        {
                            Log($"Texture load failed for '{sceneObj.Name}': {ex.Message}");
                        }
                    }
                }
            }
        }

        Log($"Loaded {meshesLoaded} meshes ({skinnedMeshesLoaded} skinned, {colliderMeshesLoaded} from MeshColliders, {meshLoadErrors} errors)");

        Log("Computing world matrices and bounds...");
        foreach (var root in RootObjects)
        {
            root.ComputeWorldMatrix();
        }

        foreach (var obj in AllObjects)
        {
            if (obj.IsSkinnedMesh && obj.RootBone != null)
            {
                obj.WorldMatrix = obj.RootBone.WorldMatrix;
            }
        }

        foreach (var obj in AllObjects)
        {
            obj.ComputeBounds();
        }

        Log($"LoadFromFile completed: {AllObjects.Count} objects total");
    }

    private void LoadTextureFromMaterial(AssetsFileInstance fileInst, int matFileId, long matPathId, SceneObject sceneObj)
    {
        var matBf = _workspace.GetBaseField(fileInst, matFileId, matPathId);
        if (matBf == null) return;

        var savedProps = matBf["m_SavedProperties"];
        if (savedProps == null || savedProps.IsDummy) return;

        var texEnvs = savedProps["m_TexEnvs.Array"];
        if (texEnvs == null || texEnvs.IsDummy) return;

        foreach (var texEnv in texEnvs)
        {
            if (texEnv == null) continue;

            var firstField = texEnv["first"];
            if (firstField == null || firstField.IsDummy) continue;

            var texName = firstField.AsString;
            if (texName == "_MainTex" || texName == "_BaseMap" || texName == "_Albedo" ||
                texName == "_BaseColorMap" || texName == "_Diffuse" || texName == "_DiffuseMap" ||
                texName == "_BaseColor" || texName == "_Color" || texName == "_AlbedoMap" ||
                texName == "_MainTexture" || texName == "_Texture")
            {
                var secondField = texEnv["second"];
                if (secondField == null || secondField.IsDummy) continue;

                var texPtr = secondField["m_Texture"];
                if (texPtr == null || texPtr.IsDummy) continue;

                var texPathId = texPtr["m_PathID"].AsLong;
                var texFileId = texPtr["m_FileID"].AsInt;

                if (texPathId != 0)
                {
                    LoadTexture(fileInst, texFileId, texPathId, sceneObj);
                    return;
                }
            }
        }

        foreach (var texEnv in texEnvs)
        {
            if (texEnv == null) continue;

            var secondField = texEnv["second"];
            if (secondField == null || secondField.IsDummy) continue;

            var texPtr = secondField["m_Texture"];
            if (texPtr == null || texPtr.IsDummy) continue;

            var texPathId = texPtr["m_PathID"].AsLong;
            var texFileId = texPtr["m_FileID"].AsInt;

            if (texPathId != 0)
            {
                LoadTexture(fileInst, texFileId, texPathId, sceneObj);
                return;
            }
        }
    }

    private void LoadTexture(AssetsFileInstance fileInst, int texFileId, long texPathId, SceneObject sceneObj)
    {
        if (fileInst == null || sceneObj == null) return;

        try
        {
            var texAsset = _workspace.GetAssetInst(fileInst, texFileId, texPathId);
            if (texAsset == null || texAsset.FileInstance == null) return;

            var texBf = _workspace.GetBaseField(texAsset);
            if (texBf == null) return;

            var texture = TextureFile.ReadTextureFile(texBf);
            if (texture == null) return;
            if (texture.m_Width <= 0 || texture.m_Height <= 0) return;
            if (texture.m_TextureFormat <= 0) return;

            byte[]? encData = null;
            try
            {
                encData = texture.FillPictureData(texAsset.FileInstance);
            }
            catch { return; }

            if (encData == null || encData.Length == 0) return;

            byte[]? decData = null;
            try
            {
                decData = texture.DecodeTextureRaw(encData);
            }
            catch { return; }

            if (decData != null && decData.Length > 0)
            {
                sceneObj.TextureData = decData;
                sceneObj.TextureWidth = texture.m_Width;
                sceneObj.TextureHeight = texture.m_Height;
            }
        }
        catch { }
    }

    private static Vector3 ReadVector3(AssetTypeValueField field)
    {
        return new Vector3(
            field["x"].AsFloat,
            field["y"].AsFloat,
            field["z"].AsFloat
        );
    }

    private static Quaternion ReadQuaternion(AssetTypeValueField field)
    {
        return new Quaternion(
            field["x"].AsFloat,
            field["y"].AsFloat,
            field["z"].AsFloat,
            field["w"].AsFloat
        );
    }

    public SceneObject? PickObject(Vector3 rayOrigin, Vector3 rayDirection)
    {
        SceneObject? closest = null;
        float closestDist = float.MaxValue;

        foreach (var obj in AllObjects)
        {
            if (obj.RayIntersects(rayOrigin, rayDirection, out float dist))
            {
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = obj;
                }
            }
        }

        return closest;
    }

    public void DeselectAll()
    {
        foreach (var obj in AllObjects)
        {
            obj.IsSelected = false;
        }
    }
}
