using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TreeDataGridUnoSamples;

namespace TreeDataGridUnoSample;

public partial class App
{
    // A fresh native process per suite prevents unrelated fixtures from sharing
    // factory, template, focus, selection or viewport state. The ordinary --smoke
    // route still exercises the full sequential integration workload.
    private async Task<bool> TryRunSelectedSuiteAsync(MainPage page)
    {
        var arguments = SampleRunContext.Arguments;
        var option = Array.IndexOf(arguments, "--suite");
        if (option < 0) return false;
        if (option + 1 >= arguments.Length) throw new ArgumentException("--suite requires a suite name.");
        var name = arguments[option + 1];
        var stopwatch = Stopwatch.StartNew();
        await RunSelectedSuiteAsync(page, name);
        Console.WriteLine($"UNO_SUITE_PASSED: {name}; elapsedMs={stopwatch.Elapsed.TotalMilliseconds:F3}");
        SampleRunContext.ReportResult(true);
        CompleteNativeValidation();
        return true;
    }

    private Task RunSelectedSuiteAsync(MainPage page, string name)
    {
        DataTemplate Template(string key) => (DataTemplate)page.Resources[key];
        ControlTemplate ControlTemplate(string key) => (ControlTemplate)page.Resources[key];
        switch (name)
        {
            case "showcase": return ShowcaseRuntimeChecks.RunAsync(page, CaptureAsync);
            case "wikipedia": return WikipediaRuntimeChecks.RunAsync(page, CaptureAsync);
            case "image-completion": return WikipediaImageCompletionRuntimeChecks.RunAsync();
            case "files-find": return FilesAndFindRuntimeChecks.RunAsync(page, CaptureAsync);
            case "recycling": return RuntimeChecks.RunAsync(page.Grid, Template("RuntimeCellTemplate"));
            case "viewport-measurement": return ViewportMeasurementRuntimeChecks.RunAsync(page.Grid);
            case "committed-extent": return CommittedExtentRuntimeChecks.RunAsync(page);
            case "drag-info":
                DragInfoRuntimeChecks.Run();
                return Task.CompletedTask;
            case "cross-column-recycling": return CrossColumnRecyclingRuntimeChecks.RunAsync(page.Grid);
            case "row-recycling-visibility": return RowRecyclingVisibilityRuntimeChecks.RunAsync(page.Grid);
            case "layout-recycling": return LayoutRecyclingRuntimeChecks.RunAsync(page.Grid);
            case "presentation-pool": return PresentationPoolRuntimeChecks.RunAsync(page.Grid);
            case "text-template-context": return TextTemplateContextRuntimeChecks.RunAsync(page.Grid);
            case "cached-state":
                CachedStateRuntimeChecks.Run();
                return Task.CompletedTask;
            case "selection": return SelectionRuntimeChecks.RunAsync(page.Grid, ControlTemplate("AlternateGridTemplate"));
            case "selection-interaction": return SelectionInteractionRuntimeChecks.RunAsync(page);
            case "presentation-selection-hooks": return ParityContractRuntimeChecks.RunSelectionHooksAsync(page);
            case "column-selection": return ColumnSelectionRuntimeChecks.RunAsync(page);
            case "mutable-text-options": return ParityContractRuntimeChecks.RunTextOptionsAsync(page);
            case "focus": return FocusRuntimeChecks.RunAsync(page);
            case "editing": return EditingRuntimeChecks.RunAsync(page.Grid, Template("RuntimeCellTemplate"), Template("RuntimeEditingTemplate"));
            case "edit-start-reentrancy": return EditStartReentrancyRuntimeChecks.RunAsync(page);
            case "edit-completion-reentrancy": return EditCompletionReentrancyRuntimeChecks.RunAsync(page);
            case "cell-lifecycle": return CellLifecycleRuntimeChecks.RunAsync(page.Grid);
            case "cell-retirement":
                CellRetirementRuntimeChecks.Run();
                return Task.CompletedTask;
            case "cell-rendering": return CellRenderingRuntimeChecks.RunAsync(page);
            case "cell-scalar-reentrancy":
                CellScalarReentrancyRuntimeChecks.Run();
                return Task.CompletedTask;
            case "binding-write-contract":
                BindingWriteContractRuntimeChecks.Run();
                return Task.CompletedTask;
            case "binding-subscription-lifetime":
                BindingSubscriptionLifetimeChecks.RunAll();
                return Task.CompletedTask;
            case "typed-binding": return TypedBindingRuntimeChecks.RunAsync(page);
            case "hierarchy-ownership":
                HierarchyOwnershipRuntimeChecks.Run();
                return Task.CompletedTask;
            case "presentation-options": return PresentationOptionsRuntimeChecks.RunAsync(page.Grid);
            case "custom-column-base": return CellColumnBaseRuntimeChecks.RunAsync(page.Grid);
            case "value-column-base": return ValueColumnBaseRuntimeChecks.RunAsync(page.Grid);
            case "column-compatibility": return ColumnCompatibilityRuntimeChecks.RunAsync(page.Grid, Template("RuntimeCellTemplate"), Template("RuntimeEditingTemplate"));
            case "source-extensions": return SourceExtensionsRuntimeChecks.RunAsync(page.Grid, Template("RuntimeCellTemplate"), Template("RuntimeEditingTemplate"));
            case "declarative": return DeclarativeRuntimeChecks.RunAsync(page);
            case "binding-lifetime": return BindingLifetimeRuntimeChecks.RunAsync(page.Grid, Template("RuntimeCellTemplate"));
            case "source-compatibility": return SourceCompatibilityRuntimeChecks.RunAsync(page.Grid);
            case "automation": return AutomationRuntimeChecks.RunAsync(page.Grid);
            case "text-search": return TextSearchRuntimeChecks.RunAsync(page);
            case "appearance": return AppearanceRuntimeChecks.RunAsync(page);
            case "element-factory": return ElementFactoryRuntimeChecks.RunAsync(page.Grid);
            case "standalone-cell":
                StandaloneCellRuntimeChecks.Run(Template("RuntimeCellTemplate"));
                return Task.CompletedTask;
            case "public-expander": return PublicExpanderRuntimeChecks.RunAsync(page);
            case "standalone-row": return StandaloneRowRuntimeChecks.RunAsync(page);
            case "row-lifetime":
                RowLifetimeRuntimeChecks.Run();
                return Task.CompletedTask;
            case "row-construction":
                RowConstructionRuntimeChecks.Run();
                return Task.CompletedTask;
            case "generic-presenter": return ReviewRuntimeDiagnostics.RunGenericPresenterAsync(page);
            case "bring-into-view": return BringIntoViewRuntimeChecks.RunAsync(page);
            case "native-layout-recovery":
                NativeLayoutRecoveryRuntimeChecks.Run();
                return Task.CompletedTask;
            case "specialized-cell": return SpecializedCellRuntimeChecks.RunAsync(page.Grid, Template("RuntimeCellTemplate"),
                Template("RuntimeEditingTemplate"), ControlTemplate("CompatibleTextCellTemplate"), ControlTemplate("CompatibleTemplateCellTemplate"));
            case "expander-factory": return ExpanderFactoryRuntimeChecks.RunAsync(page.Grid, Template("RuntimeCellTemplate"), Template("RuntimeEditingTemplate"));
            case "custom-reuse": return CustomReuseRuntimeChecks.RunAsync(page.Grid);
            case "column-sizing": return ColumnSizingRuntimeChecks.RunAsync(page.Grid);
            case "row-sizing": return RowSizingRuntimeChecks.RunAsync(page.Grid, Template("WrappingTemplate"));
            case "viewport-cache": return ViewportCacheRuntimeChecks.RunAsync(page.Grid);
            default: throw new ArgumentException($"Unknown native validation suite '{name}'.", nameof(name));
        }
    }
}
