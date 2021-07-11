﻿using ClipboardCanvas.Enums;
using ClipboardCanvas.Exceptions;
using ClipboardCanvas.Helpers.Filesystem;
using ClipboardCanvas.Helpers.SafetyHelpers;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;

namespace ClipboardCanvas.ReferenceItems
{
    /// <summary>
    /// A file containing data and reference to an actual file
    /// </summary>
    public sealed class ReferenceFile
    {
        private readonly StorageFile _innerReferenceFile;

        public SafeWrapperResult LastError { get; private set; } = SafeWrapperResult.S_SUCCESS;

        public IStorageItem ReferencedItem { get; private set; }

        public ReferenceFileData ReferenceFileData { get; private set; }

        private ReferenceFile(StorageFile innerFile, IStorageItem referencedItem)
        {
            this._innerReferenceFile = innerFile;
            this.ReferencedItem = referencedItem;
        }

        public async Task UpdateReferenceFile(ReferenceFileData referenceFileData)
        {
            string serialized = JsonConvert.SerializeObject(referenceFileData, Formatting.Indented);
            await FileIO.WriteTextAsync(_innerReferenceFile, serialized);
        }

        internal static async Task<ReferenceFileData> ReadData(StorageFile referenceFile)
        {
            string data = await FileIO.ReadTextAsync(referenceFile);
            ReferenceFileData referenceFileData = JsonConvert.DeserializeObject<ReferenceFileData>(data);

            return referenceFileData;
        }

        public static async Task<ReferenceFile> GetFile(StorageFile referenceFile)
        {
            // The file is not a Reference File
            if (!IsReferenceFile(referenceFile))
            {
                return null;
            }
            // The file does not exist
            if (!StorageHelpers.Exists(referenceFile.Path))
            {
                return new ReferenceFile(referenceFile, null)
                {
                    LastError = new SafeWrapperResult(OperationErrorCode.NotFound, new FileNotFoundException(), "Couldn't resolve item associated with path.")
                };
            }

            ReferenceFileData referenceFileData = await ReadData(referenceFile);

            return await GetFile(referenceFile, referenceFileData);
        }

        public static async Task<ReferenceFile> GetFile(StorageFile referenceFile, ReferenceFileData referenceFileData)
        {
            if (referenceFileData == null || string.IsNullOrEmpty(referenceFileData.path))
            {
                return new ReferenceFile(referenceFile, null)
                {
                    LastError = new SafeWrapperResult(OperationErrorCode.InvalidArgument, new ArgumentNullException(), "The Reference File data is corrupt.")
                };
            }

            SafeWrapper<IStorageItem> file = await StorageHelpers.ToStorageItemWithError<IStorageItem>(referenceFileData.path);

            if (!file)
            {
                if (file == OperationErrorCode.NotFound)
                {
                    // If NotFound, use custom exception for LoadCanvasFromCollection()
                    return new ReferenceFile(referenceFile, null)
                    {
                        LastError = new SafeWrapperResult(OperationErrorCode.NotFound, new ReferencedFileNotFoundException(), "The item referenced could not be found.")
                    };
                }
                else
                {
                    return new ReferenceFile(referenceFile, null)
                    {
                        LastError = (SafeWrapperResult)file
                    };
                }
            }

            return new ReferenceFile(referenceFile, file.Result);
        }

        public static bool IsReferenceFile(StorageFile file)
        {
            return file?.Path.EndsWith(Constants.FileSystem.REFERENCE_FILE_EXTENSION) ?? false;
        }
    }
}
