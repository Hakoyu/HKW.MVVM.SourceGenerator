using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace HKW.HKWReactiveUITest;

internal class Program
{
    static void Main(string[] args)
    {
        //RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();
    }
}

public partial class TestModel : ObservableObject
{
    public TestModel() { }

    [ObservableProperty]
    public string Name { get; set; } = string.Empty;
}
