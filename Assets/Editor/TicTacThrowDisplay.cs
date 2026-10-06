using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class TicTacThrowDisplay
{
    const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    static TicTacThrowDisplay()
    {
        EditorApplication.delayCall += ConfigureForScene;
        EditorSceneManager.sceneOpened += SceneOpened;
    }
    static void SceneOpened(Scene scene,OpenSceneMode mode) { EditorApplication.delayCall += ConfigureForScene; }
    static void ConfigureForScene()
    {
        if (Application.isBatchMode || SceneManager.GetActiveScene().name != "TicTacThrow") return;
        Configure();
    }
    [MenuItem("Tools/TicTacThrow/1920 x 1080 Landscape")]
    public static void Configure()
    {
        PlayerSettings.defaultScreenWidth = 1920;
        PlayerSettings.defaultScreenHeight = 1080;
        PlayerSettings.fullScreenMode = FullScreenMode.ExclusiveFullScreen;
        PlayerSettings.resizableWindow = false;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        var assembly = typeof(Editor).Assembly;
        var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        var sizes = singleton.GetProperty("instance",BindingFlags.Public|BindingFlags.Static|BindingFlags.FlattenHierarchy).GetValue(null,null);
        var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
        var group = sizesType.GetMethod("GetGroup",Flags).Invoke(sizes,new object[]{Enum.Parse(groupType,"Standalone")});
        var type = group.GetType();
        int count = (int)type.GetMethod("GetBuiltinCount",Flags).Invoke(group,null) + (int)type.GetMethod("GetCustomCount",Flags).Invoke(group,null);
        int index = -1;
        for (int i = 0; i < count; i++)
        {
            var size = type.GetMethod("GetGameViewSize",Flags).Invoke(group,new object[]{i});
            int width = (int)size.GetType().GetProperty("width",Flags).GetValue(size,null);
            int height = (int)size.GetType().GetProperty("height",Flags).GetValue(size,null);
            string mode = size.GetType().GetProperty("sizeType",Flags).GetValue(size,null).ToString();
            if(width == 1920 && height == 1080 && mode == "FixedResolution") { index = i; break; }
        }
        if (index < 0)
        {
            var sizeType = assembly.GetType("UnityEditor.GameViewSize");
            var modeType = assembly.GetType("UnityEditor.GameViewSizeType");
            var size = Activator.CreateInstance(sizeType,Flags,null,new object[]{Enum.Parse(modeType,"FixedResolution"),1920,1080,"TicTacThrow Full HD"},null);
            type.GetMethod("AddCustomSize",Flags).Invoke(group,new object[]{size});
            index = count;
        }
        var viewType = assembly.GetType("UnityEditor.GameView");
        var view = EditorWindow.GetWindow(viewType);
        viewType.GetProperty("selectedSizeIndex",Flags).SetValue(view,index,null);
        var lowResolution = viewType.GetProperty("lowResolutionForAspectRatios",Flags);
        if(lowResolution != null && lowResolution.CanWrite) lowResolution.SetValue(view,false,null);
        view.Repaint();
        Debug.Log("TicTacThrow Game view configured: fixed 1920 x 1080 landscape.");
    }
}
