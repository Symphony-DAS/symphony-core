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
        public void ShouldExposeVersion()
        {
            var expectedVersion = typeof(Controller).Assembly.GetName().Version;
            Assert.That(SymphonyFramework.Version, Is.EqualTo(expectedVersion));
        }
    }
}
