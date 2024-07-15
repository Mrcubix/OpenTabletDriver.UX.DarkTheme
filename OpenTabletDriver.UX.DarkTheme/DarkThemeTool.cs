using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.UX.Controls;
using OpenTabletDriver.UX.Theming;
using OpenTabletDriver.UX.Theming.Extensions;
using Eto.Forms;
using Eto.Drawing;
using OpenTabletDriver.Plugin.Tablet;
using OpenTabletDriver.UX.Controls.Generic.Text;
using OpenTabletDriver.UX.Controls.Output.Area;
using OpenTabletDriver.UX.Controls.Generic;
using OpenTabletDriver.UX.Controls.Bindings;

#pragma warning disable CA2255

namespace OpenTabletDriver.UX.DarkTheme;

[PluginName("Dark Theme")]
public sealed class DarkThemeTool : ITool, IThemeReplacer
{
    public static readonly Type TypeInfo = typeof(DarkThemeTool).GetTypeInfo();
    public static DarkThemeTool? Instance { get; set; }

    #region Event Related Fields

    private MenuItem? _saveItem;
    private MenuItem? _applyItem;

    #endregion

    #region Controls

    private MainForm? _mainForm;
    private MenuBar? _menu;

    // Main Content
    private TabletSwitcherPanel? _content;

    // Tabs
    private TabControl? _tabControl;

    private TrayIcon? _trayIcon;

    #endregion

    #region Initializer

    [ModuleInitializer]
    public static void ModuleInitialize()
    {
        if (Instance != null)
            return;

        var daemonDirectory = AppContext.BaseDirectory;

        // Check if OpenTabletDriver.Daemon is loaded. If it is, then load OpenTabletDriver.UX.Wpf.exe
        /*foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            if (assembly.GetName().Name == "OpenTabletDriver.Daemon")
                Assembly.LoadFrom(Path.Combine(daemonDirectory, "OpenTabletDriver.UX.Wpf.exe"));*/

        try
        {
            Instance = new DarkThemeTool();
        }
        catch (FileNotFoundException)
        {
            return;
        }
        catch (TypeInitializationException)
        {
            return;
        }

        Console.WriteLine("");
        Console.WriteLine("Instance launched");
        Console.WriteLine("");

        _ = Task.Run(StartMonitoring);
    }

    private static void StartMonitoring()
    {
        while (Instance?.Initialized == false)
        {
            Thread.Sleep(100);

            if (App.Current != null)
                Instance.Initialize();
        }
    }

    #endregion

    #region Properties

    public ReadOnlyDictionary<string, Color> ColorResources { get; } = new ReadOnlyDictionary<string, Color>(new Dictionary<string, Color>()
    {
        { "AccentColor", Color.Parse("#272727") },
        { "BodyColor", Color.Parse("#343434") },
        { "BorderColor", Color.Parse("#1C1C1C") },
        { "InputBorderColor", Color.Parse("#292929") },
        { "InputColor", Color.Parse("#4C4C4C") },
        { "SideBarColor", Color.Parse("#2F2F2F") },
        { "TextColor", Color.Parse("#EAEAEA") },
    });

    public bool Initialized { get; set; }

    #endregion

    #region Methods

    public bool Initialize()
    {
        // If App.Current is null at that point, then that probably means this instance is running on the daemon, not the UX
        if (Initialized || App.Current == null)
            return true; // true is returned as false will result in a log message that wouldn't make sense for a UX plugin

        Application.Instance?.Invoke(SubscribeToEvents);

        Initialized = true;

        // We will need to fetch the settings to know if the plugin is enabled or not
        if (IsEnabled())
            Application.Instance?.Invoke(ReplaceTheme);

        return true;
    }

    private void SubscribeToEvents()
    {
        App.Current.PropertyChanged += OnPropertyChanged;

        var instance = Application.Instance;
        _mainForm = instance.MainForm as MainForm;

        if (_mainForm == null)
            return;

        _ = Task.Run(MonitorContentChange);

        App.Driver.Connected += OnSettingsApply;
        App.Driver.TabletsChanged += OnTabletsChanged;

        if (_mainForm.Menu.Items.FirstOrDefault(x => x.Text == "&File") is not ButtonMenuItem fileMenuItems)
            return;

        _saveItem = fileMenuItems.Items.FirstOrDefault(x => x.Shortcut == (Application.Instance.CommonModifier | Keys.S));
        _applyItem = fileMenuItems.Items.FirstOrDefault(x => x.Shortcut == (Application.Instance.CommonModifier | Keys.Enter));

        if (_saveItem == null || _applyItem == null)
            return;

        _saveItem.Click += OnSettingsApply;
        _applyItem.Click += OnSettingsApply;
    }

