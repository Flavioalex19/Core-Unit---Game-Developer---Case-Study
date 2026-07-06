using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using UnityEditor.SceneManagement;

/// <summary>
/// Percorre Resources/Skins (mesma fonte que o GameService usa em runtime),
/// para cada SkinData: instancia o Brush correspondente, aplica a cor do skin,
/// gira e captura N frames, salvando em Resources/BrushIcons/{skin.name}/frame_XX.png
/// </summary>
public class SkinIconCapture : EditorWindow
{
    private int m_FrameCount = 24;
    private int m_TextureSize = 256;
    private float m_CameraDistance = 3f;
    private float m_CameraHeight = 0.5f;
    private string m_OutputFolder = "Assets/Resources/BrushIcons";
    private string m_SkinsResourcesPath = "Skins"; // Resources/Skins, igual GameService.Init()

    [MenuItem("Tools/Skin Icon Capture")]
    public static void ShowWindow()
    {
        GetWindow<SkinIconCapture>("Skin Icon Capture");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Configurações", EditorStyles.boldLabel);
        m_FrameCount = EditorGUILayout.IntSlider("Frame Count", m_FrameCount, 8, 64);
        m_TextureSize = EditorGUILayout.IntPopup("Texture Size", m_TextureSize,
            new[] { "128", "256", "512" }, new[] { 128, 256, 512 });
        m_CameraDistance = EditorGUILayout.FloatField("Distância da Câmera", m_CameraDistance);
        m_CameraHeight = EditorGUILayout.FloatField("Altura da Câmera", m_CameraHeight);
        m_OutputFolder = EditorGUILayout.TextField("Pasta de Saída", m_OutputFolder);
        m_SkinsResourcesPath = EditorGUILayout.TextField("Resources Path (Skins)", m_SkinsResourcesPath);

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Vai carregar TODOS os SkinData de Resources/" + m_SkinsResourcesPath +
            " (a mesma lista que o GameService usa em runtime) e capturar cada um " +
            "com o modelo + cor corretos.", MessageType.Info);
        EditorGUILayout.Space();

        if (GUILayout.Button("Capturar Todos os Skins", GUILayout.Height(35)))
        {
            CaptureAllSkins();
        }
    }

    private void CaptureAllSkins()
    {
        // Mesma fonte de dados que o GameService.Init() usa
        List<SkinData> skins = new List<SkinData>(Resources.LoadAll<SkinData>(m_SkinsResourcesPath));

        if (skins.Count == 0)
        {
            Debug.LogWarning($"[SkinIconCapture] Nenhum SkinData encontrado em Resources/{m_SkinsResourcesPath}");
            return;
        }

        for (int i = 0; i < skins.Count; i++)
        {
            SkinData skin = skins[i];
            if (skin == null || skin.Brush == null || skin.Brush.m_Prefab == null)
            {
                Debug.LogWarning($"[SkinIconCapture] Skin inválido no índice {i}, pulando.");
                continue;
            }

            EditorUtility.DisplayProgressBar("Capturando Skins",
                $"Processando {skin.name} ({i + 1}/{skins.Count})",
                (float)i / skins.Count);

            CaptureSkin(skin);
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.Refresh();
        FixImportSettings();

        Debug.Log("[SkinIconCapture] Captura concluída para todos os skins.");
    }

    private void CaptureSkin(SkinData skin)
    {
        // Pasta identificada pelo NOME DO SKIN (bate com o que aparece no seu screenshot: Brush01, Brush02...)
        string skinFolder = Path.Combine(m_OutputFolder, skin.name);
        Directory.CreateDirectory(skinFolder);

        Scene previewScene = EditorSceneManager.NewPreviewScene();

        // === 1. Instancia o MODELO do brush deste skin ===
        GameObject instance = Instantiate(skin.Brush.m_Prefab);
        SceneManager.MoveGameObjectToScene(instance, previewScene);
        instance.transform.position = Vector3.zero;
        instance.transform.rotation = Quaternion.identity;

        // === 2. Aplica a COR do skin (mesma lógica do UpdateSelectedBrushPreview) ===
        ApplySkinColor(instance, skin);

        // === 3. Luz ===
        GameObject lightGO = new GameObject("CaptureLight");
        SceneManager.MoveGameObjectToScene(lightGO, previewScene);
        Light light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        lightGO.transform.rotation = Quaternion.Euler(50, -30, 0);

        // === 4. Câmera ===
        GameObject camGO = new GameObject("CaptureCamera");
        SceneManager.MoveGameObjectToScene(camGO, previewScene);
        Camera cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0, 0, 0, 0); // transparente
        cam.fieldOfView = 30f;
        camGO.transform.position = new Vector3(0, m_CameraHeight, -m_CameraDistance);
        camGO.transform.LookAt(new Vector3(0, m_CameraHeight * 0.5f, 0));

        RenderTexture rt = new RenderTexture(m_TextureSize, m_TextureSize, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;

        // === 5. Gira e captura N frames ===
        float stepAngle = 360f / m_FrameCount;

        for (int frame = 0; frame < m_FrameCount; frame++)
        {
            instance.transform.rotation = Quaternion.Euler(0, stepAngle * frame, 0);

            cam.Render();

            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(m_TextureSize, m_TextureSize, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, m_TextureSize, m_TextureSize), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            byte[] png = tex.EncodeToPNG();
            string fileName = $"frame_{frame:00}.png";
            File.WriteAllBytes(Path.Combine(skinFolder, fileName), png);

            DestroyImmediate(tex);
        }

        // === 6. Limpeza — passa pro próximo skin ===
        cam.targetTexture = null;
        rt.Release();
        DestroyImmediate(rt);
        EditorSceneManager.ClosePreviewScene(previewScene);
    }

    /// <summary>
    /// Mesma lógica de aplicação de cor usada em UpdateSelectedBrushPreview/PopulateSkinButtons
    /// no MainMenuView, pra garantir consistência visual entre o preview real e o ícone gerado.
    /// </summary>
    private void ApplySkinColor(GameObject brushInstance, SkinData skin)
    {
        if (skin.Color == null || skin.Color.m_Colors == null || skin.Color.m_Colors.Count == 0)
            return;

        Color targetColor = skin.Color.m_Colors[0];

        Brush brushComp = brushInstance.GetComponent<Brush>();
        if (brushComp != null && brushComp.m_Renderers != null)
        {
            foreach (Renderer r in brushComp.m_Renderers)
                if (r != null && r.sharedMaterial != null)
                    r.material.color = targetColor; // .material cria instância, não afeta o asset original
        }
        else
        {
            foreach (Renderer r in brushInstance.GetComponentsInChildren<Renderer>())
                if (r != null && r.sharedMaterial != null)
                    r.material.color = targetColor;
        }
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
