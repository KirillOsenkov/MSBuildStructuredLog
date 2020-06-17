using Microsoft.Extensions.DependencyInjection;
using System;

namespace Blazor.FileReader
{
    /// <summary>
    /// Provides extension methods for setting up <see cref="IFileReaderService"/>
    /// </summary>
    public static class SetupExtension
    {
        /// <summary>
        /// Adds <see cref="IFileReaderService"/> as a scoped service
        /// to the specified <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="services"></param>
        public static IServiceCollection AddFileReaderService(this IServiceCollection services)
        {
            services.AddSingleton<IFileReaderServiceOptions, FileReaderServiceOptions>();
            services.AddScoped<IFileReaderService, FileReaderService>();
            return services;
        }

        /// <summary>
        /// Adds <see cref="IFileReaderService"/> as a scoped service
        /// to the specified <see cref="IServiceCollection"/> with the specifed <paramref name="setOptions"/>
        /// </summary>
        /// <param name="services"></param>
        /// <param name="setOptions">Delegate that modifies the options for <see cref="IFileReaderService"/> </param>
        public static IServiceCollection AddFileReaderService(this IServiceCollection services, Action<IFileReaderServiceOptions> setOptions)
        {
            if (setOptions is null)
            {
                throw new ArgumentNullException(nameof(setOptions));
            }

            services.AddSingleton<IFileReaderServiceOptions, FileReaderServiceOptions>(si => {
                var o = new FileReaderServiceOptions();
                setOptions(o);
                if (o.UseWasmSharedBuffer && !IJSRuntimeExtensions.IsInvokeUnmarshalledSupported())
                {
                    throw new PlatformNotSupportedException($"{nameof(o.UseWasmSharedBuffer)}=true is not supported on this platform.");
                }
                return o;
            });

            services.AddScoped<IFileReaderService, FileReaderService>();
            
            return services;
        }
    }
}
