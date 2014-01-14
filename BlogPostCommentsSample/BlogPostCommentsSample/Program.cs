using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Raven.Abstractions.Indexing;
using Raven.Client;
using Raven.Client.Embedded;
using Raven.Client.Indexes;
using Raven.Client.Listeners;

namespace BlogPostCommentsSample
{
    public class BlogPost
    {
        public BlogPost()
        {
            Tags = new List<BlogPostTag>();
        }

        public string Id { get; set; }
        public string Slug { get; set; }
        public string Title { get; set; }
        public string BriefInformation { get; set; }
        public string Content { get; set; }
        public IList<BlogPostTag> Tags { get; set; }
        public DateTimeOffset PublishedAt { get; set; }

        public string[] CommentIds { get; set; }
    }

    public class BlogPostComment
    {
        public string Id { get; set; }
        public string BlogPostId { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public string CommentedBy { get; set; }
        public DateTimeOffset CommentedAt { get; set; }
    }

    public class BlogPostTag
    {
        public string Name { get; set; }
        public string Slug { get; set; }
    }

    class Program
    {
        // Fake.Net: http://10consulting.com/2011/11/18/populating-test-data-using-c-sharp/

        static void Main(string[] args)
        {
            using (IDocumentStore store = GetStore())
            {
                using (IDocumentSession session = store.OpenSession())
                {
                    IEnumerable<BlogPost> blogPosts = Enumerable.Range(0, 100).Select(_ => GetBlogPost()).ToArray();
                    foreach (BlogPost blogPost in blogPosts)
                    {
                        BlogPost post = blogPost;
                        session.Store(post);
                    }

                    session.SaveChanges();

                    foreach (BlogPost blogPost in blogPosts)
                    {
                        int randomCommentsCount = GetRandomCount(100);
                        IEnumerable<BlogPostComment> blogPostComments = Enumerable.Range(0, randomCommentsCount).Select(_ => GetBlogPostComment(blogPost.Id)).ToArray();
                        foreach (BlogPostComment comment in blogPostComments)
                        {
                            session.Store(comment);
                        }

                        blogPost.CommentIds = blogPostComments.Select(x => x.Id).ToArray();
                    }

                    session.SaveChanges();
                }

                using (IDocumentSession session = store.OpenSession())
                {
                    IEnumerable<BlogPost> posts = session.Query<BlogPost>().ToArray();
                }

                using (IDocumentSession session = store.OpenSession())
                {
                    IEnumerable<Tags_Count.ReduceResult> tags =
                        session
                        .Query<Tags_Count.ReduceResult, Tags_Count>()
                        .OrderBy(x => x.Count)
                        .ToArray();
                }

                using (IDocumentSession session = store.OpenSession())
                {
                    BlogPostWithCommentsCount[] results = session.Query<BlogPost, BlogPosts_CommentsCount>()
                        .ProjectFromIndexFieldsInto<BlogPostWithCommentsCount>()
                        .ToArray();
                }

                using (IDocumentSession session = store.OpenSession())
                {
                    BlogPosts_LightListIndex.ReduceResult[] results = session.Query<BlogPost, BlogPosts_LightListIndex>()
                        .AsProjection<BlogPosts_LightListIndex.ReduceResult>()
                        .ToArray();
                }
            }
        }

        public class BlogPostWithCommentsCount : BlogPost
        {
            public int CommentsCount { get; set; }
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

        private static void InitData(IDocumentSession session)
        {
        }

        private static BlogPost GetBlogPost()
        {
            string title = Faker.Lorem.Sentence(30);
            BlogPost post = new BlogPost
            {
                Title = title,
                Slug = title.ToSlug(),
                BriefInformation = Faker.Lorem.Paragraph(),
                Content = string.Join(" ", Faker.Lorem.Paragraphs(20)),
                Tags = new List<BlogPostTag>(GetRandomTags()),
                PublishedAt = DateTimeOffset.Now
            };

            return post;
        }

        private static BlogPostComment GetBlogPostComment(string blogPostId)
        {
            return new BlogPostComment
            {
                BlogPostId = blogPostId,
                Title = Faker.Lorem.Sentence(),
                CommentedBy = Faker.Name.FullName(),
                Content = Faker.Lorem.Paragraph(),
                CommentedAt = DateTimeOffset.Now
            };
        }

        private static IEnumerable<BlogPostTag> GetRandomTags()
        {
            return GetRandomTags(GetRandomCount(7));
        }

        private static IEnumerable<BlogPostTag> GetRandomTags(int count)
        {
            List<BlogPostTag> tags = new List<BlogPostTag>
            {
                new BlogPostTag { Name = "RavenDb", Slug = "ravendb" }, 
                new BlogPostTag { Name = "Visual Studio", Slug = "visual-studio" },
                new BlogPostTag { Name = "ASP.NET", Slug = "asp-net" },
                new BlogPostTag { Name = "OWIN", Slug = "owin" },
                new BlogPostTag { Name = "Katana", Slug = "katana" },
                new BlogPostTag { Name = "Entity Framework", Slug = "entity-framework" },
                new BlogPostTag { Name = "C#", Slug = "c-sharp" },
                new BlogPostTag { Name = "Random", Slug = "random" },
                new BlogPostTag { Name = "ASP.NET MVC", Slug = "asp-net-mvc" },
                new BlogPostTag { Name = "ASP.NET Web API", Slug = "asp-net-web-api" },
                new BlogPostTag { Name = "Lucene.Net", Slug = "lucene-net" }
            };

            return tags.Shuffle().Take(count);
        }

        private static int GetRandomCount(int max)
        {
            Random r = new Random();
            return r.Next(1, max);
        }
    }

