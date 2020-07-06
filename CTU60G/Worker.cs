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
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Reflection;
using Serilog;

namespace CTU60G
{
    public class Worker : IHostedService
    {
        private readonly ILogger<Worker> logger;
        private readonly WorkerConfiguration workerOptions;
        private readonly EmailConfiguraton emailOptions;
        private EmailService mailing;
        private IHostApplicationLifetime appLifetime { get; }
        private TaskCompletionSource<bool> TaskCompletionSource { get; } = new TaskCompletionSource<bool>();
        Stopwatch runTime = new Stopwatch();
        public Worker(ILogger<Worker> logger, IHostApplicationLifetime appLifetime, IOptions<WorkerConfiguration> workerOptions, IOptions<EmailConfiguraton> emailOptions, IOptions<BehaviourConfiguration> behaviourOptoins)
        {
            this.logger = logger;
            this.workerOptions = workerOptions.Value;
            this.emailOptions = emailOptions.Value;
            WirellesUnitFactory.Behaviour = behaviourOptoins.Value;
            this.appLifetime = appLifetime;
        }

        private enum SourceDataType
        {
            URL,
            FILE,
            ERR
        }
        
        public Task StartAsync(CancellationToken cancellationToken)
        {
            runTime.Start();
            return DoWork(cancellationToken);
        }
        public Task StopAsync(CancellationToken cancellationToken)
        {
            runTime.Stop();
            logger.LogInformation($"Task stopped, elapsed time: {runTime.Elapsed}.");
            return TaskCompletionSource.Task;
        }

