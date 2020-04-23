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

namespace CTU60G
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly WorkerOptions _workerOptions;

        public Worker(ILogger<Worker> logger, WorkerOptions workerOptions)
        {
            _logger = logger;
            _workerOptions = workerOptions;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning("test");
                CTUClient client = new CTUClient();
                await client.LoginAsync(_workerOptions.CTULogin, _workerOptions.CTUPass);

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
               


                RegistrationJournal rj = await client.AddPTPConnectionAsync(conn);
                RegistrationJournal rj2 = await client.AddWIGIG_PTP_PTMPConnectionAsync(wigig);
                _logger.LogError("Ahoj");
                
                
                await Task.Delay(1);
                break;
            }
        }
    }
}
