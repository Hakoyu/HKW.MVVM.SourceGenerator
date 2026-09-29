using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HKW.MVVM;
using HKW.MVVM.SourceGenerator;

namespace HKW.MVVM.SourceGeneratorTest;

[TestClass]
public sealed class MVVMGeneratorTests
{
    [TestMethod]
    public void ObservableProperty_UpdatesValueAndRaisesPropertyChanged()
    {
        var model = new TestModel();
        var changes = new List<string?>();
        model.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

        model.Name = "Alice";

        Assert.AreEqual("Alice", model.Name);
        CollectionAssert.Contains(changes, nameof(TestModel.Name));
        CollectionAssert.Contains(changes, nameof(TestModel.IsNameValid));
    }

    [TestMethod]
    public void ObservableProperty_DoesNotRaiseChangeForEqualValue()
    {
        var model = new TestModel { Name = "Alice" };
        var changeCount = 0;
        model.PropertyChanged += (_, _) => changeCount++;

        model.Name = "Alice";

        Assert.AreEqual(0, changeCount);
    }

    [TestMethod]
    public void RelayCommand_ExecutesMethodAndHonorsCanExecute()
    {
        var model = new TestModel();
        var command = model.SaveCommand;

        Assert.IsFalse(command.CanExecute(null));
        model.Name = "Alice";
        Assert.IsTrue(command.CanExecute(null));

        command.Execute(null);

        Assert.AreEqual(1, model.SaveCount);
        model.Name = string.Empty;
        Assert.IsFalse(command.CanExecute(null));
    }

    [TestMethod]
    public void RelayCommand_WithParameterPassesValueToMethod()
    {
        var model = new TestModel();

        Assert.IsTrue(model.SelectCommand.CanExecute(42));
        model.SelectCommand.Execute(42);

        Assert.AreEqual(42, model.SelectedValue);
    }

    [TestMethod]
    public void NotifyPropertyChangeFrom_RaisesChangeForDerivedProperty()
    {
        var model = new TestModel();
        var changes = new List<string?>();
        model.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

        model.Name = "Alice";

        CollectionAssert.Contains(changes, nameof(TestModel.IsNameValid));
        Assert.IsTrue(model.IsNameValid);
    }

    [TestMethod]
    public void NotifyPropertyChangeFrom_WithCacheEnableUsesInitialAndUpdatedValue()
    {
        var model = new TestModel();
        var changes = new List<string?>();
        model.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

        Assert.IsFalse(model.IsNameValidCacheEnable);
        model.Name = "Alice";

        Assert.IsTrue(model.IsNameValidCacheEnable);
        CollectionAssert.Contains(changes, nameof(TestModel.IsNameValidCacheEnable));
    }

    [TestMethod]
    public void NotifyPropertyChangeFrom_WithCacheOnFirstChangeUsesInitialAndUpdatedValue()
    {
        var model = new TestModel();
        var changes = new List<string?>();
        model.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

        Assert.IsFalse(model.IsNameValidCacheOnFirstChange);
        model.Name = "Alice";

        Assert.IsTrue(model.IsNameValidCacheOnFirstChange);
        CollectionAssert.Contains(changes, nameof(TestModel.IsNameValidCacheEnable));
    }

    [TestMethod]
    public void ObservableAsProperty_TracksObservableValue()
    {
        var model = new TestModel();
        var changes = new List<string?>();
        model.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

        Assert.AreEqual(string.Empty, model.UpperName);
        model.Name = "Alice";

        Assert.AreEqual("ALICE", model.UpperName);
        CollectionAssert.Contains(changes, nameof(TestModel.UpperName));
    }
}

public partial class TestModel : ObservableObject
{
    [ObservableProperty]
    public string Name { get; set; } = string.Empty;

    [NotifyPropertyChangeFrom(nameof(Name))]
    public bool IsNameValid => string.IsNullOrWhiteSpace(Name) is false;

    [NotifyPropertyChangeFrom(NotifyPropertyChangeFromCacheMode.Enable, nameof(Name))]
    public bool IsNameValidCacheEnable => string.IsNullOrWhiteSpace(Name) is false;

    [NotifyPropertyChangeFrom(NotifyPropertyChangeFromCacheMode.OnFirstChange, nameof(Name))]
    public bool IsNameValidCacheOnFirstChange => string.IsNullOrWhiteSpace(Name) is false;

    [ObservableAsProperty]
    public string UpperName =>
        this.WhenAnyValue(x => x.Name)
            .Select(x => x.ToUpperInvariant())
            .ToProperty(this, nameof(UpperName))
            .Value;

    public int SaveCount { get; private set; }

    [RelayCommand(CanExecute = nameof(IsNameValid))]
    private void Save() => SaveCount++;

    public int SelectedValue { get; private set; }

    [RelayCommand]
    private void Select(int value) => SelectedValue = value;
}
