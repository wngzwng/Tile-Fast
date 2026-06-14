# ThreeTile MathKit 与 Extension 设计文档

## 1. 目标

`Common/Math` 目录用于统一管理 ThreeTile 项目中的数学工具能力。

当前需要覆盖：

1. Sigmoid
2. Average
3. WeightedAverage
4. UpdateAverage
5. TryAverage
6. Softmax
7. WeightedChoice
8. Manhattan Distance
9. Euclidean Distance
10. Combinatorics

同时支持两种调用方式：

1. 简单调用：代码方便，可以返回新数组。
2. 高性能调用：传入 `Span` / `ReadOnlySpan` / 外部结果容器，避免临时分配。

最终希望可以写出：

```csharp
int index = scores
    .Softmax(weights, temperature: 0.6)
    .WeightedChoice(random);
```

以及：

```csharp
foreach (ulong mask in MathKit.EnumerateChooseMasks(n, k))
{
    // 使用组合 mask
}
```

---

# 2. 目录结构

推荐目录：

```text
ThreeTile.Core
└── Common
    └── Math
        ├── MathKit.cs
        ├── MathKit.Sigmoid.cs
        ├── MathKit.Average.cs
        ├── MathKit.Softmax.cs
        ├── MathKit.WeightedChoice.cs
        ├── MathKit.Distance.cs
        ├── MathKit.Combinatorics.cs
        └── MathKitExtensions.cs
```

说明：

```text
MathKit.cs：
  MathKit partial class 主入口。

MathKit.Sigmoid.cs：
  Sigmoid 相关实现。

MathKit.Average.cs：
  Average / WeightedAverage / UpdateAverage / TryAverage。

MathKit.Softmax.cs：
  Softmax 相关实现。

MathKit.WeightedChoice.cs：
  权重随机选择相关实现。

MathKit.Distance.cs：
  距离计算相关实现。

MathKit.Combinatorics.cs：
  组合数学相关实现。
  当前包含 n choose k 的 bit mask 枚举。

MathKitExtensions.cs：
  链式调用扩展方法。
```

---

# 3. 核心原则

## 3.1 MathKit 放核心逻辑

核心实现放在：

```csharp
MathKit
```

例如：

```csharp
MathKit.Sigmoid(score);

MathKit.Average(values);
MathKit.WeightedAverage(values, weights);
MathKit.UpdateAverage(currentAverage, newCount, newValue);

MathKit.Softmax(scores, destination, temperature);
MathKit.WeightedChoice(weights, random);

MathKit.Manhattan3D(...);
MathKit.Euclidean3D(...);

MathKit.EnumerateChooseMasks(n, k);
```

---

## 3.2 Extension 只负责调用体验

链式调用放在：

```csharp
MathKitExtensions
```

Extension 不承载核心逻辑，只转发给 `MathKit`。

推荐：

```csharp
public static Span<double> Softmax(
    this ReadOnlySpan<double> scores,
    Span<double> destination,
    double temperature = 1.0)
{
    MathKit.Softmax(scores, destination, temperature);
    return destination;
}
```

不推荐：

```csharp
public static Span<double> Softmax(
    this ReadOnlySpan<double> scores,
    Span<double> destination,
    double temperature = 1.0)
{
    // 大段 softmax 核心逻辑
}
```

---

## 3.3 哪些方法需要 destination

需要 `destination` 的方法通常是：

```text
一组输入 -> 一组输出
```

例如：

```text
scores -> weights
```

所以 `Softmax` 需要：

```csharp
public static void Softmax(
    ReadOnlySpan<double> scores,
    Span<double> destination,
    double temperature = 1.0);
```

---

## 3.4 哪些方法不需要 destination

不需要 `destination` 的方法通常是：

```text
一组输入 -> 一个标量结果
```

例如：

```text
Average(values) -> double
WeightedAverage(values, weights) -> double
WeightedChoice(weights, random) -> int
```

所以 `Average` 不需要：

```csharp
Average(values, destination)
```

因为它的结果只是一个 `double`。

`Average` 的高性能点是：

```text
输入使用 ReadOnlySpan<T>
内部使用 for 循环
不使用 LINQ
不创建临时数组
```

---

## 3.5 Combinatorics 属于 MathKit

`EnumerateChooseMasks` 本质是：

```text
组合数学工具 + bit mask 表达
```

它不是对某个已有 BitSet 做增删改查，而是在生成所有组合。

所以它更适合放在：

```text
MathKit.Combinatorics.cs
```

而不是：

```text
BitSet
BitOps
BitSetOps
```

---

## 3.6 不暴露 Gosper 细节

不推荐命名为：

```csharp
ChooseBitsGosper
```

原因：

```text
Gosper 是内部实现细节。
调用方关心的是“枚举 n 选 k 的 mask”。
```

推荐命名：

```csharp
EnumerateChooseMasks
```

含义清晰：

```text
Enumerate：枚举
Choose：n choose k
Masks：返回 bit mask
```

---

# 4. 命名约定

不使用：

```text
MathOps
MathUtil
MathHelper
NumericGuard
```

统一使用：

```text
MathKit
```

原因：

```text
MathKit 短、清晰、语义稳定。
它表示数学工具包，但不像 Utils 那样泛化。
```

---

# 5. MathKit.cs

```csharp
namespace ThreeTile.Core.Common.Math;

public static partial class MathKit
{
}
```

`MathKit` 使用 `partial`，按主题拆分不同文件。

---

# 6. Sigmoid

## 6.1 作用

