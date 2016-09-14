using NUnit.Framework;

namespace Symphony.Core
{
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
