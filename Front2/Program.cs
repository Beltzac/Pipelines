using BuildInfoBlazorApp.Data;
using Common;
using Front2.Components;
using H.NotifyIcon.Core;
using Microsoft.VisualStudio.Services.Commerce;
using Quartz;
using System.Drawing;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<BuildInfoService>();
builder.Services.AddScoped<SignalRClientService>();


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
//builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

//app.MapBlazorHub();
app.MapHub<BuildInfoHub>("/buildInfoHub");




using var icon = Front2.Resource.halloween53_109170;
using var trayIcon2 = new TrayIconWithContextMenu("H.NotifyIcon.Apps.Console.SecondTrayIcon")
{
    Icon = icon.Handle,
    ToolTip = "Second Tray Icon",
    
};

trayIcon2.ContextMenu = new PopupMenu
{
    Items =
    {
        new PopupMenuItem("Create Second", (_, _) => OpenWeb()),
        new PopupMenuSeparator(),
        new PopupMenuItem("Show Info", (_, _) => ShowInfo(trayIcon2, "info")),
    },
};

trayIcon2.Create();

trayIcon2.MessageWindow.SubscribeToMouseEventReceived( (object sender, MessageWindow.MouseEventReceivedEventArgs args) =>

{
    if(args.MouseEvent == MouseEvent.IconLeftMouseUp)
    {
        Console.WriteLine("kjhds");
        OpenWeb();
    }

}
);



app.Run();



void OpenWeb()
{
    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
    {
        FileName = "https://localhost:7143",
        UseShellExecute = true
    });
}

static void ShowInfo(TrayIcon trayIcon, string message)
{
    trayIcon.ShowNotification(
        title: nameof(NotificationIcon.Info),
        message: message,
        icon: NotificationIcon.Info);
    Console.WriteLine(nameof(trayIcon.ShowNotification));
}