`Sigmoid` 用于把任意分数压缩到 `0 ~ 1`。

常用于：

```text
原始分数 score -> 概率 / 倾向值 / 激活值
```

例如：

```text
score 很大正数  -> 接近 1
score = 0       -> 0.5
score 很大负数  -> 接近 0
```

---

## 6.2 公式

```text
sigmoid(x) = 1 / (1 + e^(-x))
```

---

## 6.3 数值稳定实现

不要直接写：

```csharp
return 1.0 / (1.0 + System.Math.Exp(-score));
```

原因：

当 `score` 是很大的负数时：

```text
score = -1000
Math.Exp(-score) = Math.Exp(1000)
```

可能溢出成 `Infinity`。

所以拆成正负两种情况。

---

## 6.4 MathKit.Sigmoid.cs

```csharp
namespace ThreeTile.Core.Common.Math;

public static partial class MathKit
{
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
```

---

## 6.5 使用方式

```csharp
double probability = MathKit.Sigmoid(score);
```

---

# 7. Average API 设计

平均值相关方法包括：

1. Average
2. WeightedAverage
3. UpdateAverage
4. TryAverage

分别对应：

```text
Average：
  普通平均值。

WeightedAverage：
  加权平均值。

UpdateAverage：
  增量平均值，不需要保存全部历史数据。

TryAverage：
  更严格的平均值计算，能表达是否计算成功。
```

---

# 8. Average

## 8.1 作用

普通平均值表示：

```text
一组数的总和 / 数量
```

公式：

```text
avg = sum / count
```

适合：

```text
平均分
平均失败率
平均耗时
平均分支数
平均移动次数
平均死局数
平均匹配次数
```

---

## 8.2 为什么需要 int 输入版本

项目中的很多指标天然是 `int`：

```text
MoveCount
BranchCount
MaxDepth
DeadEndCount
MatchCount
SuccessCount
FailCount
```

所以应该提供：

```csharp
public static double Average(ReadOnlySpan<int> values);
```

但返回值仍然使用：

```csharp
double
```

原因：

```text
平均值不一定是整数。
```

例如：

```text
[1, 2] 的平均值是 1.5
```

---

## 8.3 double Average

```csharp
public static double Average(ReadOnlySpan<double> values)
{
    if (values.Length == 0)
        return 0.0;

    var sum = 0.0;
    var count = 0;

    for (var i = 0; i < values.Length; i++)
    {
        var value = values[i];

        if (double.IsNaN(value) || double.IsInfinity(value))
            continue;

        sum += value;
        count++;
    }

    if (count == 0)
        return 0.0;

    return sum / count;
}
```

说明：

```text
忽略 NaN。
忽略 Infinity。
分母使用有效值数量 count。
如果没有有效值，返回 0.0。
```

---

## 8.4 int Average

```csharp
public static double Average(ReadOnlySpan<int> values)
{
    if (values.Length == 0)
        return 0.0;

    long sum = 0;

    for (var i = 0; i < values.Length; i++)
    {
        sum += values[i];
    }

    return (double)sum / values.Length;
}
```

说明：

```text
int 输入没有 NaN / Infinity 问题。
sum 使用 long，避免 int 累加溢出。
返回 double。
```

---

# 9. WeightedAverage

## 9.1 作用

加权平均值表示：

```text
不同数据的重要性不同。
权重大，影响更大。
权重小，影响更小。
```

公式：

```text
weightedAvg = sum(value * weight) / sum(weight)
```

例如：

```text
values:  [10, 20, 30]
weights: [0.2, 0.3, 0.5]
```

结果更偏向 `30`，因为它的权重最大。

---

## 9.2 double WeightedAverage

```csharp
public static double WeightedAverage(
    ReadOnlySpan<double> values,
    ReadOnlySpan<double> weights)
{
    if (values.Length != weights.Length)
        throw new ArgumentException("Weights length must match values length.");

    if (values.Length == 0)
        return 0.0;

    var weightedSum = 0.0;
    var weightSum = 0.0;

    for (var i = 0; i < values.Length; i++)
    {
        var value = values[i];
        var weight = weights[i];

        if (double.IsNaN(value) ||
            double.IsInfinity(value) ||
            double.IsNaN(weight) ||
            double.IsInfinity(weight) ||
            weight <= 0)
        {
            continue;
        }

        weightedSum += value * weight;
        weightSum += weight;
    }

    if (weightSum <= 0)
        return 0.0;

    return weightedSum / weightSum;
}
```

---

## 9.3 int WeightedAverage

```csharp
public static double WeightedAverage(
    ReadOnlySpan<int> values,
    ReadOnlySpan<double> weights)
{
    if (values.Length != weights.Length)
        throw new ArgumentException("Weights length must match values length.");

    if (values.Length == 0)
        return 0.0;

    var weightedSum = 0.0;
    var weightSum = 0.0;

    for (var i = 0; i < values.Length; i++)
    {
        var weight = weights[i];

        if (double.IsNaN(weight) ||
            double.IsInfinity(weight) ||
            weight <= 0)
        {
            continue;
        }

        weightedSum += values[i] * weight;
        weightSum += weight;
    }

    if (weightSum <= 0)
        return 0.0;

    return weightedSum / weightSum;
}
```

说明：

```text
int value 本身不需要 NaN / Infinity 检查。
weight 仍然需要检查。
weight <= 0 忽略。
```

---

# 10. UpdateAverage

## 10.1 作用

`UpdateAverage` 用于在线更新平均值。

它不需要保存全部历史数据。

公式：