    public class Tags_Count : AbstractIndexCreationTask<BlogPost, Tags_Count.ReduceResult>
    {
        public class ReduceResult
        {
            public string Name { get; set; }
            public string Slug { get; set; }
            public int Count { get; set; }
            public DateTimeOffset LastSeenAt { get; set; }
        }

        public Tags_Count()
        {
            Map = blogPosts => from blogPost in blogPosts
                               from tag in blogPost.Tags
                               select new
                               {
                                   Name = tag.Name.ToLowerInvariant(),
                                   Slug = tag.Slug,
                                   Count = 1,
                                   LastSeenAt = blogPost.PublishedAt
                               };

            Reduce = results => from tagCount in results
                                group tagCount by new { tagCount.Name, tagCount.Slug }
                                    into groupedResult
                                    select new
                                    {
                                        Name = groupedResult.Key.Name,
                                        Slug = groupedResult.Key.Slug,
                                        Count = groupedResult.Sum(x => x.Count),
                                        LastSeenAt = groupedResult.Max(x => x.LastSeenAt)
                                    };

            Sort(x => x.Count, SortOptions.Int);
        }
    }

    public class BlogPosts_CommentsCount : AbstractIndexCreationTask<BlogPost, BlogPosts_CommentsCount.Result>
    {
        // ref: http://ravendb.net/docs/2.0/client-api/querying/static-indexes/indexing-related-documents
        //      http://ravendb.net/docs/2.0/faq/indexing-across-entities
        //      http://ravendb.net/docs/2.5/client-api/querying/handling-document-relationships
        //      http://stackoverflow.com/questions/15235723/how-to-use-raven-loaddocument
        //      https://gist.github.com/mj1856/4370309

        public class Result
        {
            public int CommentsCount { get; set; }
        }

        public BlogPosts_CommentsCount()
        {
            Map = blogPosts => from blogPost in blogPosts
                               select new
                               {
                                   CommentsCount = blogPost.CommentIds.Select(x => LoadDocument<BlogPostComment>(x).Id).Count()
                               };

            Store(x => x.CommentsCount, FieldStorage.Yes);
        }
    }

    public class BlogPosts_LightListIndex : AbstractIndexCreationTask<BlogPost, BlogPosts_LightListIndex.ReduceResult>
    {
        // ref: http://daniellang.net/using-an-index-as-a-materialized-view-in-ravendb/

        public class ReduceResult
        {
            public string Id { get; set; }
            public string Title { get; set; }
            public string BriefInformation { get; set; }
            public string Content { get; set; }
            public BlogPostTag[] Tags { get; set; }
            public int CommentsCount { get; set; }
        }

        public BlogPosts_LightListIndex()
        {
            Map = blogPosts => from blogPost in blogPosts
                               select new
                               {
                                   Title = blogPost.Title,
                                   BriefInformation = blogPost.BriefInformation,
                                   Content = blogPost.Content,
                                   Tags = blogPost.Tags,
                                   CommentsCount = blogPost.CommentIds.Select(x => LoadDocument<BlogPostComment>(x).Id).Count()
                               };

            Store(x => x.Title, FieldStorage.Yes);
            Store(x => x.BriefInformation, FieldStorage.Yes);
            Store(x => x.Content, FieldStorage.Yes);
            Store(x => x.Tags, FieldStorage.Yes);
            Store(x => x.CommentsCount, FieldStorage.Yes);
        }
    }

    public static class EnumerableExtensions
    {
        public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source)
        {
            return source.Shuffle(new Random());
        }

        public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source, Random rng)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (rng == null) throw new ArgumentNullException("rng");

            return source.ShuffleIterator(rng);
        }

        private static IEnumerable<T> ShuffleIterator<T>(this IEnumerable<T> source, Random rng)
        {
            var buffer = source.ToList();
            for (int i = 0; i < buffer.Count; i++)
            {
                int j = rng.Next(i, buffer.Count);
                yield return buffer[j];

                buffer[j] = buffer[i];
            }
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

    public static class StringExtensions
    {
        /// <summary>
        /// A regular expression for validating slugs.
        /// Does not allow leading or trailing hypens or whitespace.
        /// </summary>
        private static readonly Regex SlugRegex = new Regex(@"(^[a-z0-9])([a-z0-9_-]+)*([a-z0-9])$", RegexOptions.Compiled);

        /// <summary>
        /// Slugifies a string
        /// </summary>
        /// <param name="value">The string value to slugify</param>
        /// <returns>A URL safe slug representation of the input <paramref name="value"/>.</returns>
        public static string ToSlug(this string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }

            if (SlugRegex.IsMatch(value))
            {
                return value;
            }

            return GenerateSlug(value);
        }

        /// <summary>
        /// Credit for this method goes to http://stackoverflow.com/questions/2920744/url-slugify-alrogithm-in-cs and
        /// https://github.com/benfoster/Fabrik.Common/blob/dev/src/Fabrik.Common/StringExtensions.cs.
        /// </summary>
        private static string GenerateSlug(string phrase)
        {
            string result = RemoveAccent(phrase).ToLowerInvariant();
            result = result.Trim('-', '.');
            result = result.Replace('.', '-');
            result = result.Replace("#", "-sharp");
            result = Regex.Replace(result, @"[^a-z0-9\s-]", string.Empty); // remove invalid characters
            result = Regex.Replace(result, @"\s+", " ").Trim(); // convert multiple spaces into one space

            return Regex.Replace(result, @"\s", "-"); // replace all spaces with hyphens
        }

        private static string RemoveAccent(string txt)
        {
            byte[] bytes = Encoding.GetEncoding("Cyrillic").GetBytes(txt);
            return Encoding.ASCII.GetString(bytes);
        }
    }
}