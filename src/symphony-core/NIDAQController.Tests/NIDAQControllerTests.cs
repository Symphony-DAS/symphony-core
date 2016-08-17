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

        [Test]
        public void InitializesHardware()
        {
            foreach (var controller in NIDAQController.AvailableControllers())
            {
                Assert.False(controller.IsHardwareReady);
                controller.InitHardware();

                try
                {
                    Assert.True(controller.IsHardwareReady);
                }
                finally 
                {
                    controller.CloseHardware();
                    Assert.False(controller.IsHardwareReady);
                }
            }
        }

    }
}
