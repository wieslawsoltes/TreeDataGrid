using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace TreeDataGridUnoActivityMonitor.Droid;

[Activity(
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden | ConfigChanges.UiMode | ConfigChanges.Density,
    ScreenOrientation = ScreenOrientation.Unspecified,
    WindowSoftInputMode = SoftInput.AdjustNothing | SoftInput.StateHidden
)]
public class MainActivity : Microsoft.UI.Xaml.ApplicationActivity
{
}
