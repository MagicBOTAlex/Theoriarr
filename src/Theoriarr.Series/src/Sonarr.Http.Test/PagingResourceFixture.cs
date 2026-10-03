using FluentAssertions;
using NUnit.Framework;

namespace Sonarr.Http.Test
{
    [TestFixture]
    public class PagingResourceFixture
    {
        [Test]
        public void should_default_to_a_large_page_size_for_unpaged_clients()
        {
            var subject = new PagingResource<object>(new PagingRequestResource());

            subject.Page.Should().Be(1);
            subject.PageSize.Should().Be(PagingRequestResource.DefaultPageSize);
        }

        [Test]
        public void should_honour_an_explicit_page_size()
        {
            var subject = new PagingResource<object>(new PagingRequestResource { Page = 2, PageSize = 25 });

            subject.Page.Should().Be(2);
            subject.PageSize.Should().Be(25);
        }
    }
}
