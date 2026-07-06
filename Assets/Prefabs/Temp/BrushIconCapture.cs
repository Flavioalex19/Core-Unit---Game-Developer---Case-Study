using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

/// <summary>
/// Ferramenta de Editor para gerar sprite sheets (turntable) dos prefabs de brush.
/// Renderiza cada prefab girando em N passos e salva os frames em
/// Assets/Resources/BrushIcons/{NomeDoPrefab}/frame_XX.png
/// </summary>
public class BrushIconCapture : EditorWindow
{
    private List<GameObject> m_BrushPrefabs = new List<GameObject>();
    private int m_FrameCount = 24;
    private int m_TextureSize = 256;
    private float m_CameraDistance = 3f;
    private float m_CameraHeight = 0.5f;
    private float m_ObjectYRotationOffset = 0f;
    private string m_OutputFolder = "Assets/Resources/BrushIcons";

    private SerializedObject m_SerializedWindow;
    private SerializedProperty m_PrefabsProperty;

    [MenuItem("Tools/Brush Icon Capture")]
    public static void ShowWindow()
    {
        GetWindow<BrushIconCapture>("Brush Icon Capture");
    }

    private void OnEnable()
    {
        m_SerializedWindow = new SerializedObject(this);
        m_PrefabsProperty = m_SerializedWindow.FindProperty("m_BrushPrefabs");
    }

    private void OnGUI()
    {
        m_SerializedWindow.Update();

        EditorGUILayout.LabelField("Prefabs de Brush", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(m_PrefabsProperty, true);
        m_SerializedWindow.ApplyModifiedProperties();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Configurações", EditorStyles.boldLabel);
        m_FrameCount = EditorGUILayout.IntSlider("Frame Count", m_FrameCount, 8, 64);
        m_TextureSize = EditorGUILayout.IntPopup("Texture Size", m_TextureSize,
            new[] { "128", "256", "512" }, new[] { 128, 256, 512 });
        m_CameraDistance = EditorGUILayout.FloatField("Distância da Câmera", m_CameraDistance);
        m_CameraHeight = EditorGUILayout.FloatField("Altura da Câmera", m_CameraHeight);
        m_ObjectYRotationOffset = EditorGUILayout.FloatField("Offset Rotação Inicial (Y)", m_ObjectYRotationOffset);
        m_OutputFolder = EditorGUILayout.TextField("Pasta de Saída", m_OutputFolder);

        EditorGUILayout.Space();

        if (GUILayout.Button("Capturar Todos os Brushes", GUILayout.Height(35)))
        {
            CaptureAll();
        }
    }

    private void CaptureAll()
    {
        if (m_BrushPrefabs.Count == 0)
        {
            Debug.LogWarning("[BrushIconCapture] Nenhum prefab adicionado.");
            return;
        }

        for (int i = 0; i < m_BrushPrefabs.Count; i++)
        {
            GameObject prefab = m_BrushPrefabs[i];
            if (prefab == null) continue;

            EditorUtility.DisplayProgressBar("Capturando Brushes",
                $"Processando {prefab.name} ({i + 1}/{m_BrushPrefabs.Count})",
                (float)i / m_BrushPrefabs.Count);

            CaptureBrush(prefab);
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.Refresh();

        // Ajusta as configurações de import (Sprite + alpha) de tudo que foi gerado
        FixImportSettings();

        Debug.Log("[BrushIconCapture] Captura concluída.");
    }

    private void CaptureBrush(GameObject prefab)
    {
        string brushFolder = Path.Combine(m_OutputFolder, prefab.name);
        Directory.CreateDirectory(brushFolder);

        // Cena isolada, não afeta a cena atualmente aberta
        Scene previewScene = EditorSceneManager.NewPreviewScene();

        GameObject instance = Instantiate(prefab);
        SceneManager.MoveGameObjectToScene(instance, previewScene);
        instance.transform.position = Vector3.zero;
        instance.transform.rotation = Quaternion.Euler(0, m_ObjectYRotationOffset, 0);

        // Luz simples
        GameObject lightGO = new GameObject("CaptureLight");
        SceneManager.MoveGameObjectToScene(lightGO, previewScene);
        Light light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        lightGO.transform.rotation = Quaternion.Euler(50, -30, 0);

        // Câmera
        GameObject camGO = new GameObject("CaptureCamera");
        SceneManager.MoveGameObjectToScene(camGO, previewScene);
        Camera cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0, 0, 0, 0); // transparente
        cam.orthographic = false;
        cam.fieldOfView = 30f;
        camGO.transform.position = new Vector3(0, m_CameraHeight, -m_CameraDistance);
        camGO.transform.LookAt(new Vector3(0, m_CameraHeight * 0.5f, 0));

        RenderTexture rt = new RenderTexture(m_TextureSize, m_TextureSize, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;

        float stepAngle = 360f / m_FrameCount;

        for (int frame = 0; frame < m_FrameCount; frame++)
        {
            instance.transform.rotation = Quaternion.Euler(0, m_ObjectYRotationOffset + stepAngle * frame, 0);

            cam.Render();

            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(m_TextureSize, m_TextureSize, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, m_TextureSize, m_TextureSize), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            byte[] png = tex.EncodeToPNG();
            string fileName = $"frame_{frame:00}.png";
            File.WriteAllBytes(Path.Combine(brushFolder, fileName), png);

            DestroyImmediate(tex);
        }

        cam.targetTexture = null;
        rt.Release();
        DestroyImmediate(rt);

        EditorSceneManager.ClosePreviewScene(previewScene);
    }

    private void FixImportSettings()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { m_OutputFolder });

        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) continue;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;

            TextureImporterPlatformSettings platformSettings = importer.GetDefaultPlatformTextureSettings();
            platformSettings.maxTextureSize = m_TextureSize;
            platformSettings.format = TextureImporterFormat.RGBA32;
            importer.SetPlatformTextureSettings(platformSettings);

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }
    }
}
