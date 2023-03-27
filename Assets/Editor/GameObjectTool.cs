using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using Simplygon;
using Simplygon.SPL.v80.Processor;

namespace Ftp.Editor
{
    public class GameObjectTool : UnityEditor.Editor
    {
        private const int MINSIZE = 0;
        private const int Mesh16BitBufferVertexLimit = 65535;
        private const string floorName = "StoneFloor";
        private static List<Transform> floorObjs;

        [MenuItem("GameObject/GameObjectTools/SeparateColliderAndWallCollider", false, 30)]
        static void SeparateColliderAndWallCollider()
        {
            //"StoneFloor"
            floorObjs = new List<Transform>();
            if (Selection.activeGameObject == null)
            {
                EditorUtility.DisplayDialog("No Object Selected", "Please select any GameObject to Export to FBX",
                    "Okay");
                return;
            }

            GameObject currentGameObject = Selection.activeObject as GameObject;

            if (currentGameObject == null)
            {
                EditorUtility.DisplayDialog("Warning", "Item selected is not a GameObject", "Okay");
                return;
            }

            if (currentGameObject.name.Equals("quadtree") == false)
            {
                EditorUtility.DisplayDialog("Error", "must selected quadtree", "Okay");
                return;
            }

            GetFloorObjs(currentGameObject.transform);
            for (int i = 0; i < floorObjs.Count; i++)
            {
                SeparateColliderByLayerName(floorObjs[i], "Ground", "ColliderGround");
            }

            GameObject colliderObj = GameObject.Find("ColliderGround");
            PrefabUtility.SaveAsPrefabAssetAndConnect(colliderObj, "Assets/Resources/" + colliderObj.name + ".prefab",
                InteractionMode.AutomatedAction);
        }

        static void GetFloorObjs(Transform t)
        {
            if (t.childCount > 0)
            {
                for (int i = 0; i < t.childCount; i++)
                {
                    GetFloorObjs(t.GetChild(i));
                }
            }
            else
            {
                if (t.name.Contains(floorName) && IfLOD0(t))
                {
                    floorObjs.Add(t);
                }
            }
        }

        static bool IfLOD0(Transform t)
        {
            bool ifLOD0 = false;
            while (true)
            {
                if (t == null)
                    return false;

                if (t.name.Contains("LOD0"))
                    return true;

                t = t.parent;
            }
        }

        static void SeparateColliderByLayerName(Transform trans, string layerName, string colliderName)
        {
            GameObject colliderObj = GameObject.Find(colliderName);
            if (colliderObj == null)
                colliderObj = new GameObject(colliderName);

            MeshCollider mc = trans.gameObject.GetComponent<MeshCollider>();
            if (mc == null)
                mc = trans.gameObject.AddComponent<MeshCollider>();
            mc.convex = true;

            GameObject newGameObj = new GameObject(trans.name);
            MeshCollider newMc = newGameObj.AddComponent<MeshCollider>();
            newMc.sharedMesh = mc.sharedMesh;
            newMc.convex = false;

            Transform t = colliderObj.transform.Find(newGameObj.name);
            if (t != null)
            {
                Debug.LogError("colliderObj下面已经存在了 " + newGameObj.name + " 的GameObject");
                PrefabUtility.UnpackPrefabInstance(colliderObj, PrefabUnpackMode.OutermostRoot,
                    InteractionMode.UserAction);
                GameObject.DestroyImmediate(t.gameObject);
            }

            newGameObj.transform.SetParent(colliderObj.transform);
            newGameObj.transform.SetPositionAndRotation(trans.position, trans.rotation);
            newGameObj.transform.localScale = trans.localScale;
            newGameObj.layer = LayerMask.NameToLayer(layerName);
        }

