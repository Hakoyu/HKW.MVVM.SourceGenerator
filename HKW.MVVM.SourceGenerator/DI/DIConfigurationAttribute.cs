namespace HKW.MVVM.SourceGenerator;

/// <summary>
/// 标记继承 <see cref="DIConfigurationBase"/> 的分部配置类
/// <para>在被标记的对象内的任意方法中使用 <see cref="DIConfigurationBase.Register{T}()"/> 等方法时, 均会被源生成记录并注册</para>
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class DIConfigurationAttribute : Attribute;
