using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.Diagnostics;
using Serilog.Debugging;

namespace CTU60G
{
    public class Program
    {
        public static void Main(string[] args)
        {
            
            Serilog.Debugging.SelfLog.Enable(msg => Debug.WriteLine(msg));
            Serilog.Debugging.SelfLog.Enable(Console.Error);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) CreateWindowsHostBuilder(args).Build().Run();
                else if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) CreateLinuxHostBuilder(args).Build().Run();
                    else throw new SystemException("Unsupported system.");
        }

        public static IHostBuilder CreateWindowsHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseWindowsService()
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddTransient<Program>();
                    IConfiguration configuration = hostContext.Configuration;
                    WorkerOptions options = configuration.GetSection("Config").Get<WorkerOptions>();
                    services.AddSingleton(options);
                    services.AddHostedService<Worker>();

                }).ConfigureAppConfiguration((hostContext, configApp) =>
                {
                    configApp.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                    configApp.AddJsonFile(
                       $"appsettings.{hostContext.HostingEnvironment.EnvironmentName}.json",
                       optional: true);
                    configApp.AddEnvironmentVariables(prefix: "PREFIX_");
                    configApp.AddCommandLine(args);
                }).UseSerilog((hostingContext, loggerConfiguration) =>
                {
                    loggerConfiguration
                                .ReadFrom.Configuration(hostingContext.Configuration)
                                .Enrich.FromLogContext()
                                .Enrich.WithProperty("ApplicationName", typeof(Program).Assembly.GetName().Name)
                                .Enrich.WithProperty("Environment", hostingContext.HostingEnvironment);                                ;
                });

        
        public static IHostBuilder CreateLinuxHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSystemd()
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<Worker>();
                });
    }
}