        [MenuItem("GameObject/GameObjectTools/CreateBuildingCollider", false, 30)]
        static void CreateBuildingCollider()
        {
            string colliderObjName = "BuildingColliderObj";
            if (Selection.activeGameObject == null)
            {
                EditorUtility.DisplayDialog("No Object Selected", "Please select any GameObject to Export to FBX",
                    "Okay");
                return;
            }

            GameObject currentGameObject = Selection.activeObject as GameObject;

            if (currentGameObject == null)
            {
                EditorUtility.DisplayDialog("Warning", "Item selected is not a GameObject", "Okay");
                return;
            }

            MeshFilter[] meshFilters = CreatMeshFilterList(currentGameObject);
            if (meshFilters.Length < 1)
            {
                EditorUtility.DisplayDialog("Warning", "Item selected has no MeshFilters", "Okay");
                return;
            }

            GameObject colliderObj = GameObject.Find(colliderObjName);
            if (colliderObj == null)
                colliderObj = new GameObject(colliderObjName);

            string name = currentGameObject.name;
            GameObject obj = CombineMeshesWithMutliMaterial(meshFilters, name);

            MeshCollider mc = obj.GetComponent<MeshCollider>();
            if (mc == null)
                mc = obj.AddComponent<MeshCollider>();
            mc.convex = true;

            GameObject newGameObj = new GameObject(obj.name);
            MeshCollider newMc = newGameObj.AddComponent<MeshCollider>();
            newMc.sharedMesh = mc.sharedMesh;
            newMc.convex = true;

            GameObject.DestroyImmediate(obj);

            newGameObj.layer = LayerMask.NameToLayer("Wall");
            newGameObj.transform.SetParent(colliderObj.transform);
        }

        public static (float x_min, float y_min, float z_min, float x_max, float y_max, float z_max) GetGameObjectsMesh(
            GameObject obj)
        {
            Vector3 min = Vector3.zero;
            Vector3 max = Vector3.zero;
            var renders = obj.GetComponentsInChildren<Renderer>();
            if (renders.Length > 0)
            {
                min = renders[0].bounds.min;
                max = renders[0].bounds.max;
                foreach (var item in renders)
                {
                    min.x = Mathf.Min(min.x, item.bounds.min.x);
                    min.z = Mathf.Min(min.z, item.bounds.min.z);
                    max.x = Mathf.Max(max.x, item.bounds.max.x);
                    max.z = Mathf.Max(max.z, item.bounds.max.z);
                }
            }
            else
            {
                min = obj.transform.position;
                max = obj.transform.position;
            }

            if (min.x > max.x)
            {
                float x = min.x;
                min.x = max.x;
                max.x = x;
            }

            if (min.y > max.y)
            {
                float y = min.y;
                min.y = max.y;
                max.y = y;
            }

            if (min.z > max.z)
            {
                float z = min.z;
                min.z = max.z;
                max.z = z;
            }

            return (min.x, min.y, min.z, max.x, max.y, max.z);
        }

