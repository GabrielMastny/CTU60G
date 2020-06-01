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
using Newtonsoft.Json;
using Serilog.Context;
using CTU60G.Configuration;
using System.Net.Http;

namespace CTU60G
{
    public class Worker : BackgroundService, IDisposable
    {
        private readonly ILogger<Worker> _logger;
        private readonly WorkerConfiguration _workerOptions;
        private readonly EmailConfiguraton _emailOptions;
        private EmailService mailing;

        public Worker(ILogger<Worker> logger, IHostApplicationLifetime appLifetime, IWorkerConfiguration workerOptions, IEmailConfiguration emailOptions, IBehaviourConfiguration behaviourOptoins)
        {
            _logger = logger;
            _workerOptions = workerOptions as WorkerConfiguration;
            _emailOptions = emailOptions as EmailConfiguraton;
            WirellesUnitFactory.Behaviour = behaviourOptoins as BehaviourConfiguration;
        }

        private enum SourceDataType
        {
            URL,
            FILE,
            ERR
        }
        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {

            SourceDataType sourceDataType = default;
            await CheckWorkerOptionValues(cancellationToken, sourceDataType);
            mailing = CheckEmailOptionValues();

                //Loading source data from file or url
                _logger.LogInformation("Loading data");
                string loadedData = await LoadData(sourceDataType);

                //parsing loaded source data
                _logger.LogInformation("parsing loaded data");
                List<WirelessSite> parsedSites = await ParseData(cancellationToken, loadedData);
                try
                {
                    _logger.LogInformation("LoggingIn");
                    using (CTUClient client = new CTUClient(_workerOptions.CTULogin, _workerOptions.CTUPass, _workerOptions.SignalIsolationConsentIfMyOwn))
                    {
                        _logger.LogInformation("LoggedIn");
                        OnSiteData ctuMetaData = new OnSiteData(await client.GetMyStationsAsync());
                        foreach (var site in parsedSites)
                        {
                                if ((DateTime.Now - ctuMetaData.LasTimeRefreshed).TotalSeconds > 10) ctuMetaData.Refresh(await client.GetMyStationsAsync());

                                if (site.Infos.SiteType == "ptp")
                                {
                                    List<FixedP2PPair> pairs = (ProcessWUnitCreation(site) as List<FixedP2PPair>);
                                    if (pairs != null)
                                    {
                                        foreach (var pair in pairs)
                                        {
                                            RegistrationJournal regJournal = default;
                                            if (pair.StationB.CTUId > 0) // update already existing site
                                            {
                                                ProcessRegistrationJournal(regJournal = await client.UpdatePTPConnectionAsync(pair), site);
                                            }
                                            else // create new site
                                            {
                                                ProcessRegistrationJournal(regJournal = await client.AddPTPConnectionAsync(pair), site);
                                                if (regJournal.Phase == RegistrationJournalPhaseEnum.Published && !string.IsNullOrEmpty(_workerOptions.ResponseWithCTUID))
                                                {
                                                    HttpClient cl = new HttpClient();
                                                    HttpResponseMessage rm = await cl.GetAsync(_workerOptions.ResponseWithCTUID + $"&id={pair.StationA.OwnerId}&ctuId={regJournal.RegistrationId}");
                                                    HttpResponseMessage rm2 = await cl.GetAsync(_workerOptions.ResponseWithCTUID + $"&id={pair.StationB.OwnerId}&ctuId={regJournal.RegistrationId}");
                                                }
                                            }

                                        }
                                    }
                                    
                                }
                                else if (site.Infos.SiteType == "ptmp")
                                {
                                    WigigPTMPUnitInfo wigig = (ProcessWUnitCreation(site) as WigigPTMPUnitInfo);
                                    if (wigig != null)
                                    {
                                        ProcessRegistrationJournal(await client.AddWIGIG_PTP_PTMPConnectionAsync(wigig), site);
                                    }
                                    
                                }
                                else if (site.Infos.SiteType == "delete")
                                {
                                    ProcessDeletion(site, client);
                                }
                                else
                                {
                                    _logger.LogWarning($"{site.Infos.Ssid} site type is missing or has unrecognizable value, dont know where to publish:\n" +
                                        $"original source:\n" +
                                        JsonConvert.SerializeObject(site, Formatting.Indented));
                                }
                        }
                    }
                }
                catch (InvalidMailOrPasswordException)
                {
                    _logger.LogError("Invalid login credentials");
                    _logger.LogWarning("Shuting service down");
                    return;
                }
                catch (WebServerException)
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
                _logger.LogInformation("allDone");
            return;

            void ProcessUserApiRespnse()
            {

            }
            void ProcessRegistrationJournal(RegistrationJournal regJournal, WirelessSite site)
            {
                using(LogContext.PushProperty("phase",regJournal.Phase.ToString()))
                using (LogContext.PushProperty("type", regJournal.Type.ToString()))
                using (LogContext.PushProperty("id", regJournal.RegistrationId))
                {
                    switch (regJournal.Phase)
                    {
                        case RegistrationJournalPhaseEnum.InputValidation:
                            {
                                _logger.LogWarning("{phase}\nid:{id}\n{type}\nwas not possible due to Invalid values\n" +
                                "No records were created/updated");
                            }
                            break;
                        case RegistrationJournalPhaseEnum.Localization:
                            {
                                if (regJournal.ThrownException.GetType() == typeof(WebServerException))
                                {
                                    _logger.LogWarning("{phase}\nid:{id}\n{type}\nwas not possible due to unexpected web behaviour\n" +
                                    $"No record were created");
                                }
                                else
                                {
                                    _logger.LogWarning("{phase}\nid:{id}\n{type}\nwas not possible due to unexpected behaviour\n" +
                                                    $"No record were created");
                                }
                            }
                            break;
                        case RegistrationJournalPhaseEnum.TechnicalSpecification:
                            {
                                using (LogContext.PushProperty("record", "Draft"))
                                {
                                    if (regJournal.ThrownException.GetType() == typeof(WebServerException))
                                    {
                                            _logger.LogWarning("{phase}\nid:{id}\n{type}\nwas not possible due to unexpected web behaviour\n" +
                                                             "Record is now in state {record}");
                                    }
                                    else
                                    {
                                            _logger.LogWarning("{phase}\nid:{id}\n{type}\nwas not possible due to unexpected behaviour\n" +
                                                                "Record is now in state {record}");
                                    }
                                }
                                    
                            }
                            break;
                        case RegistrationJournalPhaseEnum.CollissionSummary:
                            {
                                using (LogContext.PushProperty("record", "WAITING"))
                                {
                                    if (regJournal.ThrownException.GetType() == typeof(WebServerException))
                                    {
                                        _logger.LogWarning("{phase}\nid:{id}\n{type}\nwas not possible due to unexpected web behaviour\n" +
                                        "Record is now in state {record}");
                                    }
                                    else if (regJournal.ThrownException.GetType() == typeof(CollisionDetectedException))
                                    {
                                        _logger.LogWarning("{phase}\nid:{id}\n{type}\nwas not possible due to possible collision with another connection\n" +
                                                                 "Record is now in state {record}");
                                        NotifyViaMail(regJournal);
                                    }
                                    else
                                    {
                                        _logger.LogWarning("{phase}\nid:{id}\n{type}\nwas not possible due to unexpected behaviour\n" +
                                                                "Record is now in state {record}");
                                    }
                                }
                                    
                            }
                            break;
                        case RegistrationJournalPhaseEnum.Published:
                            {
                                using (LogContext.PushProperty("record", "PUBLISHED"))
                                {
                                    _logger.LogInformation("{phase}\nid:{id}\n{type}\nwas successfully published\n" +
                                    "Record is now in state {record}");

                                }
                            }
                            break;
                        default:
                            break;
                    }
                }
                    
            }
            async Task ZeroDatabase(List<WirelessSite> sites)
            {
                foreach (var site in sites)
                {
                    HttpClient cl = new HttpClient();
                    if (site.Ap != null)
                    {
                        foreach (var item in site?.Ap)
                        {
                             if(!string.IsNullOrEmpty(_workerOptions.ResponseOnDelete))
                            {
                                HttpResponseMessage rm2 = await cl.GetAsync(_workerOptions.ResponseOnDelete + $"&id={item.Id}");
                                if (await rm2.Content.ReadAsStringAsync() != "ok")
                                {

                                }
                            }
                                
                        }
                    }
                    if (site.Stations != null)
                    {
                        foreach (var item in site?.Stations)
                        {
                            if (!string.IsNullOrEmpty(_workerOptions.ResponseOnDelete))
                            {
                                HttpResponseMessage rm2 = await cl.GetAsync(_workerOptions.ResponseOnDelete + $"&id={item.Id}");
                                if (await rm2.Content.ReadAsStringAsync() != "ok")
                                {

                                }
                            }
                                                                
                        }
                    }


                }
            }
            object ProcessWUnitCreation(WirelessSite site)
            {
                try
                {
                    if (site.Infos.SiteType == "ptp")
                    {
                        return WirellesUnitFactory.CreatePTP(site);
                    }
                    else if (site.Infos.SiteType == "ptmp")
                    {
                        return WirellesUnitFactory.CreateWigigPTMP(site);
                    }
                }
                catch (MissingParameterException e)
                {
                    using (LogContext.PushProperty("ci", "missing critical information"))
                    {
                        _logger.LogWarning($"{site.Infos.Ssid} will not be possible to publish, because of" + "{ci}:\n" +
                        $"{e.Message}\n" +
                        $"original source:\n" +
                        JsonConvert.SerializeObject(site, Formatting.Indented));
                    }

                }
                catch (InvalidPropertyValueException e)
                {
                    using (LogContext.PushProperty("ci", "invalid critical information"))
                    {
                        _logger.LogWarning($"{site.Infos.Ssid} will not be possible to publish, because of" + "{ci} :\n" +
                        $"Expected value: {e.ExpectedVauleInfo}\n" +
                        $"Current value: {e.CurrentValue}\n" +
                        $"original source:\n" +
                        JsonConvert.SerializeObject(site, Formatting.Indented));
                    }
                }
                catch (Exception e)
                {

                    throw e;
                }

                return null;
            }
            void ProcessDeletion(WirelessSite site, CTUClient client)
            {
                try
                {
                    if(site.Stations.Count > 0)
                    foreach (var item in site.Stations)
                    {
                        List<CtuWirelessUnit> stations = default;
                        stations = client.GetStationByIdAsync(item.CtuReported).Result;
                    }
                    else
                    {
                        List<CtuWirelessUnit> stations = default;
                        stations =  client.GetStationByIdAsync(site.Ap.FirstOrDefault()?.CtuReported).Result;
                        if (stations.Count > 0)
                            client.DeleteConnectionAsync(site.Ap.FirstOrDefault()?.CtuReported);
                    }
                    
                }
                catch (Exception e)
                {

                    throw e;
                }
            }
        }

        private struct OnSiteData
        {
            private DateTime lasTimeRefreshed;
            private List<CtuWirelessUnit> dataOnSite;

            public OnSiteData(List<CtuWirelessUnit> dataOnSite)
            {
                this.lasTimeRefreshed = DateTime.Now;
                this.dataOnSite = dataOnSite;
            }

            public void Refresh(List<CtuWirelessUnit> dataOnSite)
            {
                this.dataOnSite = dataOnSite;
                this.lasTimeRefreshed = DateTime.Now;
            }
            public DateTime LasTimeRefreshed
            {
                get { return lasTimeRefreshed; }
            }
            
            public List<CtuWirelessUnit> DataOnSite
            {
                get { return dataOnSite; }
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
                IJEnumerable<JToken> jTok = default;
                    if (jObj != null) jTok = jObj;
                foreach (var site in jTok)
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

        private async Task deleteAllPreviousData(CTUClient client)
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
