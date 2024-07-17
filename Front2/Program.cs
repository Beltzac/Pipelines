using BuildInfoBlazorApp.Data;
using Common;
using Front2.Components;
using Quartz;

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

app.Run();
