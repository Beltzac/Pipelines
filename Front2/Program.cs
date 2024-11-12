﻿﻿﻿using BlazorApplicationInsights;
using Common.Repositories;
using Common.Services;
using Common.Utils;
using ElectronNET.API;
using ElectronNET.API.Entities;
using Front2.Services;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Serilog;
using ShellLink;
using System.Runtime.InteropServices;
using System.Text;
using Vanara.PInvoke;

// electronize start
// electronize build /target win

var builder = WebApplication.CreateBuilder();

// Integrate Electron.NET with the Host
builder.WebHost.UseElectron(args);

// Configure the WebHost to set the URLs
builder.WebHost.ConfigureKestrel((context, options) =>
{
    var port = HybridSupport.IsElectronActive ? 8001 : 8002;
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

builder.Services.AddElectron();

builder.Services.AddCustomServices();

builder.Services.AddQuartz(q =>
{
    q.ScheduleJob<BuildInfoJob>(trigger => trigger
        .WithIdentity("BuildInfoJob-trigger")
        .StartNow(),
        job => job.WithIdentity("BuildInfoJob")
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

builder.Services.AddScoped<ConsulPageStateService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<Front2.Components.App>()
    .AddInteractiveServerRenderMode();

app.MapHub<BuildInfoHub>("/buildInfoHub");

var startupEnabled = IsStartupEnabled();

var menus = new List<MenuItem>()
{
    new MenuItem
    {
        Label = "Open",
        Click = async () => await OpenWeb()
    },
    new MenuItem
    {
        Label = "Exit",
        Click = () => Electron.App.Exit()
    },
    new MenuItem
    {
        Type = MenuType.separator
    }
};

menus.Add(
     new MenuItem
     {
         Label = "Enable Startup with Windows",
         Click = () => SetStartupAsync(true),
         Visible = !startupEnabled
     });

menus.Add(
    new MenuItem
    {
        Label = "Disable Startup with Windows",
        Click = () => SetStartupAsync(false),
        Visible = startupEnabled
    });

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
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        string appName = "MyBlazorApp"; // Define your application name
        string executablePath = await Electron.App.GetAppPathAsync();
        string startupFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        string shortcutPath = Path.Combine(startupFolderPath, $"{appName}.lnk");

        if (enable)
        {
            CreateStartupShortcut(shortcutPath, executablePath);
        }
        else
        {
            if (File.Exists(shortcutPath))
            {
                File.Delete(shortcutPath);
            }
        }

        menus.First(m => m.Label == "Enable Startup with Windows").Visible = !enable;
        menus.First(m => m.Label == "Disable Startup with Windows").Visible = enable;

        Electron.Tray.OnClick -= OnTrayClick;
        Electron.Tray.Destroy();
        Electron.Tray.Show(Directory.GetCurrentDirectory() + "\\Assets\\app.ico", menus.ToArray());
        Electron.Tray.SetToolTip("¯\\_(ツ)_/¯");
        Electron.Tray.OnClick += OnTrayClick;
    }
    else
    {
        Electron.Dialog.ShowMessageBoxAsync(new MessageBoxOptions("Startup setting is only supported on Windows."));
    }
}

void CreateStartupShortcut(string shortcutPath, string executablePath)
{
    var upperFolder = Path.GetDirectoryName(Path.GetDirectoryName(executablePath));
    var exePath = Path.Combine(upperFolder, "TcpDash.exe");

    var shortcut = Shortcut.CreateShortcut(exePath);

    shortcut.WriteToFile(shortcutPath);
}

async void OnTrayClick(TrayClickEventArgs args, Rectangle bounds)
{
    await OpenWeb();
}

if (HybridSupport.IsElectronActive)
{
    await Electron.Tray.Show(Directory.GetCurrentDirectory() + "\\Assets\\app.ico", menus.ToArray());
    await Electron.Tray.SetToolTip("¯\\_(ツ)_/¯");
    Electron.Tray.OnClick += OnTrayClick;
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

if (HybridSupport.IsElectronActive)
{
    Electron.App.WillQuit += async (args) =>
    {
        args.PreventDefault();
    };
}

app.Run();

async Task OpenWeb(bool warmUp = false)
{
    WebPreferences wp = new WebPreferences();
    wp.NodeIntegration = false;

    var options = new BrowserWindowOptions
    {
        SkipTaskbar = true,
        AutoHideMenuBar = true,
        WebPreferences = wp,
        Show = false
    };

    var existing = Electron.WindowManager.BrowserWindows.FirstOrDefault();

    var window = existing ?? await Electron.WindowManager.CreateWindowAsync(options);

    window.SetTitle("¯\\_(ツ)_/¯");

    if (warmUp)
    {
        return;
    }

    var topWindow = WindowUtils.EnumerarJanelas().FirstOrDefault();

    if (topWindow == null || topWindow.ToString() != (await window.GetTitleAsync()))
    {
        window.Maximize();
        window.Focus();
    }
    else
    {
        window.Minimize();
    }
}