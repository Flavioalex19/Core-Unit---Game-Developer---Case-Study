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
    private List<RectTransform> m_ActiveIconRects = new List<RectTransform>(); // paralelo, pra checar visibilidade

    private List<GameObject> m_ActiveBrushModels = new List<GameObject>();


    [Header("Fade & Scale")]
    public float m_FadeStartDistance = 80f;   // distance to fade
    public float m_FadeEndDistance = 20f;

    //DebugBtns
    public GameObject m_BoosterModeBTN;
    public GameObject m_NewSelectionBtn;
    public GameObject m_OldSelectionBtn;
    public GameObject m_oldPlayBtn;

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

        Vector3 viewportScreenPos = RectTransformUtility.WorldToScreenPoint(null, m_ScrollViewport.position);
        Rect viewportRect = m_ScrollViewport.rect;

        float topEdge = viewportScreenPos.y + viewportRect.height;
        float bottomEdge = viewportScreenPos.y;

        foreach (GameObject model in m_ActiveBrushModels)
        {
            if (model == null) continue;

            Vector3 modelScreenPos = RectTransformUtility.WorldToScreenPoint(null, model.transform.position);

            float distanceToTop = topEdge - modelScreenPos.y;
            float distanceToBottom = modelScreenPos.y - bottomEdge;
            float distanceToEdge = Mathf.Min(distanceToTop, distanceToBottom);

            float visibility = Mathf.InverseLerp(m_FadeEndDistance, m_FadeStartDistance, distanceToEdge);
            visibility = Mathf.Clamp01(visibility);

            // Scale
            float targetScale = 80f * visibility;
            model.transform.localScale = new Vector3(targetScale, targetScale, targetScale);

            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            foreach (Renderer rend in renderers)
            {
                if (rend != null && rend.material != null)
                {
                    Color currentColor;

                    if (rend.material.HasProperty("_TintColor"))
                        currentColor = rend.material.GetColor("_TintColor");
                    else if (rend.material.HasProperty("_Color"))
                        currentColor = rend.material.GetColor("_Color");
                    else
                        continue; 


                    currentColor.a = visibility;

                    if (rend.material.HasProperty("_TintColor"))
                        rend.material.SetColor("_TintColor", currentColor);
                    else if (rend.material.HasProperty("_Color"))
                        rend.material.SetColor("_Color", currentColor);
                }
            }
            // ============================================================
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

    public void PopulateSkinButtons()
    {
        /*
        foreach (Transform child in m_SkinButtonContainer)
            Destroy(child.gameObject);

        m_ActiveBrushModels.Clear();

        List<SkinData> allSkins = GameService.m_Skins;

        for (int brushIndex = 0; brushIndex < allSkins.Count; brushIndex++)
        {
            SkinData skin = allSkins[brushIndex];
            int colorCount = skin.Color.m_Colors.Count;

            for (int colorIndex = 0; colorIndex < colorCount; colorIndex++)
            {
                GameObject buttonGO = Instantiate(m_SkinButtonPrefab, m_SkinButtonContainer);
                Button btn = buttonGO.GetComponent<Button>();
                if (btn == null) continue;

                int capturedBrush = brushIndex;
                int capturedColor = colorIndex;
                btn.onClick.AddListener(() => SelectSkin(capturedBrush, capturedColor));

                Transform brushContainer = buttonGO.transform.Find("Brush");
                if (brushContainer == null)
                {
                    Debug.LogWarning("[MainMenuView] Não encontrou o filho 'Brush' no botão prefab!");
                    continue;
                }

                for (int i = brushContainer.childCount - 1; i >= 0; i--)
                {
                    Destroy(brushContainer.GetChild(i).gameObject);
                }

                GameObject brushModel = Instantiate(skin.Brush.m_Prefab, brushContainer);
                m_ActiveBrushModels.Add(brushModel);

                Color targetColor = skin.Color.m_Colors[colorIndex];

                Renderer[] renderers = brushModel.GetComponentsInChildren<Renderer>();
                foreach (Renderer rend in renderers)
                {
                    if (rend != null && rend.material != null)
                    {
                        if (rend.material.HasProperty("_TintColor"))
                            rend.material.SetColor("_TintColor", targetColor);
                        else if (rend.material.HasProperty("_Color"))
                            rend.material.SetColor("_Color", targetColor);
                    }
                }

                brushModel.transform.localPosition = Vector3.zero;
                brushModel.transform.localRotation = Quaternion.identity;
                brushModel.transform.localScale = new Vector3(80, 80, 80);
            }
        }
        */
        foreach (Transform child in m_SkinButtonContainer)
            Destroy(child.gameObject);

        m_ActiveBrushModels.Clear();

        List<SkinData> allSkins = GameService.m_Skins;

        for (int brushIndex = 0; brushIndex < allSkins.Count; brushIndex++)
        {
            SkinData skin = allSkins[brushIndex];
            int colorCount = skin.Color.m_Colors.Count;

            for (int colorIndex = 0; colorIndex < colorCount; colorIndex++)
            {
                GameObject buttonGO = Instantiate(m_SkinButtonPrefab, m_SkinButtonContainer);
                Button btn = buttonGO.GetComponent<Button>();
                if (btn == null) continue;

                int capturedBrush = brushIndex;
                int capturedColor = colorIndex;
                btn.onClick.AddListener(() => SelectSkin(capturedBrush, capturedColor));

                Transform brushContainer = buttonGO.transform.Find("Brush");
                if (brushContainer == null)
                {
                    Debug.LogWarning("[MainMenuView] Não encontrou o filho 'Brush' no botão prefab!");
                    continue;
                }

                for (int i = brushContainer.childCount - 1; i >= 0; i--)
                {
                    Destroy(brushContainer.GetChild(i).gameObject);
                }

                GameObject brushModel = Instantiate(skin.Brush.m_Prefab, brushContainer);
                m_ActiveBrushModels.Add(brushModel);

                Color targetColor = skin.Color.m_Colors[colorIndex];

                Brush brushComponent = brushModel.GetComponent<Brush>();
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
                    Renderer[] renderers = brushModel.GetComponentsInChildren<Renderer>();
                    foreach (Renderer r in renderers)
                    {
                        if (r != null && r.material != null)
                            r.material.color = targetColor;
                    }
                }
                // =====================================================

                brushModel.transform.localPosition = Vector3.zero;
                brushModel.transform.localRotation = Quaternion.identity;
                brushModel.transform.localScale = new Vector3(80, 80, 80);
            }
        }
    }

    //WHen the player click on the button- change the skin and make the player brush and color
    public void SelectSkin(int brushIndex, int colorIndex)
    {
        
        m_SelectedBrushIndex = brushIndex;
        m_SelectedColorIndex = colorIndex;
        UpdateSelectedBrushPreview(brushIndex, colorIndex);

        m_IdSkin = brushIndex;
        GameService.m_PlayerSkinID = brushIndex;
        m_StatsService.FavoriteSkin = brushIndex;

        // Update brush
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

        // Apply color
        GameService.SetColor(GameService.ComputeCurrentPlayerColor(true, 0));
    }

    //Update selected brush
    private void UpdateSelectedBrushPreview(int brushIndex, int colorIndex)
    {
      
        SkinData skin = GameService.m_Skins[brushIndex];
        Color targetColor = skin.Color.m_Colors[colorIndex];

        if (m_SelectedBrushPreviewParent.childCount > 0)
        {
            for (int i = m_SelectedBrushPreviewParent.childCount - 1; i >= 0; i--)
            {
                Destroy(m_SelectedBrushPreviewParent.GetChild(i).gameObject);
            }
        }

        GameObject newModel = Instantiate(skin.Brush.m_Prefab, m_SelectedBrushPreviewParent);
        m_CurrentPreviewBrush = newModel;

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
            Renderer[] renderers = newModel.GetComponentsInChildren<Renderer>();
            foreach (Renderer r in renderers)
            {
                if (r != null && r.material != null)
                    r.material.color = targetColor;
            }
        }

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
        if (GameService.Debug_EnableBoosterMode)
        {
            m_BoosterModeBTN.SetActive(true);
            m_oldPlayBtn.SetActive(false);
        }
        else 
        {
            m_BoosterModeBTN.SetActive(false);
            m_oldPlayBtn.SetActive(true);
        } 
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
