using UnityEngine;

/// <summary>
/// 奶茶杯堆叠：挂在杯子父物件上，以 Inspector 里拖拽的预制件为模板，
/// 自动复制补齐到 layers 层，第 i 层局部位置为 (0, i × spacing, 0)，沿 Y 轴上下堆叠。
/// 编辑器与运行时都会执行；在 Inspector 调整层数/间距后自动重新排列。
/// 注意：调小层数不会自动删除多余杯子，多余的手动删掉即可。
/// </summary>
[ExecuteAlways]
public class CupStack : MonoBehaviour
{
    [Tooltip("杯子预制件（拖拽进来）。为空时回退用第一个子对象当模板。")]
    public GameObject Cup;
    [Tooltip("堆叠层数")]
    public int layers = 5;
    [Tooltip("上下相邻杯子的间距")]
    public float spacing = 0.05f;

    void OnEnable() => Stack();

    void OnValidate()
    {
#if UNITY_EDITOR
        // 编辑器里改参数时下一帧重新排列（OnValidate 里不能直接动层级）
        UnityEditor.EditorApplication.delayCall += Stack;
#endif
    }

    void Stack()
    {
        if (this == null) return;

        // 优先用预制件，没有才回退到第一个子对象
        Transform template;
        if (Cup != null)
            template = Cup.transform;
        else if (transform.childCount > 0)
            template = transform.GetChild(0);
        else
        {
            Debug.LogWarning("[CupStack] 既没拖预制件，也没有子对象当模板，跳过。");
            return;
        }

        if (template == null) return;

        // 不足 layers 层时复制补齐
        while (transform.childCount < layers)
        {
            var clone = Instantiate(template.gameObject, transform);
            clone.name = template.name;
            // 杯架上的杯子都是空杯，清除运行时可能创建的液体子对象
            Transform liq = clone.transform.Find("液体");
            if (liq != null) Destroy(liq.gameObject);
        }

        // 从模板同步 localScale 到所有子对象，确保改 Prefab 后自动生效
        Vector3 templateScale = template.localScale;
        for (int i = 0; i < layers && i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            child.localPosition = new Vector3(0f, i * spacing, 0f);
            child.localScale = templateScale;
            // 确保杯架上的杯子没有液体
            Transform liq = child.Find("液体");
            if (liq != null) Destroy(liq.gameObject);
        }
    }
}
