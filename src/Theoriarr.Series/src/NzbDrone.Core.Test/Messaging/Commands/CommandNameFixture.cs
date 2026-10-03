using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Test.Messaging.Commands
{
    [TestFixture]
    public class CommandNameFixture
    {
        [Test]
        public void command_names_used_by_the_api_should_be_unique()
        {
            // CommandController resolves a posted command name against the command
            // class names with the `Command` suffix removed. Two commands with the
            // same name make that Single() throw "Sequence contains more than one
            // matching element" (this happened when movie + series both defined
            // `ManualImportCommand`).
            var names = typeof(Command).Assembly
                .GetTypes()
                .Where(t => !t.IsAbstract && typeof(Command).IsAssignableFrom(t))
                .Select(t => t.Name.Replace("Command", ""))
                .ToList();

            names.Should().OnlyHaveUniqueItems();
        }
    }
}
