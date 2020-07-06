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
using System.Runtime.CompilerServices;
using System.IO;

namespace CTU60G
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            
            CancellationTokenSource cancelationSource = new CancellationTokenSource();
#if DEBUG
            Serilog.Debugging.SelfLog.Enable(msg => Debug.WriteLine(msg));
            Serilog.Debugging.SelfLog.Enable(Console.Error);
#endif
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    await CreateHostBuilder(args).UseWindowsService().Build().RunAsync(cancelationSource.Token);
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    await CreateHostBuilder(args).UseSystemd().Build().RunAsync(cancelationSource.Token);
                else throw new SystemException("Unsupported system.");
            }
            catch (SystemException ex)
            {
               
            }
            catch (Exception e)
            {
            }
            
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
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
                    Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
                    IConfiguration configuration = hostContext.Configuration;

                    services.Configure<EmailConfiguraton>(configuration.GetSection("Email"));
                    services.Configure<BehaviourConfiguration>(configuration.GetSection("Behaviour"));
                    services.Configure<WorkerConfiguration>(configuration.GetSection("Config"));
                    services.AddSingleton(configuration);
                    services.AddHostedService<Worker>();
                });
    }
}
