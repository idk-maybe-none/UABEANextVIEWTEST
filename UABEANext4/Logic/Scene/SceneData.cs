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

/// <summary>
/// Loads and manages scene data from Unity asset files.
/// </summary>
public class SceneData
{
    public List<SceneObject> RootObjects { get; } = new();
    public List<SceneObject> AllObjects { get; } = new();

    private readonly Workspace _workspace;
    private readonly Dictionary<long, SceneObject> _pathIdToObject = new();

    public SceneData(Workspace workspace)
    {
        _workspace = workspace;
    }

    public void LoadFromFile(AssetsFileInstance fileInst)
    {
        RootObjects.Clear();
        AllObjects.Clear();
        _pathIdToObject.Clear();

        // First pass: Create all scene objects with transforms
        var transformInfos = fileInst.file.GetAssetsOfType(AssetClassID.Transform)
            .Concat(fileInst.file.GetAssetsOfType(AssetClassID.RectTransform))
            .ToList();

        var goInfos = fileInst.file.GetAssetsOfType(AssetClassID.GameObject).ToList();
        var goPathIdToInfo = goInfos.ToDictionary(g => g.PathId);

        // Map transform PathId to its parent PathId
        var tfmParentMap = new Dictionary<long, long>();
        var tfmToGoMap = new Dictionary<long, long>();
        var tfmChildrenMap = new Dictionary<long, List<long>>();

        foreach (var tfmInfo in transformInfos)
        {
            var tfmBf = _workspace.GetBaseField(fileInst, tfmInfo.PathId);
            if (tfmBf == null) continue;

            var fatherField = tfmBf["m_Father"];
            if (fatherField == null) continue;
            
            var parentField = fatherField["m_PathID"];
            if (parentField == null) continue;
                
            var parentPathId = parentField.AsLong;
            tfmParentMap[tfmInfo.PathId] = parentPathId;

            var goPtr = tfmBf["m_GameObject"];
            if (goPtr == null) continue;
            
            var goPathIdField = goPtr["m_PathID"];
            if (goPathIdField == null) continue;
            
            var goPathId = goPathIdField.AsLong;
            tfmToGoMap[tfmInfo.PathId] = goPathId;

            // Read transform data
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
                    var nameField = goBf["m_Name"];
                    if (nameField != null)
                    {
                        goName = nameField.AsString;
                    }
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

            // Track children
            var childrenArr = tfmBf["m_Children.Array"];
            if (childrenArr != null)
            {
                var childIds = new List<long>();
                foreach (var child in childrenArr)
                {
                    var pathIdField = child["m_PathID"];
                    if (pathIdField != null)
                    {
                        childIds.Add(pathIdField.AsLong);
                    }
                }
                tfmChildrenMap[tfmInfo.PathId] = childIds;
            }
        }

        // Second pass: Build hierarchy
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

        // Third pass: Load meshes and materials for objects with MeshFilter/MeshCollider
        var meshFilterInfos = fileInst.file.GetAssetsOfType(AssetClassID.MeshFilter).ToList();
        var meshRendererInfos = fileInst.file.GetAssetsOfType(AssetClassID.MeshRenderer).ToList();
        var meshColliderInfos = fileInst.file.GetAssetsOfType(AssetClassID.MeshCollider).ToList();

        // Map GameObject PathId to MeshFilter/MeshRenderer/MeshCollider
        var goToMeshFilter = new Dictionary<long, AssetFileInfo>();
        var goToMeshRenderer = new Dictionary<long, AssetFileInfo>();
        var goToMeshCollider = new Dictionary<long, AssetFileInfo>();

        // Track static flags for GameObjects (used to prefer MeshCollider mesh for static objects)
        var goStaticFlags = new Dictionary<long, uint>();
        foreach (var goInfo in goInfos)
        {
            var goBf = _workspace.GetBaseField(fileInst, goInfo.PathId);
            if (goBf != null)
            {
                var staticFlagsField = goBf["m_StaticEditorFlags"];
                if (staticFlagsField != null)
                {
                    // m_StaticEditorFlags: non-zero means the object has some static flags set
                    var staticFlags = staticFlagsField.AsUInt;
                    goStaticFlags[goInfo.PathId] = staticFlags;
                }
            }
        }

        foreach (var mfInfo in meshFilterInfos)
        {
            var mfBf = _workspace.GetBaseField(fileInst, mfInfo.PathId);
            if (mfBf == null) continue;

            var gameObjectField = mfBf["m_GameObject"];
            if (gameObjectField == null) continue;
            
            var pathIdField = gameObjectField["m_PathID"];
            if (pathIdField == null) continue;
            
            var goPathId = pathIdField.AsLong;
            goToMeshFilter[goPathId] = mfInfo;
        }

        foreach (var mrInfo in meshRendererInfos)
        {
            var mrBf = _workspace.GetBaseField(fileInst, mrInfo.PathId);
            if (mrBf == null) continue;

            var gameObjectField = mrBf["m_GameObject"];
            if (gameObjectField == null) continue;
            
            var pathIdField = gameObjectField["m_PathID"];
            if (pathIdField == null) continue;
            
            var goPathId = pathIdField.AsLong;
            goToMeshRenderer[goPathId] = mrInfo;
        }

        foreach (var mcInfo in meshColliderInfos)
        {
            var mcBf = _workspace.GetBaseField(fileInst, mcInfo.PathId);
            if (mcBf == null) continue;

            var gameObjectField = mcBf["m_GameObject"];
            if (gameObjectField == null) continue;
            
            var pathIdField = gameObjectField["m_PathID"];
            if (pathIdField == null) continue;
            
            var goPathId = pathIdField.AsLong;
            goToMeshCollider[goPathId] = mcInfo;
        }

        // Load mesh data for each scene object
        // For static objects, prefer MeshCollider's mesh (if available) over MeshFilter's mesh
        // This is important because static objects often use simplified collision meshes for accurate collision representation
        foreach (var sceneObj in AllObjects)
        {
            var goPathId = tfmToGoMap.GetValueOrDefault(sceneObj.PathId, 0);
            if (goPathId == 0) continue;

            // Check if this object is static (has any static flags set)
            var isStatic = goStaticFlags.TryGetValue(goPathId, out var staticFlags) && staticFlags != 0;

            // For static objects, try to use MeshCollider's mesh first
            bool meshLoaded = false;
            if (isStatic && goToMeshCollider.TryGetValue(goPathId, out var mcInfo))
            {
                var mcBf = _workspace.GetBaseField(fileInst, mcInfo.PathId);
                if (mcBf != null)
                {
                    var meshPtr = mcBf["m_Mesh"];
                    if (meshPtr != null)
                    {
                        var meshPathIdField = meshPtr["m_PathID"];
                        var meshFileIdField = meshPtr["m_FileID"];
                        if (meshPathIdField != null && meshFileIdField != null)
                        {
                            var meshPathId = meshPathIdField.AsLong;
                            var meshFileId = meshFileIdField.AsInt;

                            if (meshPathId != 0)
                            {
                                try
                                {
                                    var meshBf = _workspace.GetBaseField(fileInst, meshFileId, meshPathId);
                                    if (meshBf != null)
                                    {
                                        var version = fileInst.file.Metadata.UnityVersion;
                                        sceneObj.Mesh = new MeshObj(fileInst, meshBf, new UnityVersion(version));

                                        // Get UVs if available (though collision meshes typically don't have UVs)
                                        if (sceneObj.Mesh.UVs != null && sceneObj.Mesh.UVs.Length > 0 && sceneObj.Mesh.UVs[0] != null)
                                        {
                                            sceneObj.UVs = sceneObj.Mesh.UVs[0];
                                        }
                                        meshLoaded = true;
                                    }
                                }
                                catch
                                {
                                    // MeshCollider mesh loading failed, will fall back to MeshFilter
                                }
                            }
                        }
                    }
                }
            }

            // Fall back to MeshFilter's mesh for non-static objects or if MeshCollider loading failed
            if (!meshLoaded && goToMeshFilter.TryGetValue(goPathId, out var mfInfo))
            {
                var mfBf = _workspace.GetBaseField(fileInst, mfInfo.PathId);
                if (mfBf != null)
                {
                    var meshPtr = mfBf["m_Mesh"];
                    if (meshPtr != null)
                    {
                        var meshPathIdField = meshPtr["m_PathID"];
                        var meshFileIdField = meshPtr["m_FileID"];
                        if (meshPathIdField != null && meshFileIdField != null)
                        {
                            var meshPathId = meshPathIdField.AsLong;
                            var meshFileId = meshFileIdField.AsInt;

                            if (meshPathId != 0)
                            {
                                try
                                {
                                    var meshBf = _workspace.GetBaseField(fileInst, meshFileId, meshPathId);
                                    if (meshBf != null)
                                    {
                                        var version = fileInst.file.Metadata.UnityVersion;
                                        sceneObj.Mesh = new MeshObj(fileInst, meshBf, new UnityVersion(version));

                                        // Get UVs if available
                                        if (sceneObj.Mesh.UVs != null && sceneObj.Mesh.UVs.Length > 0 && sceneObj.Mesh.UVs[0] != null)
                                        {
                                            sceneObj.UVs = sceneObj.Mesh.UVs[0];
                                        }
                                    }
                                }
                                catch
                                {
                                    // Mesh loading failed, skip
                                }
                            }
                        }
                    }
                }
            }

            // Load texture from material
            if (goToMeshRenderer.TryGetValue(goPathId, out var mrInfo))
            {
                var mrBf = _workspace.GetBaseField(fileInst, mrInfo.PathId);
                if (mrBf == null) continue;

                var materialsArr = mrBf["m_Materials.Array"];
                if (materialsArr != null && materialsArr.Children.Count > 0)
                {
                    var matPtr = materialsArr[0];
                    if (matPtr != null)
                    {
                        var matPathIdField = matPtr["m_PathID"];
                        var matFileIdField = matPtr["m_FileID"];
                        if (matPathIdField != null && matFileIdField != null)
                        {
                            var matPathId = matPathIdField.AsLong;
                            var matFileId = matFileIdField.AsInt;

                            if (matPathId != 0)
                            {
                                try
                                {
                                    LoadTextureFromMaterial(fileInst, matFileId, matPathId, sceneObj);
                                }
                                catch
                                {
                                    // Texture loading failed, skip
                                }
                            }
                        }
                    }
                }
            }
        }

        // Compute world matrices and bounds
        foreach (var root in RootObjects)
        {
            root.ComputeWorldMatrix();
        }

        foreach (var obj in AllObjects)
        {
            obj.ComputeBounds();
        }
    }

