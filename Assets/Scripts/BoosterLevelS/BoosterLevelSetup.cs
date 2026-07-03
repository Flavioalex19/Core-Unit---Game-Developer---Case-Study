using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BoosterLevelSetup", menuName = "Booster/Level Setup")]
public class BoosterLevelSetup : ScriptableObject
{
    [Header("Power-ups Avaliable")]
    public List<PowerUpData> powerUps;
}
