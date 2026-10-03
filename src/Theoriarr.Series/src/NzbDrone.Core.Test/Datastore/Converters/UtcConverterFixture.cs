using System;
using System.Data.SQLite;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Converters;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Converters
{
    [TestFixture]
    public class UtcConverterFixture : CoreTest<DapperUtcConverter>
    {
        private SQLiteParameter _param;

        [SetUp]
        public void Setup()
        {
            _param = new SQLiteParameter();
        }

        [Test]
        public void should_return_date_time_when_saving_date_time_to_db()
        {
            var dateTime = DateTime.Now;

            Subject.SetValue(_param, dateTime);
            _param.Value.Should().Be(dateTime.ToUniversalTime());
        }

        [Test]
        public void should_treat_unspecified_date_time_as_utc_without_shifting()
        {
            var dateTime = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Unspecified);

            Subject.SetValue(_param, dateTime);

            var value = (DateTime)_param.Value;
            value.Kind.Should().Be(DateTimeKind.Utc);
            value.Ticks.Should().Be(dateTime.Ticks);
        }

        [Test]
        public void should_return_time_span_when_getting_time_span_from_db()
        {
            var dateTime = DateTime.Now.ToUniversalTime();

            Subject.Parse(dateTime).Should().Be(dateTime);
        }
    }
}
