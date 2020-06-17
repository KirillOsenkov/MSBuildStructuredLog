using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Blazor.FileReader
{
    /// <summary>
    /// Provides methods for interacting with an element that provides file streams.
    /// </summary>
    public interface IFileReaderRef
    {
        /// <summary>
        /// Register for drop events on the source element
        /// </summary>
        /// <param name="additive">If set to true, drop target file list becomes additive. Defaults to false.</param>
        /// <returns></returns>
        Task RegisterDropEventsAsync(bool additive = false);

        /// <summary>
        /// Unregister drop events on the source element
        /// </summary>
        /// <returns></returns>
        Task UnregisterDropEventsAsync();

        /// <summary>
        /// Clears any value set on the source element
        /// </summary>
        /// <returns></returns>
        Task ClearValue();

        /// <summary>
        /// Enumerates the currently selected file references
        /// </summary>
        /// <returns></returns>
        Task<IEnumerable<IFileReference>> EnumerateFilesAsync();
    }

    /// <summary>
    /// Provides properties and instance methods for the reading file metadata and aids in the creation of Readonly Stream objects. 
    /// </summary>
    public interface IFileReference
    {
        /// <summary>
        /// Opens a stream to read the file.
        /// </summary>
        /// <returns></returns>
        Task<AsyncDisposableStream> OpenReadAsync();

        /// <summary>
        /// Opens a base64-encoded string stream to read the file
        /// </summary>
        /// <returns></returns>
        Task<IBase64Stream> OpenReadBase64Async();

        /// <summary>
        /// Convenience method to read the file into memory using a single interop call 
        /// and returns it as a MemoryStream. Buffer size will be equal to the file size.
        /// </summary>
        /// <returns></returns>
        Task<MemoryStream> CreateMemoryStreamAsync();

        /// <summary>
        /// Convenience method to read the file into memory and returns it as a MemoryStream, using the specified buffersize.
        /// </summary>
        /// <returns></returns>
        Task<MemoryStream> CreateMemoryStreamAsync(int bufferSize);

        /// <summary>
        /// Reads the file metadata
        /// </summary>
        /// <returns></returns>
        Task<IFileInfo> ReadFileInfoAsync();
    }

    /// <summary>
    /// Provides a base64-encoded string view of a sequence of bytes from a file.
    /// </summary>
    public interface IBase64Stream : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// Gets or sets the current byte position in the Stream.
        /// </summary>
        long Position { get; set; }

        /// <summary>
        /// Gets the length of the stream in bytes.
        /// </summary>
        long Length { get; }

        /// <summary>
        /// Asynchronously reads a sequence of bytes as a base64 encoded string from the current stream 
        /// and advances the position within the stream by the number of bytes read.
        /// </summary>
        /// <param name="offset">The byte offset in the source at which to begin reading data from the stream.</param>
        /// <param name="count">The maximum number of bytes to read.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The requested sequence of bytes as a base64 encoded string. 
        /// The resulting string can be shorter than the number of bytes requested if
        /// the number of bytes currently available is less than the requested 
        /// number, or it can be string.empty if the end of the stream has been reached. </returns>
        Task<string> ReadAsync(int offset, int count, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Provides properties for file metadata.
    /// </summary>
    public interface IFileInfo
    {
        /// <summary>
        /// Returns the name of the file referenced by the File object.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Returns a list of non-standard DOM properties attached to the object, like the webkitRelativePath property.
        /// </summary>
        Dictionary<string,object> NonStandardProperties { get; }

        /// <summary>
        /// Returns the size of the file in bytes.
        /// </summary>
        long Size { get; }

        /// <summary>
        /// Returns the MIME type of the file.
        /// </summary>
        string Type { get; }

        /// <summary>
        /// Returns the last modified time of the file, in millisecond since the UNIX epoch (January 1st, 1970 at Midnight).
        /// </summary>
        long? LastModified { get; }

        /// <summary>
        /// Returns the last modified time of the file.
        /// </summary>
        DateTime? LastModifiedDate { get; }
    }
    
    internal class FileReaderRef : IFileReaderRef
    {
        public async Task<IEnumerable<IFileReference>> EnumerateFilesAsync() => 
            Enumerable.Range(0, Math.Max(0, await this.FileReaderJsInterop.GetFileCount(this.ElementRef)))
                .Select(index => (IFileReference)new FileReference(this, index));

        public async Task RegisterDropEventsAsync(bool additive) => await this.FileReaderJsInterop.RegisterDropEvents(this.ElementRef, additive);
        public async Task UnregisterDropEventsAsync() => await this.FileReaderJsInterop.UnregisterDropEvents(this.ElementRef);

        public async Task ClearValue() 
            => await this.FileReaderJsInterop.ClearValue(this.ElementRef);

        public ElementReference ElementRef { get; private set; }
        public FileReaderJsInterop FileReaderJsInterop { get; }

        internal FileReaderRef(ElementReference elementRef, FileReaderJsInterop fileReaderJsInterop)
        {
            this.ElementRef = elementRef;
            this.FileReaderJsInterop = fileReaderJsInterop;
        }
    }

    internal class FileReference : IFileReference
    {
        private readonly FileReaderRef fileLoaderRef;
        private readonly int index;
        private IFileInfo fileInfo;

        public FileReference(FileReaderRef fileLoaderRef, int index)
        {
            this.fileLoaderRef = fileLoaderRef;
            this.index = index;
        }

        public async Task<MemoryStream> CreateMemoryStreamAsync() {
            return await InnerCreateMemoryStreamAsync((int)(await ReadFileInfoAsync()).Size);
        }
        public Task<MemoryStream> CreateMemoryStreamAsync(int bufferSize)
        {
            return InnerCreateMemoryStreamAsync(bufferSize);
        }

        public Task<AsyncDisposableStream> OpenReadAsync()
        {
            return this.fileLoaderRef.FileReaderJsInterop.OpenFileStream(this.fileLoaderRef.ElementRef, index);
        }

        public async Task<IFileInfo> ReadFileInfoAsync()
        {
            if (fileInfo == null)
            {
                fileInfo = await this.fileLoaderRef.FileReaderJsInterop.GetFileInfoFromElement(fileLoaderRef.ElementRef, index);
            }

            return fileInfo;
        }

        public async Task<IBase64Stream> OpenReadBase64Async()
        {
            return await this.fileLoaderRef.FileReaderJsInterop.OpenBase64Stream(fileLoaderRef.ElementRef, index);
        }

        private async Task<MemoryStream> InnerCreateMemoryStreamAsync(int bufferSize)
        {
            MemoryStream memoryStream;
            if (bufferSize < 1)
            {
                throw new InvalidOperationException("Unable to determine buffersize or provided buffersize was 0 or less");
            }
            else
            {
                memoryStream = new MemoryStream(bufferSize);
            }

            var buffer = new byte[bufferSize];
            
            await using (var fs = await OpenReadAsync())
            {
                int count;
                while ((count = await fs.ReadAsync(buffer, 0, buffer.Length)) != 0)
                {
                    await memoryStream.WriteAsync(buffer, 0, count);
                }
            }
            memoryStream.Position = 0;
            return memoryStream;
        }

    }

    internal class FileInfo : IFileInfo
    {
        private static readonly DateTime Epoch = new DateTime(1970, 01, 01);
        private readonly Lazy<DateTime?> lastModifiedDate;
        public FileInfo()
        {
            this.lastModifiedDate = new Lazy<DateTime?>(() =>
                LastModified == null ? null : (DateTime?)Epoch.AddMilliseconds(this.LastModified.Value));
        }

        public string Name { get; set; }

        public Dictionary<string,object> NonStandardProperties { get; set; }

        public long Size { get; set; }

        public string Type { get; set; }

        public long? LastModified { get; set; }

        public DateTime? LastModifiedDate => this.lastModifiedDate.Value;
    }

    public abstract class AsyncDisposableStream : Stream, IAsyncDisposable
    {
        /// <inheritdoc/>
        public abstract ValueTask DisposeAsync();
    }

}
