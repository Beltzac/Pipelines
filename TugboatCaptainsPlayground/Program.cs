using Common.Jobs;
using Common.Repositories.Interno;
using Common.Utils;
using Generation;
using GlobalHotKey;
using H.NotifyIcon.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Photino.NET;
using Quartz;
using Serilog;
using ShellLink;
using SmartComponents.LocalEmbeddings;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Vanara.Windows.Shell;

internal class Program
{
    [STAThread]
    static void Main(string[] args)
    {
#if DEBUG
        var port = 8001;
#else
        var port = 8002;
#endif

        CancellationTokenSource source = new CancellationTokenSource();
        CancellationToken token = source.Token;

        var title = $"Tugboat Captain's Playground {VersionHelper.GetCurrentVersion()}";
        bool forceClose = false;

        var mainWindow = new PhotinoWindow()
               .Load($"http://localhost:{port}")
               .SetTitle(title)
               .SetMaximized(true)
               .RegisterWindowClosingHandler(WindowIsClosing)
               .SetLogVerbosity(5);

        mainWindow.WindowMaximized += HandleWindowEvent;
        mainWindow.WindowMinimized += HandleWindowEvent;
        mainWindow.WindowCreated += HandleWindowEvent;


        var hotKeyManager = new HotKeyManager();

        try
        {
            var hotKey = hotKeyManager.Register(Key.A, ModifierKeys.Control | ModifierKeys.Shift);

            hotKeyManager.KeyPressed += HotKeyManagerPressed;

            void HotKeyManagerPressed(object sender, KeyPressedEventArgs e)
            {
                ToggleWindow(mainWindow);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error registering hotkey: {ex.Message}");
        }


        //return;

        var builder = WebApplication.CreateBuilder();


        // Configure the WebHost to set the URLs
        builder.WebHost.ConfigureKestrel((context, options) =>
        {
            options.ListenLocalhost(port);
        });

        builder.Host.UseSerilog((context, services, configuration) =>
        {
            configuration
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Warning)
                //.MinimumLevel.Override("Microsoft.AspNetCore.SignalR", Serilog.Events.LogEventLevel.Debug)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.File(Path.Combine(LogUtils.LogDirectoryPath, "log-.txt"), rollingInterval: RollingInterval.Day, shared: true);
        });

        builder.Services.AddAntiforgery();
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddCircuitOptions(o =>
            {
                o.DetailedErrors = true;
            });

        builder.Services.AddCustomServices();

        builder.Services.AddHttpClient("Github").ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ClientCertificateOptions = ClientCertificateOption.Manual,
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });

        builder.Services.AddQuartz(q =>
        {
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

            var databasePath = DBUtils.MainDBPath;
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

        builder.Services.AddStateServices();

        builder.Services.AddSingleton<LocalEmbedder>();

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            app.UseHsts();
        }

        Console.WriteLine($"App running on {AppContext.BaseDirectory}");

     
        app.UseStaticFiles();



        // https://github.com/dotnet/aspnetcore/issues/50894



        var embeddedFileProvider = new EmbeddedFileProvider(typeof(Program).Assembly, "wwwroot");
        if (embeddedFileProvider.GetDirectoryContents("/").Any())
        {
            // Serve the wwwroot files from embedded resources (see tricks in the csproj for embedding static assets instead of having a wwwroot directory on disk when publishing)
            app.Environment.WebRootFileProvider = embeddedFileProvider;
        }


        var embeddedProvider2 = new EmbeddedFileProvider(
            Assembly.GetExecutingAssembly(), // your project's assembly
            "wwwroot" // this must match your LogicalName prefix
        );

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = embeddedProvider2,
            RequestPath = ""
        });

        foreach (var name in Assembly.GetExecutingAssembly().GetManifestResourceNames())
        {
            Console.WriteLine(name);
        }


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
                    ShowMessage(trayIcon, title, $"Habilitado");
                }
                else
                {
                    if (File.Exists(shortcutPath))
                    {
                        File.Delete(shortcutPath);
                    }
                    ShowMessage(trayIcon, title, $"Desabilitado");
                }

                trayIcon.ContextMenu.Items.OfType<PopupMenuItem>().First(m => m.Text == "Enable Startup with Windows").Visible = !enable;
                trayIcon.ContextMenu.Items.OfType<PopupMenuItem>().First(m => m.Text == "Disable Startup with Windows").Visible = enable;
            }
            else
            {
                ShowMessage(trayIcon, title, $"Startup setting is only supported on Windows.");
            }
        }

        void CreateStartupShortcut(string shortcutPath, string executablePath)
        {
            var shortcut = Shortcut.CreateShortcut(executablePath);
            shortcut.WriteToFile(shortcutPath);
        }

        // Apply migrations se o banco existir
        Directory.CreateDirectory(Path.GetDirectoryName(DBUtils.MainDBPath));
        using (var scope = app.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var context = services.GetRequiredService<RepositoryDbContext>();
            context.Database.Migrate();
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

        mainWindow.WaitForClose();

        source.Cancel();

        app.WaitForShutdown();

        hotKeyManager.Dispose();

        static void ShowMessage(TrayIcon trayIcon, string title, string message)
        {
            trayIcon.ShowNotification(
                title: title,
                message: message,
                icon: NotificationIcon.None);
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

    private static void HandleWindowEvent(object sender, EventArgs e)
    {
        try
        {
            // Get the native window handle
            IntPtr hwnd = ((PhotinoWindow)sender).WindowHandle;
            HideTaskbarIcon(hwnd);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling window event: {ex.Message}");
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
        catch (Exception ex)
        {
            Console.WriteLine($"Error hiding taskbar icon: {ex.Message}");
        }
    }
}
