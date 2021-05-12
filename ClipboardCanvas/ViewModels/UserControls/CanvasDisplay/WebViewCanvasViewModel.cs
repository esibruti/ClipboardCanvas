﻿using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using Windows.ApplicationModel.DataTransfer;
using Windows.Media.Core;
using Windows.Storage;

using ClipboardCanvas.Helpers.SafetyHelpers;
using ClipboardCanvas.Helpers.SafetyHelpers.ExceptionReporters;
using ClipboardCanvas.Models;
using ClipboardCanvas.Enums;
using ClipboardCanvas.ModelViews;
using ClipboardCanvas.Helpers.Filesystem;
using ClipboardCanvas.ReferenceItems;
using ClipboardCanvas.Helpers;
using ClipboardCanvas.EventArguments.CanvasControl;
using ClipboardCanvas.Extensions;
using ClipboardCanvas.UnsafeNative;

namespace ClipboardCanvas.ViewModels.UserControls.CanvasDisplay
{
    public class WebViewCanvasViewModel : BasePasteCanvasViewModel
    {
        #region Private Members

        private readonly IDynamicPasteCanvasControlView _view;

        private readonly WebViewCanvasMode _mode;

        #endregion

        #region Protected Members

        protected override ICollectionsContainerModel AssociatedContainer => _view?.CollectionContainer;

        #endregion

        #region Constructor

        public WebViewCanvasViewModel(IDynamicPasteCanvasControlView view, WebViewCanvasMode mode)
            : base(StaticExceptionReporters.DefaultSafeWrapperExceptionReporter)
        {
            this._view = view;
            this._mode = mode;
        }

        #endregion

        #region Public Properties

        public static List<string> Extensions => new List<string>() {
            ".html", ".htm", Constants.FileSystem.WEBSITE_LINK_FILE_EXTENSION,
        };

        private bool _ContentWebViewLoad;
        public bool ContentWebViewLoad
        {
            get => _ContentWebViewLoad;
            set => SetProperty(ref _ContentWebViewLoad, value);
        }

        private string _TextHtml;
        public string TextHtml
        {
            get => _TextHtml;
            set => SetProperty(ref _TextHtml, value);
        }

        private string _Source;
        public string Source
        {
            get => _Source;
            set => SetProperty(ref _Source, value);
        }

        #endregion

        #region Override

        protected override async Task<SafeWrapperResult> SetData(DataPackageView dataPackage)
        {
            SafeWrapper<string> text = await SafeWrapperRoutines.SafeWrapAsync(
                   () => dataPackage.GetTextAsync().AsTask());

            if (_mode == WebViewCanvasMode.ReadWebsite)
            {
                _Source = text;
            }
            else
            {
                _TextHtml = text;
            }

            return text;
        }

        public override async Task<SafeWrapperResult> TrySaveData()
        {
            SafeWrapperResult result;

            if (_mode == WebViewCanvasMode.ReadWebsite)
            {
                result = await SafeWrapperRoutines.SafeWrapAsync(() => FileIO.WriteTextAsync(associatedFile, Source).AsTask());
            }
            else
            {
                result = await SafeWrapperRoutines.SafeWrapAsync(() => FileIO.WriteTextAsync(associatedFile, TextHtml).AsTask());
            }

            return result;
        }

        protected override async Task<SafeWrapperResult> SetData(StorageFile file)
        {
            SafeWrapper<string> text = SafeWrapperRoutines.SafeWrap(() => UnsafeNativeHelpers.ReadStringFromFile(file.Path));

            if (_mode == WebViewCanvasMode.ReadWebsite)
            {
                _Source = text;
            }
            else
            {
                _TextHtml = text;
            }

            return text;
        }

        protected override async Task<SafeWrapper<StorageFile>> TrySetFileWithExtension()
        {
            SafeWrapper<StorageFile> file;

            if (_mode == WebViewCanvasMode.ReadWebsite)
            {
                file = await AssociatedContainer.GetEmptyFileToWrite(Constants.FileSystem.WEBSITE_LINK_FILE_EXTENSION);
            }
            else
            {
                file = await AssociatedContainer.GetEmptyFileToWrite(".html");
            }

            return file;
        }

        protected override async Task<SafeWrapperResult> TryFetchDataToView()
        {
            this.ContentWebViewLoad = true;

            if (_mode == WebViewCanvasMode.ReadWebsite)
            {
                OnPropertyChanged(nameof(Source));
            }
            else
            {
                OnPropertyChanged(nameof(TextHtml));
            }

            return await Task.FromResult(SafeWrapperResult.S_SUCCESS);
        }

        public override async Task<IEnumerable<SuggestedActionsControlItemViewModel>> GetSuggestedActions()
        {
            return null;
        }

        #endregion

        #region IDisposable

        public override void Dispose()
        {
            base.Dispose();

            this.ContentWebViewLoad = false;
        }

        #endregion
    }
}