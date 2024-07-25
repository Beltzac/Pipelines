using Blazored.Toast;
using BuildInfoBlazorApp.Data;
using Common;
using ElectronNET.API;
using Front2.Components;
using H.NotifyIcon.Core;
using Quartz;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.WebHost.UseElectron(args);
builder.Services.AddElectron();

builder.Services.AddSingleton<OracleSchemaService>();
builder.Services.AddSingleton<OracleDiffService>();
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

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<Front2.Components.App>()
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
        new PopupMenuItem("Create Second", async (_, _) => await OpenWeb()),
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

OpenWeb();


async Task OpenWeb()
{
    //System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
    //{
    //    FileName = "https://localhost:7143",
    //    UseShellExecute = true
    //});
    var window = await Electron.WindowManager.CreateWindowAsync();

    window.Show();

    //Open("https://localhost:7143");
}

static void ShowInfo(TrayIcon trayIcon, string message)
{
    trayIcon.ShowNotification(
        title: nameof(NotificationIcon.Info),
        message: message,
        icon: NotificationIcon.Info);
    Console.WriteLine(nameof(trayIcon.ShowNotification));
}

static void Open(string url)
{
    try
    {
        if (!FocusBrowserWindow(url))
        {
            var process = System.Diagnostics.Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
    }
    catch (Exception e)
    {
        // Handle the error here (e.g., logging)
        Console.WriteLine($"An error occurred: {e.Message}");
    }
}

//static bool FocusBrowserWindow(string url)
//{

//    var procs = Process.GetProcessesByName("chrome");

//    Console.WriteLine(string.Join(',', Process.GetProcessesByName("chrome").Select(x => x.MainWindowTitle).ToList()));

//    foreach (Process proc in procs)
//    {
//        IntPtr hWnd = proc.MainWindowHandle;
//        if (hWnd != IntPtr.Zero)
//        {
//            var urlParsed = new Uri(url);

//            if (proc.MainWindowTitle.Contains(urlParsed.Authority))
//            {
//                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
//                return true;
//            }

//            AutomationElement root = AutomationElement.FromHandle(process.MainWindowHandle);
//            Condition condition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem);
//            var tabs = root.FindAll(TreeScope.Descendants, condition);
//        }
//    }

//    return false;
//}

static bool FocusBrowserWindow(string url)
{
    var urlParsed = new Uri(url);


    var handle = NativeMethods.GetAllWindows()
        .Select(hWnd => new { hWnd, Title = NativeMethods.GetTitle(hWnd) })
        .FirstOrDefault(x => x.Title != null && x.Title.Contains(urlParsed.Authority))?.hWnd ?? IntPtr.Zero;

    if (handle != IntPtr.Zero)
    {
        NativeMethods.ShowWindow(handle, NativeMethods.SW_SHOWMAXIMIZED); // Restore the window if it's minimized
        NativeMethods.SetForegroundWindow(handle);
        return true;
    }

    return false;
}



public static class NativeMethods
{
    public const int SW_SHOWMAXIMIZED = 3;
    public const int SW_RESTORE = 9;

    public delegate bool Win32Callback(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(Win32Callback enumProc, IntPtr lParam);

    [DllImport("User32", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern int GetWindowText(IntPtr windowHandle, StringBuilder stringBuilder, int nMaxCount);

    [DllImport("user32.dll", EntryPoint = "GetWindowTextLength", SetLastError = true)]
    internal static extern int GetWindowTextLength(IntPtr hwnd);
    public static string GetTitle(IntPtr handle)
    {
        int length = GetWindowTextLength(handle);
        StringBuilder sb = new StringBuilder(length + 1);
        GetWindowText(handle, sb, sb.Capacity);
        return sb.ToString();
    }

    private static bool EnumWindow(IntPtr handle, IntPtr pointer)
    {
        List<IntPtr> pointers = GCHandle.FromIntPtr(pointer).Target as List<IntPtr>;
        pointers.Add(handle);
        return true;
    }

    public static List<IntPtr> GetAllWindows()
    {
        Win32Callback enumCallback = new Win32Callback(EnumWindow);
        List<IntPtr> pointers = new List<IntPtr>();
        GCHandle listHandle = GCHandle.Alloc(pointers);
        try
        {
            EnumWindows(enumCallback, GCHandle.ToIntPtr(listHandle));
        }
        finally
        {
            if (listHandle.IsAllocated) listHandle.Free();
        }
        return pointers;
    }
}
