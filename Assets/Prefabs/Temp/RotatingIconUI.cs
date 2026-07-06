using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controla a exibição de um sprite "flipbook" (turntable pré-renderizado)
/// dentro de um Image de UI. Não usa Update próprio - o avanço de frame
/// é feito externamente pelo manager central (MainMenuView), evitando
/// múltiplos Update() individuais.
/// </summary>
public class RotatingIconUI : MonoBehaviour
{
    private Image m_Image;
    private Sprite[] m_Frames;
    private int m_CurrentFrame;
    private float m_FrameTimer;

    [Tooltip("Frames por segundo do giro. 10-12 já é suficiente pra parecer suave.")]
    public float m_FramesPerSecond = 10f;

    public bool IsVisible { get; private set; } = true;

    public void Init(Sprite[] frames, Color tint)
    {
        if (m_Image == null)
            m_Image = GetComponent<Image>();

        m_Frames = frames;
        m_CurrentFrame = 0;
        m_FrameTimer = 0f;

        if (m_Image != null)
        {
            m_Image.color = tint;
            if (m_Frames != null && m_Frames.Length > 0)
                m_Image.sprite = m_Frames[0];
        }
    }

    public void SetVisible(bool visible)
    {
        IsVisible = visible;
        // Não desativa o GameObject (isso quebraria o Layout/ScrollRect),
        // só evita processar o avanço de frame quando fora da viewport.
    }

    /// <summary>
    /// Chamado pelo manager central, uma vez por "tick" de animação.
    /// deltaTime já vem controlado externamente (throttle).
    /// </summary>
    public void Tick(float deltaTime)
    {
        if (!IsVisible || m_Frames == null || m_Frames.Length <= 1)
            return;

        m_FrameTimer += deltaTime;
        float frameDuration = 1f / m_FramesPerSecond;

        if (m_FrameTimer >= frameDuration)
        {
            m_FrameTimer -= frameDuration;
            m_CurrentFrame = (m_CurrentFrame + 1) % m_Frames.Length;
            m_Image.sprite = m_Frames[m_CurrentFrame];
        }
    }
}
