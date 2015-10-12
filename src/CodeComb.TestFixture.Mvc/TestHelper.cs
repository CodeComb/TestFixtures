using System.IO;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Infrastructure;
using Microsoft.Framework.Logging;
using Microsoft.Framework.Logging.Testing;

namespace CodeComb.TestFixture.Mvc
{
    public static class TestHelper
    {
        public static IServiceCollection AddTestApplicationEnvironment<TStartup>(this IServiceCollection self)
    where TStartup : class
        {
            self.AddInstance<ILoggerFactory>(NullLoggerFactory.Instance);
            var type = typeof(TStartup);
            var applicationServices = CallContextServiceLocator.Locator.ServiceProvider;
            var libraryManager = applicationServices.GetRequiredService<ILibraryManager>();
            var applicationName = type.Assembly.GetName().Name;
            var library = libraryManager.GetLibrary(applicationName);
            var applicationRoot = Path.GetDirectoryName(library.Path);
            var applicationEnvironment = applicationServices.GetRequiredService<IApplicationEnvironment>();
            var env = new TestApplicationEnvironment(applicationEnvironment, applicationName, applicationRoot);
            self.AddInstance<IApplicationEnvironment>(
                    new TestApplicationEnvironment(applicationEnvironment, applicationName, applicationRoot));
            return self;
        }
    }
}
