using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Raven.Client;
using Raven.Client.Embedded;
using Raven.Client.Indexes;
using Raven.Client.Listeners;

namespace RavenDbDictionaryIndexSample
{

    public class Product
    {
        public Product()
        {
            Properties = new Dictionary<string, string>();
        }

        public string Id { get; set; }
        public string Name { get; set; }

        public IDictionary<string, string> Properties { get; set; }
    }

    class Program
    {
        private static IEnumerable<KeyValuePair<string, string>> _values;

        static void Main(string[] args)
        {
            using (IDocumentStore store = GetStore())
            {
                using (IDocumentSession session = store.OpenSession())
                {
                    Product item1 = GetProduct();
                    Product item2 = GetProduct();
                    Product item3 = GetProduct();
                    Product item4 = GetProduct();

                    item1.Properties.Add("Color", "Red");
                    item1.Properties.Add("Size", "50");
                    item1.Properties.Add("Category", "Action");

                    item2.Properties.Add("Color", "White");
                    item2.Properties.Add("Size", "50");
                    item2.Properties.Add("Category", "Sports");

                    item3.Properties.Add("Color", "Brown");
                    item3.Properties.Add("Size", "25");
                    item3.Properties.Add("Category", "Action");
                    item3.Properties.Add("Class", "ClassA");

                    item4.Properties.Add("Color", "Red");
                    item4.Properties.Add("Size", "20");
                    item4.Properties.Add("Category", "Kids");

                    session.Store(item1);
                    session.Store(item2);
                    session.Store(item3);
                    session.Store(item4);
                    session.SaveChanges();
                }

                using (IDocumentSession session = store.OpenSession())
                {
                    IEnumerable<Product> prods = session.Query<Product>().ToList();
                }

                using (IDocumentSession session = store.OpenSession())
                {
                    RavenQueryStatistics stats;
                    IEnumerable<Product> products = session.Query<Product, Product_ByProperty>()
                        .Statistics(out stats)
                        .Where(prod => prod.Properties["Color"] == "Red")
                        .ToList();
                }

                using (IDocumentSession session = store.OpenSession())
                {
                    Product item = GetProduct();
                    item.Properties.Add("Color", "Red");
                    item.Properties.Add("Size", "15");
                    item.Properties.Add("Category", "Sports");

                    session.Store(item);
                    session.SaveChanges();
                }

                using (IDocumentSession session = store.OpenSession())
                {
                    RavenQueryStatistics stats;
                    IEnumerable<Product> products = session.Query<Product, Product_ByProperty>()
                        .Statistics(out stats)
                        .Where(prod => prod.Properties["Color"] == "Red")
                        .ToList();
                }
            }
        }

        private static IDocumentStore GetStore()
        {
            EmbeddableDocumentStore store = new EmbeddableDocumentStore
            {
                Configuration =
                {
                    RunInUnreliableYetFastModeThatIsNotSuitableForProduction = true,
                    RunInMemory = true,
                }
            };

            store.Initialize();
            store.RegisterListener(new NoStaleQueriesListener());
            IndexCreation.CreateIndexes(Assembly.GetExecutingAssembly(), store);

            return store;
        }

        private static Product GetProduct()
        {
            return new Product
            {
                Name = Faker.Lorem.Words(1).FirstOrDefault()
            };
        }

        private static int GetRandomCount(int max)
        {
            Random r = new Random();
            return r.Next(1, max);
        }
    }

    // http://stackoverflow.com/questions/9181204/ravendb-how-to-flush
    public class NoStaleQueriesListener : IDocumentQueryListener
    {
        public void BeforeQueryExecuted(IDocumentQueryCustomization queryCustomization)
        {
            queryCustomization.WaitForNonStaleResults();
        }
    }

    // http://stackoverflow.com/questions/11451320/ravendb-static-index-on-dictionary
    // http://tom.cabanski.com/2011/12/09/setting-index-options-for-idictionary-ravendb/
    public class Product_ByProperty : AbstractIndexCreationTask<Product>
    {
        public Product_ByProperty()
        {
            Map = products => from product in products
                              select new
                              {
                                  _ = product.Properties.Select(kvp => CreateField(string.Concat("Properties_", kvp.Key), kvp.Value))
                              };
        }
    }
}