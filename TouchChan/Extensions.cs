using System.Drawing;
using System.Numerics;

namespace TouchChan
{
    // 几何类型转换的扩展方法，项目中统一使用 System.Drawing 命名空间下的 int 类型
    static class GeometryExtensions
    {
        public static Size ToSize(this Vector2 size) => new((int)size.X, (int)size.Y);
    }
}
