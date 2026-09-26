using System.Collections.Generic;
using UnityEngine;

// 曲线类型枚举，支持多种参数曲线
public enum CurveType
{
    LinearBezier,    // 一阶贝塞尔（直线）
    QuadraticBezier, // 二阶贝塞尔（3控制点）
    CubicBezier,     // 三阶贝塞尔（4控制点）
    BSplineUniform   // 均匀B样条
}

[RequireComponent(typeof(LineRenderer))]
public class CurveSystem : MonoBehaviour
{
    [Header("曲线设置")]
    public CurveType curveType = CurveType.CubicBezier;
    public int sampleCount = 50;       // 曲线采样点数，越大越平滑
    public GameObject controlPointPrefab; // 控制点小球预制体
    public Transform controlPointParent;

    [Header("交互设置")]
    public float rayMaxDistance = 100f;
    private GameObject selectedPoint; // 当前选中的控制点

    private LineRenderer lineRenderer;
    private List<Vector3> controlPoints = new List<Vector3>();
    private List<GameObject> pointObjects = new List<GameObject>();

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.loop = false;
        lineRenderer.positionCount = sampleCount;

        // 初始化默认控制点（三阶贝塞尔4个点）
        InitDefaultControlPoints();
    }

    void Update()
    {
        // 交互逻辑：鼠标选择、拖拽控制点
        HandleMouseInput();
        // 根据当前曲线类型，计算并绘制曲线
        DrawCurve();
    }

    // 初始化一组默认控制点
    void InitDefaultControlPoints()
    {
        ClearAllControlPoints();
        AddControlPoint(new Vector3(-4, 0, 0));
        AddControlPoint(new Vector3(-2, 2, 0));
        AddControlPoint(new Vector3(2, -2, 0));
        AddControlPoint(new Vector3(4, 0, 0));
    }

    // 新增控制点，生成小球
    void AddControlPoint(Vector3 pos)
    {
        GameObject pt = Instantiate(controlPointPrefab, pos, Quaternion.identity, controlPointParent);
        pointObjects.Add(pt);
        controlPoints.Add(pos);
    }

    // 清空所有控制点
    void ClearAllControlPoints()
    {
        foreach (var p in pointObjects) Destroy(p);
        pointObjects.Clear();
        controlPoints.Clear();
    }

    #region 鼠标交互逻辑
    void HandleMouseInput()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // 鼠标左键按下：选中控制点
        if (Input.GetMouseButtonDown(0))
        {
            if (Physics.Raycast(ray, out hit, rayMaxDistance))
            {
                if (pointObjects.Contains(hit.collider.gameObject))
                {
                    selectedPoint = hit.collider.gameObject;
                }
            }
            else
            {
                // 点击空白处可以新增控制点（可选功能）
                // AddControlPoint(ray.GetPoint(5f));
            }
        }

        // 左键持续拖拽
        if (Input.GetMouseButton(0) && selectedPoint != null)
        {
            if (Physics.Raycast(ray, out hit, rayMaxDistance))
            {
                selectedPoint.transform.position = hit.point;
                // 同步更新控制点数据列表
                int idx = pointObjects.IndexOf(selectedPoint);
                controlPoints[idx] = selectedPoint.transform.position;
            }
        }

        // 左键抬起取消选中
        if (Input.GetMouseButtonUp(0))
        {
            selectedPoint = null;
        }

        // 鼠标右键：删除被点击的控制点
        if (Input.GetMouseButtonDown(1))
        {
            if (Physics.Raycast(ray, out hit, rayMaxDistance))
            {
                GameObject hitObj = hit.collider.gameObject;
                if (pointObjects.Contains(hitObj))
                {
                    int idx = pointObjects.IndexOf(hitObj);
                    Destroy(hitObj);
                    pointObjects.RemoveAt(idx);
                    controlPoints.RemoveAt(idx);
                }
            }
        }
    }
    #endregion

    #region 曲线数学计算核心
    // 主绘制函数：根据类型选择参数曲线算法
    void DrawCurve()
    {
        Vector3[] curvePoints = new Vector3[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / (sampleCount - 1); // t ∈ [0,1]
            switch (curveType)
            {
                case CurveType.LinearBezier:
                    curvePoints[i] = LinearBezier(t, controlPoints);
                    break;
                case CurveType.QuadraticBezier:
                    curvePoints[i] = QuadraticBezier(t, controlPoints);
                    break;
                case CurveType.CubicBezier:
                    curvePoints[i] = CubicBezier(t, controlPoints);
                    break;
                case CurveType.BSplineUniform:
                    curvePoints[i] = UniformBSpline(t, controlPoints, 3); // 3次B样条
                    break;
            }
        }
        lineRenderer.SetPositions(curvePoints);
    }

    /// <summary>
    /// 一阶贝塞尔（直线插值）P0*(1-t)+P1*t
    /// </summary>
    Vector3 LinearBezier(float t, List<Vector3> pts)
    {
        if (pts.Count < 2) return pts[0];
        return Vector3.Lerp(pts[0], pts[1], t);
    }

    /// <summary>
    /// 二阶贝塞尔，3个控制点 P0,P1,P2
    /// </summary>
    Vector3 QuadraticBezier(float t, List<Vector3> pts)
    {
        if (pts.Count < 3) return pts[0];
        float u = 1 - t;
        return u * u * pts[0] + 2 * u * t * pts[1] + t * t * pts[2];
    }

    /// <summary>
    /// 三阶贝塞尔，4个控制点 P0,P1,P2,P3
    /// </summary>
    Vector3 CubicBezier(float t, List<Vector3> pts)
    {
        if (pts.Count < 4) return pts[0];
        float u = 1 - t;
        return u * u * u * pts[0]
            + 3 * u * u * t * pts[1]
            + 3 * u * t * t * pts[2]
            + t * t * t * pts[3];
    }

    /// <summary>
    /// 均匀3次B样条曲线
    /// t:[0,1]，k=3 三次
    /// </summary>
    Vector3 UniformBSpline(float t, List<Vector3> pts, int k)
    {
        int n = pts.Count - 1;
        if (n < k) return pts[0];

        // 将t映射到节点区间
        float knotMin = 0;
        float knotMax = n - k + 2;
        float u = knotMin + t * (knotMax - knotMin);

        Vector3 sum = Vector3.zero;
        for (int i = 0; i <= n; i++)
        {
            float N = BasisFunction(i, k, u);
            sum += pts[i] * N;
        }
        return sum;
    }

    // B样条基函数递归（德布尔递推）
    float BasisFunction(int i, int k, float u)
    {
        if (k == 1)
        {
            if (i <= u && u < i + 1) return 1;
            else return 0;
        }
        float left = 0;
        if (i + k - 1 != i)
            left = (u - i) / (i + k - 1 - i) * BasisFunction(i, k - 1, u);

        float right = 0;
        if (i + k != i + 1)
            right = (i + k - u) / (i + k - (i + 1)) * BasisFunction(i + 1, k - 1, u);
        return left + right;
    }
    #endregion

    // GUI面板：切换曲线类型，方便调试
    void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 200, 30), "当前曲线类型：" + curveType.ToString());
        if (GUI.Button(new Rect(10, 40, 180, 30), "切换：一阶贝塞尔")) curveType = CurveType.LinearBezier;
        if (GUI.Button(new Rect(10, 80, 180, 30), "切换：二阶贝塞尔")) curveType = CurveType.QuadraticBezier;
        if (GUI.Button(new Rect(10, 120, 180, 30), "切换：三阶贝塞尔")) curveType = CurveType.CubicBezier;
        if (GUI.Button(new Rect(10, 160, 180, 30), "切换：三次均匀B样条")) curveType = CurveType.BSplineUniform;
        if (GUI.Button(new Rect(10, 200, 180, 30), "重置控制点")) InitDefaultControlPoints();
    }
}