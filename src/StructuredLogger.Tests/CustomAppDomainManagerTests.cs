// using System;
// using System.Reflection;
// using System.Security.Policy;
// using Xunit;
// using Microsoft.Build.Logging.StructuredLogger;
// 
// namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
// {
//     /// <summary>
//     /// Unit tests for the <see cref="CustomAppDomainManager"/> class.
//     /// </summary>
// //     public class CustomAppDomainManagerTests [Error] (18-28)CS0246 The type or namespace name 'CustomAppDomainManager' could not be found (are you missing a using directive or an assembly reference?)
// //     {
// //         private readonly CustomAppDomainManager _manager; [Error] (14-26)CS0246 The type or namespace name 'CustomAppDomainManager' could not be found (are you missing a using directive or an assembly reference?)
// 
//         public CustomAppDomainManagerTests()
//         {
//             _manager = new CustomAppDomainManager();
//         }
// 
//         /// <summary>
//         /// Tests that the CreateDomain method creates a new AppDomain with the given friendly name.
//         /// This test simulates a happy path scenario where the base method is expected to create a new AppDomain.
//         /// </summary>
//         [Fact]
//         public void CreateDomain_WithValidParameters_ReturnsNewAppDomain()
//         {
//             // Arrange
//             string friendlyName = "TestDomain";
//             Evidence evidence = AppDomain.CurrentDomain.Evidence;
//             var setup = new AppDomainSetup
//             {
//                 ApplicationBase = AppDomain.CurrentDomain.BaseDirectory
//             };
// 
//             // Act
//             AppDomain newDomain = null;
//             try
//             {
//                 newDomain = _manager.CreateDomain(friendlyName, evidence, setup);
//                 // Assert
//                 Assert.NotNull(newDomain);
//                 // Note: The FriendlyName of a created AppDomain may not exactly match the provided friendly name,
//                 // so we perform a loose check that the returned AppDomain's FriendlyName contains the friendly name.
//                 Assert.Contains(friendlyName, newDomain.FriendlyName);
//             }
//             finally
//             {
//                 if (newDomain != null)
//                 {
//                     AppDomain.Unload(newDomain);
//                 }
//             }
//         }
// 
//         /// <summary>
//         /// Tests that the InitializeNewDomain method does not throw an exception when called in the default AppDomain.
//         /// In the default AppDomain, NotifyEntrypointAssembly should return early without attempting to load any assembly.
//         /// </summary>
// //         [Fact] [Error] (67-36)CS0117 'Record' does not contain a definition for 'Exception'
// //         public void InitializeNewDomain_InDefaultAppDomain_DoesNotThrow()
// //         {
// //             // Arrange
// //             var setup = new AppDomainSetup();
// // 
// //             // Act & Assert
// //             var exception = Record.Exception(() => _manager.InitializeNewDomain(setup));
// //             Assert.Null(exception);
// //         }
// 
//         /// <summary>
//         /// Tests that the InitializeNewDomain method throws a FileNotFoundException when executed in a non-default AppDomain
//         /// and the "TaskRunner" assembly is missing. This simulates the scenario where the assembly dependency is not available,
//         /// and thus Assembly.Load("TaskRunner") fails.
//         /// </summary>
//         [Fact]
//         public void InitializeNewDomain_InNonDefaultAppDomain_WhenAssemblyMissing_ThrowsFileNotFoundException()
//         {
//             // Arrange
//             var setup = new AppDomainSetup
//             {
//                 ApplicationBase = AppDomain.CurrentDomain.BaseDirectory
//             };
// 
//             // Create a new AppDomain with a unique name.
//             string domainName = "NonDefaultTestDomain_" + Guid.NewGuid().ToString();
//             AppDomain testDomain = null;
//             try
//             {
//                 testDomain = AppDomain.CreateDomain(domainName, AppDomain.CurrentDomain.Evidence, setup);
//                 // Create an instance of the helper in the new domain.
//                 var helper = (CustomAppDomainManagerTestHelper)testDomain.CreateInstanceAndUnwrap(
//                     typeof(CustomAppDomainManagerTestHelper).Assembly.FullName,
//                     typeof(CustomAppDomainManagerTestHelper).FullName);
//                 // Act
//                 string result = helper.CallInitializeNewDomain();
//                 // Assert
//                 Assert.Equal("FileNotFoundException", result);
//             }
//             finally
//             {
//                 if (testDomain != null)
//                 {
//                     AppDomain.Unload(testDomain);
//                 }
//             }
//         }
//     }
// 
//     /// <summary>
//     /// A helper class that is instantiated in a non-default AppDomain to invoke InitializeNewDomain.
//     /// It calls InitializeNewDomain and returns the type name of any exception encountered.
//     /// </summary>
//     public class CustomAppDomainManagerTestHelper : MarshalByRefObject
//     {
//         /// <summary>
//         /// Invokes InitializeNewDomain on a new instance of CustomAppDomainManager and returns the exception type name if thrown.
//         /// If no exception is thrown, returns "NoException".
//         /// </summary>
//         /// <returns>A string representing the exception type name, or "NoException" if none occurred.</returns>
// //         public string CallInitializeNewDomain() [Error] (125-35)CS0246 The type or namespace name 'CustomAppDomainManager' could not be found (are you missing a using directive or an assembly reference?)
// //         {
// //             try
// //             {
// //                 var manager = new CustomAppDomainManager();
// //                 // This call is expected to trigger NotifyEntrypointAssembly.
// //                 // Since the new AppDomain is not the default and the "TaskRunner" assembly is missing,
// //                 // a FileNotFoundException is expected.
// //                 manager.InitializeNewDomain(new AppDomainSetup());
// //                 return "NoException";
// //             }
// //             catch (Exception ex)
// //             {
// //                 return ex.GetType().Name;
// //             }
// //         }
//     }
// }
