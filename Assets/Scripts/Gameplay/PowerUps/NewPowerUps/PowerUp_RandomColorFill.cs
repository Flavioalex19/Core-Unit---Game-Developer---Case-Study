using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class PowerUp_RandomColorFill : PowerUp
{
    public float m_Radius = 7.5f;
    public float m_FillDuration = 0.35f;
    public AnimationCurve m_FillCurve;

    private ITerrainService m_TerrainService;

    [Inject]
    public void ChildConstruct(ITerrainService terrainService)
    {
        m_TerrainService = terrainService;
    }

    public override void OnPlayerTouched(Player _Player)
    {
        base.OnPlayerTouched(_Player);

        Vector3 randomPos = GetRandomMapPosition();
        float radiusMult = Mathf.Clamp(_Player.GetSize() / _Player.GetMinSize(), 1f, 2.5f);

        m_TerrainService.FillCircle(
            _Player,
            randomPos,
            m_Radius * radiusMult,
            m_FillDuration,
            SelfDestroy
        );
    }

    private Vector3 GetRandomMapPosition()
    {
        float pad = 18f;
        float halfW = m_TerrainService.WorldHalfWidth;
        float halfH = m_TerrainService.WorldHalfHeight;

        return new Vector3(
            Random.Range(-halfW + pad, halfW - pad),
            0f,
            Random.Range(-halfH + pad, halfH - pad)
        );
    }

    private void SelfDestroy()
    {
        Destroy(gameObject);
    }
}
