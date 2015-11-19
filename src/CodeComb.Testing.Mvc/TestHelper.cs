using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.Extensions.Logging;

namespace CodeComb.Testing.Mvc
{
    public static class TestHelper
    {
        public static IServiceCollection AddTestApplicationEnvironment<TStartup>(this IServiceCollection self)
    where TStartup : class
        {
            self.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
            var type = typeof(TStartup);
            var applicationServices = CallContextServiceLocator.Locator.ServiceProvider;
            var libraryManager = applicationServices.GetRequiredService<ILibraryManager>();
            var applicationName = type.Assembly.GetName().Name;
            var library = libraryManager.GetLibrary(applicationName);
            var applicationRoot = Path.GetDirectoryName(library.Path);
            var applicationEnvironment = applicationServices.GetRequiredService<IApplicationEnvironment>();
            var env = new TestApplicationEnvironment(applicationEnvironment, applicationName, applicationRoot);
            self.AddSingleton<IApplicationEnvironment>(
                    new TestApplicationEnvironment(applicationEnvironment, applicationName, applicationRoot));
            return self;
        }
    }
}