        private async Task DoWork(CancellationToken cancellationToken)
        {
            logger.LogInformation($"Launching CTU60G, current version: {Assembly.GetExecutingAssembly().GetName().Version}");
            SourceDataType sourceDataType = default;
            mailing = CheckEmailOptionValues();
            if (await CheckWorkerOptionValues(cancellationToken, sourceDataType))
            {
                logger.LogInformation($"Synchronization type set to: {workerOptions.Synchronization }");
                string msg = $"Run type set to: {workerOptions.Run}";
                msg += (workerOptions.Run == RunTypeEnum.InSpecifedTime.ToString()) ? $", app will run every day in {workerOptions.RunTimeSpecification.Hours}:{workerOptions.RunTimeSpecification.Minutes}":"";
                msg += (workerOptions.Run == RunTypeEnum.AfterSpecifedTime.ToString()) ? $", app will run always after {workerOptions.RunTimeSpecification}":"";
                logger.LogInformation(msg);
                do
                {
                    
                    //Loading source data from file or url
                    logger.LogInformation("Loading data");
                    string loadedData = default;
                    try
                    {
                        loadedData = await LoadData(sourceDataType);
                    }
                    catch (Exception e)
                    {
                        logger.LogError("Could not obtain source data, exception message:\n {0}.", e.Message);
                        TaskCompletionSource.SetResult(false);
                        appLifetime.StopApplication();
                        return;
                    }

                    //parsing loaded source data
                    logger.LogInformation("parsing loaded data");
                    List<WirelessSite> parsedSites = await ParseData(cancellationToken, loadedData);
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        logger.LogInformation("LoggingIn");
                        using (CTUClient client = new CTUClient(workerOptions.CTULogin, workerOptions.CTUPass, workerOptions.SignalIsolationConsentIfMyOwn))
                        {
                            logger.LogInformation("LoggedIn");
                            OnSiteData ctuMetaData = new OnSiteData(await client.GetMyStationsAsync());

                            foreach (var site in parsedSites)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                if ((DateTime.Now - ctuMetaData.LasTimeRefreshed).TotalSeconds > 10) ctuMetaData.Refresh(await client.GetMyStationsAsync());

                                if (site.Infos.SiteType == "ptp")
                                {
                                    List<P2PSite> pairs = (ProcessWUnitCreation(site) as List<P2PSite>);
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
                                                ProcessRegistrationJournal(regJournal = await client.AddPTPSiteAsync(pair), site);
                                                if (regJournal.Phase == RegistrationJournalPhaseEnum.Published && !string.IsNullOrEmpty(workerOptions.ResponseWithCTUID))
                                                {
                                                    HttpClient cl = new HttpClient();
                                                    HttpResponseMessage rm = await cl.GetAsync(workerOptions.ResponseWithCTUID + $"&id={pair.StationA.OwnerId}&ctuId={regJournal.RegistrationId}");
                                                    HttpResponseMessage rm2 = await cl.GetAsync(workerOptions.ResponseWithCTUID + $"&id={pair.StationB.OwnerId}&ctuId={regJournal.RegistrationId}");
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
                                else if (site.Infos.SiteType == "delete" && workerOptions.Synchronization == SynchronizationTypeEnum.Manual.ToString())
                                {
                                    ProcessDeletion(site, client);
                                }
                                else
                                {
                                    logger.LogWarning($"{site.Infos.Ssid} site type is missing or has unrecognizable value, dont know where to publish:\n" +
                                        $"original source:\n" +
                                        JsonConvert.SerializeObject(site, Formatting.Indented));
                                }
                            }
                        }
                    }
                    catch (InvalidMailOrPasswordException)
                    {
                        logger.LogError("Invalid login credentials");
                        TaskCompletionSource.SetResult(false);
                        appLifetime.StopApplication();
                        return;
                    }
                    catch (WebServerException e)
                    {
                        logger.LogError($"Web server exception occured during comunication with ctu\n{e.Message} {e.Status.ToString()}\n {e.StackTrace}");
                        TaskCompletionSource.SetResult(false);
                        appLifetime.StopApplication();
                        return;
                    }
                    catch (OperationCanceledException e)
                    {
                        logger.LogError($"Task was stopped as respond to system signal\n{e.Message}");
                        TaskCompletionSource.SetResult(false);
                        appLifetime.StopApplication();
                        return;
                    }
                    catch (Exception e)
                    {
                        logger.LogError($"unknown exception occured during login\n{e.Message}\n {e.StackTrace}");
                        logger.LogInformation(e.Message);
                        logger.LogWarning("Shuting service down");
                        TaskCompletionSource.SetResult(false);
                        appLifetime.StopApplication();
                        return;
                    }

                    if (workerOptions.Run == RunTypeEnum.AfterSpecifedTime.ToString())
                    {
                        Log.Information($"task goes to sleep, will resume at {DateTime.Now + workerOptions.RunTimeSpecification}");
                        await Task.Delay(workerOptions.RunTimeSpecification,cancellationToken);
                    }
                    if (workerOptions.Run == RunTypeEnum.InSpecifedTime.ToString())
                    {
                        TimeSpan timeToWait = DateTime.Today + workerOptions.RunTimeSpecification - DateTime.Now;
                        if (timeToWait < TimeSpan.Zero) timeToWait += new TimeSpan(1, 0, 0, 0);
                        Log.Information($"task goes to sleep, will resume at {DateTime.Now + timeToWait}");
                        await Task.Delay(timeToWait,cancellationToken);
                    }
                }
                while (!cancellationToken.IsCancellationRequested && (RunTypeEnum)Enum.Parse(typeof(RunTypeEnum),workerOptions.Run) != RunTypeEnum.Once);
                logger.LogInformation("allDone");
            }

            

            void ProcessRegistrationJournal(RegistrationJournal regJournal, WirelessSite site)
            {
                using (LogContext.PushProperty("phase", regJournal.Phase.ToString()))
                using (LogContext.PushProperty("type", regJournal.Type.ToString()))
                using (LogContext.PushProperty("id", regJournal.RegistrationId))
                {
                    switch (regJournal.Phase)
                    {
                        case RegistrationJournalPhaseEnum.InputValidation:
                            {
                                logger.LogWarning("{phase}\nid:{id}\n{type}\nwas not possible due to Invalid values\n" +
                                "No records were created/updated");
                            }
                            break;
                        case RegistrationJournalPhaseEnum.Localization:
                            {
                                if (regJournal.ThrownException.GetType() == typeof(WebServerException))
                                {
                                    logger.LogWarning("{phase}\nid:{id}\n{type}\nwas not possible due to unexpected web behaviour\n" +
                                    $"No record were created");
                                }
                                else
                                {
                                    logger.LogWarning("{phase}\nid:{id}\n{type}\nwas not possible due to unexpected behaviour\n" +
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
                                        logger.LogWarning("{phase}\nid:{id}\n{type}\nwas not possible due to unexpected web behaviour\n" +
                                                         "Record is now in state {record}");
                                    }
                                    else
                                    {
                                        logger.LogWarning("{phase}\nid:{id}\n{type}\nwas not possible due to unexpected behaviour\n" +
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
                                        logger.LogWarning("{phase}\nid:{id}\n{type}\nwas not possible due to unexpected web behaviour\n" +
                                        "Record is now in state {record}");
                                    }
                                    else if (regJournal.ThrownException.GetType() == typeof(CollisionDetectedException))
                                    {
                                        logger.LogWarning("{phase}\nid:{id}\n{type}\nwas not possible due to possible collision with another connection\n" +
                                                                 "Record is now in state {record}");
                                        NotifyViaMail(regJournal);
                                    }
                                    else
                                    {
                                        logger.LogWarning("{phase}\nid:{id}\n{type}\nwas not possible due to unexpected behaviour\n" +
                                                                "Record is now in state {record}");
                                    }
                                }

                            }
                            break;
                        case RegistrationJournalPhaseEnum.Published:
                            {
                                using (LogContext.PushProperty("record", "PUBLISHED"))
                                {
                                    logger.LogInformation("{phase}\nid:{id}\n{type}\nwas successfully published\n" +
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
                            if (!string.IsNullOrEmpty(workerOptions.ResponseOnDelete))
                            {
                                HttpResponseMessage rm2 = await cl.GetAsync(workerOptions.ResponseOnDelete + $"&id={item.Id}");
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
                            if (!string.IsNullOrEmpty(workerOptions.ResponseOnDelete))
                            {
                                HttpResponseMessage rm2 = await cl.GetAsync(workerOptions.ResponseOnDelete + $"&id={item.Id}");
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
                        logger.LogWarning($"{site.Infos.Ssid} will not be possible to publish, because of" + "{ci}:\n" +
                        $"{e.Message}\n" +
                        $"original source:\n" +
                        JsonConvert.SerializeObject(site, Formatting.Indented));
                    }

                }
                catch (InvalidPropertyValueException e)
                {
                    using (LogContext.PushProperty("ci", "invalid critical information"))
                    {
                        logger.LogWarning($"{site.Infos.Ssid} will not be possible to publish, because of" + "{ci} :\n" +
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
                    if (site.Stations.Count > 0)
                        foreach (var item in site.Stations)
                        {
                            List<CtuWirelessUnit> stations = default;
                            stations = client.GetStationByIdAsync(item.CtuReported).Result;
                        }
                    else
                    {
                        List<CtuWirelessUnit> stations = default;
                        stations = client.GetStationByIdAsync(site.Ap.FirstOrDefault()?.CtuReported).Result;
                        if (stations.Count > 0)
                            client.DeleteConnectionAsync(site.Ap.FirstOrDefault()?.CtuReported);
                    }

                }
                catch (Exception e)
                {

                    throw e;
                }
            }
            TaskCompletionSource.SetResult(true);
            appLifetime.StopApplication();
            return;
        }


        private struct OnSiteData
        {
            public OnSiteData(List<CtuWirelessUnit> dataOnSite)
            {
                this.LasTimeRefreshed = DateTime.Now;
                this.DataOnSite = dataOnSite;
            }

            public void Refresh(List<CtuWirelessUnit> dataOnSite)
            {
                this.DataOnSite = dataOnSite;
                this.LasTimeRefreshed = DateTime.Now;
            }
            public DateTime LasTimeRefreshed { get; private set; }

            public List<CtuWirelessUnit> DataOnSite { get; private set; }
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

        

        private SourceDataType GetSourceDataType(string possibleSourceDataType)
        {
            Uri uriResult;
            Uri.TryCreate(possibleSourceDataType, UriKind.Absolute, out uriResult);
            if (uriResult == null) return SourceDataType.ERR;
            else if (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps) return SourceDataType.URL;
            if (uriResult.IsFile && File.Exists(uriResult.AbsolutePath)) return SourceDataType.FILE;

            return SourceDataType.ERR;

        }
        private async Task<bool> CheckWorkerOptionValues(CancellationToken stoppingToken, SourceDataType sourceDataType)
        {
            if (workerOptions.CTULogin == string.Empty || workerOptions.CTUPass == string.Empty)
            {
                logger.LogError("Missing credentials for www.60ghz.ctu.cz");
                appLifetime.StopApplication();
                return false;
            }
            if ((sourceDataType = GetSourceDataType(workerOptions.DataURLOrFilePath)) == SourceDataType.ERR)
            {
                logger.LogError("Not valid file path or web url");
                appLifetime.StopApplication();
                return false;
            }
            if(String.IsNullOrEmpty(workerOptions.Synchronization))
            {
                logger.LogError("Synchronization type not specified");
                appLifetime.StopApplication();
                return false;
            }
            if(String.IsNullOrEmpty(workerOptions.Run))
            {
                logger.LogError("Run type not specified");
                appLifetime.StopApplication();
                return false;
            }
            return true;
        }

        private EmailService CheckEmailOptionValues()
        {
            if (emailOptions.Host != string.Empty &&
                        emailOptions.User != string.Empty &&
                        emailOptions.ToEmails.Count != 0 &&
                        emailOptions.FromEmail != string.Empty &&
                        emailOptions.Password != string.Empty)
                return new EmailService(emailOptions, System.Net.Mail.MailPriority.High, true);
            return null;
        }

        private async Task<string> LoadData(SourceDataType sourceDataType)
        {

            //todo check if readable + secure reading
            string data = (sourceDataType == SourceDataType.FILE) ?
                    File.ReadAllText(workerOptions.DataURLOrFilePath) :
                    new WebClient().DownloadString(workerOptions.DataURLOrFilePath);

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
                        logger.LogWarning($"Cannot deserialize part, skiping:\n{site.ToString()}");
                    }

                }

            }
            catch (Exception e) // error in general structure, data are not usable
            {
                logger.LogError("Source data are not seriazible");
                appLifetime.StopApplication();
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