```text
newAvg = oldAvg + (newValue - oldAvg) / newCount
```

适合：

```text
实时统计平均分
实时统计平均耗时
实时统计平均分支数
实时统计失败率
```

---

## 10.2 double UpdateAverage

```csharp
public static double UpdateAverage(
    double currentAverage,
    int newCount,
    double newValue)
{
    if (newCount <= 0)
        throw new ArgumentOutOfRangeException(nameof(newCount));

    return currentAverage + (newValue - currentAverage) / newCount;
}
```

---

## 10.3 int UpdateAverage

```csharp
public static double UpdateAverage(
    double currentAverage,
    int newCount,
    int newValue)
{
    if (newCount <= 0)
        throw new ArgumentOutOfRangeException(nameof(newCount));

    return currentAverage + (newValue - currentAverage) / newCount;
}
```

说明：

```text
int newValue 会自动参与 double 计算。
返回值仍然是 double。
```

---

# 11. TryAverage

## 11.1 为什么需要 TryAverage

`Average` 空输入时返回 `0.0`，调用方便。

但有些场景下需要区分：

```text
是真的平均值为 0
还是没有数据，无法计算平均值
```

所以可以提供：

```csharp
public static bool TryAverage(..., out double average)
```

---

## 11.2 double TryAverage

```csharp
public static bool TryAverage(
    ReadOnlySpan<double> values,
    out double average)
{
    average = 0.0;

    if (values.Length == 0)
        return false;

    var sum = 0.0;
    var count = 0;

    for (var i = 0; i < values.Length; i++)
    {
        var value = values[i];

        if (double.IsNaN(value) || double.IsInfinity(value))
            continue;

        sum += value;
        count++;
    }

    if (count == 0)
        return false;

    average = sum / count;
    return true;
}
```

---

## 11.3 int TryAverage

```csharp
public static bool TryAverage(
    ReadOnlySpan<int> values,
    out double average)
{
    average = 0.0;

    if (values.Length == 0)
        return false;

    long sum = 0;

    for (var i = 0; i < values.Length; i++)
    {
        sum += values[i];
    }

    average = (double)sum / values.Length;
    return true;
}
```

---

# 12. MathKit.Average.cs 完整代码

```csharp
namespace ThreeTile.Core.Common.Math;

public static partial class MathKit
{
    public static double Average(ReadOnlySpan<double> values)
    {
        if (values.Length == 0)
            return 0.0;

        var sum = 0.0;
        var count = 0;

        for (var i = 0; i < values.Length; i++)
        {
            var value = values[i];

            if (double.IsNaN(value) || double.IsInfinity(value))
                continue;

            sum += value;
            count++;
        }

        if (count == 0)
            return 0.0;

        return sum / count;
    }

    public static double Average(ReadOnlySpan<int> values)
    {
        if (values.Length == 0)
            return 0.0;

        long sum = 0;

        for (var i = 0; i < values.Length; i++)
        {
            sum += values[i];
        }

        return (double)sum / values.Length;
    }

    public static bool TryAverage(
        ReadOnlySpan<double> values,
        out double average)
    {
        average = 0.0;

        if (values.Length == 0)
            return false;

        var sum = 0.0;
        var count = 0;

        for (var i = 0; i < values.Length; i++)
        {
            var value = values[i];

            if (double.IsNaN(value) || double.IsInfinity(value))
                continue;

            sum += value;
            count++;
        }

        if (count == 0)
            return false;

        average = sum / count;
        return true;
    }

    public static bool TryAverage(
        ReadOnlySpan<int> values,
        out double average)
    {
        average = 0.0;

        if (values.Length == 0)
            return false;

        long sum = 0;

        for (var i = 0; i < values.Length; i++)
        {
            sum += values[i];
        }

        average = (double)sum / values.Length;
        return true;
    }

    public static double WeightedAverage(
        ReadOnlySpan<double> values,
        ReadOnlySpan<double> weights)
    {
        if (values.Length != weights.Length)
            throw new ArgumentException("Weights length must match values length.");

        if (values.Length == 0)
            return 0.0;

        var weightedSum = 0.0;
        var weightSum = 0.0;

        for (var i = 0; i < values.Length; i++)
        {
            var value = values[i];
            var weight = weights[i];

            if (double.IsNaN(value) ||
                double.IsInfinity(value) ||
                double.IsNaN(weight) ||
                double.IsInfinity(weight) ||
                weight <= 0)
            {
                continue;
            }

            weightedSum += value * weight;
            weightSum += weight;
        }

        if (weightSum <= 0)
            return 0.0;

        return weightedSum / weightSum;
    }

    public static double WeightedAverage(
        ReadOnlySpan<int> values,
        ReadOnlySpan<double> weights)
    {
        if (values.Length != weights.Length)
            throw new ArgumentException("Weights length must match values length.");

        if (values.Length == 0)
            return 0.0;

        var weightedSum = 0.0;
        var weightSum = 0.0;

        for (var i = 0; i < values.Length; i++)
        {
            var weight = weights[i];

            if (double.IsNaN(weight) ||
                double.IsInfinity(weight) ||
                weight <= 0)
            {
                continue;
            }

            weightedSum += values[i] * weight;
            weightSum += weight;
        }

        if (weightSum <= 0)
            return 0.0;

        return weightedSum / weightSum;
    }

    public static double UpdateAverage(
        double currentAverage,
        int newCount,
        double newValue)
    {
        if (newCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(newCount));

        return currentAverage + (newValue - currentAverage) / newCount;
    }

    public static double UpdateAverage(
        double currentAverage,
        int newCount,
        int newValue)
    {
        if (newCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(newCount));

        return currentAverage + (newValue - currentAverage) / newCount;
    }
}
```

