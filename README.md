# HKW.MVVM.SourceGenerator

基于 `CommunityToolkit.Mvvm` 的 MVVM 源代码生成器，并附带一套独立的依赖注入源生成器。项目结合 Roslyn Source Generator 与 Fody：MVVM 部分在编译期间为 `ObservableObject` 派生类生成属性通知、命令和响应式计算属性相关代码，并在程序集构建阶段完成必要的 IL 重写；DI 部分则完全基于 Roslyn Source Generator，为 `Microsoft.Extensions.DependencyInjection` 生成显式的 `IServiceCollection` 注册工厂代码，不涉及 Fody 或反射。

两部分相互独立，可以只使用其中一个。

## 功能

MVVM：

- 使用 `[ObservableProperty]` 为可写属性生成属性变更通知逻辑。
- 使用 `[NotifyPropertyChangeFrom]` 将一个或多个属性的变更传播到只读计算属性，并支持缓存计算结果。
- 使用 `CommunityToolkit.Mvvm.Input.RelayCommand` 生成同步或异步命令属性，支持 `CanExecute` 和一个命令参数。
- 使用 `[ObservableAsProperty]` 将 `HKW.MVVM` 的 `ObservableAsPropertyHelper<T>` 暴露为只读属性。

依赖注入：

- 使用 `DIConfigurationBase.Register*` 系列方法在 `Configure` 中标记服务注册，支持瞬态、作用域、单例和延迟单例。
- 使用 `[DICustomServiceRegistrar]` 将自定义泛型注册方法的指定类型参数注册为瞬态服务。
- 继承 `DIConfigurationBase` 声明配置类型，生成器为其生成 `Build()` / `Build(IServiceCollection)`，直接产出面向 `Microsoft.Extensions.DependencyInjection` 的显式工厂代码。
- 使用 `[DIConstructor]` 在多构造函数类型中选择注入使用的构造函数。
- 使用 `[DIProperty]` 标记需要属性注入的属性，支持 `init` 访问器。
- 自动识别构造函数参数中的 `Lazy<T>` 和 `IEnumerable<T>`，分别映射为延迟解析和多实现解析。

## 安装

Roslyn 源生成器面向 `.NET Standard 2.0`，可由 Visual Studio 的编译器宿主加载和调试。DI 轻量 Provider 会作为源码生成到使用方程序集，不会为生成器额外引入 `Microsoft.Bcl.AsyncInterfaces`。在需要使用生成器的应用或类库项目中添加以下包引用。版本号请根据实际发布版本调整：

```xml
<ItemGroup>
  <PackageReference Include="HKW.MVVM" Version="0.1.2" />
  <PackageReference Include="HKW.MVVM.SourceGenerator" Version="0.1.4" />
  <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.2" PrivateAssets="all" />
  <PackageReference Include="Fody" Version="6.9.3" PrivateAssets="all" />
</ItemGroup>
```

在项目根目录添加 `FodyWeavers.xml`，启用本项目的 Fody weaver：

```xml
<Weavers xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation="FodyWeavers.xsd">
  <HKW.MVVM.SourceGenerator />
</Weavers>
```

### 禁用 CommunityToolkit.Mvvm 的源生成器

本项目复用了 `CommunityToolkit.Mvvm` 的特性和运行时类型，但会自行处理相关生成逻辑。为避免两个源生成器重复生成成员，需要在使用项目中移除 CommunityToolkit.Mvvm 的 Source Generator：

```xml
<Target Name="DisableMvvmToolkitSourceGenerators" BeforeTargets="CoreCompile">
  <ItemGroup>
    <_MvvmToolkitAnalyzers
      Include="@(Analyzer)"
      Condition="$([System.String]::Copy('%(Analyzer.Filename)').Contains('CommunityToolkit.Mvvm.SourceGenerators'))" />
    <Analyzer Remove="@(_MvvmToolkitAnalyzers)" />
  </ItemGroup>
</Target>
```

## 快速开始

生成器只处理继承自 `ObservableObject` 且声明为 `partial` 的类：

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HKW.MVVM.SourceGenerator;

public partial class UserViewModel : ObservableObject
{
    [ObservableProperty]
    public string Name { get; set; } = string.Empty;

    [NotifyPropertyChangeFrom(nameof(Name))]
    public bool IsNameValid => !string.IsNullOrWhiteSpace(Name);

