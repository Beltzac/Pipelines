using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using LiteDB;
using Quartz;
using Quartz.Impl;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        // Create and configure a Quartz scheduler
        IScheduler scheduler = await StdSchedulerFactory.GetDefaultScheduler();
        await scheduler.Start();

        // Define the job and tie it to our BuildInfoJob class
        IJobDetail job = JobBuilder.Create<BuildInfoJob>()
            .WithIdentity("buildInfoJob", "group1")
            .Build();

        // Trigger the job to run now, and then every minute
        ITrigger trigger = TriggerBuilder.Create()
            .WithIdentity("trigger1", "group1")
            .StartNow()
            .WithSimpleSchedule(x => x
                .WithIntervalInMinutes(1)
                .RepeatForever())
            .Build();

        // Schedule the job using the job and trigger
        await scheduler.ScheduleJob(job, trigger);

        // Keep the console window open
        Console.WriteLine("Press [Enter] to close the application.");
        Console.ReadLine();
    }
}
