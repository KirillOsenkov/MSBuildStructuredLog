using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Blazor.FileReader
{
    internal static class IJSRuntimeExtensions
    {
        private readonly static Type WebAssemblyJSRuntime
            = Type.GetType("Microsoft.JSInterop.WebAssembly.WebAssemblyJSRuntime, Microsoft.JSInterop.WebAssembly") ??
            Type.GetType("Mono.WebAssembly.Interop.MonoWebAssemblyJSRuntime, Mono.WebAssembly.Interop");

        private readonly static MethodInfo _invokeUnmarshalled = GetInvokeUnmarshalled();
        private readonly static Dictionary<string, MethodInfo> _genericinvokeUnmarshalledMethods 
            = new Dictionary<string, MethodInfo>();

        public static bool IsInvokeUnmarshalledSupported()
        {
            return _invokeUnmarshalled != null;
        } 

        public static TReturn InvokeUnmarshalled<TParam, TReturn>(this IJSRuntime jsRuntime, string methodName, TParam parameter)
        {
            var cacheKey = $"{typeof(TReturn)}|{typeof(TParam)}";
            if (!_genericinvokeUnmarshalledMethods.TryGetValue(cacheKey, out var genericMethodInfo))
            {
                // Create InvokeUnmarshalled<T0, T1, T2, TResult>
                genericMethodInfo = _invokeUnmarshalled.MakeGenericMethod(typeof(TParam), typeof(object), typeof(object), typeof(TReturn));
                _genericinvokeUnmarshalledMethods.Add(cacheKey, genericMethodInfo);
            }

            return (TReturn)genericMethodInfo.Invoke(jsRuntime, new object[] { methodName, parameter, null, null });
        }

        private static MethodInfo GetInvokeUnmarshalled()
        {
            if (WebAssemblyJSRuntime == null)
            {
                System.Diagnostics.Debug.WriteLine($"{nameof(IJSRuntimeExtensions)} : MonoWebAssemblyJSRuntime not found.");
                return null;
            }

            foreach (var methodInfo in WebAssemblyJSRuntime.GetMethods())
            {
                // Looking for TResult InvokeUnmarshalled<T0, T1, T2, TResult>(string identifier, T0 arg0, T1 arg1, T2 arg3)
                if (methodInfo.Name == "InvokeUnmarshalled" && methodInfo.GetParameters().Length == 4)
                {
                    return methodInfo;
                }
            }

            System.Diagnostics.Debug.WriteLine($"{nameof(IJSRuntimeExtensions)} : MonoWebAssemblyJSRuntime.InvokeUnmarshalled<T0, T1, T2, TResult> method not found.");

            return null;
        }
    }
}