    private async Task MonitorContentChange()
    {
        if (_mainForm == null)
            return;

        Control? oldContent = null;

        while (true)
        {
            await Task.Delay(30);

            if (_mainForm.Content is TabletSwitcherPanel tabletSwitcher && tabletSwitcher != oldContent)
                OnSettingsApply(App.Current, EventArgs.Empty);

            oldContent = _mainForm.Content;
        }
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(App.Current.Settings):
            case nameof(MainForm.Content) when _mainForm?.Content is TabletSwitcherPanel:
                OnSettingsApply(App.Current, EventArgs.Empty);
                break;
        }
    }

    private void OnSettingsApply(object? sender, EventArgs e)
    {
        if (IsEnabled())
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                Application.Instance?.InvokeAsync(ReplaceTheme);
            });
        }
    }

    private void OnTabletsChanged(object? sender, IEnumerable<TabletReference> e)
    {
        if (IsEnabled())
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(210);
                Application.Instance?.InvokeAsync(ReplaceTheme);
            });
        }
    }

    public void ReplaceTheme()
    {
        var form = Application.Instance.MainForm;

        if (form is not MainForm mainForm || mainForm.Content is not TabletSwitcherPanel)
            return;

        FetchControls(mainForm);

        if (_mainForm != null)
        {
            _mainForm.BackgroundColor = ColorResources["AccentColor"];

            // Recolor labels
            var labels = _mainForm.GetChildren<Label>();

            foreach (var label in labels)
                label.TextColor = ColorResources["TextColor"];
        }

        if (_content != null)
        {
            var tabControl = _content.FindChild<TabControl>();

            if (tabControl != null)
            {
                tabControl.BackgroundColor = ColorResources["BodyColor"];

                foreach (var tab in tabControl.GetChildren<TabPage>())
                {
                    tab.Click -= OnTabClick;
                    tab.Click += OnTabClick;
                    HandleTabClick(tab);
                }

                foreach (var numberBox in tabControl.GetChildren<TextBox>())
                {
                    numberBox.TextColor = ColorResources["TextColor"];
                    numberBox.BackgroundColor = ColorResources["InputColor"];
                }

                foreach (var checkBox in tabControl.GetChildren<CheckBox>())
                    checkBox.TextColor = ColorResources["TextColor"];

                foreach (var areaEditor in tabControl.GetChildren<AreaDisplay>())
                {
                    typeof(AreaDisplay).SetValue(areaEditor, "AreaBoundsFillColor", ColorResources["InputColor"]);
                    var brush = typeof(AreaDisplay).GetStaticValue<SolidBrush>("TextBrush");

                    if (brush != null)
                        brush.Color = ColorResources["TextColor"];
                }

                foreach (var button in tabControl.GetChildren<Button>())
                {
                    button.TextColor = ColorResources["TextColor"];
                    button.BackgroundColor = ColorResources["InputColor"];
                }

                foreach (var listBox in tabControl.GetChildren<ListBox>())
                {
                    listBox.TextColor = ColorResources["TextColor"];
                    listBox.SelectedIndexChanged -= OnPluginSelectionChanged;
                    listBox.SelectedIndexChanged += OnPluginSelectionChanged;
                }

                foreach (var splitter in tabControl.GetChildren<Splitter>())
                    splitter.BackgroundColor = ColorResources["InputColor"];

                foreach (var dropDown in _content.GetChildren<DropDown>())
                {
                    // Some Delay is needed for some reasons, Eto issue
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(15);

                        Application.Instance?.InvokeAsync(() =>
                        {
                            dropDown.BackgroundColor = ColorResources["InputColor"];
                            dropDown.TextColor = ColorResources["TextColor"];
                        });
                    });
                }
            }
        }
    }

    private void OnTabClick(object? sender, EventArgs e)
    {
        _ = Task.Run(() => ScheduleTabHandling(sender));
    }

    private async Task ScheduleTabHandling(object? sender)
    {
        if (sender is TabPage tab)
        {
            await Task.Delay(15);
            Application.Instance?.InvokeAsync(() => HandleTabClick(tab));
        }
    }

    private void HandleTabClick(TabPage tab)
    {
        if (tab.Content is Panel panel)
            foreach (var control in panel.Children)
                control.BackgroundColor = ColorResources["BodyColor"];

        if (tab.Content is BindingEditor auxEditor)
        {
            auxEditor.ProfileChanged -= OnProfileChanged;
            auxEditor.ProfileChanged += OnProfileChanged;
            OnProfileChanged(auxEditor, EventArgs.Empty);

            foreach (var box in auxEditor.GetChildren<TextBox>())
                box.TextColor = ColorResources["TextColor"];
        }
    }

    private void OnProfileChanged(object? sender, EventArgs e)
    {
        if (sender is BindingEditor auxEditor)
        {
            foreach (var group in auxEditor.GetChildren<GroupBox>())
                group.BackgroundColor = ColorResources["AccentColor"];

            foreach (var box in auxEditor.GetChildren<TextBox>())
                box.TextColor = ColorResources["TextColor"];
        }
    }

    private void OnPluginSelectionChanged(object? sender, EventArgs e)
    {
        _ = Task.Run(() => SchedulePluginhandling(sender, e));
    }

    private async Task SchedulePluginhandling(object? sender, EventArgs e)
    {
        if (sender is ListBox listBox)
        {
            await Task.Delay(15);

            var pageRoot = listBox.Parent.Parent;
            Application.Instance?.InvokeAsync(() => HandlePluginPanel(pageRoot));
        }
    }

    private void HandlePluginPanel(Control? panel)
    {
        if (panel is Splitter splitter)
        {
            // Panel 2 contains the ToggleablePluginSettingStoreEditor
            if (splitter.Panel2 is Panel panelToFix)
            {
                // Change color of Toggle Checkbox Text, containing the plugin's name
                var toggleCheckBox = panelToFix.FindChild<CheckBox>();

                if (toggleCheckBox != null)
                    toggleCheckBox.TextColor = ColorResources["TextColor"];

                // Change color of various part of the properties
                foreach (var group in panelToFix.GetChildren<Group>())
                {
                    group.BackgroundColor = ColorResources["AccentColor"];

                    foreach (var checkBox in group.GetChildren<CheckBox>())
                        checkBox.TextColor = ColorResources["TextColor"];

                    foreach (var textBox in group.GetChildren<TextBox>())
                    {
                        textBox.BackgroundColor = ColorResources["InputColor"];
                        textBox.TextColor = ColorResources["TextColor"];
                    }

                    foreach (var label in group.GetChildren<Label>())
                        label.TextColor = ColorResources["TextColor"];

                    foreach (var textBox in group.GetChildren<DropDown>())
                    {
                        textBox.BackgroundColor = ColorResources["InputColor"];
                        textBox.TextColor = ColorResources["TextColor"];
                    }
                }

                foreach (var group in panelToFix.GetChildren<GroupBox>())
                    group.BackgroundColor = ColorResources["AccentColor"];
            }
        }
    }

    private void FetchControls(MainForm mainForm)
    {
        _mainForm = mainForm;
        _menu = mainForm.Menu;
        _content = mainForm.Content as TabletSwitcherPanel;
        _tabControl = _content?.FindChild<TabControl>();
        _trayIcon = typeof(MainForm).GetValue<TrayIcon>(mainForm, "trayIcon");
    }

    private static bool IsEnabled()
    {
        if (App.Current == null || App.Current.Settings == null)
            return false;

        var settings = App.Current.Settings;
        var store = settings.Tools.FirstOrDefault(x => x.Path == TypeInfo?.FullName);

        return store != null && store.Enable == true;
    }

    #endregion

    #region Disposal

    public void Dispose()
    {
        DisposeEventHandlers();
    }

    private void DisposeEventHandlers()
    {
        if (App.Current != null)
            App.Current.PropertyChanged -= OnPropertyChanged;

        if (_saveItem != null && _applyItem != null)
        {
            _saveItem.Click -= OnSettingsApply;
            _applyItem.Click -= OnSettingsApply;
        }

        if (App.Driver != null)
        {
            App.Driver.Connected -= OnSettingsApply;
            App.Driver.TabletsChanged -= OnTabletsChanged;
        }
    }

    #endregion
}