    [RelayCommand(CanExecute = nameof(IsNameValid))]
    private void Save()
    {
        // 保存数据
    }
}
```

Roslyn 会为这个类生成一个同名的 `partial` 文件，经 Fody 重编织后生成以下代码。

```csharp
public partial class UserViewModel
{
    private UserViewModelObservableObjectHelper _userViewModelObservableHelper = default!;

    protected UserViewModelObservableObjectHelper UserViewModelObservableHelper =>
        _userViewModelObservableHelper ??= new UserViewModelObservableObjectHelper(this);

    public RelayCommand SaveCommand =>
        UserViewModelObservableHelper._saveCommand
        ?? (UserViewModelObservableHelper._saveCommand =
            new RelayCommand(Save, () => IsNameValid));

    // 此字段由 Fody 生成, 用户无法使用
	private string $Name = string.Empty;

    // Fody 对 IL 进行重编织, 将 get 和 set 重定向到 $PropertyName 字段上
	[ObservableProperty]
	public string Name
	{
		get
		{
			return $Name;
		}
		set
		{
			UserViewModelObservableHelper.SetPropertyName(ref $Name, value);
		}
	}
    partial void OnNameChanging(string oldValue, string newValue, ref bool cancel);
    partial void OnNameChanged(string oldValue, string newValue);

    // 生成一个类型专用 ObservableHelper
    protected sealed partial class UserViewModelObservableObjectHelper
    {
        private readonly UserViewModel _source;
        public RelayCommand _saveCommand = default!;

        public UserViewModelObservableObjectHelper(UserViewModel source)
        {
            _source = source;
        }

        private void NotifyIsNameValidChanging()
        {
            _source.OnPropertyChanging("IsNameValid");
        }

        private void NotifyIsNameValidChanged()
        {
            _source.OnPropertyChanged("IsNameValid");
        }

        public void SetPropertyName(ref string backingField, string newValue)
        {
            if (global::System.Collections.Generic.EqualityComparer<string>.Default
                .Equals(backingField, newValue))
                return;

            var oldValue = backingField;
            _source.OnPropertyChanging("Name");
            var cancel = false;
            _source.OnNameChanging(oldValue, newValue, ref cancel);
            if (cancel)
                return;

            NotifyIsNameValidChanging();
            backingField = newValue;
            _source.OnPropertyChanged("Name");
            _source.OnNameChanged(oldValue, newValue);
            NotifyIsNameValidChanged();
        }
    }
}
```

## 属性变更通知

`[ObservableProperty]` 应用于带有 setter 的属性。对于下面的输入：

```csharp
[ObservableProperty]
public int Age { get; set; }
```

生成器会在 helper 中生成完整的 setter 逻辑：

```csharp
public void SetPropertyAge(ref int backingField, int newValue)
{
    if (EqualityComparer<int>.Default.Equals(backingField, newValue))
        return;

    var oldValue = backingField;
    _source.OnPropertyChanging("Age");
    var cancel = false;
    _source.OnAgeChanging(oldValue, newValue, ref cancel);
    if (cancel)
        return;

    backingField = newValue;
    _source.OnPropertyChanged("Age");
    _source.OnAgeChanged(oldValue, newValue);
}

partial void OnAgeChanging(int oldValue, int newValue, ref bool cancel);
partial void OnAgeChanged(int oldValue, int newValue);
```

Fody 随后把原属性重写为等价的调用：

```csharp
private int $Age;

[ObservableProperty]
public int Age
{
    get
    {
        return $Age;
    }
    set
    {
        UserViewModelObservableHelper.SetPropertyAge(ref $Age, value);
    }
}
```

### 依赖属性通知

`[NotifyPropertyChangeFrom]` 应用于只读属性，并通过参数指定依赖属性。下面的输入：

```csharp
[NotifyPropertyChangeFrom(nameof(FirstName), nameof(LastName))]
public string FullName => $"{FirstName} {LastName}";
```

会为 `FirstName` 和 `LastName` 的变更分别接入以下通知方法：

```csharp
private void NotifyFullNameChanging()
{
    _source.OnPropertyChanging("FullName");
}

