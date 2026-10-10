using HKW.SourceGeneratorUtils;
using Microsoft.CodeAnalysis;

namespace HKW.MVVM.SourceGenerator;

internal sealed class NotifyCanExecuteChangedForGenerator
{
    public static void Generate(ClassInfo classInfo)
    {
        foreach (var property in classInfo.PropertyByName.Values)
        {
#pragma warning disable S3267
            foreach (
                var att in property
                    .GetAttributes()
                    .Where(x =>
                        x.AttributeClass!.IsName(MVVMGenerator.NotifyCanExecuteChangedForAttribute)
                    )
            )
            {
                var diagnostic = Diagnostic.Create(
                    MVVMDescriptors.NotifyCanExecuteChangedForWarning,
                    att.ApplicationSyntaxReference?.SyntaxTree.GetLocation(
                        att.ApplicationSyntaxReference.Span
                    ),
                    property.Name
                );
                classInfo.ProductionContext.ReportDiagnostic(diagnostic);
            }
#pragma warning restore S3267
        }
    }
}
