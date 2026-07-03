using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class BoosterChoiceView : View<BoosterChoiceView>
{
    public GameObject m_ContinueButton;     
    [Header("PowerUpInfo")]
    public List<Button> m_PowerUpButtons;
    public List<Text> m_PowerUpTexts;
    [Header("Selected PowerUps")]
    public Button m_SelectedButtonPrefab;
    public Transform m_SelectedButtonsContainer;

    private const int MAX_SELECTED_POWERUPS = 3;

    private IGameService m_GameService;

    [Inject]
    public void Construct(IGameService gameService)
    {
        m_GameService = gameService;
    }

    protected override void OnGamePhaseChanged(GamePhase _GamePhase)
    {
        base.OnGamePhaseChanged(_GamePhase);

        if (_GamePhase == GamePhase.BOOSTER_CHOICE)
        {
            Transition(true);
            m_ContinueButton.SetActive(true);

            // Clear selected booster - protection
            GameService gameService = (GameService)m_GameService;
            gameService.m_SelectedBoosterPowerUps.Clear();

            // protection
            foreach (Transform child in m_SelectedButtonsContainer)
            {
                Destroy(child.gameObject);
            }

            PopulatePowerUpButtons();
        }
        else
        {
            if (m_Visible)
                Transition(false);
        }
    }
    private void PopulatePowerUpButtons()
    {
        List<PowerUpData> powerUpList = ((GameService)m_GameService).GetPowerUpList();

        for (int i = 0; i < m_PowerUpButtons.Count; i++)
        {
            if (i >= powerUpList.Count) break;

            int index = i;
            PowerUpData powerUp = powerUpList[i];

            m_PowerUpButtons[i].onClick.RemoveAllListeners();
            m_PowerUpButtons[i].onClick.AddListener(() => OnPowerUpSelected(index, powerUp));
        }

        for (int i = 0; i < m_PowerUpTexts.Count; i++)
        {
            if (i >= powerUpList.Count) break;

            m_PowerUpTexts[i].text = powerUpList[i].name;
        }
    }

    private void OnPowerUpSelected(int index, PowerUpData powerUp)
    {
        GameService gameService = (GameService)m_GameService;

        if (gameService.m_SelectedBoosterPowerUps.Count >= MAX_SELECTED_POWERUPS)
            return;

        if (gameService.m_SelectedBoosterPowerUps.Contains(powerUp))
            return;

        gameService.m_SelectedBoosterPowerUps.Add(powerUp);
        CreateSelectedPowerUpButton(powerUp);
    }
    private void CreateSelectedPowerUpButton(PowerUpData powerUp)
    {
        if (m_SelectedButtonPrefab == null || m_SelectedButtonsContainer == null)
            return;

        Button newButton = Instantiate(m_SelectedButtonPrefab, m_SelectedButtonsContainer);

        Text buttonText = newButton.GetComponentInChildren<Text>();
        if (buttonText != null)
        {
            buttonText.text = powerUp.name;
        }

        // Add listener to remove
        newButton.onClick.AddListener(() =>
        {
            GameService gameService = (GameService)m_GameService;
            gameService.m_SelectedBoosterPowerUps.Remove(powerUp);
            Destroy(newButton.gameObject);
        });
    }

    public void OnContinueButton()
    {
        if (m_GameService.currentPhase == GamePhase.BOOSTER_CHOICE)
        {
            m_GameService.ChangePhase(GamePhase.GAME);
        }
    }

   
    
}
