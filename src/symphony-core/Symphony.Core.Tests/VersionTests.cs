using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Symphony.Core
{
    using NUnit.Framework;
    using System;

    [TestFixture]
    class VersionTests
    {
        [Test]
        public void ShouldExposeMarketingVersion()
        {
            var env = Environment.GetEnvironmentVariables();

            var expectedVersion = env.Contains("SYMPHONY_VERSION") ? env["SYMPHONY_VERSION"] : "0.0.0";

            Assert.That(SymphonyFramework.VersionString, Is.EqualTo(expectedVersion));

        }

        [Test]
        public void ShouldExposeVersion()
        {
            var env = Environment.GetEnvironmentVariables();
            var expectedVersion = env.Contains("SYMPHONY_VERSION") ? env["SYMPHONY_VERSION"] : "0.0.0";

            Assert.That(expectedVersion, Is.Not.Null);
            Assert.That(SymphonyFramework.Version, Is.EqualTo(new Version((string) expectedVersion)));
        }
    }
}
