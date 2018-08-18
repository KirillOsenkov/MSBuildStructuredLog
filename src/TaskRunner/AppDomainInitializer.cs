namespace TaskRunner
{
    public class AppDomainInitializer
    {
        /// <summary>
        /// This is called everytime a new appdomain is created in the current process.
        /// </summary>
        public AppDomainInitializer()
        {
            Microsoft.Build.Locator.MSBuildLocator.RegisterDefaults();
        }
    }
}
