using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Autofac;
using Autofac.Integration.Mvc;
using Raven.Client.Document;

namespace HelloWorld.Web.Infrastructure {

    public class AutofacMVC3 {

        public static void Initialize() {

            var builder = new ContainerBuilder();
            DependencyResolver.SetResolver(new AutofacDependencyResolver(RegisterServices(builder)));
        }

        private static IContainer RegisterServices(ContainerBuilder builder) {

            builder.RegisterControllers(typeof(MvcApplication).Assembly).PropertiesAutowired();

            builder.Register(c => c.Resolve<DocumentStore>().OpenSession()).InstancePerLifetimeScope();

            return builder.Build();
        }
    }
}