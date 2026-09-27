# HKW.MVVM.SourceGenerator

基于 `CommunityToolkit.Mvvm` 的 MVVM 源代码生成器。项目结合 Roslyn Source Generator 与 Fody，在编译期间为 `ObservableObject` 派生类生成属性通知、命令和响应式计算属性相关代码，并在程序集构建阶段完成必要的 IL 重写。

## 功能

- 使用 `[ObservableProperty]` 为可写属性生成属性变更通知逻辑。
- 使用 `[NotifyPropertyChangeFrom]` 将一个或多个属性的变更传播到只读计算属性，并支持缓存计算结果。
- 使用 `CommunityToolkit.Mvvm.Input.RelayCommand` 生成同步或异步命令属性，支持 `CanExecute` 和一个命令参数。
- 使用 `[ObservableAsProperty]` 将 `HKW.MVVM` 的 `ObservableAsPropertyHelper<T>` 暴露为只读属性。

## 安装

项目目标框架为 `.NET Standard 2.0`。在需要使用生成器的应用或类库项目中添加以下包引用。版本号请根据实际发布版本调整：

```xml
<ItemGroup>
  <PackageReference Include="HKW.MVVM" Version="0.1.2" />
  <PackageReference Include="HKW.MVVM.SourceGenerator" Version="0.1.3" />
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
            OnNameChanging(oldValue, newValue, ref cancel);
            if (cancel)
                return;

            NotifyIsNameValidChanging();
            backingField = newValue;
            _source.OnPropertyChanged("Name");
            OnNameChanged(oldValue, newValue);
            NotifyIsNameValidChanged();
        }

        partial void OnNameChanging(string oldValue, string newValue, ref bool cancel);
        partial void OnNameChanged(string oldValue, string newValue);
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
    OnAgeChanging(oldValue, newValue, ref cancel);
    if (cancel)
        return;

    backingField = newValue;
    _source.OnPropertyChanged("Age");
    OnAgeChanged(oldValue, newValue);
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

## 使用限制

- 目标类必须继承 `CommunityToolkit.Mvvm.ComponentModel.ObservableObject`，并声明为 `partial`。
- `[ObservableProperty]` 只能用于带 setter 的属性。
- `[NotifyPropertyChangeFrom]` 和 `[ObservableAsProperty]` 只能用于只读属性。
- `[RelayCommand]` 标注的方法最多只能有一个参数。
- 使用 `[ObservableAsProperty]` 时需要引用 `HKW.MVVM`，且属性表达式必须包含 `.ToProperty(...)` 并以 `.Value` 或 `.Value!` 结束。
- 必须移除 `CommunityToolkit.Mvvm` 自带的源生成器，否则可能产生重复成员或编译冲突。

## 开发与验证

- 仓库包含源生成器、Fody weaver 和测试项目。
- 使用 `dotnet build HKW.MVVM.SourceGenerator` 即可编译完整的程序集。
- 因 Fody 的特殊性，此项目的单元测试仅能在 VisualStudio 中进行编译测试, 无法使用 `dotnet build` 命令编译测试。

## 许可证

[MIT](LICENSE.txt)