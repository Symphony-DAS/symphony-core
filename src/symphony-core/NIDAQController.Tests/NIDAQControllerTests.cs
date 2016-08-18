using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using NationalInstruments.DAQmx;
using Symphony.Core;

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

        [Test]
        public void SetsChannelInfo()
        {
            foreach (var daq in NIDAQController.AvailableControllers())
            {
                const decimal srate = 10000;

                daq.InitHardware();
                Assert.True(daq.IsHardwareReady);
                Assert.False(daq.IsRunning);

                try
                {
                    foreach (IDAQOutputStream s in daq.OutputStreams)
                    {
                        daq.SampleRate = new Measurement(srate, "Hz");
                        var externalDevice = new TestDevice("OUT-DEVICE", null);

                        s.Devices.Add(externalDevice);
                    }

                    daq.ConfigureChannels();

                    foreach (NIDAQStream s in daq.OutputStreams.Cast<NIDAQStream>())
                    {
                        var type = (ChannelType) 0;
                        switch (s.PhysicalChannelType)
                        {
                            case PhysicalChannelTypes.AI:
                                type = ChannelType.AI;
                                break;
                            case PhysicalChannelTypes.AO:
                                type = ChannelType.AO;
                                break;
                            case PhysicalChannelTypes.DOPort:
                                type = ChannelType.DO;
                                break;
                            case PhysicalChannelTypes.DIPort:
                                type = ChannelType.DI;
                                break;
                        }

                        var actual = daq.ChannelInfo(type, s.FullName);
                        var expected = s.ChannelInfo;

                        Assert.AreEqual(expected.PhysicalName, actual.PhysicalName);
                    }
                }
                finally
                {
                    daq.CloseHardware();
                }
            }
        }

    }
}
