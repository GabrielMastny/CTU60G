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
using Newtonsoft.Json.Schema;
using Newtonsoft.Json;
using System.Runtime.CompilerServices;
using Serilog.Context;
using CTU60GLib.CollisionTable;
using Serilog;
using System.Data.Common;

namespace CTU60G
{
    public class Worker : BackgroundService, IDisposable
    {
        private readonly ILogger<Worker> _logger;
        private readonly WorkerOptions _workerOptions;
        private readonly EmailOptions _emailOptions;
        private EmailService mailing;

        public Worker(ILogger<Worker> logger, IHostApplicationLifetime appLifetime, WorkerOptions workerOptions, EmailOptions emailOptions)
        {
            _logger = logger;
            _workerOptions = workerOptions;
            _emailOptions = emailOptions;
        }

        private enum SourceDataType
        {
            URL,
            FILE,
            ERR
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                SourceDataType sourceDataType = default;
                await CheckWorkerOptionValues(stoppingToken, sourceDataType);
                mailing = CheckEmailOptionValues();

                _logger.LogInformation("Loading data");
                string loadedData = await LoadData(sourceDataType);

                _logger.LogInformation("parsing data");
                List<WirelessSite> sites = await ParseData(stoppingToken, loadedData);
                
                //todo make ctuClient idisposable
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
                _logger.LogInformation("LoggedIn");

#if DEBUG
                deleteAllPreviousData(client);
#endif

                foreach (var site in sites)
                {
                    try
                    {
                        if (site.Infos.SiteType == "ptp")
                        {
                            ProcessRegistrationJournal(await client.AddPTPConnectionAsync(WirellesUnitFactory.CreatePTP(site)),site);
                        }
                        else if (site.Infos.SiteType == "ptmp")
                        {
                            ProcessRegistrationJournal( await client.AddWIGIG_PTP_PTMPConnectionAsync(WirellesUnitFactory.CreateWigigPTMP(site)),site);
                        }
                        else
                        {
                            _logger.LogWarning($"{site.Infos.Ssid} site type is missing, dont know where to publish:\n" +
                             $"original source:\n" +
                             JsonConvert.SerializeObject(site, Formatting.Indented));
                        }
                    }
                    catch (MissingParameterException e)
                    {
                        using(LogContext.PushProperty("ci", "missing critical information"))
                        {
                            _logger.LogWarning($"{site.Infos.Ssid} will not be possible to publish, because of" + "{ci}:\n" +
                            $"{e.Message}\n" +
                            $"original source:\n" +
                            JsonConvert.SerializeObject(site, Formatting.Indented));
                        }
                        
                    }
                    catch(InvalidPropertyValueException e)
                    {
                        using (LogContext.PushProperty("ci", "invalid critical information"))
                        {
                            _logger.LogWarning($"{site.Infos.Ssid} will not be possible to publish, because of" +"{ci} :\n" +
                            $"Expected value: {e.ExpectedVauleInfo}\n" +
                            $"Current value: {e.CurrentValue}\n" +
                            $"original source:\n" +
                            JsonConvert.SerializeObject(site, Formatting.Indented));
                        }
                    }
                    

                }

                
                _logger.LogInformation("allDone");
                break;
            }

