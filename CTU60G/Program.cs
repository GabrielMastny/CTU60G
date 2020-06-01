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
using System.Threading;
using System.Security.Cryptography;
using CTU60G.Configuration;

namespace CTU60G
{
    public class Program
    {
        static IHostBuilder hostB = default;
        public static async Task Main(string[] args)
        {
            
            CancellationTokenSource cancelationSource = new CancellationTokenSource();

            Serilog.Debugging.SelfLog.Enable(msg => Debug.WriteLine(msg));
            Serilog.Debugging.SelfLog.Enable(Console.Error);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) hostB = CreateWindowsHostBuilder(args);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) hostB = CreateLinuxHostBuilder(args);
            else throw new SystemException("Unsupported system.");

            var g = hostB.RunConsoleAsync(cancelationSource.Token);

            await g;

        }

        public static IHostBuilder CreateWindowsHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseConsoleLifetime(opts => opts.SuppressStatusMessages = true)
                .UseWindowsService()
                .UseConsoleLifetime()
                .ConfigureAppConfiguration((hostContext, configApp) =>
                {
                    configApp.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                    configApp.AddJsonFile(
                       $"appsettings.{hostContext.HostingEnvironment.EnvironmentName}.json",
                       optional: true);
                    configApp.AddEnvironmentVariables(prefix: "PREFIX_");
                    configApp.AddCommandLine(args);
                })
                .UseSerilog((hostingContext, loggerConfiguration) =>
                {
                    loggerConfiguration
                                .ReadFrom.Configuration(hostingContext.Configuration)
                                .Enrich.FromLogContext()
                                .Enrich.WithProperty("ApplicationName", typeof(Program).Assembly.GetName().Name)
                                .Enrich.WithProperty("Environment", hostingContext.HostingEnvironment);
                })
                .ConfigureServices((hostContext, services) =>
                {
                    
                    IConfiguration configuration = hostContext.Configuration;

                    Thread.Sleep(500);
                    IEmailConfiguration eOptions = configuration.GetSection("Email").Get<EmailConfiguraton>();
                    services.AddSingleton(eOptions);
                    IBehaviourConfiguration bOptions = configuration.GetSection("Behaviour").Get<BehaviourConfiguration>();
                    services.AddSingleton(bOptions);
                    IWorkerConfiguration wOptions = configuration.GetSection("Config").Get<WorkerConfiguration>();
                    services.AddSingleton(wOptions);

                    services.AddHostedService<Worker>();
                });

        
        public static IHostBuilder CreateLinuxHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseConsoleLifetime(opts => opts.SuppressStatusMessages = true)
                .UseSystemd()
                .UseConsoleLifetime()
                .ConfigureAppConfiguration((hostContext, configApp) =>
                {
                    configApp.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                    configApp.AddJsonFile(
                       $"appsettings.{hostContext.HostingEnvironment.EnvironmentName}.json",
                       optional: true);
                    configApp.AddEnvironmentVariables(prefix: "PREFIX_");
                    configApp.AddCommandLine(args);
                })
                .UseSerilog((hostingContext, loggerConfiguration) =>
                {
                    loggerConfiguration
                                .ReadFrom.Configuration(hostingContext.Configuration)
                                .Enrich.FromLogContext()
                                .Enrich.WithProperty("ApplicationName", typeof(Program).Assembly.GetName().Name)
                                .Enrich.WithProperty("Environment", hostingContext.HostingEnvironment);
                })
                .ConfigureServices((hostContext, services) =>
                {

                    IConfiguration configuration = hostContext.Configuration;

                    Thread.Sleep(500);
                    IEmailConfiguration eOptions = configuration.GetSection("Email").Get<EmailConfiguraton>();
                    services.AddSingleton(eOptions);
                    IBehaviourConfiguration bOptions = configuration.GetSection("Behaviour").Get<BehaviourConfiguration>();
                    services.AddSingleton(bOptions);
                    IWorkerConfiguration wOptions = configuration.GetSection("Config").Get<WorkerConfiguration>();
                    services.AddSingleton(wOptions);

                    services.AddHostedService<Worker>();
                });
    }
}
