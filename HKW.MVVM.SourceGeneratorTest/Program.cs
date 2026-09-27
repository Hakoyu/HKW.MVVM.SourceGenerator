using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace HKW.MVVM.SourceGeneratorTest;

internal class Program
{
    internal static void Main(string[] args) { }
}

public partial class TestModel : ObservableObject
{
    public TestModel() { }

    [ObservableProperty]
    public string Name { get; set; } = string.Empty;
}
