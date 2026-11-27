using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// 摆放 / 场景吸附 自检：在空场景里生成地板、墙、Socket 与可摆放物，
/// 断言贴合关系。命令行：-executeMethod PlacementSelfTest.Run
/// </summary>
public static class PlacementSelfTest
{
    static int failures;

    public static void Run()
    {
        failures = 0;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 1) 地板：上表面 = FloorTopY
        GameObject floor = SceneSetupTool.CreateFloorPiece("地板", 10f, 10f);
        Bounds fb = PlacementService.GetBounds(floor);
        Check("地板上表面高度", fb.max.y, SceneSetupTool.FloorTopY, 0.01f);

        // 2) 墙：内侧面贴地板边、底部下沉 1cm、顶边 = 底 + 高
        GameObject wall = SceneSetupTool.CreateWallOnFloor(floor, 10f, Vector3.forward, 0f);
        Bounds wb = PlacementService.GetBounds(wall);
        Check("墙内侧面贴地板边", wb.min.z, fb.max.z, 0.01f);
        Check("墙底沉入地板", wb.min.y, fb.max.y - SceneSetupTool.WallSink, 0.01f);
        Check("墙顶高度", wb.max.y, wb.min.y + SceneSetupTool.WallHeight, 0.01f);

        // 3) 地板拼接：选中第一块，第二块应共享边
        Selection.activeGameObject = floor;
        GameObject floor2 = SceneSetupTool.CreateFloorPiece("半地板（5×10）", 5f, 10f);
        Bounds f2b = PlacementService.GetBounds(floor2);
        Check("地板拼接共享边", f2b.min.x, fb.max.x, 0.01f);
        Check("拼接面不重叠", f2b.min.x - fb.max.x, 0f, 0.01f);

        // 4) Socket 吸附：底面贴到 Socket 平面 + gap
        var socketGo = new GameObject("TestSocket");
        socketGo.transform.position = new Vector3(0f, 2f, 0f);
        var socket = socketGo.AddComponent<Socket>();
        socket.gap = 0.001f;

        var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crate.name = "TestCrate";
        crate.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
        crate.AddComponent<Mountable>();
        crate.transform.position = new Vector3(0f, 5f, 0f);

        PlacementService.Place(crate, socket);
        Bounds cb = PlacementService.GetBounds(crate);
        Check("Socket 吸附底面", cb.min.y, 2f + socket.gap, 0.005f);

        // 5) 旋转对齐：Socket 朝 +X，物体正面也朝 +X
        socketGo.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        PlacementService.Place(crate, socket);
        float dot = Vector3.Dot(crate.transform.forward, Vector3.right);
        Check("Socket 朝向对齐", dot, 1f, 0.01f);

        Debug.Log(failures == 0
            ? "[自检] PASS：地板/墙/拼接/Socket 吸附全部通过"
            : "[自检] FAIL：" + failures + " 项不通过");

        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    static void Check(string name, float actual, float expected, float tolerance)
    {
        float diff = Mathf.Abs(actual - expected);
        if (diff <= tolerance)
        {
            Debug.Log(string.Format("[自检] OK  {0}：{1:0.####}（期望 {2:0.####}）", name, actual, expected));
        }
        else
        {
            failures++;
            Debug.LogError(string.Format("[自检] BAD {0}：{1:0.####}（期望 {2:0.####}，差 {3:0.####}）",
                name, actual, expected, diff));
        }
    }
}
