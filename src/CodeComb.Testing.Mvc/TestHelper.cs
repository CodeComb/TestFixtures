using System.IO;
using System.Reflection;
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
#if NET451
            var applicationName = type.Assembly.GetName().Name;
#else
            var applicationName = type.GetTypeInfo().Assembly.GetName().Name;
#endif
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
