using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using HelloWorld.Web.Infrastructure;
using HelloWorld.Web.Models;

namespace HelloWorld.Web.Controllers {

    public class HomeController : RavenController {

        public ActionResult Index() {

            var model = RavenSession.Query<Person>().ToList();

            return View(model);
        }

        public string Create() {

            RavenSession.Store(new Person {

                Name = "Tugberk",
                Surname = "Ugurlu",
                Date = DateTime.Now,
                Hobbies = new List<Hobby> { 
                    new Hobby { Name = "Foreign Movies" },
                    new Hobby { Name = "Formula 1" }
                }
            });

            return "done";
        }

        public string Edit(int? id) {

            if (!id.HasValue)
                return "Not Found!";

            var person = RavenSession.Load<Person>(id);

            if (person == null)
                return "Not Found!";

            person.Surname = "<strong>Ugurlu</strong>";
            RavenSession.SaveChanges();

            return "done";
        }
    }
}