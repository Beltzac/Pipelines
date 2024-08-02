using Blazored.Toast;
using BuildInfoBlazorApp.Data;
using Common;
using ElectronNET.API;
using ElectronNET.API.Entities;
using Front2.Components;
using H.NotifyIcon.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.Pipelines.WebApi;
using Quartz;
using Serilog;
using Serilog.Sinks.LiteDB;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;


var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
var logger = new LoggerConfiguration()
    .WriteTo.LiteDB(@"Filename=C:\Users\Beltzac\Documents\Builds.db;Connection=shared", logCollectionName: "logEvents", RollingPeriod.Monthly)
    .WriteTo.BrowserConsole()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog(logger);

// Add services to the container.
builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.WebHost.UseElectron(args);
builder.Services.AddElectron();

builder.Services.AddScoped<OracleSchemaService>();
builder.Services.AddScoped<OracleDiffService>();
builder.Services.AddScoped<BuildInfoService>();
builder.Services.AddScoped<SignalRClientService>();

builder.Services.AddBlazoredToast();

builder.Services.AddSignalR();

// Add Quartz services
builder.Services.AddQuartz(q =>
{
    q.UseMicrosoftDependencyInjectionJobFactory();

    // Configure the job and trigger
    q.AddJob<BuildInfoJob>(opts => opts.WithIdentity("buildInfoJob", "group1"));

    q.AddTrigger(opts => opts
        .ForJob("buildInfoJob", "group1")
        .WithIdentity("trigger1", "group1")
        .StartNow()
        .WithSimpleSchedule(x => x
            .WithIntervalInMinutes(3)
            .RepeatForever()));
});

// Add Quartz hosted service
builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

//app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<Front2.Components.App>()
    .AddInteractiveServerRenderMode();

//app.MapBlazorHub();
app.MapHub<BuildInfoHub>("/buildInfoHub");


var startupEnabled = IsStartupEnabled();

var menus = new List<MenuItem>()
{
    new MenuItem
    {
        Label = "Open",
        Click = () => OpenWeb()
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
         Click = () => SetStartup(true),
         Visible = !startupEnabled
     });

menus.Add(
    new MenuItem
    {
        Label = "Disable Startup with Windows",
        Click = () => SetStartup(false),
        Visible = startupEnabled
    });

bool IsStartupEnabled()
{
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false);
        if (key != null)
        {
            return key.GetValue("MyElectronApp") != null;
        }
    }
    return false;
}

void SetStartup(bool enable)
{
    var startupPath = $"\"{System.Reflection.Assembly.GetExecutingAssembly().Location}\"";

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
        if (enable)
        {
            key.SetValue("MyElectronApp", startupPath);
        }
        else
        {
            key.DeleteValue("MyElectronApp", false);
        }

        menus.First(m => m.Label == "Enable Startup with Windows").Visible = !enable;
        menus.First(m => m.Label == "Disable Startup with Windows").Visible = enable;

        Electron.Tray.OnClick -= OnTrayClick;
        Electron.Tray.Destroy();
        Electron.Tray.Show(System.IO.Directory.GetCurrentDirectory() + "\\Assets\\app.ico", menus.ToArray());
        Electron.Tray.SetToolTip("¯\\_(ツ)_/¯");
        Electron.Tray.OnClick += OnTrayClick;
    }
    else
    {
        Electron.Dialog.ShowMessageBoxAsync(new MessageBoxOptions("Startup setting is only supported on Windows."));
    }
}

// Define the event handler method
void OnTrayClick(TrayClickEventArgs args, Rectangle bounds)
{
    OpenWeb();
}

// Check if is running the http launch profile

if (!app.Environment.IsDevelopment())
{
    await Electron.Tray.Show(System.IO.Directory.GetCurrentDirectory() + "\\Assets\\app.ico", menus.ToArray());
    await Electron.Tray.SetToolTip("¯\\_(ツ)_/¯");
    Electron.Tray.OnClick += OnTrayClick;
}


app.Run();

async Task OpenWeb()
{
    var options = new BrowserWindowOptions
    {
        //Frame = false
        SkipTaskbar = true,
        AutoHideMenuBar = true,
        Closable = false,
        
    };

    var existing = Electron.WindowManager.BrowserWindows.FirstOrDefault();

    var window = existing ?? await Electron.WindowManager.CreateWindowAsync(options);

    window.SetTitle("¯\\_(ツ)_/¯");

    if (existing != null)
    {
        if (await window.IsMinimizedAsync() || !await window.IsVisibleAsync())
        {
            window.Maximize();
            window.Focus();
        }
        else
        {
            window.Minimize();
        }
    }
    else
    {
        window.OnReadyToShow += () => {
            window.Maximize(); 
            window.Focus(); 
        };
    }
}
