using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PowerUp_SpeedBoost : PowerUp
{

    public float m_speedMultiplier = 1.6f;

    public override void OnPlayerTouched(Player _Player)
    {
        _Player.ApplyTemporarySpeedBoost(m_Duration, m_speedMultiplier);
        SelfDestroy();
    }
    private void SelfDestroy()
    {
        Destroy(gameObject);
    }
}
