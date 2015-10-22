using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Xml.Linq;
using System.Reflection;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Hosting;
using Microsoft.AspNet.TestHost;
using Microsoft.AspNet.Testing;
using Microsoft.AspNet.Mvc.Infrastructure;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Newtonsoft.Json;

namespace CodeComb.TestFixture
{
    public class HostTestFixture
    {
        public TestServer server { get; set; }
        public HttpClient client { get; set; }
        public WebSocketClient wsclient { get; set; }
        public HostTestFixture(object startupInstance)
        {
            var applicationServices = CallContextServiceLocator.Locator.ServiceProvider;
            var libraryManager = applicationServices.GetRequiredService<ILibraryManager>();

            var applicationEnvironment = applicationServices.GetRequiredService<IApplicationEnvironment>();

            var startupTypeInfo = startupInstance.GetType().GetTypeInfo();
            var configureApplication = (Action<IApplicationBuilder>)startupTypeInfo
                .DeclaredMethods
                .FirstOrDefault(m => m.Name == "Configure" && m.GetParameters().Length == 1)
                ?.CreateDelegate(typeof(Action<IApplicationBuilder>), startupInstance);
            if (configureApplication == null)
            {
                var configureWithLogger = (Action<IApplicationBuilder, ILoggerFactory>)startupTypeInfo
                    .DeclaredMethods
                    .FirstOrDefault(m => m.Name == "Configure" && m.GetParameters().Length == 2)
                    ?.CreateDelegate(typeof(Action<IApplicationBuilder, ILoggerFactory>), startupInstance);
                // Debug.Assert(configureWithLogger != null);

                configureApplication = application => configureWithLogger(application, NullLoggerFactory.Instance);
            }

            var buildServices = (Func<IServiceCollection, IServiceProvider>)startupTypeInfo
                .DeclaredMethods
                .FirstOrDefault(m => m.Name == "ConfigureServices" && m.ReturnType == typeof(IServiceProvider))
                ?.CreateDelegate(typeof(Func<IServiceCollection, IServiceProvider>), startupInstance);
            if (buildServices == null)
            {
                var configureServices = (Action<IServiceCollection>)startupTypeInfo
                    .DeclaredMethods
                    .FirstOrDefault(m => m.Name == "ConfigureServices" && m.ReturnType == typeof(void))
                    ?.CreateDelegate(typeof(Action<IServiceCollection>), startupInstance);
                // Debug.Assert(configureServices != null);

                buildServices = services =>
                {
                    configureServices(services);
                    return services.BuildServiceProvider();
                };
            }

            // RequestLocalizationOptions saves the current culture when constructed, potentially changing response
            // localization i.e. RequestLocalizationMiddleware behavior. Ensure the saved culture
            // (DefaultRequestCulture) is consistent regardless of system configuration or personal preferences.
            using (new CultureReplacer())
            {
                server = TestServer.Create(
                    configureApplication,
                    configureServices: InitializeServices(startupTypeInfo.Assembly, buildServices));
            }

            client = server.CreateClient();
            wsclient = server.CreateWebSocketClient();
        }

        protected StringContent PostJson(object value)
        {
            return new StringContent(JsonConvert.SerializeObject(value), Encoding.UTF8, "application/json");
        }

        protected FormUrlEncodedContent PostUrl(object value)
        {
            var ret = new List<KeyValuePair<string, string>>();
            var t = value.GetType();
            var prop = t.GetProperties();
            foreach (var p in prop)
                ret.Add(new KeyValuePair<string, string>(p.Name, p.GetValue(value).ToString()));
            return new FormUrlEncodedContent(ret);
        }

        protected virtual void AddAdditionalServices(IServiceCollection services)
        {
        }

        private Func<IServiceCollection, IServiceProvider> InitializeServices(
            Assembly startupAssembly,
            Func<IServiceCollection, IServiceProvider> buildServices)
        {
            var applicationServices = CallContextServiceLocator.Locator.ServiceProvider;
            var libraryManager = applicationServices.GetRequiredService<ILibraryManager>();

            // When an application executes in a regular context, the application base path points to the root
            // directory where the application is located, for example .../samples/MvcSample.Web. However, when
            // executing an application as part of a test, the ApplicationBasePath of the IApplicationEnvironment
            // points to the root folder of the test project.
            // To compensate, we need to calculate the correct project path and override the application
            // environment value so that components like the view engine work properly in the context of the test.
            var applicationName = startupAssembly.GetName().Name;
            var library = libraryManager.GetLibrary(applicationName);
            var applicationRoot = Path.GetDirectoryName(library.Path);

            var applicationEnvironment = applicationServices.GetRequiredService<IApplicationEnvironment>();

            return (services) =>
            {
                services.AddInstance<IApplicationEnvironment>(
                    new TestApplicationEnvironment(applicationEnvironment, applicationName, applicationRoot));

                var hostingEnvironment = new HostingEnvironment();
                hostingEnvironment.Initialize(applicationRoot, "testing");
                services.AddInstance<IHostingEnvironment>(hostingEnvironment);

                // Inject a custom assembly provider. Overrides AddMvc() because that uses TryAdd().
                var assemblyProvider = new StaticAssemblyProvider();
                assemblyProvider.CandidateAssemblies.Add(startupAssembly);
                services.AddInstance<IAssemblyProvider>(assemblyProvider);

                AddAdditionalServices(services);

                return buildServices(services);
            };
        }

        public static CookieMetadata RetrieveAntiforgeryCookie(HttpResponseMessage response)
        {
            var setCookieArray = response.Headers.GetValues("Set-Cookie").ToArray();
            var cookie = setCookieArray[0].Split(';').First().Split('=');
            var cookieKey = cookie[0];
            var cookieData = cookie[1];

            return new CookieMetadata()
            {
                Key = cookieKey,
                Value = cookieData
            };
        }

        public static string RetrieveAntiforgeryToken(string htmlContent, string actionUrl)
        {
            return RetrieveAntiforgeryTokens(
                htmlContent,
                attribute => attribute.Value.EndsWith(actionUrl, StringComparison.OrdinalIgnoreCase) ||
                    attribute.Value.EndsWith($"HtmlEncode[[{ actionUrl }]]", StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();
        }

        public static IEnumerable<string> RetrieveAntiforgeryTokens(
            string htmlContent,
            Func<XAttribute, bool> predicate = null)
        {
            predicate = predicate ?? (_ => true);
            htmlContent = "<Root>" + htmlContent + "</Root>";
            var reader = new StringReader(htmlContent);
            var htmlDocument = XDocument.Load(reader);

            foreach (var form in htmlDocument.Descendants("form"))
            {
                foreach (var attribute in form.Attributes())
                {
                    if (string.Equals(attribute.Name.LocalName, "action", StringComparison.OrdinalIgnoreCase)
                        && predicate(attribute))
                    {
                        foreach (var input in form.Descendants("input"))
                        {
                            if (input.Attribute("name") != null &&
                                input.Attribute("type") != null &&
                                (input.Attribute("name").Value == "__RequestVerificationToken" &&
                                 input.Attribute("type").Value == "hidden" ||
                                 input.Attribute("name").Value == "HtmlEncode[[__RequestVerificationToken]]" &&
                                 input.Attribute("type").Value == "HtmlEncode[[hidden]]"))
                            {
                                yield return input.Attributes("value").First().Value;
                            }
                        }
                    }
                }
            }
        }

        public void Dispose()
        {
            server.Dispose();
        }
    }

    public class CookieMetadata
    {
        public string Key { get; set; }

        public string Value { get; set; }
    }
}
