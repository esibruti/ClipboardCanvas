﻿using Microsoft.Toolkit.Mvvm.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

using ClipboardCanvas.Enums;
using ClipboardCanvas.Helpers;
using ClipboardCanvas.Helpers.SafetyHelpers;
using ClipboardCanvas.ReferenceItems;
using ClipboardCanvas.Services;
using ClipboardCanvas.ViewModels.UserControls.CanvasDisplay;

namespace ClipboardCanvas.DataModels.PastedContentDataModels
{
    public abstract class BaseContentTypeModel
    {
        public static readonly SafeWrapperResult FoldersNotSupportedResult = new SafeWrapperResult(OperationErrorCode.InvalidOperation, new InvalidOperationException(), "Displaying content for folders is not yet supported.");

        public static readonly SafeWrapperResult CannotDisplayContentForTypeResult = new SafeWrapperResult(OperationErrorCode.InvalidOperation, new InvalidOperationException(), "Couldn't display content for this file");

        public static readonly SafeWrapperResult CannotReceiveClipboardDataResult = new SafeWrapperResult(OperationErrorCode.AccessUnauthorized, "Couldn't retrieve clipboard data");

        public static async Task<BaseContentTypeModel> GetContentType(CanvasItem canvasFile, BaseContentTypeModel contentType)
        {
            if (contentType is InvalidContentTypeDataModel invalidContentType)
            {
                if (!invalidContentType.needsReinitialization)
                {
                    return invalidContentType;
                }
            }

            if (contentType != null)
            {
                return contentType;
            }
            
            if ((await canvasFile.SourceItem) is StorageFolder folder)
            {
                if (folder.Path.EndsWith(Constants.FileSystem.INFINITE_CANVAS_EXTENSION))
                {
                    return new InfiniteCanvasContentType();
                }
                else
                {
                    return new InvalidContentTypeDataModel(FoldersNotSupportedResult, false);
                }    
            }
            else if ((await canvasFile.SourceItem) is StorageFile file)
            {
                string ext = Path.GetExtension(file.Path);

                return await GetContentTypeFromExtension(file, ext);
            }
            else // The sourceFile was null
            {
                return new InvalidContentTypeDataModel(CannotDisplayContentForTypeResult, false);
            }
        }

        public static async Task<BaseContentTypeModel> GetContentType(IStorageItem item, BaseContentTypeModel contentType)
        {
            if (contentType is InvalidContentTypeDataModel invalidContentType)
            {
                if (!invalidContentType.needsReinitialization)
                {
                    return invalidContentType;
                }
            }

            if (contentType != null)
            {
                return contentType;
            }

            if (item is StorageFile file)
            {
                string ext = Path.GetExtension(file.Path);

                if (ReferenceFile.IsReferenceFile(file))
                {
                    // Reference File, get the destination file extension
                    ReferenceFile referenceFile = await ReferenceFile.GetFile(file);

                    if (referenceFile.ReferencedItem == null)
                    {
                        return new InvalidContentTypeDataModel(referenceFile.LastError, false);
                    }

                    if (referenceFile.ReferencedItem is StorageFolder)
                    {
                        return new InvalidContentTypeDataModel(new SafeWrapperResult(OperationErrorCode.InvalidOperation, new InvalidOperationException(), "Displaying content for folders is not yet supported."), false);
                    }
                    else
                    {
                        file = referenceFile.ReferencedItem as StorageFile;
                    }

                    ext = Path.GetExtension(file.Path);
                }

                return await GetContentTypeFromExtension(file, ext);
            }
            else if (item is StorageFolder)
            {
                return new InvalidContentTypeDataModel(FoldersNotSupportedResult, false);
            }
            else
            {
                return new InvalidContentTypeDataModel(CannotDisplayContentForTypeResult, false);
            }
        }

