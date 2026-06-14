namespace Tile.Core.Common.Math;

public static partial class MathKit
{
    /// <summary>
     /// 将任意分数压缩到 <c>[0, 1]</c> 区间。
    /// 公式：<c>sigmoid(x) = 1 / (1 + e^(-x))</c>。
     /// 这里使用数值更稳定的实现，避免大正数 / 大负数时出现指数溢出。
     /// </summary>
    public static double Sigmoid(double score)
    {
        if (score >= 0)
        {
            var exp = System.Math.Exp(-score);
            return 1.0 / (1.0 + exp);
        }

        var negativeExp = System.Math.Exp(score);
        return negativeExp / (1.0 + negativeExp);
    }
}