            void ProcessRegistrationJournal(RegistrationJournal regJournal, WirelessSite site)
            {
                switch (regJournal.Phase)
                {
                    case RegistrationJournalPhaseEnum.InputValidation: _logger.LogWarning($"Publication was not possible due to Invalid values\n" +
                        $"No record were created");
                        break;
                    case RegistrationJournalPhaseEnum.Localization:
                        {
                            if (regJournal.ThrownException.GetType() == typeof(WebServerException))
                            {
                                using(LogContext.PushProperty("No record",1))
                                {
                                    _logger.LogWarning($"Publication was not possible due to unexpected web behaviour\n" +
                                                                                                                    $"No record were created");
                                }
                                
                            }
                            else
                            {
                                using(LogContext.PushProperty("No record",1))
                                {
                                    _logger.LogWarning($"Publication was not possible due to unexpected behaviour\n" +
                                                $"No record were created");
                                }
                                
                            }
                        }
                        break;
                    case RegistrationJournalPhaseEnum.TechnicalSpecification:
                        {
                            if (regJournal.ThrownException.GetType() == typeof(WebServerException))
                            {
                                using (LogContext.PushProperty("record", "Draft"))
                                {
                                    _logger.LogWarning($"Publication was not possible due to unexpected web behaviour\n" +
                                                     "Record is now in state {record}");
                                }
                            }
                            else
                            {
                                using (LogContext.PushProperty("record", "Draft"))
                                {
                                    _logger.LogWarning($"Publication was not possible due to unexpected behaviour\n" +
                                                        "Record is now in state {record}");
                                }
                            }    
                        }
                        break;
                    case RegistrationJournalPhaseEnum.CollissionSummary:
                        {
                            if (regJournal.ThrownException.GetType() == typeof(WebServerException))
                            {
                                using (LogContext.PushProperty("record", "WAITING"))
                                {
                                    _logger.LogWarning($"Publication was not possible due to unexpected web behaviour\n" +
                                                     "Record is now in state {record}");
                                }
                            }
                            else if(regJournal.ThrownException.GetType() == typeof(CollisionDetectedException))
                            {
                                using(LogContext.PushProperty("record","WAITING"))
                                _logger.LogWarning($"Publication was not possible due to possible collision with another connection\n" +
                                                     "Record is now in state {record}");
                                NotifyViaMail(regJournal);
                            }
                            else
                            {
                                using (LogContext.PushProperty("record", "WAITING"))
                                {
                                    _logger.LogWarning($"Publication was not possible due to unexpected behaviour\n" +
                                                        "Record is now in state {record}");
                                }
                            }
                        }
                        break;
                    case RegistrationJournalPhaseEnum.Published:
                        {
                            using (LogContext.PushProperty("record", "PUBLISHED"))
                            {
                                _logger.LogWarning($"Publication was successfully published\n" +
                                                    "Record is now in state {record}");
                            }
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        private void NotifyViaMail(RegistrationJournal regJournal)
        {
            if(mailing != null)
            {
                string message = $"<p><a href=\"https://60ghz.ctu.cz/en/station/{regJournal.RegistrationId}/3\" target=\"_blank\" rel=\"noopener\">{regJournal.RegistrationId}</a> is enterfering with stations listed below</p>";
                
                message += "<table><tbody>";
                message += "<tr><td> Id </td><td> name </td ><td> owned </td><td> type </td></tr> ";
                foreach (var item in regJournal.CollisionStations)
                {
                    message += $"<tr><td> {item.Id} </td><td><a href = \"https://60ghz.ctu.cz/en/station/{item.Id}/3\" target = \"_blank\" rel = \"noopener\" > {item.Name} </a></td><td> {item.Owned} </td><td> {item.Type} </td>";

                }
                message += "</tr></tbody></table> ";
                mailing.Send("collision detected", message);
            }
            
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            stoppingToken.ThrowIfCancellationRequested();
            await Task.CompletedTask;
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
        private async Task CheckWorkerOptionValues(CancellationToken stoppingToken, SourceDataType sourceDataType)
        {
            if (_workerOptions.CTULogin == string.Empty || _workerOptions.CTUPass == string.Empty)
            {
                _logger.LogError("Missing credentials for www.60ghz.ctu.cz");
                await StopAsync(stoppingToken);
                return;
            }
            else if (_workerOptions.DataURLOrFilePath == string.Empty)
            {
                _logger.LogError("Missing data source for www.60ghz,ctu.cz");
                await StopAsync(stoppingToken);
                return;
            }
            else if ((sourceDataType = GetSourceDataType()) == SourceDataType.ERR)
            {
                _logger.LogError("Not valid file path or web url");
                await StopAsync(stoppingToken);
                return;
            }
        }

        private EmailService CheckEmailOptionValues()
        {
            if (_emailOptions.Host != string.Empty &&
                        _emailOptions.User != string.Empty &&
                        _emailOptions.ToEmails.Count != 0 &&
                        _emailOptions.FromEmail != string.Empty &&
                        _emailOptions.Password != string.Empty)
                return new EmailService(_emailOptions, System.Net.Mail.MailPriority.High, true);
            return null;
        }

        private async Task<string> LoadData(SourceDataType sourceDataType)
        {

            //todo check if readable + secure reading
            string data = (sourceDataType == SourceDataType.FILE) ?
                    File.ReadAllText(_workerOptions.DataURLOrFilePath) :
                    new WebClient().DownloadString(_workerOptions.DataURLOrFilePath);

            return data;
        }

        private async Task<List<WirelessSite>> ParseData(CancellationToken stoppingToken, string loadedData)
        {
            List<WirelessSite> sites = new List<WirelessSite>();
            try
            {

                var jObj = JObject.Parse(loadedData).Children().Children();
                foreach (var site in jObj)
                {
                    try
                    {
                        sites.Add(site.ToObject<WirelessSite>());
                    }
                    catch (Exception) // if general structure was ok but in propertyes is for instance forbiden character this exception will ocure and only part of whole data will be skipped.
                    {
                        _logger.LogWarning($"Cannot deserialize part, skiping:\n{site.ToString()}");
                    }

                }

            }
            catch (Exception e) // error in general structure, data are not usable
            {
                _logger.LogError("Source data are not seriazible");
                await StopAsync(stoppingToken);
                return null;
            }

            return sites;
        }

        private async void deleteAllPreviousData(CTUClient client)
        {
            List<Client.Json.CtuWirelessUnit> toRemove = await client.GetMyStationsAsync();
            List<string> alreadyRemoved = new List<string>();
            foreach (var item in toRemove)
            {
                    if (!alreadyRemoved.Contains(item.Id))
                    {
                        await client.DeleteConnectionAsync(item.Id);
                        alreadyRemoved.Add(item.IdStationPair);
                    }
            }
        }

    }
}