---

# 13. Softmax

## 13.1 作用

`Softmax` 用于：

```text
一组分数 -> 一组概率权重
```

例如：

```text
scores = [1.0, 2.0, 3.0]
```

softmax 后：

```text
weights ≈ [0.09, 0.24, 0.67]
```

分数越大，权重越大。

---

## 13.2 公式

```text
p_i = exp(x_i) / sum(exp(x_j))
```

实际代码里不要直接 `Math.Exp(score)`。

应该先减去最大值：

```text
exp(score - maxScore)
```

因为：

```text
softmax(x) == softmax(x - max(x))
```

这样可以避免：

```text
Math.Exp(1000)
```

这种溢出问题。

---

## 13.3 带温度 Softmax

公式：

```text
p_i = exp(x_i / T) / sum(exp(x_j / T))
```

温度 `T` 的作用：

```text
T 越小：越偏向最高分，更激进
T = 1：标准 softmax
T 越大：越平均，更随机
```

例如：

```text
T = 0.2  -> 非常贪心
T = 0.6  -> 偏向高分
T = 1.0  -> 标准
T = 2.0  -> 更平滑
```

---

## 13.4 简单版：返回新数组

```csharp
public static double[] Softmax(
    ReadOnlySpan<double> scores,
    double temperature = 1.0);
```

使用：

```csharp
double[] weights = MathKit.Softmax(scores, temperature: 0.6);
```

特点：

```text
调用方便。
会分配一个新的 double[]。
适合普通路径。
```

---

## 13.5 高性能版：写入外部容器

```csharp
public static void Softmax(
    ReadOnlySpan<double> scores,
    Span<double> destination,
    double temperature = 1.0);
```

使用：

```csharp
Span<double> weights = stackalloc double[scores.Length];

MathKit.Softmax(scores, weights, temperature: 0.6);
```

或者 extension 链式调用：

```csharp
int index = scores
    .Softmax(weights, temperature: 0.6)
    .WeightedChoice(random);
```

特点：

```text
不分配新数组。
结果写入调用者提供的 destination。
适合热路径。
```

---

## 13.6 MathKit.Softmax.cs

```csharp
namespace ThreeTile.Core.Common.Math;

public static partial class MathKit
{
    public static void Softmax(
        ReadOnlySpan<double> scores,
        Span<double> destination,
        double temperature = 1.0)
    {
        if (scores.Length != destination.Length)
            throw new ArgumentException("Destination length must match scores length.");

        if (scores.Length == 0)
            return;

        if (temperature <= 0 ||
            double.IsNaN(temperature) ||
            double.IsInfinity(temperature))
        {
            throw new ArgumentOutOfRangeException(nameof(temperature));
        }

        destination.Clear();

        var max = double.NegativeInfinity;

        for (var i = 0; i < scores.Length; i++)
        {
            var score = scores[i];

            if (double.IsNaN(score) || double.IsInfinity(score))
                continue;

            if (score > max)
                max = score;
        }

        if (double.IsNegativeInfinity(max))
            return;

        var total = 0.0;

        for (var i = 0; i < scores.Length; i++)
        {
            var score = scores[i];

            if (double.IsNaN(score) || double.IsInfinity(score))
                continue;

            var value = System.Math.Exp((score - max) / temperature);

            destination[i] = value;
            total += value;
        }

        if (total <= 0 ||
            double.IsNaN(total) ||
            double.IsInfinity(total))
        {
            destination.Clear();
            return;
        }

        for (var i = 0; i < destination.Length; i++)
        {
            destination[i] /= total;
        }
    }

    public static double[] Softmax(
        ReadOnlySpan<double> scores,
        double temperature = 1.0)
    {
        if (scores.Length == 0)
            return Array.Empty<double>();

        var result = new double[scores.Length];

        Softmax(scores, result, temperature);

        return result;
    }
}
```

---

# 14. WeightedChoice

## 14.1 作用

`WeightedChoice` 负责：

```text
给定一组权重，按权重随机选择一个 index。
```

例如：

```text
index:   0    1    2
weight:  0.1  0.3  0.6
```

那么：

```text
index 2 被选中的概率最大。
index 0 被选中的概率最小。
```

---

## 14.2 行为约定

```text
weights 为空：
  返回 -1

所有权重都 <= 0 / NaN / Infinity：
  返回 -1

NaN / Infinity / <= 0 的权重：
  忽略

正常情况：
  按正权重随机选择一个 index
```

---

## 14.3 MathKit.WeightedChoice.cs

```csharp
namespace ThreeTile.Core.Common.Math;

public static partial class MathKit
{
    public static int WeightedChoice(
        ReadOnlySpan<double> weights,
        Random random)
    {
        if (weights.Length == 0)
            return -1;

        var total = 0.0;

        for (var i = 0; i < weights.Length; i++)
        {
            var weight = weights[i];

            if (double.IsNaN(weight) ||
                double.IsInfinity(weight) ||
                weight <= 0)
            {
                continue;
            }

            total += weight;
        }

        if (total <= 0)
            return -1;

        var target = random.NextDouble() * total;
        var cumulative = 0.0;

        for (var i = 0; i < weights.Length; i++)
        {
            var weight = weights[i];

            if (double.IsNaN(weight) ||
                double.IsInfinity(weight) ||
                weight <= 0)
            {
                continue;
            }

            cumulative += weight;

            if (target < cumulative)
                return i;
        }

        return weights.Length - 1;
    }
}
```