        public static GameObject CombineMeshesWithMutliMaterial(MeshFilter[] meshFilters, string name)
        {
            GameObject gameObject = new GameObject();
            gameObject.name = name; //System.Math.Abs(gameObject.GetInstanceID()).ToString();

            #region Get MeshFilters, MeshRenderers and unique Materials from all children:

            MeshRenderer[] meshRenderers = new MeshRenderer[meshFilters.Length];

            for (int i = 0; i != meshFilters.Length; ++i)
            {
                meshRenderers[i] = meshFilters[i].GetComponent<MeshRenderer>();
            }

            List<Material> uniqueMaterialsList = new List<Material>();
            for (int i = 0; i < meshFilters.Length; i++)
            {
                meshRenderers[i] = meshFilters[i].GetComponent<MeshRenderer>();
                if (meshRenderers[i] != null)
                {
                    Material[] materials = meshRenderers[i].sharedMaterials; // Get all Materials from child Mesh.
                    for (int j = 0; j < materials.Length; j++)
                    {
                        if (!uniqueMaterialsList
                                .Contains(materials[j])) // If Material doesn't exists in the list then add it.
                        {
                            uniqueMaterialsList.Add(materials[j]);
                        }
                    }
                }
            }

            #endregion Get MeshFilters, MeshRenderers and unique Materials from all children.

            #region Combine children Meshes with the same Material to create submeshes for final Mesh:

            List<CombineInstance> finalMeshCombineInstancesList = new List<CombineInstance>();

            // If it will be over 65535 then use the 32 bit index buffer:
            long verticesLength = 0;

            for (int i = 0;
                 i < uniqueMaterialsList.Count;
                 i++) // Create each Mesh (submesh) from Meshes with the same Material.
            {
                List<CombineInstance> submeshCombineInstancesList = new List<CombineInstance>();

                for (int j = 0; j < meshFilters.Length; j++) // Get only childeren Meshes (skip our Mesh).
                {
                    if (meshRenderers[j] != null)
                    {
                        Material[] submeshMaterials =
                            meshRenderers[j].sharedMaterials; // Get all Materials from child Mesh.

                        for (int k = 0; k < submeshMaterials.Length; k++)
                        {
                            // If Materials are equal, combine Mesh from this child:
                            if (uniqueMaterialsList[i] == submeshMaterials[k])
                            {
                                CombineInstance combineInstance = new CombineInstance();
                                combineInstance.subMeshIndex = k; // Mesh may consist of smaller parts - submeshes.
                                // Every part have different index. If there are 3 submeshes
                                // in Mesh then MeshRender needs 3 Materials to render them.
                                combineInstance.mesh = meshFilters[j].sharedMesh;
                                if (combineInstance.mesh != null && combineInstance.mesh.vertices != null)
                                {
                                    combineInstance.transform = meshFilters[j].transform.localToWorldMatrix;
                                    submeshCombineInstancesList.Add(combineInstance);
                                    verticesLength += combineInstance.mesh.vertices.Length;
                                }
                            }
                        }
                    }
                }

                // Create new Mesh (submesh) from Meshes with the same Material:
                Mesh submesh = new Mesh();

                if (verticesLength > Mesh16BitBufferVertexLimit)
                {
                    submesh.indexFormat =
                        UnityEngine.Rendering.IndexFormat.UInt32; // Only works on Unity 2017.3 or higher.
                }

                submesh.CombineMeshes(submeshCombineInstancesList.ToArray(), true);


                CombineInstance finalCombineInstance = new CombineInstance();
                finalCombineInstance.subMeshIndex = 0;
                finalCombineInstance.mesh = submesh;
                finalCombineInstance.transform = Matrix4x4.identity;
                finalMeshCombineInstancesList.Add(finalCombineInstance);
            }

            #endregion Combine submeshes (children Meshes) with the same Material.

            #region Set Materials array & combine submeshes into one multimaterial Mesh:

            MeshRenderer renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = uniqueMaterialsList.ToArray();

            Mesh combinedMesh = new Mesh();
            //string name = System.Math.Abs(combinedMesh.GetInstanceID()).ToString();
            combinedMesh.name = name;

            if (verticesLength > Mesh16BitBufferVertexLimit)
            {
                combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            combinedMesh.CombineMeshes(finalMeshCombineInstancesList.ToArray(), false);
            //GenerateUV(combinedMesh);
            MeshFilter filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = combinedMesh;

            if (true)
            {
                if (verticesLength <= Mesh16BitBufferVertexLimit)
                {
                    Debug.Log("<color=#00cc00><b>Mesh \"" + name + "\" was created from " + (meshFilters.Length - 1) +
                              " children meshes and has "
                              + finalMeshCombineInstancesList.Count + " submeshes, and " + verticesLength +
                              " vertices.</b></color>");
                }
                else
                {
                    Debug.Log("<color=#ff3300><b>Mesh \"" + name + "\" was created from " + (meshFilters.Length - 1) +
                              " children meshes and has "
                              + finalMeshCombineInstancesList.Count + " submeshes, and " + verticesLength
                              + " vertices. Some old devices, like Android with Mali-400 GPU, do not support over 65535 vertices.</b></color>");
                }
            }

            #endregion Set Materials array & combine submeshes into one multimaterial Mesh.

            return gameObject;
        }

        public static MeshFilter[] CreatMeshFilterList(GameObject rootObj)
        {
            Stack<GameObject> openList = new Stack<GameObject>();
            List<MeshFilter> mFilters = new List<MeshFilter>();
            openList.Push(rootObj);
            while (openList.Count > 0)
            {
                var obj = openList.Pop();

                (float x_min, float y_min, float z_min, float x_max, float y_max, float z_max) =
                    GetGameObjectsMesh(obj);
                if (Math.Abs(x_max - x_min) < MINSIZE && Math.Abs(z_max - z_min) < MINSIZE)
                {
                    continue;
                }

                LODGroup lgroup = obj.GetComponent<LODGroup>();
                if (lgroup != null)
                {
                    LOD[] group = lgroup.GetLODs();
                    if (group.Length > 0)
                    {
                        Renderer[] renderers = group[group.Length - 1].renderers;
                        for (int renderIDX = 0; renderIDX < renderers.Length; renderIDX++)
                        {
                            Renderer renderer = renderers[renderIDX];
                            MeshFilter m = renderer.transform.GetComponentInChildren<MeshFilter>();

                            if (m != null)
                            {
                                mFilters.Add(m);
                            }
                            else
                            {
                                SkinnedMeshRenderer sm =
                                    renderer.transform.GetComponentInChildren<SkinnedMeshRenderer>();
                                if (sm != null)
                                {
                                    Mesh bm = new Mesh();
                                    sm.BakeMesh(bm);
                                    GameObject newobj = new GameObject();
                                    newobj.transform.position = rootObj.transform.position;
                                    newobj.transform.rotation = rootObj.transform.rotation;
                                    newobj.transform.localScale = rootObj.transform.localScale;
                                    newobj.transform.localEulerAngles = rootObj.transform.localEulerAngles;
                                    newobj.transform.parent = rootObj.transform.parent;
                                    newobj.transform.name = "TemporaryGameObject";
                                    newobj.AddComponent<MeshFilter>();
                                    newobj.AddComponent<MeshRenderer>();
                                    MeshFilter n = newobj.GetComponent<MeshFilter>();
                                    MeshRenderer r = newobj.GetComponent<MeshRenderer>();
                                    n.sharedMesh = bm;
                                    r.sharedMaterial = sm.sharedMaterial;
                                    mFilters.Add(n);
                                }
                            }
                        }

                        continue;
                    }
                }
                else
                {
                    //该mesh没有创建lod group
                    MeshFilter m = obj.transform.GetComponent<MeshFilter>();
                    if (m != null)
                    {
                        mFilters.Add(m);
                    }
                }

                for (int i = 0; i < obj.transform.childCount; i++)
                {
                    openList.Push(obj.transform.GetChild(i).gameObject);
                }
            }

            return mFilters.ToArray();
        }

        [MenuItem("GameObject/GameObjectTools/CreateBuildingMesh", false, 30)]
        static void CreateBuildingMesh()
        {
            string colliderObjName = "BuildingColliderObj";
            if (Selection.activeGameObject == null)
            {
                EditorUtility.DisplayDialog("No Object Selected", "Please select any GameObject to Export to FBX",
                    "Okay");
                return;
            }

            GameObject currentGameObject = Selection.activeObject as GameObject;

            if (currentGameObject == null)
            {
                EditorUtility.DisplayDialog("Warning", "Item selected is not a GameObject", "Okay");
                return;
            }

            MeshFilter[] meshFilters = CreatMeshFilterList(currentGameObject);
            if (meshFilters.Length < 1)
            {
                EditorUtility.DisplayDialog("Warning", "Item selected has no MeshFilters", "Okay");
                return;
            }

            GameObject colliderObj = GameObject.Find(colliderObjName);
            if (colliderObj == null)
                colliderObj = new GameObject(colliderObjName);

            string name = currentGameObject.name;
            GameObject obj = CombineMeshesWithMutliMaterial(meshFilters, name);

        }



        #region oldCode

        //[MenuItem("GameObject/GameObjectTools/SeparateColliderToWall", false, 30)]
        static void SeparateColliderToWall()
        {
            SeparateCollider("Wall");
        }

        //[MenuItem("GameObject/GameObjectTools/SeparateColliderToGround", false, 30)]
        static void SeparateColliderToGround()
        {
            SeparateCollider("Ground");
        }

        //[MenuItem("GameObject/GameObjectTools/CancelSeparateCollider", false, 30)]
        static void CancelSeparateColliderSelect()
        {
            if (Selection.activeGameObject == null)
            {
                EditorUtility.DisplayDialog("No Object Selected", "Please select any GameObject to Export to FBX",
                    "Okay");
                return;
            }

            GameObject currentGameObject = Selection.activeObject as GameObject;

            if (currentGameObject == null)
            {
                EditorUtility.DisplayDialog("Warning", "Item selected is not a GameObject", "Okay");
                return;
            }

            MeshCollider mc = currentGameObject.GetComponent<MeshCollider>();
            if (mc != null)
            {
                GameObject.DestroyImmediate(mc);
            }

            GameObject colliderObj = GameObject.Find("colliderObj");
            if (colliderObj == null)
            {
                Debug.LogError("没有找到 colliderObj !!!");
                return;
            }

            Transform t = colliderObj.transform.Find(currentGameObject.name);
            if (t != null)
            {
                Debug.LogError("删除成功 " + t.gameObject.name);
                PrefabUtility.UnpackPrefabInstance(colliderObj, PrefabUnpackMode.OutermostRoot,
                    InteractionMode.UserAction);
                GameObject.DestroyImmediate(t.gameObject);
            }

            PrefabUtility.SaveAsPrefabAssetAndConnect(colliderObj, "Assets/Resources/" + colliderObj.name + ".prefab",
                InteractionMode.AutomatedAction);
        }

        static void SeparateCollider(string layerName)
        {
            if (Selection.activeGameObject == null)
            {
                EditorUtility.DisplayDialog("No Object Selected", "Please select any GameObject to Export to FBX",
                    "Okay");
                return;
            }

            GameObject currentGameObject = Selection.activeObject as GameObject;

            if (currentGameObject == null)
            {
                EditorUtility.DisplayDialog("Warning", "Item selected is not a GameObject", "Okay");
                return;
            }

            GameObject colliderObj = GameObject.Find("colliderObj");
            if (colliderObj == null)
                colliderObj = new GameObject("colliderObj");

            MeshCollider mc = currentGameObject.GetComponent<MeshCollider>();
            if (mc == null)
                mc = currentGameObject.AddComponent<MeshCollider>();
            mc.convex = true;

            GameObject newGameObj = new GameObject(currentGameObject.name);
            MeshCollider newMc = newGameObj.AddComponent<MeshCollider>();
            newMc.sharedMesh = mc.sharedMesh;
            newMc.convex = true;

            Transform t = colliderObj.transform.Find(newGameObj.name);
            if (t != null)
            {
                Debug.LogError("colliderObj下面已经存在了 " + newGameObj.name + " 的GameObject");
                PrefabUtility.UnpackPrefabInstance(colliderObj, PrefabUnpackMode.OutermostRoot,
                    InteractionMode.UserAction);
                GameObject.DestroyImmediate(t.gameObject);
            }

            newGameObj.transform.SetParent(colliderObj.transform);
            newGameObj.transform.SetPositionAndRotation(currentGameObject.transform.position,
                currentGameObject.transform.rotation);
            newGameObj.transform.localScale = currentGameObject.transform.localScale;
            newGameObj.layer = LayerMask.NameToLayer(layerName);

            PrefabUtility.SaveAsPrefabAssetAndConnect(colliderObj, "Assets/Resources/" + colliderObj.name + ".prefab",
                InteractionMode.AutomatedAction);
        }

        #endregion
    }
}