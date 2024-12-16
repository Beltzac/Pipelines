using BlazorApplicationInsights;
using Common.Jobs;
using Common.Repositories;
using Common.Services;
using Common.Utils;
using Generation;
using H.NotifyIcon.Core;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.EntityFrameworkCore;
using Photino.NET;
using Quartz;
using Serilog;
using ShellLink;
using SmartComponents.LocalEmbeddings;
using System.Drawing;
using System.Runtime.InteropServices;

// electronize start
// electronize build /target win
// python.exe -m pip install --upgrade --upgrade-strategy only-if-needed git+https://github.com/Aider-AI/aider.git

internal class Program
{
    //private static void Main(string[] args)
    [STAThread]
    static void Main(string[] args)
    {
        CancellationTokenSource source = new CancellationTokenSource();
        CancellationToken token = source.Token;

        var title = "¯\\_(ツ)_/¯";
        bool forceClose = false;

        var mainWindow = new PhotinoWindow()
               .Load("http://localhost:8002/")
               .SetTitle(title)
               .SetMaximized(true)
               .RegisterWindowClosingHandler(WindowIsClosing)
               .SetLogVerbosity(5);


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
            q.ScheduleJob<BuildInfoJob>(trigger => trigger
                .WithIdentity("BuildInfoJob-trigger")
                .WithCronSchedule("0 0 0/4 * * ?"), // Every 4 hours
                job => job.WithIdentity("BuildInfoJob")
            );

            // Schedule hourly runs
            q.ScheduleJob<OracleViewsBackupJob>(trigger => trigger
                .WithIdentity("OracleViewsBackupJob-hourly-trigger")
                .WithCronSchedule("0 0 * * * ?"), // Run every hour
                job => job.WithIdentity("OracleViewsBackupJob-hourly")
            );

            // Schedule hourly runs for ConsulBackupJob
            q.ScheduleJob<ConsulBackupJob>(trigger => trigger
                .WithIdentity("ConsulBackupJob-hourly-trigger")
                .WithCronSchedule("0 0 * * * ?"), // Run every hour
                job => job.WithIdentity("ConsulBackupJob-hourly")
            );

            var provider = builder.Services.BuildServiceProvider();
            var configService = provider.GetRequiredService<IConfigurationService>();
            var config = configService.GetConfig();
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

        app.UseAntiforgery();

        app.MapRazorComponents<Front2.Components.App>()
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
        Front2.Resource.halloween53_109170.Save(iconStream);
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
        trayIcon.ContextMenu.Items.Add(new PopupMenuItem("Toggle", (s, e) => ToggleWindow(mainWindow)));

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
}