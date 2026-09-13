# HKW.MVVM.SourceGenerator

An source generator MVVM library, based on `CommunityToolkit.Mvvm`, using SourceGenerator and Fody for mixed source code generation.

When you use this source generator, you need disable `CommunityToolkit.Mvvm` analyzers assets.
```xml
<ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.2" PrivateAssets="all" />
</ItemGroup>
<Target Name="DisableMvvmToolkitSourceGenerators" BeforeTargets="CoreCompile">
<ItemGroup>
    <_MvvmToolkitAnalyzers
    Include="@(Analyzer)"
    Condition="$([System.String]::Copy('%(Analyzer.Filename)').Contains('CommunityToolkit.Mvvm.SourceGenerators'))"
    />
    <Analyzer Remove="@(_MvvmToolkitAnalyzers)" />
</ItemGroup>
</Target>
```