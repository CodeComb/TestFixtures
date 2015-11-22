using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.Http.Internal;
using Microsoft.AspNet.Hosting;
using Microsoft.AspNet.Http.Authentication;
using Microsoft.Extensions.Primitives;
using Microsoft.AspNet.Identity;
using Moq;

namespace CodeComb.Testing.Mvc
{
    public class MvcTestFixture<TStartup>
        where TStartup : class, new()
    {
        public IServiceProvider GenerateServiceProvider(Action<IServiceCollection> configureServices = null, Action<Mock<HttpContext>> mockHttpContext = null, string environmentName = "testing")
        {
            var services = new ServiceCollection();
            if (configureServices != null)
                configureServices(services);
            services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
            services.AddLogging();
            
            var httpContext = new Mock<HttpContext>();
            var user = new Mock<ClaimsPrincipal>();
            user.Setup(x => x.Identities)
                .Returns(new List<ClaimsIdentity>());
            user.Setup(x => x.Claims)
                .Returns(new List<Claim>());
            httpContext.Setup(x => x.User)
                .Returns(user.Object);
            var httpResponse = new Mock<HttpResponse>();
            httpContext.Setup(x => x.Response)
                .Returns(httpResponse.Object);
            var httpRequest = new Mock<HttpRequest>();
            httpRequest.Setup(x => x.Query)
                .Returns(new QueryCollection(new Dictionary<string, StringValues>()));
            httpRequest.Setup(x => x.QueryString)
                .Returns(new QueryString());
            httpRequest.Setup(x => x.Headers)
                .Returns(new HeaderDictionary());
            httpRequest.Setup(x => x.Cookies)
                .Returns(new RequestCookieCollection(new Dictionary<string, string>()));
            httpContext.Setup(x => x.Request)
                .Returns(httpRequest.Object);
            var auth = new Mock<AuthenticationManager>();
            SetupSignIn(auth);
            httpContext.Setup(x => x.Authentication)
                .Returns(auth.Object);
            if (mockHttpContext != null)
                mockHttpContext(httpContext);

            var accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(x => x.HttpContext)
                .Returns(httpContext.Object);
            services.AddSingleton(accessor.Object);

            var manifest = CallContextServiceLocator.Locator.ServiceProvider.GetService<IRuntimeServices>();
            if (manifest != null)
            {
                foreach (var service in manifest.Services)
                {
                    services.AddTransient(service, sp => CallContextServiceLocator.Locator.ServiceProvider.GetService(service));
                }
            }
            
            dynamic startup = new TStartup();
            var host = new HostingEnvironment
            {
                EnvironmentName = environmentName
            };
            services.AddSingleton<IHostingEnvironment>(host);
            services.AddTestApplicationEnvironment<TStartup>();
            startup.ConfigureServices(services);


            var provider = services.BuildServiceProvider();

            httpContext.Setup(x => x.RequestServices)
                .Returns(provider);

            return provider;
        }

        public void SetupSignIn(Mock<AuthenticationManager> auth, string userId = null, bool? isPersistent = null, string loginProvider = null)
        {
            auth.Setup(a => a.SignInAsync(IdentityCookieOptions.ApplicationCookieAuthenticationType,
                It.Is<ClaimsPrincipal>(id =>
                    (userId == null || id.FindFirstValue(ClaimTypes.NameIdentifier) == userId) &&
                    (loginProvider == null || id.FindFirstValue(ClaimTypes.AuthenticationMethod) == loginProvider)),
                It.Is<AuthenticationProperties>(v => isPersistent == null || v.IsPersistent == isPersistent))).Returns(Task.FromResult(0)).Verifiable();
        }

        public static ClaimsPrincipal CreateIdentityFromUserName(string UserName, string authType = null)
        {
            authType = authType ?? new IdentityCookieOptions().ApplicationCookieAuthenticationScheme;
            return new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.Name, UserName)
                },
                authType));
        }

        public static ClaimsPrincipal CreateIdentityFromUserId(string UserId, string authType = null)
        {
            authType = authType ?? new IdentityCookieOptions().ApplicationCookieAuthenticationScheme;
            return new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, UserId)
                },
                authType));
        }

        public static ClaimsPrincipal CreateIdentity(string UserName, string UserId, string authType = null)
        {
            authType = authType ?? new IdentityCookieOptions().ApplicationCookieAuthenticationScheme;
            return new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, UserId),
                    new Claim(ClaimTypes.Name, UserName)
                },
                authType));
        }
    }
}