    private void LoadTextureFromMaterial(AssetsFileInstance fileInst, int matFileId, long matPathId, SceneObject sceneObj)
    {
        var matBf = _workspace.GetBaseField(fileInst, matFileId, matPathId);
        if (matBf == null) return;

        var savedProperties = matBf["m_SavedProperties"];
        if (savedProperties == null) return;
        
        var texEnvs = savedProperties["m_TexEnvs.Array"];
        if (texEnvs == null) return;

        foreach (var texEnv in texEnvs)
        {
            var firstField = texEnv["first"];
            if (firstField == null) continue;
            
            var texName = firstField.AsString;
            if (texName == "_MainTex" || texName == "_BaseMap" || texName == "_Albedo")
            {
                var secondField = texEnv["second"];
                if (secondField == null) continue;
                
                var texPtr = secondField["m_Texture"];
                if (texPtr == null) continue;
                
                var texPathIdField = texPtr["m_PathID"];
                var texFileIdField = texPtr["m_FileID"];
                if (texPathIdField != null && texFileIdField != null)
                {
                    var texPathId = texPathIdField.AsLong;
                    var texFileId = texFileIdField.AsInt;

                    if (texPathId != 0)
                    {
                        LoadTexture(fileInst, texFileId, texPathId, sceneObj);
                        return;
                    }
                }
            }
        }
    }

