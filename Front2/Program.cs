using BlazorApplicationInsights;
using Common.Jobs;
using Common.Repositories;
using Common.Services;
using Common.Utils;


using Generation;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Photino.NET;
using Quartz;
using Serilog;
using Serilog.Events;
using ShellLink;
using SmartComponents.LocalEmbeddings;
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

        var mainWindow = new PhotinoWindow()
               //.Load(new Uri("https://google.com"))
               //.Load("https://duckduckgo.com/?t=ffab&q=user+agent+&ia=answer")
               .Load("http://localhost:8002/")
               //.Load("https://localhost:8080/")
               //.Load("wwwroot/main.html")
               //.Load("wwwroot/index.html")
               //.LoadRawString("<h1>Hello Photino!</h1>")

               //Window settings
               //.SetIconFile(iconFile)
               //.SetTitle($"My Photino Window {_windowNumber++}")
               //.SetChromeless(true)
               //.SetTransparent(true)
               //.SetFullScreen(true)
               //.SetMaximized(true)
               //.SetMaxSize(640, 480)
               //.SetMinimized(true)
               //.SetMinHeight(240)
               //.SetMinWidth(320)
               //.SetMinSize(320, 240)
               //.SetResizable(false)
               //.SetTopMost(true)
               //.SetUseOsDefaultLocation(false)
               //.SetUseOsDefaultSize(false)
               .Center()
               //.SetSize(new Size(800, 600))
               //.SetHeight(600)
               //.SetWidth(800)
               //.SetLocation(new Point(50, 50))
               //.SetTop(50)
               //.SetLeft(50)
               //.MoveTo(new Point(10, 10))
               //.MoveTo(20, 20)
               //.Offset(new Point(150, 150))
               //.Offset(250, 250)
               //.SetNotificationRegistrationId("8FDF1B15-3408-47A6-8EF5-2B0676B76277")  //Replaces the window title when registering toast notifications
               //.SetNotificationsEnabled(false)

               //Browser settings
               //.SetContextMenuEnabled(false)
               .SetDevToolsEnabled(true)
               //.SetGrantBrowserPermissions(false)
               //.SetZoom(150)

               //Browser startup flags
               //.SetBrowserControlInitParameters("--ignore-certificate-errors ")
               .SetUserAgent("Custom Photino User Agent")
               //.SetMediaAutoplayEnabled(true)
               //.SetFileSystemAccessEnabled(true)
               //.SetWebSecurityEnabled(true)
               //.SetJavascriptClipboardAccessEnabled(true)
               //.SetMediaStreamEnabled(true)
               //.SetSmoothScrollingEnabled(true)
               //.SetTemporaryFilesPath(@"C:\Temp")
               //.SetIgnoreCertificateErrorsEnabled(false)

               //.RegisterCustomSchemeHandler("app", AppCustomSchemeUsed)

               //.RegisterWindowCreatingHandler(WindowCreating)
               //.RegisterWindowCreatedHandler(WindowCreated)
               //.RegisterLocationChangedHandler(WindowLocationChanged)
               //.RegisterSizeChangedHandler(WindowSizeChanged)
               //.RegisterWebMessageReceivedHandler(MessageReceivedFromWindow)
               //.RegisterWindowClosingHandler(WindowIsClosing)
               //.RegisterFocusInHandler(WindowFocusIn)
               //.RegisterFocusOutHandler(WindowFocusOut)

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

        //app.UseStaticFiles();
        app.MapStaticAssets();

        app.UseAntiforgery();

        app.MapRazorComponents<Front2.Components.App>()
            .AddInteractiveServerRenderMode();

        var startupEnabled = IsStartupEnabled();

        //var menus = new List<MenuItem>()
        //{
        //    new MenuItem
        //    {
        //        Label = "Open",
        //        Click = async () => await OpenWeb()
        //    },
        //    new MenuItem
        //    {
        //        Label = "Exit",
        //        Click = () => Electron.App.Exit()
        //    },
        //    new MenuItem
        //    {
        //        Type = MenuType.separator
        //    }
        //};

        //menus.Add(
        //     new MenuItem
        //     {
        //         Label = "Enable Startup with Windows",
        //         Click = () => SetStartupAsync(true),
        //         Visible = !startupEnabled
        //     });

        //menus.Add(
        //    new MenuItem
        //    {
        //        Label = "Disable Startup with Windows",
        //        Click = () => SetStartupAsync(false),
        //        Visible = startupEnabled
        //    });

        bool IsStartupEnabled()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string appName = "MyBlazorApp"; // Define your application name

                string startupFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                string shortcutPath = Path.Combine(startupFolderPath, $"{appName}.lnk");
                return File.Exists(shortcutPath);
            }

            return false;
        }

        async Task SetStartupAsync(bool enable)
        {
            //if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            //{
            //    string appName = "MyBlazorApp"; // Define your application name
            //    string executablePath = await Electron.App.GetAppPathAsync();
            //    string startupFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            //    string shortcutPath = Path.Combine(startupFolderPath, $"{appName}.lnk");

            //    if (enable)
            //    {
            //        CreateStartupShortcut(shortcutPath, executablePath);
            //    }
            //    else
            //    {
            //        if (File.Exists(shortcutPath))
            //        {
            //            File.Delete(shortcutPath);
            //        }
            //    }

            //    menus.First(m => m.Label == "Enable Startup with Windows").Visible = !enable;
            //    menus.First(m => m.Label == "Disable Startup with Windows").Visible = enable;

            //    Electron.Tray.OnClick -= OnTrayClick;
            //    Electron.Tray.Destroy();
            //    Electron.Tray.Show(Directory.GetCurrentDirectory() + "\\Assets\\app.ico", menus.ToArray());
            //    Electron.Tray.SetToolTip("¯\\_(ツ)_/¯");
            //    Electron.Tray.OnClick += OnTrayClick;
            //}
            //else
            //{
            //    Electron.Dialog.ShowMessageBoxAsync(new MessageBoxOptions("Startup setting is only supported on Windows."));
            //}
        }

        void CreateStartupShortcut(string shortcutPath, string executablePath)
        {
            var upperFolder = Path.GetDirectoryName(Path.GetDirectoryName(executablePath));
            var exePath = Path.Combine(upperFolder, "TcpDash.exe");

            var shortcut = Shortcut.CreateShortcut(exePath);

            shortcut.WriteToFile(shortcutPath);
        }

        //async void OnTrayClick(TrayClickEventArgs args, Rectangle bounds)
        //{
        //    await OpenWeb();
        //}

        //if (HybridSupport.IsElectronActive)
        //{
        //    await Electron.Tray.Show(Directory.GetCurrentDirectory() + "\\Assets\\app.ico", menus.ToArray());
        //    await Electron.Tray.SetToolTip("¯\\_(ツ)_/¯");
        //    Electron.Tray.OnClick += OnTrayClick;
        //}

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

        //if (HybridSupport.IsElectronActive)
        //{
        //    Electron.App.WillQuit += async (args) =>
        //    {
        //        args.PreventDefault();
        //    };
        //}

        CancellationTokenSource source = new CancellationTokenSource();
        CancellationToken token = source.Token;

        var task = app.RunAsync(token);

  

        mainWindow.WaitForClose();


        source.Cancel();

        //await task;
        task.GetAwaiter().GetResult();



        async Task OpenWeb(bool warmUp = false)
        {
            //WebPreferences wp = new WebPreferences();
            //wp.NodeIntegration = false;

            //var options = new BrowserWindowOptions
            //{
            //    SkipTaskbar = true,
            //    AutoHideMenuBar = true,
            //    WebPreferences = wp,
            //    Show = false
            //};

            //var existing = Electron.WindowManager.BrowserWindows.FirstOrDefault();

            //var window = existing ?? await Electron.WindowManager.CreateWindowAsync(options);

            //window.SetTitle("¯\\_(ツ)_/¯");

            //if (warmUp)
            //{
            //    return;
            //}

            //var topWindow = WindowUtils.EnumerarJanelas().FirstOrDefault();

            //if (topWindow == null || topWindow.ToString() != (await window.GetTitleAsync()))
            //{
            //    window.Maximize();
            //    window.Focus();
            //}
            //else
            //{
            //    window.Minimize();
            //}
        }
    }
}