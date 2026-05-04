using System.Collections.Generic;
using UnityEngine;

namespace LightGameFrame.Services
{
    /// <summary>
    /// 绘制模式枚举
    /// </summary>
    public enum DrawMode
    {
        /// <summary>
        /// 一次性绘制 - 只在当前帧绘制一次
        /// </summary>
        Once,
        
        /// <summary>
        /// 持续绘制 - 在指定时间内持续绘制
        /// </summary>
        Duration,
        
        /// <summary>
        /// 永久绘制 - 一直绘制直到手动清除
        /// </summary>
        Permanent
    }

    /// <summary>
    /// Debug绘制服务 - 负责持续渲染各种调试图形
    /// </summary>
    [AutoService]
    public class DebugDrawService : MonoServiceBase<DebugDrawService>
    {
        public override int ServicePriority => 50;

        #region 绘制数据结构

        private class DrawCommand
        {
            public DrawMode Mode;
            public float Duration;
            public float RemainingTime;
            public bool DepthTest;
        }

        private class LineCommand : DrawCommand
        {
            public Vector3 Start;
            public Vector3 End;
            public Color Color;
        }

        private class RayCommand : DrawCommand
        {
            public Vector3 Origin;
            public Vector3 Direction;
            public Color Color;
        }

        private class CurveCommand : DrawCommand
        {
            public List<Vector3> Points;
            public Color Color;
            public int Segments;
        }

        private class BezierCommand : DrawCommand
        {
            public Vector3 Start;
            public Vector3 Control1;
            public Vector3 Control2;
            public Vector3 End;
            public Color Color;
            public int Segments;
        }

        private class BoxCommand : DrawCommand
        {
            public Vector3 Center;
            public Vector3 Size;
            public Quaternion Rotation;
            public Color Color;
        }

        private class SphereCommand : DrawCommand
        {
            public Vector3 Center;
            public float Radius;
            public Color Color;
            public int Segments;
        }

        private class CircleCommand : DrawCommand
        {
            public Vector3 Center;
            public float Radius;
            public Vector3 Normal;
            public Color Color;
            public int Segments;
        }

        private class CylinderCommand : DrawCommand
        {
            public Vector3 Start;
            public Vector3 End;
            public float Radius;
            public Color Color;
            public int Segments;
        }

        private class TextCommand : DrawCommand
        {
            public Vector3 Position;
            public string Text;
            public Color Color;
            public int FontSize;
        }

        #endregion

        #region 命令列表

        private readonly List<LineCommand> _lineCommands = new List<LineCommand>();
        private readonly List<RayCommand> _rayCommands = new List<RayCommand>();
        private readonly List<CurveCommand> _curveCommands = new List<CurveCommand>();
        private readonly List<BezierCommand> _bezierCommands = new List<BezierCommand>();
        private readonly List<BoxCommand> _boxCommands = new List<BoxCommand>();
        private readonly List<SphereCommand> _sphereCommands = new List<SphereCommand>();
        private readonly List<CircleCommand> _circleCommands = new List<CircleCommand>();
        private readonly List<CylinderCommand> _cylinderCommands = new List<CylinderCommand>();
        private readonly List<TextCommand> _textCommands = new List<TextCommand>();

        #endregion

        #region 配置选项

        [Header("Debug Draw Settings")]
        [SerializeField] private bool enableDrawing = true;
        [SerializeField] private int maxCommandsPerType = 1000;

        public bool EnableDrawing
        {
            get => enableDrawing;
            set => enableDrawing = value;
        }

        #endregion

        #region 生命周期

        protected override void OnInitialize()
        {
            Debug.Log("[DebugDrawService] Initialized");
        }