---

# 15. Distance

## 15.1 距离类型

距离计算包括：

```text
Manhattan2D
Manhattan3D
Euclidean2D
Euclidean3D
```

说明：

```text
曼哈顿距离：
  Manhattan Distance

欧几里得距离：
  Euclidean Distance
```

注意：

```text
“欧拉距离”更准确应叫“欧几里得距离”。
```

---

## 15.2 MathKit.Distance.cs

```csharp
namespace ThreeTile.Core.Common.Math;

public static partial class MathKit
{
    public static int Manhattan2D(
        int x1, int y1,
        int x2, int y2)
    {
        return System.Math.Abs(x1 - x2)
             + System.Math.Abs(y1 - y2);
    }

    public static int Manhattan3D(
        int x1, int y1, int z1,
        int x2, int y2, int z2)
    {
        return System.Math.Abs(x1 - x2)
             + System.Math.Abs(y1 - y2)
             + System.Math.Abs(z1 - z2);
    }

    public static double Euclidean2D(
        double x1, double y1,
        double x2, double y2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;

        return System.Math.Sqrt(dx * dx + dy * dy);
    }

    public static double Euclidean3D(
        double x1, double y1, double z1,
        double x2, double y2, double z2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        var dz = z1 - z2;

        return System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }
}
```

---

# 16. Combinatorics

## 16.1 作用

`Combinatorics` 用于提供组合数学相关工具。

当前主要用于：

```text
从 n 个候选位置中，选择 k 个位置
```

并把每一种选择结果表示成一个 `ulong mask`。

例如：

```csharp
foreach (ulong mask in MathKit.EnumerateChooseMasks(5, 3))
{
    // mask 表示一种 5 选 3 的组合
}
```

适合用于：

```text
候选组枚举
FSE pick 组合
Finder 小规模组合搜索
从可选集合中选择固定数量元素
```

---

## 16.2 参数语义

```csharp
EnumerateChooseMasks(int n, int k)
```

含义：

| 参数  | 说明           |
| --- | ------------ |
| `n` | 可选择的位置数量     |
| `k` | 每个组合中选择的位置数量 |

例如：

```csharp
MathKit.EnumerateChooseMasks(5, 3)
```

表示：

```text
从 0、1、2、3、4 这 5 个位置中，选择 3 个位置。
```

返回的每个 `ulong mask` 中，恰好有 `k` 个 bit 为 `1`。

---

## 16.3 示例

```csharp
foreach (ulong mask in MathKit.EnumerateChooseMasks(5, 3))
{
    Console.WriteLine(Convert.ToString((long)mask, 2).PadLeft(5, '0'));
}
```

输出类似：

```text
00111
01011
01101
01110
10011
10101
10110
11001
11010
11100
```

这些结果就是：

```text
5 选 3 的所有组合
```

---

## 16.4 边界情况

### k < 0

```csharp
MathKit.EnumerateChooseMasks(5, -1)
```

返回空枚举。

### k > n

```csharp
MathKit.EnumerateChooseMasks(5, 6)
```

返回空枚举。

### k == 0

```csharp
MathKit.EnumerateChooseMasks(5, 0)
```

返回：

```csharp
0UL
```

表示：

```text
什么都不选。
```

### n == 0 且 k == 0

```csharp
MathKit.EnumerateChooseMasks(0, 0)
```

返回：

```csharp
0UL
```

这是合理的，表示：

```text
空集合中选择 0 个元素。
```

---

## 16.5 为什么限制 n <= 63

实现中会用到：

```csharp
ulong limit = 1UL << n;
```

在 C# 中，`ulong` 左移时，位移数量只取低 6 位。

所以：

```csharp
1UL << 64
```

实际等价于：

```csharp
1UL << 0
```

这会导致错误。

因此当前版本限制：

```text
0 <= n <= 63
```

对于当前 ThreeTile 的小规模候选组合场景已经足够。

如果未来确实需要支持 `n == 64`，再单独扩展，不在当前阶段提前复杂化。

---

## 16.6 为什么暂时保留 IEnumerable

当前阶段不做过度优化。

优先级是：

```text
算法正确 > 命名清晰 > 调用简单 > 确认热点后再优化
```

所以这里保留：

```csharp
IEnumerable<ulong>
yield return
```

虽然 `yield return` 会生成枚举器状态机，但它调用简单、可读性好、心智负担低。

只有在性能分析确认这里是真热点之后，再考虑升级为：

```text
struct Enumerator
回调式 ForEachChooseMask
```

---

## 16.7 MathKit.Combinatorics.cs

```csharp
namespace ThreeTile.Core.Common.Math;

public static partial class MathKit
{
    public static IEnumerable<ulong> EnumerateChooseMasks(int n, int k)
    {
        if (n < 0 || n > 63)
            throw new ArgumentOutOfRangeException(nameof(n), "n must be in [0, 63].");

        if (k < 0 || k > n)
            yield break;

        if (k == 0)
        {
            yield return 0UL;
            yield break;
        }

        ulong mask = (1UL << k) - 1UL;
        ulong limit = 1UL << n;

        while (mask < limit)
        {
            yield return mask;

            ulong lowbit = mask & (0UL - mask);
            ulong next = mask + lowbit;

            mask = (((next ^ mask) >> 2) / lowbit) | next;
        }
    }
}
```

---

## 16.8 使用方式

```csharp
foreach (ulong mask in MathKit.EnumerateChooseMasks(n, k))
{
    // 使用 mask
}
```