private void NotifyFullNameChanged()
{
    _source.OnPropertyChanged("FullName");
}
```

因此，带有 `[ObservableProperty]` 的 `FirstName` setter 的关键部分等价于：

```csharp
// SetPropertyFirstName
_source.OnPropertyChanging("FirstName");
NotifyFullNameChanging();
backingField = newValue;
_source.OnPropertyChanged("FirstName");
NotifyFullNameChanged();
...
// SetPropertyLastName
_source.OnPropertyChanging("LastName");
NotifyFullNameChanging();
backingField = newValue;
_source.OnPropertyChanged("LastName");
NotifyFullNameChanged();
```

`FullName` 的 getter 保持用户定义的计算表达式：

```csharp
public string FullName => $"{FirstName} {LastName}";
```

默认情况下每次读取都会计算表达式。使用缓存模式时，下面的输入：

```csharp
[NotifyPropertyChangeFrom(NotifyPropertyChangeFromCacheMode.Enable, nameof(Name))]
public bool IsNameValid => !string.IsNullOrWhiteSpace(Name);
```

会额外生成缓存字段、计算方法和初始化代码：

```csharp
public bool _isNameValidCache = default!;

private bool GetIsNameValid()
{
    return !string.IsNullOrWhiteSpace(_source.Name);
}

private void NotifyIsNameValidChanged()
{
    _isNameValidCache = GetIsNameValid();
    _source.OnPropertyChanged("IsNameValid");
}

public UserViewModelObservableObjectHelper(UserViewModel source)
{
    _source = source;
    _isNameValidCache = GetIsNameValid();
}
```

Fody 会把原属性 getter 重写为读取缓存字段：

```csharp
public bool IsNameValid => UserViewModelObservableHelper._isNameValidCache;
```

`OnFirstChange` 的生成代码不在 helper 构造函数中初始化缓存，而是在第一次关联属性变更时执行 `GetIsNameValid()` 并写入缓存。

- `Disable`：不缓存，默认模式。
- `Enable`：对象初始化时计算并缓存。
- `OnFirstChange`：第一次关联属性变更时计算并开始缓存。

## 命令生成

`[RelayCommand]` 应用于方法。方法最多只能有一个参数；下面的输入：

```csharp
[RelayCommand]
private void Select(int value)
{
    SelectedValue = value;
}
```

会生成命令字段和公开属性：

```csharp
public RelayCommand<int> _selectCommand = default!;

public RelayCommand<int> SelectCommand => UserViewModelObservableHelper._selectCommand
    ?? (UserViewModelObservableHelper._selectCommand =
        new RelayCommand<int>(x => Select(x)));
```

无参数命令带 `CanExecute` 时：

```csharp
private bool CanRefresh { get; set; }
[RelayCommand(CanExecute = nameof(CanRefresh))]
private async Task RefreshAsync()
{
    await LoadAsync();
}
```

会生成：

```csharp
public AsyncRelayCommand _refreshCommand = default!;

public AsyncRelayCommand RefreshCommand => UserViewModelObservableHelper._refreshCommand
    ?? (UserViewModelObservableHelper._refreshCommand =
        new AsyncRelayCommand(RefreshAsync, () => CanRefresh));
```

## 响应式计算属性

`[ObservableAsProperty]` 适用于只读属性。属性表达式必须创建 `ObservableAsPropertyHelper<T>`，并以 `.Value` 结尾。下面的输入：

```csharp
using HKW.MVVM.SourceGenerator;

[ObservableAsProperty]
public string UpperName =>
    this.WhenAnyValue(x => x.Name)
        .Select(x => x.ToUpperInvariant())
        .ToProperty(this, nameof(UpperName))
        .Value;
```

会生成 helper 字段、初始化方法和构造函数初始化代码：

```csharp
public global::HKW.MVVM.ObservableAsPropertyHelper<string> _upperNameOAPH = default!;

private global::HKW.MVVM.ObservableAsPropertyHelper<string> UpperNameOAPHInitializa()
{
    return _source.WhenAnyValue(x => x.Name)
        .Select(x => x.ToUpperInvariant())
        .ToProperty(_source, nameof(UpperName));
}

public UserViewModelObservableObjectHelper(UserViewModel source)
{
    _source = source;
    _upperNameOAPH = UpperNameOAPHInitializa();
}
```

Fody 会将原属性 getter 重写为读取 helper 的 `Value`：

```csharp
public string UpperName => UserViewModelObservableHelper._upperNameOAPH.Value;
```

### 诊断

| ID | 级别 | 说明 |
|---|---|---|
| `HKWMVVM0001` | Error | 类型继承了 `ObservableObject`，但没有声明为 `partial` |
| `HKWMVVM0002` | Error | `[ObservableProperty]` 标注的属性没有 setter |
| `HKWMVVM0003` | Error | `[NotifyPropertyChangeFrom]` 或 `[ObservableAsProperty]` 标注的属性带有 setter |
| `HKWMVVM0004` | Error | `[RelayCommand]` 标注的方法参数个数超过 1 个 |

## 依赖注入生成

`DIGenerator` 是独立于 MVVM 部分的源生成器，基于标记 API 收集当前程序集内的服务注册声明，并为每个直接继承 `DIConfigurationBase` 的类型生成显式的 `Microsoft.Extensions.DependencyInjection.IServiceCollection` 注册代码。整个过程只产生普通 C# 代码，不依赖反射，也不经过 Fody。

### 声明配置

```csharp
using HKW.MVVM.SourceGenerator;
using Microsoft.Extensions.DependencyInjection;

