using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Symphony.Core;

namespace Symphony.ExternalDevices
{
    [TestFixture]
    class Axopatch200BTests
    {
        [Test]
        public void ShouldReadSimpleTelegraph()
        {
            IAxopatch patch = new Axopatch200B();

            IDictionary<string, IInputData> data = new Dictionary<string, IInputData>();

            data[AxopatchDevice.GAIN_TELEGRAPH_STREAM_NAME] = new InputData(Enumerable.Repeat(new Measurement(1.9, "V"), 10), null, DateTimeOffset.Now);
            data[AxopatchDevice.MODE_TELEGRAPH_STREAM_NAME] = new InputData(Enumerable.Repeat(new Measurement(6.1, "V"), 10), null, DateTimeOffset.Now);

            AxopatchInterop.AxopatchData telegraph = patch.ReadTelegraphData(data);
            Assert.That(telegraph.Gain, Is.EqualTo(0.5));
            Assert.That(telegraph.OperatingMode, Is.EqualTo(AxopatchInterop.OperatingMode.VClamp));
            Assert.That(telegraph.ExternalCommandSensitivity, Is.EqualTo(0.02));
            Assert.That(telegraph.ExternalCommandSensitivityUnits, Is.EqualTo(AxopatchInterop.ExternalCommandSensitivityUnits.V_V));
        }
    }

    [TestFixture]
    class AxopatchDeviceTests
    {
        [Test]
        public void ShouldConvertOutputUnitsInIClamp(
            [Values(
                AxopatchInterop.OperatingMode.I0, 
                AxopatchInterop.OperatingMode.IClampFast, 
                AxopatchInterop.OperatingMode.IClampNormal)] AxopatchInterop.OperatingMode operatingMode)
        {
            var c = new Controller();
            var p = new Axopatch200B();

            var patchDevice = new AxopatchDevice(p, c, null);

            var data = new AxopatchInterop.AxopatchData()
                {
                    OperatingMode = operatingMode,
                    ExternalCommandSensitivity = 2.5,
                    ExternalCommandSensitivityUnits = AxopatchInterop.ExternalCommandSensitivityUnits.A_V
                };

            var cmd = new Measurement(20, -12, "A");

            var expected = operatingMode == AxopatchInterop.OperatingMode.I0 ?
                new Measurement(0, "V") :
                new Measurement(cmd.Quantity / (decimal)data.ExternalCommandSensitivity,
                                           cmd.Exponent, "V");

            var actual = AxopatchDevice.ConvertOutput(cmd, data);

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void ShouldConvertOutputUnitsInVClamp(
            [Values(
                AxopatchInterop.OperatingMode.VClamp,
                AxopatchInterop.OperatingMode.Track)] AxopatchInterop.OperatingMode operatingMode)
        {
            var data = new AxopatchInterop.AxopatchData()
            {
                OperatingMode = operatingMode,
                ExternalCommandSensitivity = 2.5,
                ExternalCommandSensitivityUnits = AxopatchInterop.ExternalCommandSensitivityUnits.V_V
            };

            var cmd = new Measurement(20, -3, "V");

            var expected = operatingMode == AxopatchInterop.OperatingMode.Track ?
                new Measurement(0, "V") :
                new Measurement(cmd.Quantity / (decimal)data.ExternalCommandSensitivity,
                                           cmd.Exponent, "V");

            var actual = AxopatchDevice.ConvertOutput(cmd, data);

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void ShouldConvertInputUnitsInVClamp(
            [Values(
                AxopatchInterop.OperatingMode.VClamp, 
                AxopatchInterop.OperatingMode.Track)] AxopatchInterop.OperatingMode operatingMode,
            [Values(1, 2, 10)] double gain)
        {
            var data = new AxopatchInterop.AxopatchData()
                {
                    OperatingMode = operatingMode,
                    Gain = gain
                };

            var cmd = new Measurement(20, -3, "V");

            var expected = new Measurement(1000 * cmd.QuantityInBaseUnits/(decimal) gain, -12, "A");

            var actual = AxopatchDevice.ConvertInput(cmd, data);

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void ShouldConvertInputUnitsInIClamp(
            [Values(
                AxopatchInterop.OperatingMode.I0, 
                AxopatchInterop.OperatingMode.IClampFast,
                AxopatchInterop.OperatingMode.IClampNormal)] AxopatchInterop.OperatingMode operatingMode,
            [Values(1, 2, 10)] double gain)
        {
            var data = new AxopatchInterop.AxopatchData()
                {
                    OperatingMode = operatingMode,
                    Gain = gain
                };

            var cmd = new Measurement(20, -3, "V");

            var expected = new Measurement(1000 * cmd.QuantityInBaseUnits/(decimal) gain, -3, "V");

            var actual = AxopatchDevice.ConvertInput(cmd, data);

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void ShouldUseBackgroundForMode()
        {
            const string VClampUnits = "V";
            const string IClampUnits = "A";

            Measurement VClampBackground = new Measurement(2, -3, VClampUnits);
            Measurement IClampBackground = new Measurement(-10, -3, IClampUnits);

            var c = new Controller();
            var p = new FakeAxopatch();

            var bg = new Dictionary<AxopatchInterop.OperatingMode, IMeasurement>()
                         {
                             {AxopatchInterop.OperatingMode.VClamp, VClampBackground},
                             {AxopatchInterop.OperatingMode.IClampNormal, IClampBackground},  
                         };

            var patch = new AxopatchDevice(p, c, bg);
            patch.BindStream(new DAQOutputStream("stream"));

            var data = new AxopatchInterop.AxopatchData()
            {
                OperatingMode = AxopatchInterop.OperatingMode.VClamp,
                ExternalCommandSensitivity = 2.5,
                ExternalCommandSensitivityUnits = AxopatchInterop.ExternalCommandSensitivityUnits.V_V
            };

            p.Data = data;

            Assert.That(patch.OutputBackground, Is.EqualTo(AxopatchDevice.ConvertOutput(VClampBackground, patch.CurrentDeviceParameters)));

            data = new AxopatchInterop.AxopatchData()
            {
                OperatingMode = AxopatchInterop.OperatingMode.IClampNormal,
                ExternalCommandSensitivity = 1.5,
                ExternalCommandSensitivityUnits = AxopatchInterop.ExternalCommandSensitivityUnits.A_V
            };

            p.Data = data;

            Assert.That(patch.OutputBackground, Is.EqualTo(AxopatchDevice.ConvertOutput(IClampBackground, patch.CurrentDeviceParameters)));
        }
    }

    internal class FakeAxopatch : IAxopatch
    {
        public AxopatchInterop.AxopatchData Data { get; set; }

        public AxopatchInterop.AxopatchData ReadTelegraphData(IDictionary<string, IInputData> data)
        {
            return Data;
        }
    }
}
