using Raven.Client;
using Raven.Client.Document;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RavenDbPrivateSetterSample
{
    class Program
    {
        static void Main(string[] args)
        {
            IDocumentStore store = new DocumentStore
            {
                Url = "http://localhost:8080",
                DefaultDatabase = "People"
            }.Initialize();

            // InitData(store);
            // RetrieveData(store);
        }

        private static void InitData(IDocumentStore store)
        {
            using (BulkInsertOperation bulkInstOp = store.BulkInsert())
            {
                for (int i = 0; i < 100; i++)
                {
                    Person person = new Person(Faker.Name.First() + "-" + Guid.NewGuid().ToString("N"), DateTime.Now.AddDays(-37625).Date);
                    bulkInstOp.Store(person);
                }
            }
        }

        private static void RetrieveData(IDocumentStore store)
        {
            using (var ses = store.OpenSession())
            {
                IEnumerable<Person> people = ses.Query<Person>().ToList();
            }
        }
    }

    public class Person
    {
        public Person(string name, DateTime dateOfBirth)
        {
            Id = GenerateKey(name);
            Name = name;
            DateOfBirth = dateOfBirth;
        }

        public string Id { get; private set; }
        public string Name { get; private set; }
        public DateTime DateOfBirth { get; set; }

        private static string GenerateKey(string name)
        {
            return string.Concat("People", "/", name);
        }
    }
}