例如：

```csharp
foreach (ulong pickMask in MathKit.EnumerateChooseMasks(candidateCount, pickCount))
{
    // pickMask 表示当前选择了哪些候选
}
```

---

## 16.9 是否需要 Extension

暂时不建议为 `EnumerateChooseMasks` 增加 extension。

不推荐：

```csharp
n.ChooseMasks(k)
```

原因：

```text
n 本身不是一个集合。
extension 语义不如 MathKit.EnumerateChooseMasks 清楚。
```

当前推荐直接使用：

```csharp
MathKit.EnumerateChooseMasks(n, k)
```

---

# 17. MathKitExtensions.cs

```csharp
namespace ThreeTile.Core.Common.Math;

public static class MathKitExtensions
{
    public static double[] Softmax(
        this double[] scores,
        double temperature = 1.0)
    {
        return MathKit.Softmax(scores, temperature);
    }

    public static double[] Softmax(
        this ReadOnlySpan<double> scores,
        double temperature = 1.0)
    {
        return MathKit.Softmax(scores, temperature);
    }

    public static double[] Softmax(
        this double[] scores,
        double[] destination,
        double temperature = 1.0)
    {
        MathKit.Softmax(scores, destination, temperature);
        return destination;
    }

    public static Span<double> Softmax(
        this ReadOnlySpan<double> scores,
        Span<double> destination,
        double temperature = 1.0)
    {
        MathKit.Softmax(scores, destination, temperature);
        return destination;
    }

    public static Span<double> Softmax(
        this Span<double> scores,
        Span<double> destination,
        double temperature = 1.0)
    {
        MathKit.Softmax(scores, destination, temperature);
        return destination;
    }

    public static int WeightedChoice(
        this double[] weights,
        Random random)
    {
        return MathKit.WeightedChoice(weights, random);
    }

    public static int WeightedChoice(
        this ReadOnlySpan<double> weights,
        Random random)
    {
        return MathKit.WeightedChoice(weights, random);
    }

    public static int WeightedChoice(
        this Span<double> weights,
        Random random)
    {
        return MathKit.WeightedChoice(weights, random);
    }

    public static double Average(this double[] values)
    {
        return MathKit.Average(values);
    }

    public static double Average(this ReadOnlySpan<double> values)
    {
        return MathKit.Average(values);
    }

    public static double Average(this Span<double> values)
    {
        return MathKit.Average(values);
    }

    public static double Average(this int[] values)
    {
        return MathKit.Average(values);
    }

    public static double Average(this ReadOnlySpan<int> values)
    {
        return MathKit.Average(values);
    }

    public static double Average(this Span<int> values)
    {
        return MathKit.Average(values);
    }

    public static bool TryAverage(
        this double[] values,
        out double average)
    {
        return MathKit.TryAverage(values, out average);
    }

    public static bool TryAverage(
        this ReadOnlySpan<double> values,
        out double average)
    {
        return MathKit.TryAverage(values, out average);
    }

    public static bool TryAverage(
        this Span<double> values,
        out double average)
    {
        return MathKit.TryAverage(values, out average);
    }

    public static bool TryAverage(
        this int[] values,
        out double average)
    {
        return MathKit.TryAverage(values, out average);
    }

    public static bool TryAverage(
        this ReadOnlySpan<int> values,
        out double average)
    {
        return MathKit.TryAverage(values, out average);
    }

    public static bool TryAverage(
        this Span<int> values,
        out double average)
    {
        return MathKit.TryAverage(values, out average);
    }

    public static double WeightedAverage(
        this double[] values,
        ReadOnlySpan<double> weights)
    {
        return MathKit.WeightedAverage(values, weights);
    }

    public static double WeightedAverage(
        this ReadOnlySpan<double> values,
        ReadOnlySpan<double> weights)
    {
        return MathKit.WeightedAverage(values, weights);
    }

    public static double WeightedAverage(
        this Span<double> values,
        ReadOnlySpan<double> weights)
    {
        return MathKit.WeightedAverage(values, weights);
    }

    public static double WeightedAverage(
        this int[] values,
        ReadOnlySpan<double> weights)
    {
        return MathKit.WeightedAverage(values, weights);
    }

    public static double WeightedAverage(
        this ReadOnlySpan<int> values,
        ReadOnlySpan<double> weights)
    {
        return MathKit.WeightedAverage(values, weights);
    }

    public static double WeightedAverage(
        this Span<int> values,
        ReadOnlySpan<double> weights)
    {
        return MathKit.WeightedAverage(values, weights);
    }
}
```

---

# 18. 使用方式

## 18.1 Sigmoid

```csharp
double probability = MathKit.Sigmoid(score);
```

---

## 18.2 double Average

```csharp
double[] values = [1.0, 2.0, 3.0];

double avg = values.Average();
```

---

## 18.3 int Average

```csharp
int[] values = [10, 20, 30];

double avg = values.Average();
```

结果：

```text
20.0
```

---

## 18.4 stackalloc int Average

```csharp
ReadOnlySpan<int> values = stackalloc int[]
{
    10, 20, 30
};

double avg = values.Average();
```

特点：

```text
不分配托管数组。
适合热路径。
```

---

## 18.5 double WeightedAverage

```csharp
double[] values = [10.0, 20.0, 30.0];
double[] weights = [0.2, 0.3, 0.5];

double avg = values.WeightedAverage(weights);
```

---

## 18.6 int WeightedAverage