    private void LoadTexture(AssetsFileInstance fileInst, int texFileId, long texPathId, SceneObject sceneObj)
    {
        var texAsset = _workspace.GetAssetInst(fileInst, texFileId, texPathId);
        if (texAsset == null) return;

        var texBf = _workspace.GetBaseField(texAsset);
        if (texBf == null) return;

        try
        {
            var texture = TextureFile.ReadTextureFile(texBf);
            var encData = texture.FillPictureData(texAsset.FileInstance);
            var decData = texture.DecodeTextureRaw(encData);

            if (decData != null)
            {
                sceneObj.TextureData = decData;
                sceneObj.TextureWidth = texture.m_Width;
                sceneObj.TextureHeight = texture.m_Height;
            }
        }
        catch
        {
            // Texture decode failed
        }
    }

    private static Vector3 ReadVector3(AssetTypeValueField field)
    {
        if (field == null) return Vector3.Zero;
        
        var xField = field["x"];
        var yField = field["y"];
        var zField = field["z"];
        
        return new Vector3(
            xField?.AsFloat ?? 0f,
            yField?.AsFloat ?? 0f,
            zField?.AsFloat ?? 0f
        );
    }

    private static Quaternion ReadQuaternion(AssetTypeValueField field)
    {
        if (field == null) return Quaternion.Identity;
        
        var xField = field["x"];
        var yField = field["y"];
        var zField = field["z"];
        var wField = field["w"];
        
        return new Quaternion(
            xField?.AsFloat ?? 0f,
            yField?.AsFloat ?? 0f,
            zField?.AsFloat ?? 0f,
            wField?.AsFloat ?? 1f
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
