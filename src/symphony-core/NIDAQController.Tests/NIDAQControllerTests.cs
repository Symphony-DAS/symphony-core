using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace NI
{
    [TestFixture]
    class NIDAQControllerTests
    {

        [Test]
        public void AvailableControllers()
        {
            Assert.GreaterOrEqual(NIDAQController.AvailableControllers().Count(), 1);
        }

    }
}