```csharp
int[] values = [10, 20, 30];
double[] weights = [0.2, 0.3, 0.5];

double avg = values.WeightedAverage(weights);
```

---

## 18.7 UpdateAverage

```csharp
double avg = 0;

for (var i = 0; i < values.Length; i++)
{
    var count = i + 1;
    avg = MathKit.UpdateAverage(avg, count, values[i]);
}
```

---

## 18.8 TryAverage

```csharp
if (values.TryAverage(out var avg))
{
    // 使用 avg
}
else
{
    // 没有有效数据
}
```

---

## 18.9 简单数组版 Softmax + WeightedChoice

```csharp
double[] scores = [1.0, 2.0, 3.0];

int index = scores
    .Softmax(temperature: 0.6)
    .WeightedChoice(random);
```

特点：

```text
写法最舒服。
会分配一个新的 double[] 作为 weights。
```

---

## 18.10 复用数组容器版

```csharp
double[] scores = [1.0, 2.0, 3.0];
double[] weights = new double[scores.Length];

int index = scores
    .Softmax(weights, temperature: 0.6)
    .WeightedChoice(random);
```

特点：

```text
weights 由外部提供。
Softmax 只负责写入。
适合重复调用时复用数组。
```

---

## 18.11 stackalloc / Span 版

```csharp
ReadOnlySpan<double> scores = stackalloc double[]
{
    1.0, 2.0, 3.0
};

Span<double> weights = stackalloc double[scores.Length];

int index = scores
    .Softmax(weights, temperature: 0.6)
    .WeightedChoice(random);
```

特点：

```text
不分配托管数组。
适合热路径。
```

---

## 18.12 Combinatorics

```csharp
foreach (ulong mask in MathKit.EnumerateChooseMasks(5, 3))
{
    Console.WriteLine(Convert.ToString((long)mask, 2).PadLeft(5, '0'));
}
```

输出：

```text
00111
01011
01101
01110
10011
10101
10110
11001
11010
11100
```

---

# 19. Span 链式调用规则

这句可以成立：

```csharp
int index = scores
    .Softmax(weights, temperature: 0.6)
    .WeightedChoice(random);
```

前提是：

```text
Softmax 返回的是调用者传进来的 destination Span。
```

也就是：

```csharp
return destination;
```

---

# 20. Span 能做什么

可以：

```csharp
ReadOnlySpan<double> scores = stackalloc double[]
{
    1.0, 2.0, 3.0
};

Span<double> weights = stackalloc double[3];

int index = scores
    .Softmax(weights, temperature: 0.6)
    .WeightedChoice(random);
```

因为 `weights` 是调用者提供的，生命周期由调用者控制。

---

# 21. Span 不能做什么

## 21.1 不能返回方法内部创建的 stackalloc

不允许：

```csharp
public static Span<double> Softmax(this ReadOnlySpan<double> scores)
{
    Span<double> temp = stackalloc double[scores.Length];

    return temp;
}
```

原因：

```text
temp 是当前方法栈上的内存。
方法结束后它就失效。
```

---

## 21.2 不能存到普通 class 字段里

不允许：

```csharp
public sealed class Foo
{
    private Span<double> _span;
}
```

原因：

```text
Span<T> 是 ref struct。
它不能进入普通堆对象。
```

---

## 21.3 不能跨 async / await

不允许：

```csharp
async Task Foo()
{
    Span<double> weights = stackalloc double[10];

    await Task.Delay(100);

    weights[0] = 1.0;
}
```

原因：

```text
async 会生成状态机对象。
Span<T> 不能被放进这个状态机对象里。
```

---

## 21.4 不能被 lambda / iterator 捕获

不允许：

```csharp
Span<double> weights = stackalloc double[10];

Func<int> f = () => weights.Length;
```

原因：

```text
lambda 捕获会把变量提升到闭包对象中。
Span<T> 不能进入堆对象。
```

---

# 22. 最终 API 约定

## 22.1 MathKit 核心 API

```csharp
public static double Sigmoid(double score);

public static double Average(ReadOnlySpan<double> values);
public static double Average(ReadOnlySpan<int> values);

public static bool TryAverage(
    ReadOnlySpan<double> values,
    out double average);

public static bool TryAverage(
    ReadOnlySpan<int> values,
    out double average);

public static double WeightedAverage(
    ReadOnlySpan<double> values,
    ReadOnlySpan<double> weights);

public static double WeightedAverage(
    ReadOnlySpan<int> values,
    ReadOnlySpan<double> weights);

public static double UpdateAverage(
    double currentAverage,
    int newCount,
    double newValue);

public static double UpdateAverage(
    double currentAverage,
    int newCount,
    int newValue);

public static void Softmax(
    ReadOnlySpan<double> scores,
    Span<double> destination,
    double temperature = 1.0);

public static double[] Softmax(
    ReadOnlySpan<double> scores,
    double temperature = 1.0);

public static int WeightedChoice(
    ReadOnlySpan<double> weights,
    Random random);

public static int Manhattan2D(
    int x1, int y1,
    int x2, int y2);

public static int Manhattan3D(
    int x1, int y1, int z1,
    int x2, int y2, int z2);

public static double Euclidean2D(
    double x1, double y1,
    double x2, double y2);

public static double Euclidean3D(
    double x1, double y1, double z1,
    double x2, double y2, double z2);

public static IEnumerable<ulong> EnumerateChooseMasks(
    int n,
    int k);
```

---

## 22.2 Extension API