public partial class AppServices : DIConfigurationBase
{
    protected override void Configure(IServiceCollection services)
    {
        Register<ILogger, ConsoleLogger>();
        RegisterScoped<IRequestContext, RequestContext>();
        RegisterLazySingleton<ICache, MemoryCache>();
    }
}
```

配置类型必须是：

- 直接继承 `DIConfigurationBase`；
- 顶层类型（不能是嵌套类型）；
- 非抽象的 `partial` 类；
- 非泛型；
- 具有 `public` 或 `internal` 无参数构造函数。

不满足以上任意一点会触发 `DI006`。

生成器会重写注册方法：

```csharp
public partial class AppServices
{
    public override IServiceCollection Build(IServiceCollection services);
    public override DIServiceProvider BuildServiceProvider();
    public override DIServiceProvider BuildServiceProvider(ServiceProviderOptions options);
}
```

创建配置实例后，基类的 `Build()` 会创建新的 `ServiceCollection`；`Build(IServiceCollection)` 接受一个已存在的服务集合，写入注册后返回同一个实例，便于和其他注册来源组合：

```csharp
var configuration = new AppServices();
var services = configuration.Build();
// 或者传入自定义的服务集合
var services = new ServiceCollection();
configuration.Build(services);
```

也可以跳过 Microsoft DI 的运行时容器，直接创建源生成的轻量 Provider：

```csharp
using var provider = new AppServices().BuildServiceProvider();
var logger = provider.GetRequiredService<ILogger>();

using var scope = provider.CreateScope();
var requestContext = scope.ServiceProvider.GetRequiredService<IRequestContext>();
```

`BuildServiceProvider()` 会复用 `Build(IServiceCollection)` 创建服务集合，并执行一次 `Configure(IServiceCollection)`，因此其中的普通语句和自定义注册方法副作用仍会发生。轻量 Provider 包含生成器识别到的 `Register*` 注册，也接受通过工厂方法添加的手动注册：

```csharp
services.AddSingleton<IClock>(_ => new SystemClock());
services.AddScoped<IHandler>(provider =>
    new Handler(provider.GetRequiredService<IClock>()));
```

`BuildServiceProvider()` 会将 `Build(IServiceCollection)` 收集到的手动工厂和源生成工厂统一交给 sealed `DIServiceProvider`；服务类型通过预构建索引查询。`DIServiceProvider` 会在创建时检查每个描述符是否为非 keyed、闭合服务类型且具有 `ImplementationFactory`。工厂沿用 Transient、Scoped 或 Singleton 生命周期，并可解析其他手动或源生成服务。类型注册（如 `AddSingleton<TService, TImplementation>()`）、实例注册和 keyed service 不受支持；遇到这些描述符时会立即抛出 `InvalidOperationException`。原有 `Build()` 仍可与 Microsoft DI 配合以支持全部描述符形式。

可通过本库的 `ServiceProviderOptions` 启用与 Microsoft Provider 对应的验证：

```csharp
using var provider = new AppServices().BuildServiceProvider(
    new HKW.MVVM.SourceGenerator.ServiceProviderOptions
    {
        ValidateScopes = true,
        ValidateOnBuild = true,
    });
```

`ValidateScopes` 禁止从根 Provider 解析 Scoped 服务，也会阻止 Singleton 直接或间接捕获 Scoped 服务。`ValidateOnBuild` 在创建 Provider 时验证生成器静态已知的依赖图，并聚合错误，但不会实例化服务或执行用户工厂。无法静态分析的工厂依赖仍在实际解析时验证。

无参数的 `Register*` 方法只是编译期标记，本身不执行任何操作。生成器只收集 `Configure(IServiceCollection)` 方法中的直接 `Register*` 调用。标准 `AddTransient`、`AddScoped` 和 `AddSingleton` 工厂由 `Build(IServiceCollection)` 在运行时收集，生成器不分析其 lambda 或依赖关系，因此它们不进入 `ValidateOnBuild` 的静态依赖图；相关错误会在实际解析服务时暴露。

自定义泛型方法可以使用 `[DICustomServiceRegistrar]` 指定需要注册的泛型参数。生成器会分析该方法在 `Configure` 中的闭合泛型调用，并按指定参数生成瞬态注册；方法体仍会在 `Configure` 执行时正常运行：

```csharp
protected override void Configure(IServiceCollection services)
{
    RegisterMVVM<MainViewModel, MainView>();
}

