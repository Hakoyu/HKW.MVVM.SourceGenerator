using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HKW.MVVM;
using HKW.MVVM.SourceGenerator;

namespace HKW.MVVM.SourceGeneratorTest;

// 仅能在VS中进行编译测试, 无法使用 `dotnet build`
internal class Program
{
    internal static void Main(string[] args) { }
}

public partial class TestModel : ObservableObject
{
    [ObservableProperty]
    public string Name { get; set; } = string.Empty;

    [NotifyPropertyChangeFrom(nameof(Name))]
    public bool IsNameValid => string.IsNullOrWhiteSpace(Name) is false;

    [NotifyPropertyChangeFrom(NotifyPropertyChangeFromCacheMode.Enable, nameof(Name))]
    public bool IsNameValidCached => string.IsNullOrWhiteSpace(Name) is false;

    [ObservableAsProperty]
    public string UpperName =>
        this.WhenAnyValue(x => x.Name)
            .Select(x => x.ToUpperInvariant())
            .ToProperty(this, nameof(UpperName))
            .Value;

    public int SaveCount { get; private set; }

    [RelayCommand(CanExecute = nameof(IsNameValid))]
    private void Save()
    {
        SaveCount++;
    }

    public int SelectedValue { get; private set; }

    [RelayCommand]
    private void Select(int value)
    {
        SelectedValue = value;
    }
}