        public static async Task<BaseContentTypeModel> GetContentTypeFromExtension(IStorageItem item, string ext)
        {
            if (item is StorageFolder folder)
            {
                if (folder.Path.EndsWith(Constants.FileSystem.INFINITE_CANVAS_EXTENSION))
                {
                    return new InfiniteCanvasContentType();
                }
                else
                {
                    return new InvalidContentTypeDataModel(FoldersNotSupportedResult, false);
                }
            }

            if (item is not StorageFile file)
            {
                return null;
            }

            // Image
            if (ImageCanvasViewModel.Extensions.Contains(ext))
            {
                return new ImageContentType();
            }

            // Text
            if (TextCanvasViewModel.Extensions.Contains(ext))
            {
                return new TextContentType();
            }

            // Media
            if (MediaCanvasViewModel.Extensions.Contains(ext))
            {
                return new MediaContentType();
            }

            // WebView
            if (WebViewCanvasViewModel.Extensions.Contains(ext))
            {
                if (ext == Constants.FileSystem.WEBSITE_LINK_FILE_EXTENSION)
                {
                    return new WebViewContentType(WebViewCanvasMode.ReadWebsite);
                }

                return new WebViewContentType(WebViewCanvasMode.ReadHtml);
            }

            // Markdown
            if (MarkdownCanvasViewModel.Extensions.Contains(ext))
            {
                return new MarkdownContentType();
            }

            // Default, try as text
            if (await TextCanvasViewModel.CanLoadAsText(file))
            {
                // Text
                return new TextContentType();
            }

            // Use fallback
            return new FallbackContentType();
        }

        public static async Task<BaseContentTypeModel> GetContentTypeFromDataPackage(DataPackageView dataPackage)
        {
            IUserSettingsService userSettings = Ioc.Default.GetService<IUserSettingsService>();

            // Decide content type and initialize view model

            // From raw clipboard data
            if (dataPackage.Contains(StandardDataFormats.Bitmap))
            {
                // Image
                return new ImageContentType();
            }
            else if (dataPackage.Contains(StandardDataFormats.Text))
            {
                SafeWrapper<string> text = await SafeWrapperRoutines.SafeWrapAsync(() => dataPackage.GetTextAsync().AsTask());

                if (!text)
                {
                    Debugger.Break(); // What!?
                    return new InvalidContentTypeDataModel(CannotReceiveClipboardDataResult);
                }

                // Check if it's url
                if (StringHelpers.IsUrl(text))
                {
                    // The url may point to file
                    if (StringHelpers.IsUrlFile(text))
                    {
                        // Image
                        return new SafeWrapper<BaseContentTypeModel>(new ImageContentType(), SafeWrapperResult.SUCCESS);
                    }
                    else
                    {
                        // Webpage link
                        //InitializeViewModel(() => new WebViewCanvasViewModel(_view, WebViewCanvasMode.ReadWebsite, CanvasPreviewMode.InteractionAndPreview));
                        if (userSettings.PrioritizeMarkdownOverText)
                        {
                            // Markdown
                            return new MarkdownContentType();
                        }
                        else
                        {
                            // Normal text
                            return new TextContentType();
                        }
                    }
                }
                else
                {
                    if (userSettings.PrioritizeMarkdownOverText)
                    {
                        // Markdown
                        return new MarkdownContentType();
                    }
                    else
                    {
                        // Normal text
                        return new TextContentType();
                    }
                }
            }
            else if (dataPackage.Contains(StandardDataFormats.StorageItems)) // From clipboard storage items
            {
                IReadOnlyList<IStorageItem> items = await dataPackage.GetStorageItemsAsync();

                if (items.Count > 1)
                {
                    // TODO: More than one item, paste in Boundless Canvas
                }
                else if (items.Count == 1)
                {
                    // One item, decide view model for it
                    IStorageItem item = items.First();

                    BaseContentTypeModel contentType = await BaseContentTypeModel.GetContentType(item, null);
                    if (contentType is InvalidContentTypeDataModel)
                    {
                        return new InvalidContentTypeDataModel(CannotReceiveClipboardDataResult);
                    }

                    return contentType;
                }
                else
                {
                    // No items
                    return new InvalidContentTypeDataModel(CannotReceiveClipboardDataResult);
                }
            }

            return new InvalidContentTypeDataModel(CannotReceiveClipboardDataResult);
        }
    }
}