[DICustomServiceRegistrar(nameof(TViewModel))]
private void RegisterMVVM<TViewModel, TView>()
    where TViewModel : class
    where TView : Control, new()
{
    ViewLocator.Register<TViewModel, TView>();
    Register<TViewModel>(); // 不会被重复收集
}
```

`DICustomServiceRegistrarAttribute` 接受一个或多个泛型参数名称。指定的参数必须在 `Configure` 调用处闭合为可注册的实现类型。仅传名称时默认使用 `Normal`（瞬态）注册；也可以为每个名称单独指定注册方式：

```csharp
[DICustomServiceRegistrar(
    nameof(TViewModel), DIServiceRegistration.Scoped,
    nameof(TCache), DIServiceRegistration.LazySingleton
)]
private void RegisterMVVM<TViewModel, TCache>()
    where TViewModel : class
    where TCache : class { }
```

特性参数不支持元组常量，因此该重载以 `泛型名, 注册方式` 交替成对传入。可用方式为 `Normal`、`Scoped`、`Singleton` 和 `LazySingleton`。

### 生命周期

| 标记方法 | 生命周期 | 对应 `IServiceCollection` 扩展 |
|---|---|---|
| `Register<T>()` / `Register<TService, TImplementation>()` | Transient | `AddTransient` |
| `RegisterScoped<T>()` / `RegisterScoped<TService, TImplementation>()` | Scoped | `AddScoped` |
| `RegisterSingleton<T>()` / `RegisterSingleton<TService, TImplementation>()` | Singleton | `AddSingleton` |
| `RegisterLazySingleton<T>()` / `RegisterLazySingleton<TService, TImplementation>()` | Singleton | `AddSingleton` |

单类型重载（如 `Register<T>()`）等价于 `Register<T, T>()`，把 `T` 既作为服务类型也作为实现类型注册。

轻量 Provider 的生命周期规则与上表一致：

- Transient 每次解析创建，并由创建它的根容器或 scope 追踪释放；
- Scoped 在每个 scope 内只创建一次，从根容器直接解析时归根作用域；
- Singleton 和 LazySingleton 都在首次解析时线程安全地创建一次，工厂返回的 `null` 也会被缓存；
- 同一服务类型有多个实现时，单项解析返回最后一个注册，`IEnumerable<T>` 按声明顺序返回全部；显式注册的 `IEnumerable<T>` 优先，任意未注册的闭合 `IEnumerable<T>` 返回空数组；
- Provider 内置提供 `IServiceProvider`、`IServiceScopeFactory` 和 `IServiceProviderIsService`；
- Provider/scope 按创建逆序释放并按引用去重；使用方编译环境提供 `IAsyncDisposable` 时生成 `DisposeAsync()` 并优先异步释放，否则自动降级为仅同步 `IDisposable`；释放失败时仍会继续处理剩余服务并汇总多个异常；
- Provider/scope 释放后继续解析会抛出 `ObjectDisposedException`，释放竞态中新创建的 disposable 会被立即释放，循环依赖会抛出包含依赖链的 `InvalidOperationException`。

### 构造函数选择

- 实现类型只有一个实例构造函数时，直接使用它。
- 有多个实例构造函数时，必须且只能有一个标注 `[DIConstructor]`，否则报 `DI001`。
- 选中的构造函数必须是 `public`、`internal` 或 `protected internal`，否则报 `DI003`。

```csharp
public sealed class Greeter
{
    public Greeter() { }

