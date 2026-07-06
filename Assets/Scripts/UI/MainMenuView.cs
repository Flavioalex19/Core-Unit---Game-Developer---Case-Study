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
    private List<RotatingIconUI> m_ActiveRotatingIcons = new List<RotatingIconUI>();
    private List<RectTransform> m_ActiveIconRects = new List<RectTransform>(); // paralelo, pra checar visibilidade

    [Header("Rotating Icons Throttle")]
    public float m_IconTickInterval = 0.05f; // ~20x/seg checagem de visibilidade, controla o "tick" de todos
    private float m_IconTickTimer;

    [Header("Rotating Icons - Resources Path")]
    public string m_BrushIconsResourcesFolder = "BrushIcons"; // Assets/Resources/BrushIcons/{NomeDoBrush}/

    private Dictionary<string, Sprite[]> m_IconFramesCache = new Dictionary<string, Sprite[]>();

    /// <summary>
    /// Carrega (com cache) os frames do turntable de um brush a partir de
    /// Resources/BrushIcons/{NomeDoPrefab}/frame_XX.png
    /// </summary>
    private Sprite[] GetIconFramesForBrush(SkinData skin)
    {
        string brushKey = skin.Brush.m_Prefab.name; // usa o nome do prefab como chave

        if (m_IconFramesCache.TryGetValue(brushKey, out Sprite[] cachedFrames))
            return cachedFrames;

        string path = m_BrushIconsResourcesFolder + "/" + brushKey;
        Sprite[] frames = Resources.LoadAll<Sprite>(path);

        if (frames == null || frames.Length == 0)
        {
            Debug.LogWarning($"[MainMenuView] Nenhum frame encontrado em Resources/{path}");
            frames = new Sprite[0];
        }
        else
        {
            // Resources.LoadAll não garante ordem numérica correta em todas as plataformas,
            // então ordenamos pelo nome do arquivo (frame_00, frame_01, ...)
            System.Array.Sort(frames, (a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
        }

        m_IconFramesCache[brushKey] = frames;
        return frames;
    }

    //DebugBtns
    public GameObject m_BoosterModeBTN;
    public GameObject m_NewSelectionBtn;
    public GameObject m_OldSelectionBtn;

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
        /*
        if (GameService.currentPhase != GamePhase.MAIN_MENU ||
        m_ScrollViewport == null ||
        m_ActiveBrushModels.Count == 0)
            return;

        Rect viewportRect = m_ScrollViewport.rect;
        Vector3 viewportWorldPos = m_ScrollViewport.position;

        foreach (GameObject model in m_ActiveBrushModels)
        {
            if (model == null) continue;

            foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
            {
                if (r == null) continue;

                // Pega a posição do renderer no espaço da tela
                Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(null, r.transform.position);

                // Verifica se está dentro da área visível do ScrollView
                bool isVisible = screenPos.y > viewportWorldPos.y - 50 &&
                                 screenPos.y < viewportWorldPos.y + viewportRect.height + 50;

                // Ativa ou desativa o renderer (muito mais estável que mudar material)
                r.enabled = isVisible;
            }
        }
        */
        if (GameService.currentPhase != GamePhase.MAIN_MENU ||
        m_ScrollViewport == null ||
        m_ActiveRotatingIcons.Count == 0)
            return;

        // Throttle: não precisa checar visibilidade nem avançar frame todo Update
        m_IconTickTimer += Time.deltaTime;
        if (m_IconTickTimer < m_IconTickInterval)
            return;

        float elapsed = m_IconTickTimer;
        m_IconTickTimer = 0f;

        Vector3 viewportScreenPos = RectTransformUtility.WorldToScreenPoint(null, m_ScrollViewport.position);
        Rect viewportRect = m_ScrollViewport.rect;

        float topLimit = viewportScreenPos.y + viewportRect.height;
        float bottomLimit = viewportScreenPos.y;
        float margin = 60f;

        for (int i = 0; i < m_ActiveRotatingIcons.Count; i++)
        {
            RotatingIconUI icon = m_ActiveRotatingIcons[i];
            RectTransform rect = m_ActiveIconRects[i];
            if (icon == null || rect == null) continue;

            Vector3 iconScreenPos = RectTransformUtility.WorldToScreenPoint(null, rect.position);
            bool isVisible = iconScreenPos.y > (bottomLimit - margin) &&
                              iconScreenPos.y < (topLimit + margin);

            icon.SetVisible(isVisible);
            icon.Tick(elapsed); // só avança frame de verdade se IsVisible == true (checado dentro do Tick)
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
                int currentSkin = Mathf.Min(m_StatsService.FavoriteSkin, GameService.m_Skins.Count - 1);

                m_SelectedBrushIndex = currentSkin;
                m_SelectedColorIndex = Mathf.Clamp(m_SelectedColorIndex, 0, GameService.m_Skins[currentSkin].Color.m_Colors.Count - 1);

                UpdateSelectedBrushPreview(currentSkin, m_SelectedColorIndex);
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
        */
        // Limpa
        foreach (Transform child in m_SkinButtonContainer)
            Destroy(child.gameObject);

        m_ActiveRotatingIcons.Clear();
        m_ActiveIconRects.Clear();

        for (int brushIndex = 0; brushIndex < GameService.m_Skins.Count; brushIndex++)
        {
            SkinData skin = GameService.m_Skins[brushIndex];
            int colorCount = skin.Color.m_Colors.Count;

            for (int colorIndex = 0; colorIndex < colorCount; colorIndex++)
            {
                GameObject buttonGO = Instantiate(m_SkinButtonPrefab, m_SkinButtonContainer);

                Button btn = buttonGO.GetComponent<Button>();
                if (btn == null) continue;

                int capturedBrush = brushIndex;
                int capturedColor = colorIndex;
                btn.onClick.AddListener(() => SelectSkin(capturedBrush, capturedColor));

                // === Visual 2D com rotação (sprite sheet) ===
                Transform visualChild = buttonGO.transform.Find("Visual");
                if (visualChild != null)
                {
                    Image img = visualChild.GetComponent<Image>();
                    RotatingIconUI rotatingIcon = visualChild.GetComponent<RotatingIconUI>();
                    if (rotatingIcon == null)
                        rotatingIcon = visualChild.gameObject.AddComponent<RotatingIconUI>();

                    if (img != null)
                    {
                        Color targetColor = skin.Color.m_Colors[colorIndex];
                        Sprite[] frames = GetIconFramesForBrush(skin);

                        rotatingIcon.Init(frames, targetColor);

                        m_ActiveRotatingIcons.Add(rotatingIcon);
                        m_ActiveIconRects.Add(visualChild as RectTransform);
                    }
                }
            }
        }
    }
    /// <summary>
    /// Chamado quando o jogador clica em um skin da lista
    /// </summary>
    public void SelectSkin(int brushIndex, int colorIndex)
    {
        
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
        newModel.transform.localScale = new Vector3(100 , 100, 100);
    }
    
    //DebugBtns Activate/Deactivate and Check
    public void ActivateDeactivateBoosterMode()
    {
        GameService.Debug_EnableBoosterMode = !GameService.Debug_EnableBoosterMode;
        CheckBoosterModeLevelAvaliable();
    }
    public void ActivateDeactivateNewSelection()
    {
        GameService.Debug_EnableNewSelectionBrush = !GameService.Debug_EnableNewSelectionBrush;
        CheckNewSelectionBrushAvaliable();
    }
    public void CheckBoosterModeLevelAvaliable()
    {
        if (GameService.Debug_EnableBoosterMode) m_BoosterModeBTN.SetActive(true);
        else m_BoosterModeBTN.SetActive(false);
    }
    public void CheckNewSelectionBrushAvaliable()
    {
        if (GameService.Debug_EnableNewSelectionBrush)
        {
            m_NewSelectionBtn.SetActive(true);
            m_OldSelectionBtn.SetActive(false);
        }
        else
        {
            m_NewSelectionBtn.SetActive(false);
            m_OldSelectionBtn.SetActive(true);
        }
    }
}
