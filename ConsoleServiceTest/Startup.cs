using Owin;
using System.Web.Http;

namespace LabServiceLibrary
{
    public class Startup
    {
        // This code configures Web API. The Startup class is specified as a type
        // parameter in the WebApp.Start method.
        public void Configuration(IAppBuilder appBuilder)
        {
            // Configure Web API for self-host. 
            HttpConfiguration config = new HttpConfiguration();
            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "SwitchApi",
                routeTemplate: "api/{controller}/{s1}/{s2}/{s3}/{s4}",
                defaults: new
                {
                    s1 = RouteParameter.Optional,
                    s2 = RouteParameter.Optional,
                    s3 = RouteParameter.Optional,
                    s4 = RouteParameter.Optional
                }
            );

            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}/{freq}/{amp}",
                defaults: new
                {
                    id = RouteParameter.Optional,
                    freq = RouteParameter.Optional,
                    amp = RouteParameter.Optional
                }
            );

            appBuilder.UseWebApi(config);
        }
    }
}