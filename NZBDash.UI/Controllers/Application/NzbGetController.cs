﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Mvc;
using NZBDash.Common;
using NZBDash.Common.Helpers;
using NZBDash.Common.Interfaces;
using NZBDash.Common.Mapping;
using NZBDash.Common.Models.ViewModels.NzbGet;
using NZBDash.Core.Interfaces;
using NZBDash.Core.Model.Settings;
using NZBDash.ThirdParty.Api.Interfaces;
using NZBDash.UI.Helpers;
using NZBDash.UI.Models.Dashboard;

using Omu.ValueInjecter;

using Ploeh.AutoFixture;

using UrlHelper = NZBDash.UI.Helpers.UrlHelper;

namespace NZBDash.UI.Controllers.Application
{
    public class NzbGetController : BaseController
    {
        public NzbGetController(ISettingsService<NzbGetSettingsDto> settingsService, IThirdPartyService api, ILogger logger)
        {
            SettingsService = settingsService;
            Api = api;
            Logger = logger;
        }

        private ISettingsService<NzbGetSettingsDto> SettingsService { get; set; }
        private IThirdPartyService Api { get; set; }

        [HttpGet]
        public ActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public JsonResult GetNzbGetStatus()
        {
            Logger.Trace("Getting Config");
            var config = SettingsService.GetSettings();
            var formattedUri = UrlHelper.ReturnUri(config.IpAddress, config.Port).ToString();
            try
            {
                Logger.Trace("Getting NzbGetStatus");
                var statusInfo = Api.GetNzbGetStatus(formattedUri, config.Username, config.Password);

                var nzbModel = new NzbGetViewModel
                {
                    Status = statusInfo.Result.ServerPaused ? "Paused" : "Running",
                };

                Logger.Trace("Returning Model");
                return Json(nzbModel, JsonRequestBehavior.AllowGet);
            }
            catch (Exception e)
            {
                Logger.Error(e.Message, e);
                return Json("Error", JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public ActionResult GetNzbGetDownloadInformation()
        {
            Logger.Trace("Getting Config");
            var config = SettingsService.GetSettings();
            var formattedUri = UrlHelper.ReturnUri(config.IpAddress, config.Port).ToString();
            try
            {
                Logger.Trace("Getting NzbGetStatus");
                var statusInfo = Api.GetNzbGetStatus(formattedUri, config.Username, config.Password);

                Logger.Trace("Getting Current NZBGetlist");
                var downloadInfo = Api.GetNzbGetList(formattedUri, config.Username, config.Password);

                var downloadSpeed = statusInfo.Result.DownloadRate / 1024;

                var model = new DownloaderViewModel
                {
                    Application = Applications.NzbGet,
                    DownloadSpeed = MemorySizeConverter.SizeSuffix(downloadSpeed),
                    DownloadItem = new List<DownloadItem>()
                };

                var results = downloadInfo.result;
                Logger.Trace(string.Format("Results count : {0}", results.Count));
                foreach (var result in results)
                {
                    Logger.Trace(string.Format("Going through result {0}", result.NZBName));
                    var percentage = (result.DownloadedSizeMB / (result.RemainingSizeMB + (double)result.DownloadedSizeMB) * 100);
                    Logger.Trace(string.Format("Percentage : {0}", percentage));

                    var status = EnumHelper<DownloadStatus>.Parse(result.Status);
                    var progressBar = "progress-bar-danger";
                    if (status == DownloadStatus.PAUSED || status == DownloadStatus.QUEUED)
                    {
                        progressBar = "progress-bar-warning";
                    }
                    if (status == DownloadStatus.DOWNLOADING)
                    {
                        progressBar = "progress-bar-success";
                    }

                    model.DownloadItem.Add(new DownloadItem
                    {
                        FontAwesomeIcon = IconHelper.ChooseIcon(EnumHelper<DownloadStatus>.Parse(result.Status)),
                        DownloadPercentage = Math.Ceiling(percentage).ToString(CultureInfo.CurrentUICulture),
                        DownloadingName = result.NZBName,
                        Status = status,
                        NzbId = result.NZBID,
                        ProgressBarClass = progressBar
                    });
                }

                return PartialView("Partial/_Download", model);
            }
            catch (Exception e)
            {
                Logger.Error(e.Message, e);
                ViewBag.Error = e.Message;
                return PartialView("DashletError");
            }
        }

        [HttpGet]
        public ActionResult GetNzbGetDownloadHistory()
        {
            try
            {
                var config = SettingsService.GetSettings();
                var formattedUri = UrlHelper.ReturnUri(config.IpAddress, config.Port).ToString();
                var history = Api.GetNzbGetHistory(formattedUri, config.Username, config.Password);

                var model = new List<NzbGetHistoryViewModel>();
                foreach (var result in history.result)
                {
                    var singleItem = new NzbGetHistoryViewModel();
                    var mappedResult = (NzbGetHistoryViewModel)singleItem.InjectFrom(new NzbGetHistoryMapper(), result);
                    if (!string.IsNullOrEmpty(mappedResult.FileSize))
                    {
                        long newFileSize;
                        long.TryParse(mappedResult.FileSize.ToString(), out newFileSize);
                        mappedResult.FileSize = MemorySizeConverter.SizeSuffixMb(newFileSize);
                    }
                    model.Add(mappedResult);
                }

                return PartialView("Partial/History", model);
            }
            catch (Exception e)
            {
                Logger.Error(e.Message, e);
                return PartialView("DashletError");
            }
        }

        [HttpGet]
        public JsonResult History()
        {
            var f = new Fixture();
            var model = f.CreateMany<NzbGetHistoryViewModel>();

            return Json(model, JsonRequestBehavior.AllowGet);
        }
    }
}