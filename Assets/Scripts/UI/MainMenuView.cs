using System;
using System.Collections.Generic;
//using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class MainMenuView : View<MainMenuView>
{
    private const string m_BestScorePrefix = "BEST SCORE ";

    public Text m_BestScoreText;
    public Image m_BestScoreBar;
    public GameObject m_BestScoreObject;
    public InputField m_InputField;
    public List<Image> m_ColoredImages;
    public List<Text> m_ColoredTexts;
    //BoosterMode
    public Text m_BoosterLevelText;


    public GameObject m_BrushGroundLight;
    public GameObject m_BrushesPrefab;
    public int m_IdSkin = 0;
    public GameObject m_PointsPerRank;
    public RankingView m_RankingView;

    [Header("Ranks")]
    public string[] m_Ratings;

    private IStatsService m_StatsService;
    [Header("Skin Selection Screen")]
    public Transform m_SelectedBrushPreviewParent;   
    public GameObject m_SkinButtonPrefab;            
    public Transform m_SkinButtonContainer;          

    private GameObject m_CurrentPreviewBrush;
    private int m_SelectedBrushIndex;
    private int m_SelectedColorIndex;

    [Header("Scroll Fade")]
    public RectTransform m_ScrollViewport;     // Viewport do ScrollRect
    public float m_TopFadeStart = 90f;         // Distância do topo onde começa o fade (em pixels)
    public float m_TopFadeEnd = 25f;           // Distância onde some completamente
    private List<GameObject> m_ActiveBrushModels = new List<GameObject>();
    private Dictionary<Renderer, float> m_RendererMinAlpha = new Dictionary<Renderer, float>();

    [Inject]
    public void Construct(IStatsService statsService)
    {
        m_StatsService = statsService;
    }

    protected override void Awake()
    {
        base.Awake();

        m_IdSkin = m_StatsService.FavoriteSkin;
    }
    private void LateUpdate()
    {
        if (GameService.currentPhase != GamePhase.MAIN_MENU ||
        m_ScrollViewport == null ||
        m_ActiveBrushModels.Count == 0)
            return;

        float viewportHeight = m_ScrollViewport.rect.height;

        foreach (GameObject model in m_ActiveBrushModels)
        {
            if (model == null) continue;

            foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
            {
                if (r == null || r.material == null) continue;

                // Calcula a distância do topo usando a posição DE CADA Renderer
                Vector3 screenPos = Camera.main.WorldToScreenPoint(r.transform.position);
                float distanceFromTop = viewportHeight - (screenPos.y - m_ScrollViewport.position.y);

                // Fade individual por objeto
                float alpha = Mathf.InverseLerp(m_TopFadeEnd, m_TopFadeStart, distanceFromTop);
                alpha = Mathf.Clamp01(alpha);
                
                if (alpha < 0.95f)
                {
                    Material mat = r.material;

                    // Muda para modo Fade (transparente)
                    mat.SetFloat("_Mode", 2); // 2 = Fade
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = 3000;
                }
                
                // Aplica alpha no material
                if (r.material.HasProperty("_Color"))
                {
                    Color c = r.material.color;
                    r.material.color = new Color(c.r, c.g, c.b, alpha);
                }
                else if (r.material.HasProperty("_TintColor"))
                {
                    Color c = r.material.GetColor("_TintColor");
                    r.material.SetColor("_TintColor", new Color(c.r, c.g, c.b, alpha));
                }

                // Corrige sobreposição
                r.material.SetInt("_ZWrite", alpha < 0.95f ? 0 : 1);
            }
        }
    }
    public void OnPlayButton()
    {
        if (GameService.currentPhase == GamePhase.MAIN_MENU)
            GameService.ChangePhase(GamePhase.LOADING);
    }

    protected override void OnGamePhaseChanged(GamePhase _GamePhase)
    {
        base.OnGamePhaseChanged(_GamePhase);

        switch (_GamePhase)
        {
            case GamePhase.MAIN_MENU:
                m_BrushGroundLight.SetActive(true);
                Transition(true);
                //Update BoosterMode Level
                if (m_BoosterLevelText != null)
                    m_BoosterLevelText.text = GameService.BoosterLevel.ToString();
                PopulateSkinButtons();
                if (m_BrushesPrefab != null)
                {
                    m_BrushesPrefab.SetActive(true);
                    int favoriteSkin = Mathf.Min(m_StatsService.FavoriteSkin, GameService.m_Skins.Count - 1);

                    var brushMainMenu = m_BrushesPrefab.GetComponent<BrushMainMenu>();
                    if (brushMainMenu != null)
                    {
                        brushMainMenu.Set(GameService.m_Skins[favoriteSkin]);

                        // Força reset de rotação (evita o brush ficar rotacionado)
                        m_BrushesPrefab.transform.localRotation = Quaternion.identity;
                    }
                }
                break;

            case GamePhase.LOADING:
                m_BrushGroundLight.SetActive(false);

                    m_BrushesPrefab.SetActive(false);

                if (m_Visible)
                    Transition(false);
                break;
        }
    }

    public void SetTitleColor(Color _Color)
    {
        m_BrushesPrefab.SetActive(true);
        int favoriteSkin = Mathf.Min(m_StatsService.FavoriteSkin, GameService.m_Skins.Count - 1);
        m_BrushesPrefab.GetComponent<BrushMainMenu>().Set(GameService.m_Skins[favoriteSkin]);
        string playerName = m_StatsService.GetNickname();

        if (playerName != null)
            m_InputField.text = playerName;

        for (int i = 0; i < m_ColoredImages.Count; ++i)
            m_ColoredImages[i].color = _Color;

        for (int i = 0; i < m_ColoredTexts.Count; i++)
            m_ColoredTexts[i].color = _Color;
            
        m_RankingView.gameObject.SetActive(true);
        m_RankingView.RefreshNormal();
    }

    public void OnSetPlayerName(string _Name)
    {
        m_StatsService.SetNickname(_Name);
    }

    public string GetRanking(int _Rank)
    {
        return m_Ratings[_Rank];
    }

    public int GetRankingCount()
    {
        return m_Ratings.Length;
    }

    public void LeftButtonBrush()
    {
        ChangeBrush(m_IdSkin - 1);
    }

    public void RightButtonBrush()
    {
        ChangeBrush(m_IdSkin + 1);
    }

    public void ChangeBrush(int _NewBrush)
    {
        _NewBrush = Mathf.Clamp(_NewBrush, 0, GameService.m_Skins.Count);
        m_IdSkin = _NewBrush;
        if (m_IdSkin >= GameService.m_Skins.Count)
            m_IdSkin = 0;
        GameService.m_PlayerSkinID = m_IdSkin;
        int favoriteSkin = Mathf.Min(m_StatsService.FavoriteSkin, GameService.m_Skins.Count - 1);
        m_BrushesPrefab.GetComponent<BrushMainMenu>().Set(GameService.m_Skins[favoriteSkin]);
        m_StatsService.FavoriteSkin = m_IdSkin;
        GameService.SetColor(GameService.ComputeCurrentPlayerColor(true, 0));
    }
    public void OnBoosterModeButton()
    {
      GameService.StartBoosterMode();
    }


    /// <summary>
    /// Popula o ScrollView com todos os skins (Brush + Cores)
    /// Lógica: Para cada brush, cria botões de todas as suas cores
    /// </summary>
    public void PopulateSkinButtons()
    {
        /*
        // Limpa botões anteriores
        foreach (Transform child in m_SkinButtonContainer)
            Destroy(child.gameObject);

        for (int brushIndex = 0; brushIndex < GameService.m_Skins.Count; brushIndex++)
        {
            SkinData skin = GameService.m_Skins[brushIndex];
            int colorCount = skin.Color.m_Colors.Count;

            for (int colorIndex = 0; colorIndex < colorCount; colorIndex++)
            {
                GameObject buttonGO = Instantiate(m_SkinButtonPrefab, m_SkinButtonContainer);

                // Configura o botão (adicione um componente SkinButton no prefab ou use isso)
                Button btn = buttonGO.GetComponent<Button>();
                int capturedBrush = brushIndex;
                int capturedColor = colorIndex;

                btn.onClick.AddListener(() => SelectSkin(capturedBrush, capturedColor));
            }
        }
        */
        // Limpa botões anteriores
        foreach (Transform child in m_SkinButtonContainer)
            Destroy(child.gameObject);

        for (int brushIndex = 0; brushIndex < GameService.m_Skins.Count; brushIndex++)
        {
            SkinData skin = GameService.m_Skins[brushIndex];
            int colorCount = skin.Color.m_Colors.Count;

            for (int colorIndex = 0; colorIndex < colorCount; colorIndex++)
            {
                GameObject buttonGO = Instantiate(m_SkinButtonPrefab, m_SkinButtonContainer);
                

                // Segurança do Button
                Button btn = buttonGO.GetComponent<Button>();
                if (btn == null)
                {
                    Debug.LogError("[MainMenuView] m_SkinButtonPrefab não tem Button no root!");
                    continue;
                }

                int capturedBrush = brushIndex;
                int capturedColor = colorIndex;
                btn.onClick.AddListener(() => SelectSkin(capturedBrush, capturedColor));

                // =====================================================
                // NOVA LÓGICA: Instancia o modelo 3D dentro do filho "brush"
                // =====================================================
                Transform brushContainer = buttonGO.transform.Find("Brush");
                if (brushContainer == null)
                {
                    Debug.LogWarning("[MainMenuView] Não encontrou o filho 'brush' no botão");
                    continue;
                }

                // Limpa qualquer modelo anterior dentro do container
                for (int i = brushContainer.childCount - 1; i >= 0; i--)
                {
                    Destroy(brushContainer.GetChild(i).gameObject);
                }

                // Instancia o modelo do brush
                GameObject brushModel = Instantiate(skin.Brush.m_Prefab, brushContainer);
                m_ActiveBrushModels.Add(brushModel);//iNSTATIATE

                // Aplica a cor
                Color targetColor = skin.Color.m_Colors[colorIndex];

                Brush brushComp = brushModel.GetComponent<Brush>();
                if (brushComp != null && brushComp.m_Renderers != null)
                {
                    foreach (Renderer r in brushComp.m_Renderers)
                        if (r != null && r.material != null)
                            r.material.color = targetColor;
                }
                else
                {
                    foreach (Renderer r in brushModel.GetComponentsInChildren<Renderer>())
                        if (r != null && r.material != null)
                            r.material.color = targetColor;
                }

                brushModel.transform.localPosition = Vector3.zero;
                brushModel.transform.localRotation = Quaternion.identity;
                brushModel.transform.localScale = new Vector3(80,80,80);
                m_ActiveBrushModels.Add(brushModel);
            }
        }
    }

    /// <summary>
    /// Chamado quando o jogador clica em um skin da lista
    /// </summary>
    public void SelectSkin(int brushIndex, int colorIndex)
    {
        /*
        m_SelectedBrushIndex = brushIndex;
        m_SelectedColorIndex = colorIndex;

        UpdateSelectedBrushPreview(brushIndex, colorIndex);

        // Aqui você pode salvar temporariamente ou já aplicar
        GameService.m_PlayerSkinID = brushIndex;
        // Se quiser salvar direto:
        // m_StatsService.FavoriteSkin = brushIndex;
        */
        m_SelectedBrushIndex = brushIndex;
        m_SelectedColorIndex = colorIndex;
        UpdateSelectedBrushPreview(brushIndex, colorIndex);

        // =====================================================
        // AGORA EQUIPA O SKIN NO JOGADOR (igual ao ChangeBrush)
        // =====================================================
        m_IdSkin = brushIndex;
        GameService.m_PlayerSkinID = brushIndex;
        m_StatsService.FavoriteSkin = brushIndex;

        // Atualiza o brush do título com o skin selecionado
        int clampedIndex = Mathf.Min(brushIndex, GameService.m_Skins.Count - 1);
        if (m_BrushesPrefab != null)
        {
            var brushMainMenu = m_BrushesPrefab.GetComponent<BrushMainMenu>();
            if (brushMainMenu != null)
            {
                brushMainMenu.Set(GameService.m_Skins[clampedIndex]);
                m_BrushesPrefab.transform.localRotation = Quaternion.identity;
            }
        }

        // Aplica a cor no jogador (igual ao ChangeBrush)
        GameService.SetColor(GameService.ComputeCurrentPlayerColor(true, 0));
    }

    /// <summary>
    /// Atualiza o preview 3D do brush selecionado
    /// </summary>
    private void UpdateSelectedBrushPreview(int brushIndex, int colorIndex)
    {
        /*
        SkinData skin = GameService.m_Skins[brushIndex];
        Color targetColor = skin.Color.m_Colors[colorIndex];

        // 1. Deleta o modelo anterior (filho do SelectedBrush)
        if (m_SelectedBrushPreviewParent.childCount > 0)
        {
            for (int i = m_SelectedBrushPreviewParent.childCount - 1; i >= 0; i--)
            {
                Destroy(m_SelectedBrushPreviewParent.GetChild(i).gameObject);
            }
        }

        // 2. Instancia o novo modelo como FILHO do SelectedBrush
        GameObject newModel = Instantiate(skin.Brush.m_Prefab, m_SelectedBrushPreviewParent);
        m_CurrentPreviewBrush = newModel;

        // 3. Aplica a cor correta
        Brush brushComponent = newModel.GetComponent<Brush>();
        if (brushComponent != null && brushComponent.m_Renderers != null)
        {
            foreach (Renderer renderer in brushComponent.m_Renderers)
            {
                if (renderer != null && renderer.material != null)
                {
                    renderer.material.color = targetColor;
                }
            }
        }
        else
        {
            // Fallback caso o prefab não tenha o componente Brush configurado
            Renderer[] renderers = newModel.GetComponentsInChildren<Renderer>();
            foreach (Renderer r in renderers)
            {
                if (r.material != null)
                    r.material.color = targetColor;
            }
        }

        // 4. Garante posicionamento correto
        newModel.transform.localPosition = Vector3.zero;
        newModel.transform.localRotation = Quaternion.identity;
        newModel.transform.localScale = new Vector3(80,80,80);
        */
        SkinData skin = GameService.m_Skins[brushIndex];
        Color targetColor = skin.Color.m_Colors[colorIndex];

        // 1. Deleta o modelo anterior (filho do SelectedBrush)
        if (m_SelectedBrushPreviewParent.childCount > 0)
        {
            for (int i = m_SelectedBrushPreviewParent.childCount - 1; i >= 0; i--)
            {
                Destroy(m_SelectedBrushPreviewParent.GetChild(i).gameObject);
            }
        }

        // 2. Instancia o novo modelo como FILHO do SelectedBrush
        GameObject newModel = Instantiate(skin.Brush.m_Prefab, m_SelectedBrushPreviewParent);
        m_CurrentPreviewBrush = newModel;

        // 3. Aplica a cor (exatamente igual ao PopulateSkinButtons)
        Brush brushComponent = newModel.GetComponent<Brush>();
        if (brushComponent != null && brushComponent.m_Renderers != null)
        {
            foreach (Renderer renderer in brushComponent.m_Renderers)
            {
                if (renderer != null && renderer.material != null)
                {
                    renderer.material.color = targetColor;
                }
            }
        }
        else
        {
            // Fallback caso o prefab não tenha o componente Brush configurado
            Renderer[] renderers = newModel.GetComponentsInChildren<Renderer>();
            foreach (Renderer r in renderers)
            {
                if (r != null && r.material != null)
                    r.material.color = targetColor;
            }
        }

        // 4. Garante posicionamento e rotação corretos
        newModel.transform.localPosition = Vector3.zero;
        newModel.transform.localRotation = Quaternion.identity;
        newModel.transform.localScale = new Vector3(80, 80, 80);
    }


}