        protected override void OnUpdate()
        {
            float deltaTime = Time.deltaTime;

            // 更新并移除过期的命令
            UpdateCommands(_lineCommands, deltaTime);
            UpdateCommands(_rayCommands, deltaTime);
            UpdateCommands(_curveCommands, deltaTime);
            UpdateCommands(_bezierCommands, deltaTime);
            UpdateCommands(_boxCommands, deltaTime);
            UpdateCommands(_sphereCommands, deltaTime);
            UpdateCommands(_circleCommands, deltaTime);
            UpdateCommands(_cylinderCommands, deltaTime);
            UpdateCommands(_textCommands, deltaTime);
        }

        protected override void OnCleanup()
        {
            ClearAll();
            Debug.Log("[DebugDrawService] Cleaned up");
        }

        private void OnDrawGizmos()
        {
            if (!enableDrawing || !IsInitialized) return;

            // 绘制所有命令
            DrawLines();
            DrawRays();
            DrawCurves();
            DrawBeziers();
            DrawBoxes();
            DrawSpheres();
            DrawCircles();
            DrawCylinders();
        }

        private void OnGUI()
        {
            if (!enableDrawing || !IsInitialized) return;
            DrawTexts();
        }

        #endregion

        #region 更新逻辑

        private void UpdateCommands<T>(List<T> commands, float deltaTime) where T : DrawCommand
        {
            for (int i = commands.Count - 1; i >= 0; i--)
            {
                var cmd = commands[i];
                
                // 根据绘制模式处理生命周期
                switch (cmd.Mode)
                {
                    case DrawMode.Once:
                        // 一次性绘制：下一帧立即移除
                        commands.RemoveAt(i);
                        break;
                        
                    case DrawMode.Duration:
                        // 持续绘制：倒计时
                        cmd.RemainingTime -= deltaTime;
                        if (cmd.RemainingTime <= 0)
                        {
                            commands.RemoveAt(i);
                        }
                        break;
                        
                    case DrawMode.Permanent:
                        // 永久绘制：不做任何处理，直到手动清除
                        break;
                }
            }
        }

        #endregion

        #region 绘制实现

        private void DrawLines()
        {
            foreach (var cmd in _lineCommands)
            {
                Gizmos.color = cmd.Color;
                Gizmos.DrawLine(cmd.Start, cmd.End);
            }
        }

        private void DrawRays()
        {
            foreach (var cmd in _rayCommands)
            {
                Gizmos.color = cmd.Color;
                Gizmos.DrawRay(cmd.Origin, cmd.Direction);
            }
        }

        private void DrawCurves()
        {
            foreach (var cmd in _curveCommands)
            {
                if (cmd.Points == null || cmd.Points.Count < 2) continue;

                Gizmos.color = cmd.Color;
                for (int i = 0; i < cmd.Points.Count - 1; i++)
                {
                    Gizmos.DrawLine(cmd.Points[i], cmd.Points[i + 1]);
                }
            }
        }

        private void DrawBeziers()
        {
            foreach (var cmd in _bezierCommands)
            {
                Gizmos.color = cmd.Color;
                Vector3 previousPoint = cmd.Start;

                for (int i = 1; i <= cmd.Segments; i++)
                {
                    float t = i / (float)cmd.Segments;
                    Vector3 point = CalculateBezierPoint(t, cmd.Start, cmd.Control1, cmd.Control2, cmd.End);
                    Gizmos.DrawLine(previousPoint, point);
                    previousPoint = point;
                }
            }
        }

        private void DrawBoxes()
        {
            foreach (var cmd in _boxCommands)
            {
                Gizmos.color = cmd.Color;
                Gizmos.matrix = Matrix4x4.TRS(cmd.Center, cmd.Rotation, Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, cmd.Size);
                Gizmos.matrix = Matrix4x4.identity;
            }
        }

        private void DrawSpheres()
        {
            foreach (var cmd in _sphereCommands)
            {
                Gizmos.color = cmd.Color;
                Gizmos.DrawWireSphere(cmd.Center, cmd.Radius);
            }
        }

        private void DrawCircles()
        {
            foreach (var cmd in _circleCommands)
            {
                Gizmos.color = cmd.Color;
                DrawCircleGizmo(cmd.Center, cmd.Radius, cmd.Normal, cmd.Segments);
            }
        }

