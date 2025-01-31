using BlazorApplicationInsights;
using Common.Jobs;
using Common.Repositories;
using Common.Services;
using Common.Utils;
using Generation;
using GlobalHotKey;
using H.NotifyIcon.Core;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Photino.NET;
using Quartz;
using Serilog;
using ShellLink;
using SmartComponents.LocalEmbeddings;
using System.Drawing;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Vanara.PInvoke;
using Vanara.Windows.Shell;

internal class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        //ToggleConsole();

        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

        CancellationTokenSource source = new CancellationTokenSource();
        CancellationToken token = source.Token;

        var title = "Tugboat Captain's Playground";
        bool forceClose = false;

        var mainWindow = new PhotinoWindow()
               .Load("http://localhost:8002/")
               .SetTitle(title)
               .SetMaximized(true)
               .RegisterWindowClosingHandler(WindowIsClosing)
               .SetLogVerbosity(5);

        mainWindow.WindowMaximized += (sender, e) =>
        {
            try
            {
                // Get the native window handle
                IntPtr hwnd = ((PhotinoWindow)sender).WindowHandle;

                HideTaskbarIcon(hwnd);
            }
            catch (Exception)
            {

          
            }


      


            return;
        };

        mainWindow.WindowMinimized += (sender, e) =>
        {
            // Get the native window handle
            IntPtr hwnd = ((PhotinoWindow)sender).WindowHandle;

            HideTaskbarIcon(hwnd);

            return;
        };

            // Subscribe to the WindowCreated event to access the native handle
        mainWindow.WindowCreated += (sender, e) =>
        {
            // Get the native window handle
            IntPtr hwnd = ((PhotinoWindow)sender).WindowHandle;

            HideTaskbarIcon(hwnd);

            return;

            // Get current extended window style
            var exStyle = (User32.WindowStylesEx)User32.GetWindowLong(hwnd, User32.WindowLongFlags.GWL_EXSTYLE);

            // Add the WS_EX_TOOLWINDOW style to hide from taskbar and adjust appearance
            exStyle |= User32.WindowStylesEx.WS_EX_TOOLWINDOW;

            // Apply the new extended style
            User32.SetWindowLong(hwnd, User32.WindowLongFlags.GWL_EXSTYLE, (IntPtr)exStyle);

           
        };


        var hotKeyManager = new HotKeyManager();

        var hotKey = hotKeyManager.Register(Key.A, ModifierKeys.Control | ModifierKeys.Shift);

        hotKeyManager.KeyPressed += HotKeyManagerPressed;

        void HotKeyManagerPressed(object sender, KeyPressedEventArgs e)
        {
            ToggleWindow(mainWindow);
            //mainWindow.Load("http://localhost:8002/");
        }

        //return;

        var builder = WebApplication.CreateBuilder();

        var port = false ? 8001 : 8002;

        // Configure the WebHost to set the URLs
        builder.WebHost.ConfigureKestrel((context, options) =>
        {
            options.ListenLocalhost(port);
        });

        builder.Host.UseSerilog((context, services, loggerConfiguration) =>
        {
            loggerConfiguration
                .WriteTo.ApplicationInsights(services.GetRequiredService<TelemetryConfiguration>(), TelemetryConverter.Traces)
                .WriteTo.Console();
        });

        builder.Services.AddAntiforgery();
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddCircuitOptions(o =>
            {
                o.DetailedErrors = true;
            });

        builder.Services.AddCustomServices();

        builder.Services.AddQuartz(q =>
        {
            var provider = builder.Services.BuildServiceProvider();
            var configService = provider.GetRequiredService<IConfigurationService>();
            var config = configService.GetConfig();

            q.ScheduleJob<CreateRepositoriesJobsJob>(trigger => trigger
                .WithIdentity("BuildInfoJob-trigger")
                .WithCronSchedule("0 0 0/4 * * ?"), // Every 4 hours
                job => job.WithIdentity("BuildInfoJob")
            );

            // Schedule hourly runs
            q.ScheduleJob<BackupOracleViewsJob>(trigger => trigger
                .WithIdentity("OracleViewsBackupJob-hourly-trigger")
                .WithCronSchedule("0 0 * * * ?"), // Run every hour
                job => job.WithIdentity("OracleViewsBackupJob-hourly")
            );

            // Schedule hourly runs for ConsulBackupJob
            q.ScheduleJob<BackupConsulJob>(trigger => trigger
                .WithIdentity("ConsulBackupJob-hourly-trigger")
                .WithCronSchedule("0 0 * * * ?"), // Run every hour
                job => job.WithIdentity("ConsulBackupJob-hourly")
            );

            var databasePath = Path.Combine(config.LocalCloneFolder, "Builds.db");
            var connectionString = $"Data Source={databasePath}"; //;Journal Mode=WAL

            q.UsePersistentStore(s =>
            {
                s.UseProperties = true;
                s.UseNewtonsoftJsonSerializer();
                s.Properties["quartz.jobStore.txIsolationLevelSerializable"] = "true";
                s.UseSQLite(connectionString);
            });
        });

        builder.Services.AddQuartzHostedService(q =>
        {
            q.WaitForJobsToComplete = true;
            q.AwaitApplicationStarted = true;
        });

        builder.Services.AddApplicationInsightsTelemetry(opt =>
        {
            opt.ConnectionString = "InstrumentationKey=dc41b1b0-0640-43b1-b968-6e33c1d4463c;IngestionEndpoint=https://eastus-8.in.applicationinsights.azure.com/;LiveEndpoint=https://eastus.livediagnostics.monitor.azure.com/;ApplicationId=7a9dc142-a6d0-4ad7-b6c3-e6af1e56d4ad";
            opt.ApplicationVersion = AutoUpdateService.GetCurrentVersion().ToString();
        });

        builder.Services.AddBlazorApplicationInsights(opt =>
        {
            opt.ConnectionString = "InstrumentationKey=dc41b1b0-0640-43b1-b968-6e33c1d4463c;IngestionEndpoint=https://eastus-8.in.applicationinsights.azure.com/;LiveEndpoint=https://eastus.livediagnostics.monitor.azure.com/;ApplicationId=7a9dc142-a6d0-4ad7-b6c3-e6af1e56d4ad";
            opt.AutoTrackPageVisitTime = true;
        });

        builder.Services.AddApplicationInsightsTelemetryWorkerService(opt =>
        {
            opt.ConnectionString = "InstrumentationKey=dc41b1b0-0640-43b1-b968-6e33c1d4463c;IngestionEndpoint=https://eastus-8.in.applicationinsights.azure.com/;LiveEndpoint=https://eastus.livediagnostics.monitor.azure.com/;ApplicationId=7a9dc142-a6d0-4ad7-b6c3-e6af1e56d4ad";
            opt.ApplicationVersion = AutoUpdateService.GetCurrentVersion().ToString();
        });

        builder.Services.AddStateServices();

        builder.Services.AddSingleton<LocalEmbedder>();

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            app.UseHsts();
        }

        //app.MapStaticAssets();
        app.UseStaticFiles();

        //app.UseStaticFiles(new StaticFileOptions
        //{
        //    FileProvider = new PhysicalFileProvider(Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), "wwwroot")),
        //    RequestPath = ""
        //});

        //var provider = new ManifestEmbeddedFileProvider(Assembly.GetAssembly(type: typeof(Program)), "wwwroot");
        //app.UseStaticFiles(new StaticFileOptions
        //{
        //    FileProvider = provider,
        //    RequestPath = "",
        //});

        app.UseAntiforgery();

        app.MapRazorComponents<TugboatCaptainsPlayground.Components.App>()
            .AddInteractiveServerRenderMode();

        var startupEnabled = IsStartupEnabled();

        bool IsStartupEnabled()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                //string appName = "MyBlazorApp"; // Define your application name
                // Get running exe
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;

                // Get name from path
                var appName = Path.GetFileNameWithoutExtension(exePath);


                string startupFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                string shortcutPath = Path.Combine(startupFolderPath, $"{appName}.lnk");
                return File.Exists(shortcutPath);
            }

            return false;
        }

        void SetStartup(TrayIconWithContextMenu trayIcon, bool enable)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Get running exe
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;

                // Get name from path
                var appName = Path.GetFileNameWithoutExtension(exePath);

                string startupFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                string shortcutPath = Path.Combine(startupFolderPath, $"{appName}.lnk");

                if (enable)
                {
                    CreateStartupShortcut(shortcutPath, exePath);
                    ShowMessage(trayIcon, $"Habilitado");
                }
                else
                {
                    if (File.Exists(shortcutPath))
                    {
                        File.Delete(shortcutPath);
                    }
                    ShowMessage(trayIcon, $"Desabilitado");
                }

                trayIcon.ContextMenu.Items.OfType<PopupMenuItem>().First(m => m.Text == "Enable Startup with Windows").Visible = !enable;
                trayIcon.ContextMenu.Items.OfType<PopupMenuItem>().First(m => m.Text == "Disable Startup with Windows").Visible = enable;
            }
            else
            {
                ShowMessage(trayIcon, $"Startup setting is only supported on Windows.");
            }
        }

        void CreateStartupShortcut(string shortcutPath, string executablePath)
        {
            var shortcut = Shortcut.CreateShortcut(executablePath);
            shortcut.WriteToFile(shortcutPath);
        }

        // Apply migrations at startup
        using (var scope = app.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            try
            {
                var context = services.GetRequiredService<RepositoryDbContext>();
                context.Database.Migrate();
            }
            catch (Exception ex)
            {
                // Log the error
                Console.WriteLine($"An error occurred while migrating the database: {ex.Message}");
                throw;
            }
        }

        using var iconStream = new MemoryStream();
        TugboatCaptainsPlayground.Resource.halloween53_109170.Save(iconStream);
        iconStream.Position = 0;

        using var icon = new Icon(iconStream);
        using var trayIcon = new TrayIconWithContextMenu
        {
            Icon = icon.Handle,
            ToolTip = title
        };

        trayIcon.MessageWindow.SubscribeToMouseEventReceived((s, e) =>
        {

            if (e.MouseEvent == MouseEvent.IconLeftMouseUp)
            {
                ToggleWindow(mainWindow);
            }
        });

        trayIcon.ContextMenu = new PopupMenu();

        // Add the "Open" menu item
        trayIcon.ContextMenu.Items.Add(new PopupMenuItem("Toggle (CTRL + SHIFT + A)", (s, e) => ToggleWindow(mainWindow)));
        //trayIcon.ContextMenu.Items.Add(new PopupMenuItem("Toggle Console", (s, e) => ToggleConsole()));

        // Add the "Exit" menu item
        trayIcon.ContextMenu.Items.Add(new PopupMenuItem("Exit", (s, e) => { forceClose = true; mainWindow.Close(); }/*source.Cancel()*/ /*Environment.Exit(0)*/));

        // Add a separator
        trayIcon.ContextMenu.Items.Add(new PopupMenuSeparator());

        // Add "Enable Startup with Windows" menu item
        trayIcon.ContextMenu.Items.Add(new PopupMenuItem(
            "Enable Startup with Windows",
            (s, e) => SetStartup(trayIcon, true))
        {
            Visible = !startupEnabled
        });

        // Add "Disable Startup with Windows" menu item
        trayIcon.ContextMenu.Items.Add(new PopupMenuItem(
            "Disable Startup with Windows",
            (s, e) => SetStartup(trayIcon, false))
        {
            Visible = startupEnabled
        });

        trayIcon.Create();

        var task = app.RunAsync(token);

        //var mainWindowTask = Task.Run(() => mainWindow.WaitForClose(), token);

        mainWindow.WaitForClose();

        source.Cancel();

        //await task;
        task.GetAwaiter().GetResult();

        hotKeyManager.Dispose();


        static void ShowMessage(TrayIcon trayIcon, string message)
        {
            trayIcon.ShowNotification(
                title: nameof(NotificationIcon.None),
                message: message,
                icon: NotificationIcon.None);
            Console.WriteLine(nameof(trayIcon.ShowNotification));
        }

        bool WindowIsClosing(object sender, EventArgs e)
        {
            if (!forceClose)
            {
                var window = (PhotinoWindow)sender;
                window.Minimized = true;
            }

            return !forceClose;
        }
    }

    private static void ToggleWindow(PhotinoWindow mainWindow)
    {
        var topWindow = WindowUtils.EnumerarJanelas().FirstOrDefault();

        if (topWindow == null || topWindow.ToString() != mainWindow.Title)
        {
            mainWindow.Maximized = true;
            mainWindow.Topmost = true;
            mainWindow.Topmost = false;
        }
        else
        {
            mainWindow.Minimized = true;
        }
    }


    public static void HideTaskbarIcon(IntPtr hWnd)
    {
        try
        {
            TaskbarList.UnregisterTab(hWnd);
        }
        catch (Exception)
        {

        }
    }
   

    private static void ToggleConsole()
    {
        var console = Kernel32.GetConsoleWindow();

        
        HideTaskbarIcon(console.DangerousGetHandle());

        //// Get current extended window style
        //var exStyle = (User32.WindowStylesEx)User32.GetWindowLong(console, User32.WindowLongFlags.GWL_EXSTYLE);

        //// Add the WS_EX_TOOLWINDOW style to hide from taskbar and adjust appearance
        //exStyle |= User32.WindowStylesEx.WS_EX_TOOLWINDOW;

        //// Apply the new extended style
        //User32.SetWindowLong(console, User32.WindowLongFlags.GWL_EXSTYLE, (IntPtr)exStyle);


        //User32.SetForegroundWindow(console);

        //console = User32.GetForegroundWindow();

        //User32.ShowWindow(console, ShowWindowCommand.SW_HIDE);
        //User32.ShowWindow(console, ShowWindowCommand.SW_HIDE);


        //var visivel = User32.IsWindowVisible(console);



        //if (visivel)
        //{
        //    User32.ShowWindow(console, ShowWindowCommand.SW_HIDE);

        //}
        //else
        //{
        //    User32.ShowWindow(console, ShowWindowCommand.SW_SHOW);
        //}
    }

    //private static void ToggleWindow(PhotinoWindow mainWindow)
    //{
    //    IntPtr hwnd = mainWindow.WindowHandle;

    //    if (IsWindowInTopmostLayer(hwnd))
    //    {
    //        // Minimize if in topmost layer or just below topmost
    //        User32.ShowWindow(hwnd, ShowWindowCommand.SW_MINIMIZE);
    //    }
    //    else
    //    {
    //        // Bring to appropriate position
    //        if (User32.IsIconic(hwnd))
    //            User32.ShowWindow(hwnd, ShowWindowCommand.SW_RESTORE);

    //        BringToOptimalPosition(hwnd);
    //    }
    //}

    //private static bool IsWindowInTopmostLayer(IntPtr hwnd)
    //{
    //    // Check if window itself is topmost
    //    var exStyle = (User32.WindowStylesEx)User32.GetWindowLong(hwnd,
    //        User32.WindowLongFlags.GWL_EXSTYLE);
    //    if (exStyle.HasFlag(User32.WindowStylesEx.WS_EX_TOPMOST))
    //        return true;

    //    // Check if directly below a topmost window
    //    IntPtr hwndAbove = User32.GetWindow(hwnd, User32.GetWindowCmd.GW_HWNDPREV);
    //    if (hwndAbove != IntPtr.Zero)
    //    {
    //        var aboveStyle = (User32.WindowStylesEx)User32.GetWindowLong(hwndAbove,
    //            User32.WindowLongFlags.GWL_EXSTYLE);
    //        if (aboveStyle.HasFlag(User32.WindowStylesEx.WS_EX_TOPMOST))
    //            return true;
    //    }

    //    return false;
    //}

    //private static void BringToOptimalPosition(IntPtr hwnd)
    //{
    //    // Find highest external topmost window
    //    IntPtr topmostHwnd = FindHighestExternalTopmost();

    //    if (topmostHwnd != IntPtr.Zero)
    //    {
    //        // Place just below the topmost overlay
    //        User32.SetWindowPos(
    //            hwnd,
    //            topmostHwnd,
    //            0, 0, 0, 0,
    //            User32.SetWindowPosFlags.SWP_NOSIZE |
    //            User32.SetWindowPosFlags.SWP_NOMOVE |
    //            User32.SetWindowPosFlags.SWP_NOACTIVATE |
    //            User32.SetWindowPosFlags.SWP_SHOWWINDOW);
    //    }
    //    else
    //    {
    //        // Become topmost if no overlays exist
    //        User32.SetWindowPos(
    //            hwnd,
    //            User32.HWND_TOPMOST,
    //            0, 0, 0, 0,
    //            User32.SetWindowPosFlags.SWP_NOSIZE |
    //            User32.SetWindowPosFlags.SWP_NOMOVE |
    //            User32.SetWindowPosFlags.SWP_SHOWWINDOW);
    //    }

    //    User32.SetForegroundWindow(hwnd);
    //}

    //private static IntPtr FindHighestExternalTopmost()
    //{
    //    var currentPID = Process.GetCurrentProcess().Id;
    //    IntPtr highestHwnd = IntPtr.Zero;

    //    User32.EnumWindows((hwnd, param) =>
    //    {
    //        if (!User32.IsWindowVisible(hwnd)) return true;

    //        // Check for WS_EX_TOPMOST
    //        var exStyle = (User32.WindowStylesEx)User32.GetWindowLong(hwnd,
    //            User32.WindowLongFlags.GWL_EXSTYLE);
    //        if (!exStyle.HasFlag(User32.WindowStylesEx.WS_EX_TOPMOST)) return true;

    //        // Check if from another process
    //        User32.GetWindowThreadProcessId(hwnd, out uint pid);
    //        if (pid != currentPID && IsSystemOwnedWindow(hwnd))
    //        {
    //            highestHwnd = hwnd;  // Return last found (topmost in Z-order)
    //            return true;
    //        }
    //        return true;
    //    }, IntPtr.Zero);

    //    return highestHwnd;
    //}

    //private static bool IsSystemOwnedWindow(IntPtr hwnd)
    //{
    //    // Filter out system-owned windows that shouldn't be considered
    //    int classNameLen = 256;
    //    System.Text.StringBuilder className = new System.Text.StringBuilder(classNameLen);
    //    User32.GetClassName(hwnd, className, classNameLen);

    //    return !className.ToString().StartsWith("Windows.UI.Core.");
    //}

}