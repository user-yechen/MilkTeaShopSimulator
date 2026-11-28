using UnityEngine;

/// <summary>
/// 台面展示杯的订单信息：TryPlaceCup 会销毁源杯子的 Cup 组件（只保留外观），
/// 这里把杯型 / 液体抄一份，供顾客取餐匹配用。
/// </summary>
public class CounterCupInfo : MonoBehaviour
{
    public Cup.CupType cupType = Cup.CupType.Medium;
    public string teaType = "奶茶";
}
