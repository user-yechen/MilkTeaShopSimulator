using UnityEngine;

/// <summary>
/// 流体供给物（挂在牛奶等可注入奶茶机的物品预制件上）：
/// 只负责记录"一单位该物品能提供多少 ml 哪种流体"；
/// 奶茶机（TeaMachine）仅作接收方读取这两个字段，不存供给物信息。
/// </summary>
public class FluidProvider : MonoBehaviour
{
    [Tooltip("对应奶茶机流体格子的名称（如 牛奶），必须和 FluidCellButton.fluidName 一致")]
    public string fluidName = "牛奶";
    [Tooltip("一单位提供多少 ml（当前设定：1 单位牛奶 = 1000ml）")]
    public float mlPerUnit = 1000f;
}
