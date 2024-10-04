using Common.Repositories;
using Common.Services;
using Common.Utils;
using ElectronNET.API;
using ElectronNET.API.Entities;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Serilog;
using ShellLink;
using System.Runtime.InteropServices;

//electronize build /target win

var builder = WebApplication.CreateBuilder();

// Configure Serilog
var logger = new LoggerConfiguration()
    //.WriteTo.LiteDB(@"Filename=C:\Users\Beltzac\Documents\Builds.db;Connection=shared", logCollectionName: "logEvents", RollingPeriod.Monthly)
    //.WriteTo.BrowserConsole()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog(logger);



builder.Services.AddAntiforgery();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddCircuitOptions(o =>
    {
        o.DetailedErrors = true;
    });

builder.WebHost.UseElectron(args);
builder.Services.AddElectron();

builder.Services.AddCustomServices();

// Add Quartz services
builder.Services.AddQuartz(q =>
{
    //q.UsePersistentStore(s =>
    //{
    //    s.UseLiteDb(options =>
    //    {
    //        options.ConnectionString = @"Filename=C:\Users\Beltzac\Documents\QuartzWorker.db;Connection=shared";
    //    });
    //    s.UseNewtonsoftJsonSerializer();
    //});

    q.ScheduleJob<BuildInfoJob>(trigger => trigger
        .WithIdentity("BuildInfoJob-trigger")
        .StartNow(),
        job => job.WithIdentity("BuildInfoJob")
    );
});

// Add Quartz hosted service
builder.Services.AddQuartzHostedService(q =>
{
    q.WaitForJobsToComplete = true;
    q.AwaitApplicationStarted = true;
    //q.StartDelay = TimeSpan.FromSeconds(10);
});

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
        string appName = "MyBlazorApp"; // Define your application name

        string startupFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        string shortcutPath = Path.Combine(startupFolderPath, $"{appName}.lnk");
        return File.Exists(shortcutPath);
    }
    return false;
}

void SetStartup(bool enable)
{


    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        string appName = "MyBlazorApp"; // Define your application name
        string executablePath = Environment.ProcessPath;
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
        Electron.Tray.Show(System.IO.Directory.GetCurrentDirectory() + "\\Assets\\app.ico", menus.ToArray());
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
    string workingDirectory = Path.GetDirectoryName(executablePath);

    // Create the ShellLink object and set its properties
    var shortcut = Shortcut.CreateShortcut(executablePath);
    //shortcut.StringData.WorkingDir = workingDirectory;

    // Optional: Set icon location
    //shortcut.StringData.IconLocation = Path.Combine(workingDirectory, "wwwroot", "favicon.ico");

    // Save the shortcut to the specified path
    shortcut.WriteToFile(shortcutPath);
}

// Define the event handler method
async void OnTrayClick(TrayClickEventArgs args, Rectangle bounds)
{
    await OpenWeb();
}

// Check if is running the http launch profile

if (!app.Environment.IsDevelopment())
{
    await Electron.Tray.Show(System.IO.Directory.GetCurrentDirectory() + "\\Assets\\app.ico", menus.ToArray());
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

Electron.App.Ready += async () =>
{
    await OpenWeb(true);
};

app.Run();

async Task OpenWeb(bool warmUp = false)
{
    WebPreferences wp = new WebPreferences();
    wp.NodeIntegration = false;

    var options = new BrowserWindowOptions
    {
        //Frame = false
        SkipTaskbar = true,
        AutoHideMenuBar = true,
        Closable = false,
        WebPreferences = wp,
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