```csharp
public static double[] Softmax(
    this double[] scores,
    double temperature = 1.0);

public static double[] Softmax(
    this ReadOnlySpan<double> scores,
    double temperature = 1.0);

public static double[] Softmax(
    this double[] scores,
    double[] destination,
    double temperature = 1.0);

public static Span<double> Softmax(
    this ReadOnlySpan<double> scores,
    Span<double> destination,
    double temperature = 1.0);

public static int WeightedChoice(
    this ReadOnlySpan<double> weights,
    Random random);

public static double Average(
    this ReadOnlySpan<double> values);

public static double Average(
    this ReadOnlySpan<int> values);

public static bool TryAverage(
    this ReadOnlySpan<double> values,
    out double average);

public static bool TryAverage(
    this ReadOnlySpan<int> values,
    out double average);

public static double WeightedAverage(
    this ReadOnlySpan<double> values,
    ReadOnlySpan<double> weights);

public static double WeightedAverage(
    this ReadOnlySpan<int> values,
    ReadOnlySpan<double> weights);
```

暂时不提供：

```csharp
n.ChooseMasks(k)
```

原因：

```text
n 本身不是集合。
extension 语义不如 MathKit.EnumerateChooseMasks 清楚。
```

---

# 23. 关于 NumericGuard

暂时不创建：

```text
NumericGuard.cs
```

原因：

```text
NumericGuard 语义偏虚。
容易变成数值检查垃圾桶。
```

数值检查先就近放在具体方法内部。

例如：

```text
Softmax 的 temperature 检查放在 MathKit.Softmax.cs。
WeightedChoice 的 weight 检查放在 MathKit.WeightedChoice.cs。
Average 的 value / weight 检查放在 MathKit.Average.cs。
Distance 的参数检查放在 MathKit.Distance.cs。
Combinatorics 的 n / k 检查放在 MathKit.Combinatorics.cs。
```

只有后续大量重复出现相同数值检查时，再考虑抽取。

---

# 24. 最终结论

最终目录：

```text
ThreeTile.Core
└── Common
    └── Math
        ├── MathKit.cs
        ├── MathKit.Sigmoid.cs
        ├── MathKit.Average.cs
        ├── MathKit.Softmax.cs
        ├── MathKit.WeightedChoice.cs
        ├── MathKit.Distance.cs
        ├── MathKit.Combinatorics.cs
        └── MathKitExtensions.cs
```

最终原则：

```text
MathKit 负责核心实现。
MathKitExtensions 负责链式调用体验。
```

`Sigmoid` 负责：

```text
单个分数 -> 0~1
```

`Average` 负责：

```text
一组数 -> 一个普通平均值
```

`WeightedAverage` 负责：

```text
一组数 + 一组权重 -> 一个加权平均值
```

`UpdateAverage` 负责：

```text
不保存历史数据，实时更新平均值
```

`TryAverage` 负责：

```text
尝试计算平均值，并返回是否成功
```

`Softmax` 负责：

```text
一组分数 -> 一组概率权重
```

`WeightedChoice` 负责：

```text
一组权重 -> 按概率随机选一个 index
```

`Distance` 负责：

```text
二维 / 三维距离计算
```

`Combinatorics` 负责：

```text
组合数学工具。
当前提供 n choose k 的 bit mask 枚举。
```

---

# 25. 最常用组合

## 25.1 Softmax + WeightedChoice

```csharp
int index = scores
    .Softmax(weights, temperature: 0.6)
    .WeightedChoice(random);
```

理解：

```text
scores -> Softmax -> weights -> WeightedChoice -> selected index
```

---

## 25.2 EnumerateChooseMasks

```csharp
foreach (ulong mask in MathKit.EnumerateChooseMasks(n, k))
{
    // 使用组合 mask
}
```

理解：

```text
n 和 k -> 所有 n choose k 的 bit mask
```

---

# 26. 高性能理解

## 26.1 Average 的高性能理解

`Average` 不需要传入结果容器。

原因：

```text
Average 的结果只是一个 double。
```

`Average` 的高性能点是：

```text
输入使用 ReadOnlySpan<T>
内部使用 for 循环
不使用 LINQ
不创建临时数组
```

---

## 26.2 Softmax 的高性能理解

`Softmax` 需要传入结果容器。

原因：

```text
Softmax 的结果是一组 weights。
```

`Softmax` 的高性能点是：

```text
输入使用 ReadOnlySpan<double>
输出使用 Span<double>
调用者复用 destination
避免每次 new double[]
```

---

## 26.3 Combinatorics 的高性能理解

`EnumerateChooseMasks` 当前不提前做极致优化。

原因：

```text
组合枚举是否是热点，需要性能分析确认。
当前阶段优先保证可读性和调用简单。
```

当前版本：

```text
使用 IEnumerable<ulong>
使用 yield return
内部使用 Gosper Hack 生成下一个组合
```

未来如果确认是热点，再考虑：

```text
struct Enumerator
回调式 ForEachChooseMask
```

但暂时不引入。

---

# 27. 当前推荐调用总结

```csharp
double probability = MathKit.Sigmoid(score);
```

```csharp
double avg = values.Average();
```

```csharp
double weightedAvg = values.WeightedAverage(weights);
```

```csharp
avg = MathKit.UpdateAverage(avg, count, value);
```

```csharp
if (values.TryAverage(out var avg))
{
    // 使用 avg
}
```

```csharp
int index = scores
    .Softmax(weights, temperature: 0.6)
    .WeightedChoice(random);
```

```csharp
foreach (ulong mask in MathKit.EnumerateChooseMasks(n, k))
{
    // 使用组合 mask
}
```
