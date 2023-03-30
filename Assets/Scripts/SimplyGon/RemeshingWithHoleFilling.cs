// Copyright (c) Microsoft Corporation. 
// Licensed under the MIT License. 

using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class RemeshingWithHoleFilling
{
    static Simplygon.spScene LoadScene(Simplygon.ISimplygon sg, string path)
    {
        // Create scene importer 
        using (Simplygon.spSceneImporter sgSceneImporter = sg.CreateSceneImporter())
        {
            sgSceneImporter.SetImportFilePath(path);

            // Run scene importer. 
            var importResult = sgSceneImporter.Run();
            if (Simplygon.Simplygon.Failed(importResult))
            {
                throw new System.Exception("Failed to load scene.");
            }

            Simplygon.spScene sgScene = sgSceneImporter.GetScene();
            return sgScene;
        }
    }

    static void SaveScene(Simplygon.ISimplygon sg, Simplygon.spScene sgScene, string outPutfolder, string path)
    {
        // Create scene exporter. 
        using (Simplygon.spSceneExporter sgSceneExporter = sg.CreateSceneExporter())
        {
            string outputScenePath = string.Join("",
                new string[] { outPutfolder + "\\", path });
            sgSceneExporter.SetExportFilePath(outputScenePath);
            sgSceneExporter.SetScene(sgScene);

            // Run scene exporter. 
            var exportResult = sgSceneExporter.Run();
            if (Simplygon.Simplygon.Failed(exportResult))
            {
                throw new System.Exception("Failed to save scene.");
            }
        }
    }

    static void CheckLog(Simplygon.ISimplygon sg)
    {
        // Check if any errors occurred. 
        bool hasErrors = sg.ErrorOccurred();
        if (hasErrors)
        {
            Simplygon.spStringArray errors = sg.CreateStringArray();
            sg.GetErrorMessages(errors);
            var errorCount = errors.GetItemCount();
            if (errorCount > 0)
            {
                Debug.LogError("Errors:");
                for (uint errorIndex = 0; errorIndex < errorCount; ++errorIndex)
                {
                    string errorString = errors.GetItem((int)errorIndex);
                    Debug.LogError(errorString);
                }

                sg.ClearErrorMessages();
            }
        }
        else
        {
            Debug.Log("No errors.");
        }

        // Check if any warnings occurred. 
        bool hasWarnings = sg.WarningOccurred();
        if (hasWarnings)
        {
            Simplygon.spStringArray warnings = sg.CreateStringArray();
            sg.GetWarningMessages(warnings);
            var warningCount = warnings.GetItemCount();
            if (warningCount > 0)
            {
                Debug.LogWarning("Warnings:");
                for (uint warningIndex = 0; warningIndex < warningCount; ++warningIndex)
                {
                    string warningString = warnings.GetItem((int)warningIndex);
                    Debug.LogWarning(warningString);
                }

                sg.ClearWarningMessages();
            }
        }
        else
        {
            Debug.Log("No warnings.");
        }
    }

    static int RunRemeshing(string path, string outPutfolder, string outPutName)
    {
        using (var sg = Simplygon.Loader.InitSimplygon(out var errorCode, out var errorMessage))
        {
            if (errorCode != Simplygon.EErrorCodes.NoError)
            {
                Debug.Log($"Failed to initialize Simplygon: ErrorCode({(int)errorCode}) {errorMessage}");
                return (int)errorCode;
            }

            // Load scene to process.         
            Debug.Log("Load scene to process.");
            Simplygon.spScene sgScene = LoadScene(sg, path);

            // Create the remeshing processor. 
            using (Simplygon.spRemeshingProcessor sgRemeshingProcessor = sg.CreateRemeshingProcessor())
            {
                sgRemeshingProcessor.SetScene(sgScene);
                using (Simplygon.spRemeshingSettings sgRemeshingSettings = sgRemeshingProcessor.GetRemeshingSettings())
                {
                    // Set on-screen size target for remeshing. 
                    sgRemeshingSettings.SetOnScreenSize(80);

                    // Enable hole filling. 
                    sgRemeshingSettings.SetHoleFilling(Simplygon.EHoleFilling.Medium);
                }

                // Start the remeshing process.         
                Debug.Log("Start the remeshing process.");
                sgRemeshingProcessor.RunProcessing();
            }


            // Replace original materials and textures from the scene with a new empty material, as the 
            // remeshed object has a new UV set.  
            sgScene.GetTextureTable().Clear();
            sgScene.GetMaterialTable().Clear();
            sgScene.GetMaterialTable().AddMaterial(sg.CreateMaterial());

            // Save processed scene.         
            Debug.Log("Save processed scene.");
            SaveScene(sg, sgScene, outPutfolder, outPutName);

            // Check log for any warnings or errors.         
            Debug.Log("Check log for any warnings or errors.");
            CheckLog(sg);
        }

        return 0;
    }

    private const string outPutFloder = "SimplyGonOutPut";
    private const string suffixFbx = ".fbx";
    private const string suffixPrefab = ".prefab";
    public static int StartRemeshing(string path)
    {
        string outPutName = Path.GetFileNameWithoutExtension(path);
        string outPutFbxPath = outPutFloder + "/" + outPutName + suffixFbx;
        string outPutPrefabPath = outPutFloder + "/" + outPutName + suffixPrefab;
        RunRemeshing(path, outPutFloder, outPutName + suffixFbx);
        string destFileName = Application.dataPath + "/" + outPutFbxPath;
        if (AssetDatabase.IsValidFolder(Path.Combine("Assets", outPutFloder)) == false)
        {
            AssetDatabase.CreateFolder("Assets",outPutFloder);
        }

        if(File.Exists(destFileName))
            File.Delete(destFileName);
            
        File.Copy(outPutFbxPath, destFileName);
        AssetDatabase.ImportAsset("Assets/" + outPutFbxPath);
        GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/" + outPutFbxPath);
        go = GameObject.Instantiate(go);
        PrefabUtility.SaveAsPrefabAssetAndConnect(go, "Assets/" + outPutPrefabPath, InteractionMode.UserAction);
        
        return 0;
    }
}