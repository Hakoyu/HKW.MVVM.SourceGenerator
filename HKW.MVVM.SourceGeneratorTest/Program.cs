using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Windows.Input;
using HKW.HKWReactiveUI;
using ReactiveUI;
using ReactiveUI.Builder;
using ReactiveUI.Primitives;

namespace HKW.HKWReactiveUITest;

internal class Program
{
    static void Main(string[] args)
    {
        //RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();
    }
}

public partial class TestModel : ReactiveObject
{
    public TestModel() { }

    [ReactiveProperty]
    public string Name { get; set; } = string.Empty;
}
