using System.Collections.Concurrent;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Markup;

namespace F1Telemetry.Tests;

/// <summary>
/// Ensures a WPF <see cref="Application"/> instance exists with the merged
/// resource dictionaries from <c>Styles/</c> so that <c>StaticResource</c>
/// lookups succeed in unit-test contexts.
/// </summary>
/// <remarks>
/// Call <c>WpfApplicationHelper.EnsureApplication()</c> before instantiating
/// any WPF element that depends on shared styles. WPF allows one Application
/// per AppDomain; once created, subsequent calls are no-ops.
/// </remarks>
internal static class WpfApplicationHelper
{
    private static readonly object _lock = new();
    private static readonly object _threadLock = new();
    private static readonly (string FileName, string SentinelKey)[] StyleDictionaries =
    {
        ("ThemeColors.xaml", "BgDeepBrush"),
        ("Spacing.xaml", "CardPadding"),
        ("SharedStyles.xaml", "GlassCardStyle"),
        ("ShellStyles.xaml", "ShellNavigationTemplateSelector"),
        ("ScrollBarStyles.xaml", "AppScrollBarTrackBrush"),
    };
    private static BlockingCollection<Action>? _actions;
    private static Thread? _thread;
    private static int _threadId;

    /// <summary>
    /// Runs WPF test code on a shared long-lived STA thread.
    /// </summary>
    public static void RunOnStaThread(Action action)
    {
        if (Thread.CurrentThread.ManagedThreadId == Volatile.Read(ref _threadId))
        {
            EnsureApplication();
            action();
            return;
        }

        EnsureStaThread();

        Exception? capturedException = null;
        using var completed = new ManualResetEventSlim();
        _actions!.Add(() =>
        {
            try
            {
                EnsureApplication();
                action();
            }
            catch (Exception ex)
            {
                capturedException = ex;
            }
            finally
            {
                completed.Set();
            }
        });

        completed.Wait();
        if (capturedException is not null)
            ExceptionDispatchInfo.Capture(capturedException).Throw();
    }

    /// <summary>
    /// Creates or updates the current WPF application with shared test resources.
    /// </summary>
    public static void EnsureApplication()
    {
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            throw new InvalidOperationException("WPF resource tests must run on an STA thread.");

        lock (_lock)
        {
            var app = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            EnsureStyleResources(app.Resources);
        }
    }

    private static void EnsureStaThread()
    {
        lock (_threadLock)
        {
            if (_thread is not null)
                return;

            _actions = new BlockingCollection<Action>();
            _thread = new Thread(() =>
            {
                Volatile.Write(ref _threadId, Thread.CurrentThread.ManagedThreadId);
                foreach (var action in _actions.GetConsumingEnumerable())
                    action();
            })
            {
                IsBackground = true,
                Name = "F1Telemetry.Tests.WpfApplication",
            };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }
    }

    private static void EnsureStyleResources(ResourceDictionary resources)
    {
        var root = FindRepositoryRoot();
        var stylesDir = Path.Combine(root, "F1Telemetry.App", "Styles");
        if (!Directory.Exists(stylesDir))
            throw new DirectoryNotFoundException($"WPF styles directory was not found: {stylesDir}");

        foreach (var (fileName, sentinelKey) in StyleDictionaries)
        {
            if (ContainsResource(resources, sentinelKey))
                continue;

            var path = Path.Combine(stylesDir, fileName);
            if (!File.Exists(path))
                throw new FileNotFoundException($"WPF style resource dictionary was not found: {path}", path);

            try
            {
                var rd = fileName == "ShellStyles.xaml"
                    ? new ResourceDictionary
                    {
                        Source = new Uri("/F1Telemetry.App;component/Styles/ShellStyles.xaml", UriKind.Relative),
                    }
                    : LoadLooseResourceDictionary(path);
                resources.MergedDictionaries.Add(rd);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load WPF style resource dictionary: {path}", ex);
            }
        }
    }

    private static ResourceDictionary LoadLooseResourceDictionary(string path)
    {
        using var reader = System.Xml.XmlReader.Create(path);
        return (ResourceDictionary)XamlReader.Load(reader);
    }

    private static bool ContainsResource(ResourceDictionary resources, string key)
    {
        if (resources.Contains(key))
            return true;

        foreach (var dictionary in resources.MergedDictionaries)
        {
            if (ContainsResource(dictionary, key))
                return true;
        }

        return false;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "F1Telemetry.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }
        return AppContext.BaseDirectory;
    }
}