    [DIConstructor]
    internal Greeter(ILogger logger)
    {
        ...
    }
}
```

### 属性注入

标注 `[DIProperty]` 的属性会在对象初始化器中被赋值：

```csharp
public sealed class Greeter
{
    [DIProperty]
    public ILogger Logger { get; init; } = null!;
}
```

- 只扫描实现类型及其在**当前程序集**内声明的基类；引用其他程序集的基类会被忽略。
- 属性必须具有 `public`、`internal` 或 `protected internal` 的 setter（含 `init`），否则报 `DI002`，且该服务的整个注册都会被跳过。
- 不支持静态属性和索引器。

### 参数解析

构造函数参数和标注属性按以下规则解析：

| 类型 | 生成的解析代码 |
|---|---|
| `T` | `serviceProvider.GetRequiredService<T>()` |
| `Lazy<T>` | `new Lazy<T>(() => serviceProvider.GetRequiredService<T>())` |
| `IEnumerable<T>` | `serviceProvider.GetServices<T>()` |

`Lazy<T>` 和 `IEnumerable<T>` 之外的其他集合类型（如 `List<T>`、数组）按普通服务解析，不做特殊处理。

### 诊断

| ID | 级别 | 说明 |
|---|---|---|
| `DI001` | Error | 实现类型有多个构造函数，但没有一个标注 `[DIConstructor]` |
| `DI002` | Error | 标注 `[DIProperty]` 的属性缺少可访问的 setter |
| `DI003` | Error | 选中的构造函数可访问性低于 `internal` |
| `DI004` | Error | 实现类型不是非抽象的封闭类 |
| `DI005` | Error | 实现类型不能转换为服务类型 |
| `DI006` | Error | `DIConfigurationBase` 派生类型不符合配置类约束 |
| `DI007` | Error | `[DICustomServiceRegistrar]` 指定的泛型名称不是目标方法的类型参数 |
| `DI008` | Error | `[DICustomServiceRegistrar]` 的泛型名称与注册方式数量不一致 |
| `DI009` | Warning | 同一配置中服务类型、实现类型和注册方式完全相同的注册出现多次；仅生成首个注册 |
| `DI010` | Error | 静态已知的依赖图包含循环依赖 |
| `DI011` | Error | Singleton 通过静态已知路径捕获 Scoped 服务 |
| `DI012` | Warning | 静态已知依赖未出现在注册图中，可能需要由 `Configure` 提供 |

`DI010`–`DI012` 同时分析源生成注册和可识别的标准手动工厂注册。依赖不透明的工厂保守降级到运行时验证。

## 使用限制

MVVM：

- 目标类必须继承 `CommunityToolkit.Mvvm.ComponentModel.ObservableObject`，并声明为 `partial`。
- `[ObservableProperty]` 只能用于带 setter 的属性。
- `[NotifyPropertyChangeFrom]` 和 `[ObservableAsProperty]` 只能用于只读属性。
- `[RelayCommand]` 标注的方法最多只能有一个参数。
- 使用 `[ObservableAsProperty]` 时需要引用 `HKW.MVVM`，且属性表达式必须包含 `.ToProperty(...)` 并以 `.Value` 或 `.Value!` 结束。
- 必须移除 `CommunityToolkit.Mvvm` 自带的源生成器，否则可能产生重复成员或编译冲突。

依赖注入：

- 配置类必须直接继承 `DIConfigurationBase`，并满足顶层、非抽象、`partial`、非泛型和可访问无参数构造函数等约束。
- `Register*` 调用只有直接出现在 `Configure(IServiceCollection)` 中才会被收集；自定义泛型注册方法应使用 `[DICustomServiceRegistrar]`。
- 泛型服务/实现类型、开放泛型定义暂不支持。
- 轻量 Provider 只接受具有 `ImplementationFactory` 的非 keyed 手写 `ServiceDescriptor`；类型/实例描述符、keyed service 和开放泛型不受支持，需要这些功能时请继续使用 `Build()` 与 Microsoft DI。
- `ValidateOnBuild` 只验证生成器能够静态描述的依赖，不执行用户工厂；完全不透明的工厂依赖仅能在解析时发现。
- 引用其他程序集的实现类型只有在只有一个可访问构造函数时才能使用；`[DIConstructor]`/`[DIProperty]` 只对当前编译中的类型成员生效。

## 开发与验证

- 仓库包含源生成器、Fody weaver 和测试项目。
- 使用 `dotnet build HKW.MVVM.SourceGenerator` 即可编译完整的程序集。
- 因 Fody 的特殊性，MVVM 部分的单元测试仅能在 Visual Studio 中进行编译测试，无法使用 `dotnet build` 命令编译测试。
- 依赖注入部分不依赖 Fody，可以通过 `dotnet build -p:DisableFody=true` 和 `dotnet test -p:DisableFody=true` 单独编译和运行 DI 相关测试。

## 许可证

[MIT](LICENSE.txt)
