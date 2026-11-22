using System.ComponentModel;

namespace HKW.MVVM.SourceGenerator;

/// <summary>
/// 标记需要由源生成依赖注入在对象构建与属性注入完成后执行的方法
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class DIMethodAttribute : Attribute
{
    /// <inheritdoc/>
    public DIMethodAttribute() { }

    /// <inheritdoc/>
    /// <param name="Order">执行顺序, 数值越小越先执行</param>
    public DIMethodAttribute(int Order)
    {
        this.Order = Order;
    }

    /// <summary>
    /// 方法执行顺序, 数值越小越先执行
    /// </summary>
    [DefaultValue(0)]
    public int Order { get; set; }
}
