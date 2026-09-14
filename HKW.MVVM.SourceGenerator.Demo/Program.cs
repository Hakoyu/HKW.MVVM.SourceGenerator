using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HKW.MVVM.SourceGenerator;

namespace HKW.MVVM.Demo;

internal class Program
{
    static void Main(string[] args)
    {
        var tm = new TestModel();
        tm.PropertyChanged += Tm_PropertyChanged;
        tm.Name = "114";
        tm.Name = "514";
    }

    private static void Tm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Console.WriteLine(e.PropertyName);
    }
}

public partial class TestModel : ObservableObject
{
    public TestModel()
    {
        //var oaph = this.WhenAnyValue(x => x.FirstName).ToProperty(this, x => x.FullName);
        //Method1Command.Execute(1);
        //Method2Command.Execute(1);
        //Method3Command.Execute(1);
        //Method4Command.Execute(1);
        //Method5Command.Execute(1);
        //Method6Command.Execute(1);
        //Task.Delay(10000).Wait();
    }

    [ObservableAsProperty]
    public string Name1 => this.WhenAnyValue(x => x.Name).ToProperty(this, nameof(Name1)).Value!;

    [ObservableProperty]
    public string Name { get; set; } = string.Empty;

    //[ObservableProperty]
    //public string FirstName { get; set; } = string.Empty;

    //[ObservableProperty]
    //public string LastName { get; set; } = string.Empty;

    //[NotifyPropertyChangeFrom(nameof(FirstName), nameof(LastName))]
    //public string FullName => $"{FirstName} {LastName}";

    //[ObservableProperty]
    //public bool CanExecute { get; set; }

    //[RelayCommand(CanExecute = nameof(CanExecute))]
    //private void Method1()
    //{
    //    Console.WriteLine(nameof(Method1));
    //}

    //[RelayCommand]
    //private void Method2(int i)
    //{
    //    Console.WriteLine(nameof(Method2));
    //}

    //[RelayCommand]
    //private int Method3(int i)
    //{
    //    Console.WriteLine(nameof(Method3));
    //    return i;
    //}

    //[RelayCommand]
    //private async Task Method4()
    //{
    //    Console.WriteLine(nameof(Method4));
    //    await Task.Delay(100);
    //}

    //[RelayCommand]
    //private async Task Method5(int i)
    //{
    //    Console.WriteLine(nameof(Method5));
    //    await Task.Delay(100);
    //}

    //[RelayCommand]
    //private async Task<int> Method6(int i)
    //{
    //    Console.WriteLine(nameof(Method6));
    //    await Task.Delay(100);
    //    return i;
    //}
}

/// <summary>
/// 可观察点
/// </summary>
/// <typeparam name="T">数据类型</typeparam>
[DebuggerDisplay("({X}, {Y})")]
internal partial class ObservablePoint<T> : ObservableObject
    where T : struct, INumber<T>
{
    /// <inheritdoc/>
    public ObservablePoint() { }

    /// <inheritdoc/>
    /// <param name="x">坐标X</param>
    /// <param name="y">坐标Y</param>
    public ObservablePoint(T x, T y)
    {
        X = x;
        Y = y;
    }

    /// <inheritdoc/>
    [ObservableProperty]
    public T X { get; set; }

    /// <inheritdoc/>
    [ObservableProperty]
    public T Y { get; set; }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"X = {X}, Y = {Y}";
    }

    partial class ObservablePointObservableObjectHelper
    {
        partial void OnXChanging(T oldValue, T newValue, ref bool cancel)
        {
            return;
        }
    }
}