        private void DrawCylinders()
        {
            foreach (var cmd in _cylinderCommands)
            {
                Gizmos.color = cmd.Color;
                DrawCylinderGizmo(cmd.Start, cmd.End, cmd.Radius, cmd.Segments);
            }
        }

        private void DrawTexts()
        {
            foreach (var cmd in _textCommands)
            {
                Vector3 screenPos = Camera.main.WorldToScreenPoint(cmd.Position);
                if (screenPos.z > 0)
                {
                    GUIStyle style = new GUIStyle();
                    style.normal.textColor = cmd.Color;
                    style.fontSize = cmd.FontSize;
                    GUI.Label(new Rect(screenPos.x, Screen.height - screenPos.y, 200, 50), cmd.Text, style);
                }
            }
        }

        #endregion

        #region 辅助绘制方法

        private Vector3 CalculateBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            float u = 1 - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            Vector3 point = uuu * p0;
            point += 3 * uu * t * p1;
            point += 3 * u * tt * p2;
            point += ttt * p3;

            return point;
        }

        private void DrawCircleGizmo(Vector3 center, float radius, Vector3 normal, int segments)
        {
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, normal.normalized);
            Vector3 prevPoint = center + rotation * new Vector3(radius, 0, 0);

            for (int i = 1; i <= segments; i++)
            {
                float angle = (i / (float)segments) * 360f * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                Vector3 point = center + rotation * offset;
                Gizmos.DrawLine(prevPoint, point);
                prevPoint = point;
            }
        }

        private void DrawCylinderGizmo(Vector3 start, Vector3 end, float radius, int segments)
        {
            Vector3 direction = end - start;
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);

            // 绘制顶部和底部圆
            DrawCircleGizmo(start, radius, direction, segments);
            DrawCircleGizmo(end, radius, direction, segments);

            // 绘制连接线
            for (int i = 0; i < 4; i++)
            {
                float angle = (i / 4f) * 360f * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                Vector3 startPoint = start + rotation * offset;
                Vector3 endPoint = end + rotation * offset;
                Gizmos.DrawLine(startPoint, endPoint);
            }
        }

        #endregion

        #region 公共API - 直线

        /// <summary>
        /// 绘制直线
        /// </summary>
        /// <param name="start">起点</param>
        /// <param name="end">终点</param>
        /// <param name="color">颜色</param>
        /// <param name="mode">绘制模式（Once=一次性，Duration=持续，Permanent=永久）</param>
        /// <param name="duration">持续时间（仅Mode=Duration时有效）</param>
        /// <param name="depthTest">是否进行深度测试</param>
        public void DrawLine(Vector3 start, Vector3 end, Color color, DrawMode mode = DrawMode.Once, float duration = 0f, bool depthTest = true)
        {
            if (_lineCommands.Count >= maxCommandsPerType) return;

            _lineCommands.Add(new LineCommand
            {
                Start = start,
                End = end,
                Color = color,
                Mode = mode,
                Duration = duration,
                RemainingTime = duration,
                DepthTest = depthTest
            });
        }

        /// <summary>
        /// 绘制射线
        /// </summary>
        public void DrawRay(Vector3 origin, Vector3 direction, Color color, DrawMode mode = DrawMode.Once, float duration = 0f, bool depthTest = true)
        {
            if (_rayCommands.Count >= maxCommandsPerType) return;

            _rayCommands.Add(new RayCommand
            {
                Origin = origin,
                Direction = direction,
                Color = color,
                Mode = mode,
                Duration = duration,
                RemainingTime = duration,
                DepthTest = depthTest
            });
        }

        #endregion

        #region 公共API - 曲线

        /// <summary>
        /// 绘制多段线曲线
        /// </summary>
        public void DrawCurve(List<Vector3> points, Color color, DrawMode mode = DrawMode.Once, float duration = 0f, bool depthTest = true)
        {
            if (_curveCommands.Count >= maxCommandsPerType) return;

            _curveCommands.Add(new CurveCommand
            {
                Points = new List<Vector3>(points),
                Color = color,
                Mode = mode,
                Duration = duration,
                RemainingTime = duration,
                DepthTest = depthTest,
                Segments = points.Count - 1
            });
        }

        /// <summary>
        /// 绘制贝塞尔曲线（三次）
        /// </summary>
        public void DrawBezier(Vector3 start, Vector3 control1, Vector3 control2, Vector3 end, 
            Color color, DrawMode mode = DrawMode.Once, int segments = 20, float duration = 0f, bool depthTest = true)
        {
            if (_bezierCommands.Count >= maxCommandsPerType) return;

            _bezierCommands.Add(new BezierCommand
            {
                Start = start,
                Control1 = control1,
                Control2 = control2,
                End = end,
                Color = color,
                Segments = segments,
                Mode = mode,
                Duration = duration,
                RemainingTime = duration,
                DepthTest = depthTest
            });
        }

        #endregion

        #region 公共API - 3D形状

        /// <summary>
        /// 绘制立方体边框
        /// </summary>
        public void DrawBox(Vector3 center, Vector3 size, Color color, DrawMode mode = DrawMode.Once, float duration = 0f, bool depthTest = true)
        {
            DrawBox(center, size, Quaternion.identity, color, mode, duration, depthTest);
        }

        /// <summary>
        /// 绘制旋转的立方体边框
        /// </summary>
        public void DrawBox(Vector3 center, Vector3 size, Quaternion rotation, Color color, DrawMode mode = DrawMode.Once, float duration = 0f, bool depthTest = true)
        {
            if (_boxCommands.Count >= maxCommandsPerType) return;

            _boxCommands.Add(new BoxCommand
            {
                Center = center,
                Size = size,
                Rotation = rotation,
                Color = color,
                Mode = mode,
                Duration = duration,
                RemainingTime = duration,
                DepthTest = depthTest
            });
        }

        /// <summary>
        /// 绘制球体边框
        /// </summary>
        public void DrawSphere(Vector3 center, float radius, Color color, DrawMode mode = DrawMode.Once, int segments = 16, float duration = 0f, bool depthTest = true)
        {
            if (_sphereCommands.Count >= maxCommandsPerType) return;

            _sphereCommands.Add(new SphereCommand
            {
                Center = center,
                Radius = radius,
                Color = color,
                Segments = segments,
                Mode = mode,
                Duration = duration,
                RemainingTime = duration,
                DepthTest = depthTest
            });
        }

        /// <summary>
        /// 绘制圆形
        /// </summary>
        public void DrawCircle(Vector3 center, float radius, Vector3 normal, Color color, DrawMode mode = DrawMode.Once, int segments = 32, float duration = 0f, bool depthTest = true)
        {
            if (_circleCommands.Count >= maxCommandsPerType) return;

            _circleCommands.Add(new CircleCommand
            {
                Center = center,
                Radius = radius,
                Normal = normal,
                Color = color,
                Segments = segments,
                Mode = mode,
                Duration = duration,
                RemainingTime = duration,
                DepthTest = depthTest
            });
        }

        /// <summary>
        /// 绘制圆柱体边框
        /// </summary>
        public void DrawCylinder(Vector3 start, Vector3 end, float radius, Color color, DrawMode mode = DrawMode.Once, int segments = 16, float duration = 0f, bool depthTest = true)
        {
            if (_cylinderCommands.Count >= maxCommandsPerType) return;

            _cylinderCommands.Add(new CylinderCommand
            {
                Start = start,
                End = end,
                Radius = radius,
                Color = color,
                Segments = segments,
                Mode = mode,
                Duration = duration,
                RemainingTime = duration,
                DepthTest = depthTest
            });
        }

        #endregion

        #region 公共API - 文本

        /// <summary>
        /// 在3D空间位置绘制文本
        /// </summary>
        public void DrawText(Vector3 position, string text, Color color, DrawMode mode = DrawMode.Once, int fontSize = 12, float duration = 0f)
        {
            if (_textCommands.Count >= maxCommandsPerType) return;

            _textCommands.Add(new TextCommand
            {
                Position = position,
                Text = text,
                Color = color,
                FontSize = fontSize,
                Mode = mode,
                Duration = duration,
                RemainingTime = duration
            });
        }

        #endregion

        #region 公共API - 便捷方法

        /// <summary>
        /// 绘制坐标轴
        /// </summary>
        public void DrawAxes(Vector3 origin, DrawMode mode = DrawMode.Once, float length = 1f, float duration = 0f)
        {
            DrawLine(origin, origin + Vector3.right * length, Color.red, mode, duration);
            DrawLine(origin, origin + Vector3.up * length, Color.green, mode, duration);
            DrawLine(origin, origin + Vector3.forward * length, Color.blue, mode, duration);
        }

        /// <summary>
        /// 绘制网格
        /// </summary>
        public void DrawGrid(Vector3 center, int gridSize, float cellSize, Color color, DrawMode mode = DrawMode.Once, float duration = 0f)
        {
            float halfSize = gridSize * cellSize * 0.5f;

            for (int i = 0; i <= gridSize; i++)
            {
                float offset = i * cellSize - halfSize;
                
                // X方向线
                DrawLine(
                    center + new Vector3(-halfSize, 0, offset),
                    center + new Vector3(halfSize, 0, offset),
                    color, mode, duration
                );
                
                // Z方向线
                DrawLine(
                    center + new Vector3(offset, 0, -halfSize),
                    center + new Vector3(offset, 0, halfSize),
                    color, mode, duration
                );
            }
        }

        /// <summary>
        /// 绘制箭头
        /// </summary>
        public void DrawArrow(Vector3 start, Vector3 end, Color color, DrawMode mode = DrawMode.Once, float arrowHeadLength = 0.3f, float arrowHeadAngle = 20f, float duration = 0f)
        {
            DrawLine(start, end, color, mode, duration);

            Vector3 direction = (end - start).normalized;
            Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 + arrowHeadAngle, 0) * Vector3.forward;
            Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 - arrowHeadAngle, 0) * Vector3.forward;

            DrawLine(end, end + right * arrowHeadLength, color, mode, duration);
            DrawLine(end, end + left * arrowHeadLength, color, mode, duration);
        }

        /// <summary>
        /// 绘制Bounds边框
        /// </summary>
        public void DrawBounds(Bounds bounds, Color color, DrawMode mode = DrawMode.Once, float duration = 0f)
        {
            DrawBox(bounds.center, bounds.size, color, mode, duration);
        }

        #endregion

        #region 管理方法

        /// <summary>
        /// 清除所有绘制命令
        /// </summary>
        public void ClearAll()
        {
            _lineCommands.Clear();
            _rayCommands.Clear();
            _curveCommands.Clear();
            _bezierCommands.Clear();
            _boxCommands.Clear();
            _sphereCommands.Clear();
            _circleCommands.Clear();
            _cylinderCommands.Clear();
            _textCommands.Clear();
        }

        /// <summary>
        /// 获取当前绘制命令数量
        /// </summary>
        public int GetTotalCommandCount()
        {
            return _lineCommands.Count +
                   _rayCommands.Count +
                   _curveCommands.Count +
                   _bezierCommands.Count +
                   _boxCommands.Count +
                   _sphereCommands.Count +
                   _circleCommands.Count +
                   _cylinderCommands.Count +
                   _textCommands.Count;
        }

        #endregion

        #region 静态便捷访问

        /// <summary>
        /// 静态便捷访问实例
        /// </summary>
        public static DebugDrawService Instance =>
            RuntimeLocator.GetService<DebugDrawService>()
            ?? FindObjectOfType<DebugDrawService>();

        #endregion
    }
}
