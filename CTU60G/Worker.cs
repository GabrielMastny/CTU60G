using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CTU60G.Json;
using Newtonsoft.Json.Linq;
using CTU60GLib.Client;
using Client.Json;
using CTU60GLib;
using CTU60GLib.Exceptions;
using System.IO;

namespace CTU60G
{
    public class Worker : BackgroundService, IDisposable
    {
        private readonly ILogger<Worker> _logger;
        private readonly WorkerOptions _workerOptions;
        private readonly IHostApplicationLifetime _applifetime;

        public Worker(ILogger<Worker> logger, IHostApplicationLifetime appLifetime, WorkerOptions workerOptions)
        {
            _logger = logger;
            _workerOptions = workerOptions;
            _applifetime = appLifetime;

            _applifetime.ApplicationStarted.Register(OnStarted);
            _applifetime.ApplicationStopping.Register(OnStopping);
            _applifetime.ApplicationStopped.Register(OnStopped);
        }

        private enum SourceDataType
        {
            URL,
            FILE,
            ERR
        }

        private SourceDataType GetSourceDataType()
        {
            Uri uriResult;
            Uri.TryCreate(_workerOptions.DataURLOrFilePath, UriKind.Absolute, out uriResult);
            if (uriResult == null) return SourceDataType.ERR;
            else if (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps) return SourceDataType.URL;
            if (uriResult.IsFile && File.Exists(uriResult.AbsolutePath)) return SourceDataType.FILE;

            return SourceDataType.ERR;
            
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            
            while (!stoppingToken.IsCancellationRequested)
            {
                SourceDataType sourceDataType;
                if (_workerOptions.CTULogin == string.Empty || _workerOptions.CTUPass == string.Empty)
                {
                    _logger.LogError("Missing credentials for www.60ghz.ctu.cz");
                    await StopAsync(stoppingToken);
                    return;
                }
                else if(_workerOptions.DataURLOrFilePath == string.Empty)
                {
                    _logger.LogError("Missing data source for www.60ghz,ctu.cz");
                    await StopAsync(stoppingToken);
                    return;
                }
                else if( (sourceDataType = GetSourceDataType()) == SourceDataType.ERR)
                {
                    _logger.LogError("Not valid file path or web url");
                    await StopAsync(stoppingToken);
                    return;
                }

                _logger.LogInformation("Loading data");
                string data = (sourceDataType == SourceDataType.FILE)?
                    File.ReadAllText(_workerOptions.DataURLOrFilePath) :
                    new WebClient().DownloadString(_workerOptions.DataURLOrFilePath);

                _logger.LogInformation("DeserializingData");
                List<WirelessSite> sites = new List<WirelessSite>();
                try
                {
                    var jObj = JObject.Parse(data).Children().Children();
                    foreach (var site in jObj)
                    {
                        try
                        {
                            sites.Add(site.ToObject<WirelessSite>());
                        }
                        catch (Exception)
                        {
                            _logger.LogWarning($"Cannot deserialize part, skiping:\n{site.ToString()}");
                        }
                        
                    }

                }
                catch (Exception e)
                {
                    _logger.LogError("Source data are not seriazible");
                    await StopAsync(stoppingToken);
                    return;
                }


                
                CTUClient client = new CTUClient();
                _logger.LogInformation("LoggingIn");
                try
                {
                    await client.LoginAsync(_workerOptions.CTULogin, _workerOptions.CTUPass);
                }
                catch (InvalidMailOrPasswordException )
                {
                    _logger.LogError("Invalid login credentials");
                    _logger.LogWarning("Shuting service down");
                    return;
                }
                catch (WebServerException )
                {
                    _logger.LogError("Web server exception occured during login");
                    _logger.LogWarning("Shuting service down");
                    return;
                }
                catch (Exception e)
                {

                    _logger.LogError("unknown exception occured during login");
                    _logger.LogWarning("Shuting service down");
                    return;
                }


#if DEBUG
                List<Client.Json.CtuWirelessUnit> qq = await client.GetMyStationsAsync();
                List<string> alreadyRemoved = new List<string>();
                foreach (var item in qq)
                {
                    if (item.Name == "test")
                    {
                        if (!alreadyRemoved.Contains(item.Id))
                        {
                            await client.DeleteConnectionAsync(item.Id);
                            alreadyRemoved.Add(item.IdStationPair);
                        }


                    }
                }
#endif

                #region
                FixedP2PPair conn;
                WigigPTMPUnitInfo wigig;
                try
                {
                    conn = new FixedP2PPair(
                   new FixedStationInfo(
                       "test",
                       "",
                       "6F:5E:4D:3C:2B:1A",
                       "15.79548252828888",
                       "49.95501062285150",
                       "30",
                       "2160",
                       "4",
                       "64800",
                       "12"
                       ),
                   new FixedStationInfo(
                       "test",
                       "0123456789",
                       "",
                       "15.79980922061566",
                       "49.95731458058119",
                       "30",
                       "2160",
                       "4",
                       "64800",
                       "12"));

                     wigig = new WigigPTMPUnitInfo(
                        "test",
                        "",
                        "6F:5E:4D:3C:2B:1A",
                        "15.987315748193009",
                        "49.63796777844475",
                        "30",
                        "2160",
                        "4",
                        "64800",
                        "",
                        "0");
                }
                catch (Exception e)
                {

                    throw;
                }
                //RegistrationJournal rj = await client.AddPTPConnectionAsync(conn);
                //RegistrationJournal rj2 = await client.AddWIGIG_PTP_PTMPConnectionAsync(wigig);

                #endregion
                foreach (var site in sites)
                {
                    if(site.Ap != null && site.Stations.Count == 1)
                    {
                        //p2p
                    }
                    //else if()

                }


                
                
                await Task.Delay(1);
                break;
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            stoppingToken.ThrowIfCancellationRequested();
            await Task.CompletedTask;
        }

        private void OnStarted()
        {
            
            _logger.LogInformation("OnStarted has been called.");

            // Perform post-startup activities here
        }

        private void OnStopping()
        {
            _logger.LogInformation("OnStopping has been called.");

            // Perform on-stopping activities here
        }

        private void OnStopped()
        {
            _logger.LogInformation("OnStopped has been called.");

            // Perform post-stopped activities here
        }

    }
}
