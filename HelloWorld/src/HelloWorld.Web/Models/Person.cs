using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace HelloWorld.Web.Models {

    public class Person {

        public int Id { get; set; }
        public string Name { get; set; }
        public string Surname { get; set; }
        public DateTime Date { get; set; }
        public IEnumerable<Hobby> Hobbies { get; set; }
    }
}