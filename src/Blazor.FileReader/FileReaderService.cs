using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Threading.Tasks;

namespace Blazor.FileReader
{
    /// <summary>
    /// Provides configuration options for <see cref="IFileReaderService"/>
    /// </summary>
    public interface IFileReaderServiceOptions
    {
        /// <summary>
        /// Initializes the file service on the first interop call.
        /// Redundant for client-side blazor.
        /// </summary>
        /// <remarks>
        /// Initializing on the first call is neccessary only if the javascript 
        /// interop file (FileReaderComponent.js)
        /// has not been loaded manually using a script tag.
        /// </remarks>
        bool InitializeOnFirstCall { get; set; }

        /// <summary>
        /// For client-side blazor, uses shared memory buffer to transfer data quickly.
        /// Not available for server-side blazor.
        /// </summary>
        bool UseWasmSharedBuffer { get; set; }
    }

    internal class FileReaderServiceOptions : IFileReaderServiceOptions
    {
        public bool InitializeOnFirstCall { get; set; } = true;

        public bool UseWasmSharedBuffer { get; set; } = false;
    }

    /// <summary>
    /// Servive for creating a <see cref="IFileReaderRef"/> instance from an element.
    /// </summary>
    public interface IFileReaderService
    {
        /// <summary>
        /// Explicitly initializes this instance by loading the neccessary interop code to the browser.
        /// </summary>
        /// <returns></returns>
        Task EnsureInitializedAsync();

        /// <summary>
        /// Creates a new instance of <see cref="IFileReaderRef"/> for the specified element.
        /// </summary>
        /// <param name="element">A reference to an element that can provide file streams. 
        /// Should be obtained using the @ref attribute. 
        /// Should reference either an input element of type file or a drop target.</param>
        /// <returns>a new instance of <see cref="IFileReaderRef"/></returns>
        IFileReaderRef CreateReference(ElementReference element);
    }

    internal class FileReaderService : IFileReaderService
    {
        private readonly FileReaderJsInterop _fileReaderJsInterop;

        public FileReaderService(IJSRuntime jsRuntime, IFileReaderServiceOptions options)
        {
            this.Options = options;
            this._fileReaderJsInterop = new FileReaderJsInterop(jsRuntime, options);
        }

        public IFileReaderServiceOptions Options { get; }

        public IFileReaderRef CreateReference(ElementReference element)
        {
            return new FileReaderRef(element, this._fileReaderJsInterop);
        }

        public async Task EnsureInitializedAsync()
        {
            await this._fileReaderJsInterop.EnsureInitializedAsync(true);
        }
    }
}
