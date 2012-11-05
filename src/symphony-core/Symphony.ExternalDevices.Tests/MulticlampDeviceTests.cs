using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Mocks;
using Symphony.Core;

namespace Symphony.ExternalDevices
{
    using NUnit.Framework;

    [TestFixture]
    class MulticlampInteropTests
    {
        [Test]
        public void ShouldDetermineUnitExponent()
        {
            Assert.That(MultiClampInterop.ExponentForScaleFactorUnits(MultiClampInterop.ScaleFactorUnits.V_A), Is.EqualTo(1));
            Assert.That(MultiClampInterop.ExponentForScaleFactorUnits(MultiClampInterop.ScaleFactorUnits.V_mA), Is.EqualTo(-3));
            Assert.That(MultiClampInterop.ExponentForScaleFactorUnits(MultiClampInterop.ScaleFactorUnits.V_uA), Is.EqualTo(-6));
            Assert.That(MultiClampInterop.ExponentForScaleFactorUnits(MultiClampInterop.ScaleFactorUnits.V_nA), Is.EqualTo(-9));
            Assert.That(MultiClampInterop.ExponentForScaleFactorUnits(MultiClampInterop.ScaleFactorUnits.V_pA), Is.EqualTo(-12));


            Assert.That(MultiClampInterop.ExponentForScaleFactorUnits(MultiClampInterop.ScaleFactorUnits.V_V), Is.EqualTo(1));
            Assert.That(MultiClampInterop.ExponentForScaleFactorUnits(MultiClampInterop.ScaleFactorUnits.V_mV), Is.EqualTo(-3));
            Assert.That(MultiClampInterop.ExponentForScaleFactorUnits(MultiClampInterop.ScaleFactorUnits.V_uV), Is.EqualTo(-6));
        }
    }


    [TestFixture]
    internal class MultiClampDeviceTests
    {


        private static readonly Measurement UNUSED_BACKGROUND = new Measurement(0, "V");
        private static readonly IMeasurement UNUSED_MEASUREMENT = UNUSED_BACKGROUND;


        [Test]
        public void ShouldThrowForUnexpectedScaleFactorUnitsWhenExpectingAmps(
            [Values(new object[]
                        {
                            MultiClampInterop.ScaleFactorUnits.V_mV,
                            MultiClampInterop.ScaleFactorUnits.V_uV,
                            MultiClampInterop.ScaleFactorUnits.V_V
                        })] MultiClampInterop.ScaleFactorUnits scaleFactorUnits,
            [Values(new object[]
                        {
                            MultiClampInterop.SignalIdentifier.AXMCD_OUT_PRI_VC_GLDR_I_MEMB,
                            MultiClampInterop.SignalIdentifier.AXMCD_OUT_PRI_IC_GLDR_I_CMD_MEMB,
                            MultiClampInterop.SignalIdentifier.AXMCD_OUT_PRI_IC_GLDR_I_CMD_SUMMED
                        })] MultiClampInterop.SignalIdentifier signalIdentifier
            )
        {
            var mc = new FakeMulticlampCommander();

            MultiClampInterop.OperatingMode mode;
            switch (signalIdentifier)
            {
                case MultiClampInterop.SignalIdentifier.AXMCD_OUT_PRI_VC_GLDR_I_MEMB:
                    mode = MultiClampInterop.OperatingMode.VClamp;
                    break;
                case MultiClampInterop.SignalIdentifier.AXMCD_OUT_PRI_IC_GLDR_I_CMD_MEMB:
                case MultiClampInterop.SignalIdentifier.AXMCD_OUT_PRI_IC_GLDR_I_CMD_SUMMED:
                    mode = MultiClampInterop.OperatingMode.IClamp;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("signalIdentifier");
            }

            var data = new MultiClampInterop.MulticlampData()
                           {
                               ScaledOutputSignal = signalIdentifier,
                               ScaleFactorUnits = scaleFactorUnits,
                               OperatingMode = mode
                           };

            Assert.Throws<MultiClampDeviceException>(
                () => MultiClampDevice.ConvertInput(new Measurement(1.0m, "A"), data));

        }

        [Test]
        public void ShouldThrowForUnexpectedScaleFactorUnitsWhenExpectingVolts(
            [Values(new object[]
                        {
                            MultiClampInterop.ScaleFactorUnits.V_A,
                            MultiClampInterop.ScaleFactorUnits.V_mA,
                            MultiClampInterop.ScaleFactorUnits.V_nA,
                            MultiClampInterop.ScaleFactorUnits.V_uA,
                            MultiClampInterop.ScaleFactorUnits.V_pA
                        })] MultiClampInterop.ScaleFactorUnits scaleFactorUnits,
            [Values(new object[]
                        {
                            MultiClampInterop.SignalIdentifier.AXMCD_OUT_PRI_IC_GLDR_V_MEMBx10,
                            MultiClampInterop.SignalIdentifier.AXMCD_OUT_PRI_IC_GLDR_V_MEMBx100,
                            MultiClampInterop.SignalIdentifier.AXMCD_OUT_PRI_VC_GLDR_V_CMD_EXT,
                            MultiClampInterop.SignalIdentifier.AXMCD_OUT_PRI_VC_GLDR_V_CMD_MEMB,
                            MultiClampInterop.SignalIdentifier.AXMCD_OUT_PRI_VC_GLDR_V_CMD_SUMMED
                        })] MultiClampInterop.SignalIdentifier signalIdentifier
            )
        {

            MultiClampInterop.OperatingMode mode;
            switch (signalIdentifier)
            {
                case MultiClampInterop.SignalIdentifier.AXMCD_OUT_PRI_VC_GLDR_V_CMD_EXT:
                case MultiClampInterop.SignalIdentifier.AXMCD_OUT_PRI_VC_GLDR_V_CMD_MEMB:
                case MultiClampInterop.SignalIdentifier.AXMCD_OUT_PRI_VC_GLDR_V_CMD_SUMMED:
                    mode = MultiClampInterop.OperatingMode.VClamp;
                    break;
                case MultiClampInterop.SignalIdentifier.AXMCD_OUT_PRI_IC_GLDR_V_MEMBx10:
                case MultiClampInterop.SignalIdentifier.AXMCD_OUT_PRI_IC_GLDR_V_MEMBx100:
                    mode = MultiClampInterop.OperatingMode.IClamp;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("signalIdentifier");
            }

            var data = new MultiClampInterop.MulticlampData()
                           {
                               ScaledOutputSignal = signalIdentifier,
                               ScaleFactorUnits = scaleFactorUnits,
                               OperatingMode = mode
                           };


            Assert.Throws<MultiClampDeviceException>(() => MultiClampDevice.ConvertInput(UNUSED_MEASUREMENT, data),
                                                     scaleFactorUnits +
                                                     " is not an allowed unit conversion for scaled output mode.");

        }

        [Test]
        public void ShouldSetExternalCommandSensitivityUnits(
            [Values((UInt32)0, (UInt32)1, (UInt32)2)] UInt32 operatingMode
            )
        {
            var data = new MultiClampInterop.MC_TELEGRAPH_DATA()
                           {
                               uOperatingMode = operatingMode
                           };

            var mcd = new MultiClampInterop.MulticlampData(data);
            switch (mcd.OperatingMode)
            {
                case MultiClampInterop.OperatingMode.VClamp:
                    Assert.That(mcd.ExternalCommandSensitivityUnits, Is.EqualTo(MultiClampInterop.ExternalCommandSensitivityUnits.V_V));
                    break;
                case MultiClampInterop.OperatingMode.IClamp:
                    Assert.That(mcd.ExternalCommandSensitivityUnits, Is.EqualTo(MultiClampInterop.ExternalCommandSensitivityUnits.A_V));

                    break;
                case MultiClampInterop.OperatingMode.I0:
                    Assert.That(mcd.ExternalCommandSensitivityUnits, Is.EqualTo(MultiClampInterop.ExternalCommandSensitivityUnits.OFF));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        [Test]
        public void ShouldConvertOutputUnitsInIClamp(
            [Values(MultiClampInterop.OperatingMode.I0, MultiClampInterop.OperatingMode.IClamp)] MultiClampInterop.OperatingMode operatingMode
            )
        {
            var c = new Controller();
            var mc = new FakeMulticlampCommander();

            var mcd = new MultiClampDevice(mc, c,
                                           new Dictionary<MultiClampInterop.OperatingMode, IMeasurement>()
                                               {
                                                   {operatingMode, UNUSED_BACKGROUND}
                                               }
                );

            var data = new MultiClampInterop.MulticlampData()
                           {
                               OperatingMode = operatingMode,
                               ExternalCommandSensitivity = 2.5,
                               ExternalCommandSensitivityUnits = MultiClampInterop.ExternalCommandSensitivityUnits.A_V
                           };

            mc.FireParametersChanged(DateTimeOffset.Now, data);

            var cmd = new Measurement(20, -12, "A");

            var expected = operatingMode == MultiClampInterop.OperatingMode.I0 ?
                new Measurement(0, "V") :
                new Measurement(cmd.QuantityInBaseUnit / (decimal)data.ExternalCommandSensitivity,
                                           cmd.Exponent, "V");

            var actual = MultiClampDevice.ConvertOutput(cmd, data);

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void ShouldConvertOutputUnitsInVClamp(
            [Values(MultiClampInterop.OperatingMode.VClamp)] MultiClampInterop.OperatingMode operatingMode
            )
        {
            var data = new MultiClampInterop.MulticlampData()
                           {
                               OperatingMode = operatingMode,
                               ExternalCommandSensitivity = 2.5,
                               ExternalCommandSensitivityUnits = MultiClampInterop.ExternalCommandSensitivityUnits.V_V
                           };

            var cmd = new Measurement(20, -3, "V");

            var expected = new Measurement(cmd.QuantityInBaseUnit / (decimal)data.ExternalCommandSensitivity,
                                           cmd.Exponent, "V");

            var actual = MultiClampDevice.ConvertOutput(cmd, data);

            Assert.That(actual, Is.EqualTo(expected));
        }


        [Test]
        public void ShouldConvertInputUnitsInVClamp(
            [Values(MultiClampInterop.OperatingMode.VClamp)] MultiClampInterop.OperatingMode operatingMode,
            [Values(new object[]
                        {
                            MultiClampInterop.ScaleFactorUnits.V_A,
                            MultiClampInterop.ScaleFactorUnits.V_mA,
                            MultiClampInterop.ScaleFactorUnits.V_nA,
                            MultiClampInterop.ScaleFactorUnits.V_uA,
                            MultiClampInterop.ScaleFactorUnits.V_pA
                        })] MultiClampInterop.ScaleFactorUnits scaleFactorUnits,
            [Values(1, 2, 10)] double alpha
            )
        {
            var data = new MultiClampInterop.MulticlampData()
                           {
                               OperatingMode = operatingMode,
                               ScaleFactor = 2.5,
                               ScaleFactorUnits = scaleFactorUnits,
                               ScaledOutputSignal = MultiClampInterop.SignalIdentifier.AXMCD_OUT_SEC_VC_GLDR_I_MEMB,
                               Alpha = alpha
                           };

            var cmd = new Measurement(20, -3, "V");

            int exponent = 0;
            switch (scaleFactorUnits)
            {
                case MultiClampInterop.ScaleFactorUnits.V_V:
                    exponent = -3;
                    break;
                case MultiClampInterop.ScaleFactorUnits.V_mV:
                    exponent = 0;
                    break;
                case MultiClampInterop.ScaleFactorUnits.V_uV:
                    exponent = 3;
                    break;
                case MultiClampInterop.ScaleFactorUnits.V_A:
                    exponent = -12;
                    break;
                case MultiClampInterop.ScaleFactorUnits.V_mA:
                    exponent = -9;
                    break;
                case MultiClampInterop.ScaleFactorUnits.V_uA:
                    exponent = -6;
                    break;
                case MultiClampInterop.ScaleFactorUnits.V_nA:
                    exponent = -3;
                    break;
                case MultiClampInterop.ScaleFactorUnits.V_pA:
                    exponent = 0;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("scaleFactorUnits");
            }


            var expected = new Measurement((cmd.QuantityInBaseUnit / (decimal)data.ScaleFactor / (decimal)alpha) * (decimal)Math.Pow(10, -exponent), -12, "A");

            var actual = MultiClampDevice.ConvertInput(cmd, data);

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void ShouldConvertInputUnitsInIClamp(
            [Values(MultiClampInterop.OperatingMode.IClamp, MultiClampInterop.OperatingMode.I0)] MultiClampInterop.OperatingMode operatingMode,
            [Values(new object[]
                        {
                            MultiClampInterop.ScaleFactorUnits.V_mV,
                            MultiClampInterop.ScaleFactorUnits.V_uV,
                            MultiClampInterop.ScaleFactorUnits.V_V
                        })] MultiClampInterop.ScaleFactorUnits scaleFactorUnits,
            [Values(1, 2, 10, 20, 100)] double alpha
            )
        {
            var data = new MultiClampInterop.MulticlampData()
                           {
                               OperatingMode = operatingMode,
                               ScaleFactor = 2.5,
                               ScaleFactorUnits = scaleFactorUnits,
                               ScaledOutputSignal = MultiClampInterop.SignalIdentifier.AXMCD_OUT_PRI_IC_GLDR_V_MEMBx10,
                               Alpha = alpha
                           };

            var cmd = new Measurement(20, -3, "V");

            int exponent = 0;
            switch (scaleFactorUnits)
            {
                case MultiClampInterop.ScaleFactorUnits.V_V:
                    exponent = -3;
                    break;
                case MultiClampInterop.ScaleFactorUnits.V_mV:
                    exponent = 0;
                    break;
                case MultiClampInterop.ScaleFactorUnits.V_uV:
                    exponent = 3;
                    break;
                case MultiClampInterop.ScaleFactorUnits.V_A:
                    exponent = -12;
                    break;
                case MultiClampInterop.ScaleFactorUnits.V_mA:
                    exponent = -9;
                    break;
                case MultiClampInterop.ScaleFactorUnits.V_uA:
                    exponent = -6;
                    break;
                case MultiClampInterop.ScaleFactorUnits.V_nA:
                    exponent = -3;
                    break;
                case MultiClampInterop.ScaleFactorUnits.V_pA:
                    exponent = 0;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("scaleFactorUnits");
            }


            var expected = new Measurement((cmd.QuantityInBaseUnit / (decimal)data.ScaleFactor / (decimal)alpha) * (decimal)Math.Pow(10, -exponent), -3, "V");

            var actual = MultiClampDevice.ConvertInput(cmd, data);

            Assert.That(actual, Is.EqualTo(expected));
        }


        readonly private
        IDictionary<MultiClampInterop.OperatingMode, IMeasurement> UNUSED_BACKGROUND_DICTIONARY = new Dictionary<MultiClampInterop.OperatingMode, IMeasurement>()
    {
        {MultiClampInterop.OperatingMode.IClamp, new Measurement(0, "A")},
        {MultiClampInterop.OperatingMode.I0,new Measurement(0, "A")},
        {MultiClampInterop.OperatingMode.VClamp,new Measurement(0, "V")},
        
    };
        [Test]
        public void ShouldTakeMostRecentParametersForOutput()
        {

            var data1 = new MultiClampInterop.MulticlampData();
            var expected = new MultiClampInterop.MulticlampData();
            var data3 = new MultiClampInterop.MulticlampData();

            var c = new Controller();
            var mc = new FakeMulticlampCommander();

            var mcd = new MultiClampDevice(mc, c, UNUSED_BACKGROUND_DICTIONARY);
            mcd.BindStream(new DAQOutputStream(UNUSED_NAME));


            mc.FireParametersChanged(DateTimeOffset.Now.Subtract(TimeSpan.FromHours(1)), data1);
            mc.FireParametersChanged(DateTimeOffset.Now.Subtract(TimeSpan.FromMilliseconds(1)), expected);
            mc.FireParametersChanged(DateTimeOffset.Now.Add(TimeSpan.FromHours(1)), data3);

            Assert.That(((MultiClampParametersChangedArgs)mcd.DeviceParametersForOutput(DateTimeOffset.Now)).Data,
                Is.EqualTo(expected));
        }


        [Test]
        public void ShouldTakeMostRecentParametersForInput()
        {
            var c = new Controller();
            var mc = new FakeMulticlampCommander();

            var mcd = new MultiClampDevice(mc, c, UNUSED_BACKGROUND_DICTIONARY);
            mcd.BindStream(new DAQInputStream(UNUSED_NAME));

            var data1 = new MultiClampInterop.MulticlampData();
            var expected = new MultiClampInterop.MulticlampData();
            var data3 = new MultiClampInterop.MulticlampData();

            mc.FireParametersChanged(DateTimeOffset.Now.Subtract(TimeSpan.FromHours(1)), data1);
            mc.FireParametersChanged(DateTimeOffset.Now.Subtract(TimeSpan.FromMilliseconds(1)), expected);
            mc.FireParametersChanged(DateTimeOffset.Now.Add(TimeSpan.FromHours(1)), data3);

            Assert.That(((MultiClampParametersChangedArgs)mcd.DeviceParametersForInput(DateTimeOffset.Now)).Data,
                Is.EqualTo(expected));
        }

        [Test]
        public void ShouldProvideCurrentInputParameters()
        {
            var c = new Controller();
            var mc = new FakeMulticlampCommander();

            var mcd = new MultiClampDevice(mc, c, UNUSED_BACKGROUND_DICTIONARY);
            mcd.BindStream(new DAQInputStream(UNUSED_NAME));

            var expected = new MultiClampInterop.MulticlampData();

            mc.FireParametersChanged(DateTimeOffset.Now.Subtract(TimeSpan.FromHours(1)), expected);

            Assert.That(mcd.CurrentDeviceInputParameters.Data,
                Is.EqualTo(expected));
        }

        [Test]
        public void ShouldProvideCurrentInputParametersAfterMultipleParameterChangedEvents()
        {
            var c = new Controller();
            var mc = new FakeMulticlampCommander();

            var mcd = new MultiClampDevice(mc, c, UNUSED_BACKGROUND_DICTIONARY);
            mcd.BindStream(new DAQInputStream(UNUSED_NAME));

            var data1 = new MultiClampInterop.MulticlampData();
            var expected = new MultiClampInterop.MulticlampData();

            mc.FireParametersChanged(DateTimeOffset.Now.Subtract(TimeSpan.FromHours(1)), data1);
            mc.FireParametersChanged(DateTimeOffset.Now.Subtract(TimeSpan.FromHours(1)), expected);

            Assert.That(mcd.CurrentDeviceInputParameters.Data,
                Is.EqualTo(expected));
        }

        [Test]
        public void ShouldProvideCurrentOutputParameters()
        {
            var c = new Controller();
            var mc = new FakeMulticlampCommander();

            var mcd = new MultiClampDevice(mc, c, UNUSED_BACKGROUND_DICTIONARY);
            mcd.BindStream(new DAQOutputStream(UNUSED_NAME));

            var expected = new MultiClampInterop.MulticlampData();

            mc.FireParametersChanged(DateTimeOffset.Now.Subtract(TimeSpan.FromHours(1)), expected);

            Assert.That(mcd.CurrentDeviceOutputParameters.Data,
                Is.EqualTo(expected));
        }

        [Test]
        public void ShouldProvideCurrentOutputParametersAfterMultipleParameterChangedEvents()
        {
            var c = new Controller();
            var mc = new FakeMulticlampCommander();

            var mcd = new MultiClampDevice(mc, c, UNUSED_BACKGROUND_DICTIONARY);
            mcd.BindStream(new DAQOutputStream(UNUSED_NAME));

            var data1 = new MultiClampInterop.MulticlampData();
            var expected = new MultiClampInterop.MulticlampData();

            mc.FireParametersChanged(DateTimeOffset.Now.Subtract(TimeSpan.FromHours(1)), data1);
            mc.FireParametersChanged(DateTimeOffset.Now.Subtract(TimeSpan.FromHours(1)), expected);

            Assert.That(mcd.CurrentDeviceOutputParameters.Data,
                Is.EqualTo(expected));
        }


        //[Test]
        //public void ShouldIncludeMostRecentParametersInConfiguration()
        //{
        //    var c = new Controller();
        //    var mc = new FakeMulticlampCommander();

        //    var mcd = new MultiClampDevice(mc, c, UNUSED_BACKGROUND);
        //    mcd.BindStream(new DAQInputStream(UNUSED_NAME));

        //    var data1 = new MultiClampInterop.MulticlampData();
        //    var expected = new MultiClampInterop.MulticlampData()
        //                       {
        //                           Alpha = 1,
        //                           OperatingMode = MultiClampInterop.OperatingMode.VClamp
        //                       };
        //    var data3 = new MultiClampInterop.MulticlampData();

        //    mc.FireParametersChanged(DateTimeOffset.Now.Subtract(TimeSpan.FromHours(1)), data1);
        //    mc.FireParametersChanged(DateTimeOffset.Now.Subtract(TimeSpan.FromMilliseconds(1)), expected);
        //    mc.FireParametersChanged(DateTimeOffset.Now.Add(TimeSpan.FromHours(1)), data3);

        //    Assert.That(mcd.Configuration[MultiClampDevice.MultiClampDeviceConfigurationKey],
        //        Is.EqualTo(expected));
        //    Assert.That(((MultiClampInterop.MulticlampData)mcd.Configuration[MultiClampDevice.MultiClampDeviceConfigurationKey]).OperatingMode,
        //        Is.EqualTo(expected.OperatingMode));
        //}

        [Test]
        public void ShouldThrowExceptionConvertingOutputGivenEmptyParametersQueue()
        {
            var c = new Controller();
            var mc = new FakeMulticlampCommander();

            var mcd = new MultiClampDevice(mc, c, UNUSED_BACKGROUND_DICTIONARY);


            Assert.Throws<MultiClampDeviceException>(() => mcd.ConvertOutput(new Measurement(0, "V"), DateTimeOffset.Now));
        }


        [Test]
        public void ShouldThrowExceptionConvertingOutputGivenNoParametersForTime()
        {
            var c = new Controller();
            var mc = new FakeMulticlampCommander();

            var mcd = new MultiClampDevice(mc, c, UNUSED_BACKGROUND_DICTIONARY);
            mcd.BindStream(new DAQOutputStream(UNUSED_NAME));

            mc.FireParametersChanged(DateTimeOffset.Now.Add(TimeSpan.FromHours(1)), new MultiClampInterop.MulticlampData());


            Assert.Throws<MultiClampDeviceException>(() => mcd.ConvertOutput(new Measurement(0, "V"), DateTimeOffset.Now));
        }

        [Test]
        public void ShouldDiscardAllButMostRecentStaleOutputParametersAfterStalenessInterval()
        {
            var c = new Controller();
            var mc = new FakeMulticlampCommander();

            var mcd = new MultiClampDevice(mc, c, UNUSED_BACKGROUND_DICTIONARY);
            mcd.BindStream(new DAQOutputStream(UNUSED_NAME));

            var data1 = new MultiClampInterop.MulticlampData();
            var expected2 = new MultiClampInterop.MulticlampData();
            var expected = new MultiClampInterop.MulticlampData();

            var marker = DateTimeOffset.Now.ToUniversalTime();

            mc.FireParametersChanged(marker.Subtract(TimeSpan.FromSeconds(MultiClampDevice.ParameterStalenessInterval.TotalSeconds * 3)), data1);
            mc.FireParametersChanged(marker.Subtract(TimeSpan.FromSeconds(MultiClampDevice.ParameterStalenessInterval.TotalSeconds * 2)), expected);
            mc.FireParametersChanged(marker, expected2);

            Assert.That(((MultiClampParametersChangedArgs)mcd.DeviceParametersForOutput(DateTimeOffset.Now)).Data,
                Is.EqualTo(expected2));


            var oldTime = DateTimeOffset.Now.Subtract(MultiClampDevice.ParameterStalenessInterval);
            Assert.That(((MultiClampParametersChangedArgs)mcd.DeviceParametersForOutput(oldTime)).Data,
                Is.EqualTo(expected));

            var veryOldTime = marker.Subtract(TimeSpan.FromSeconds(MultiClampDevice.ParameterStalenessInterval.TotalSeconds * 3));
            Assert.Throws<MultiClampDeviceException>(() => mcd.DeviceParametersForOutput(veryOldTime));
        }

        [Test]
        public void ShouldThrowExceptionConvertingInputGivenEmptyParametersQueue()
        {
            var c = new Controller();
            var mc = new FakeMulticlampCommander();

            var mcd = new MultiClampDevice(mc, c, UNUSED_BACKGROUND_DICTIONARY);
            mcd.BindStream(new DAQInputStream(UNUSED_NAME));


            Assert.Throws<MultiClampDeviceException>(() => mcd.ConvertInput(new Measurement(0, "V"), DateTimeOffset.Now));

        }

        [Test]
        public void ShouldThrowExceptionConvertingInputGivenNoParametersForTime()
        {
            var c = new Controller();
            var mc = new FakeMulticlampCommander();

            var mcd = new MultiClampDevice(mc, c, UNUSED_BACKGROUND_DICTIONARY);
            mcd.BindStream(new DAQInputStream(UNUSED_NAME));

            mc.FireParametersChanged(DateTimeOffset.Now.Subtract(TimeSpan.FromHours(1)), new MultiClampInterop.MulticlampData());


            Assert.Throws<MultiClampDeviceException>(() => mcd.ConvertInput(new Measurement(0, "V"), DateTimeOffset.Now));
        }

        [Test]
        public void ShouldDiscardAllButMostRecentStaleInputParametersAfterStalenessInterval()
        {
            var c = new Controller();
            var mc = new FakeMulticlampCommander();

            var mcd = new MultiClampDevice(mc, c, UNUSED_BACKGROUND_DICTIONARY);
            mcd.BindStream(new DAQInputStream(UNUSED_NAME));

            var data1 = new MultiClampInterop.MulticlampData();
            var expected2 = new MultiClampInterop.MulticlampData();
            var expected = new MultiClampInterop.MulticlampData();

            var marker = DateTimeOffset.Now.ToUniversalTime();

            mc.FireParametersChanged(marker.Subtract(TimeSpan.FromSeconds(MultiClampDevice.ParameterStalenessInterval.TotalSeconds * 3)), data1);
            mc.FireParametersChanged(marker.Subtract(TimeSpan.FromSeconds(MultiClampDevice.ParameterStalenessInterval.TotalSeconds * 2)), expected);
            mc.FireParametersChanged(marker, expected2);

            Assert.That(((MultiClampParametersChangedArgs)mcd.DeviceParametersForInput(DateTimeOffset.Now)).Data,
                Is.EqualTo(expected2));


            var oldTime = DateTimeOffset.Now.Subtract(MultiClampDevice.ParameterStalenessInterval);
            Assert.That(((MultiClampParametersChangedArgs)mcd.DeviceParametersForInput(oldTime)).Data,
                Is.EqualTo(expected));

            var veryOldTime = marker.Subtract(TimeSpan.FromSeconds(MultiClampDevice.ParameterStalenessInterval.TotalSeconds * 3));
            Assert.Throws<MultiClampDeviceException>(() => mcd.DeviceParametersForInput(veryOldTime));
        }

        [Test]
        public void ShouldConvertBackgroundUnitsWithMostRecentOutputParametersInIClamp(
            [Values(MultiClampInterop.OperatingMode.I0, MultiClampInterop.OperatingMode.IClamp)] 
            MultiClampInterop.OperatingMode operatingMode
            )
        {
            BackgroundTest(operatingMode, "A", MultiClampInterop.ExternalCommandSensitivityUnits.A_V);
        }

        private void BackgroundTest(MultiClampInterop.OperatingMode operatingMode,
            string units,
            MultiClampInterop.ExternalCommandSensitivityUnits extSensUnits)
        {
            var c = new Controller();
            var mc = new FakeMulticlampCommander();

            var bg = new Measurement(2, -3, units);

            var background = new Dictionary<MultiClampInterop.OperatingMode, IMeasurement>() { { operatingMode, bg } };
            var mcd = new MultiClampDevice(mc, c, background);
            mcd.BindStream(new DAQOutputStream(UNUSED_NAME));

            var data = new MultiClampInterop.MulticlampData()
                           {
                               OperatingMode = operatingMode,
                               ExternalCommandSensitivity = 2.5,
                               ExternalCommandSensitivityUnits = extSensUnits
                           };

            mc.FireParametersChanged(DateTimeOffset.Now, data);


            var expected = operatingMode == MultiClampInterop.OperatingMode.I0 ? 
                new Measurement(0, "V") :
                new Measurement(bg.QuantityInBaseUnit / (decimal)data.ExternalCommandSensitivity, bg.Exponent, "V");

            mc.FireParametersChanged(DateTimeOffset.Now.Subtract(TimeSpan.FromHours(1)), data);

            var actual = mcd.OutputBackground;

            Assert.That(actual, Is.EqualTo(expected));
        }


        [Test]
        public void ShouldApplyBackgroundToStoppedStreams()
        {
            const string units = "V";
            const MultiClampInterop.OperatingMode vclampMode = MultiClampInterop.OperatingMode.VClamp;
            const MultiClampInterop.OperatingMode iclampMode = MultiClampInterop.OperatingMode.IClamp;


            var c = new Controller();
            var mc = new FakeMulticlampCommander();

            var vclampBackground = new Measurement(2, -3, units);

            var background = new Dictionary<MultiClampInterop.OperatingMode, IMeasurement>()
                                 {
                                     { vclampMode, vclampBackground }
                                 };

            var dataVClamp = new MultiClampInterop.MulticlampData()
            {
                OperatingMode = vclampMode,
                ExternalCommandSensitivity = 2.5,
                ExternalCommandSensitivityUnits = MultiClampInterop.ExternalCommandSensitivityUnits.V_V
            };


            var daq = new DynamicMock(typeof(IDAQController));
            var s = new DAQOutputStream("test", daq.MockInstance as IDAQController);


            var mcd = new MultiClampDevice(mc, c, background);
            mcd.BindStream(s);

            daq.ExpectAndReturn("get_Running", false);
            daq.Expect("ApplyStreamBackground", new object[] {s});

            mc.FireParametersChanged(DateTimeOffset.Now, dataVClamp);

            daq.Verify();
        }

        [Test]
        public void ShouldNotApplyBackgroundToRunningStreams()
        {
            const string units = "V";
            const MultiClampInterop.OperatingMode vclampMode = MultiClampInterop.OperatingMode.VClamp;
            const MultiClampInterop.OperatingMode iclampMode = MultiClampInterop.OperatingMode.IClamp;


            var c = new Controller();
            var mc = new FakeMulticlampCommander();

            var vclampBackground = new Measurement(2, -3, units);

            var background = new Dictionary<MultiClampInterop.OperatingMode, IMeasurement>()
                                 {
                                     { vclampMode, vclampBackground }
                                 };

            var dataVClamp = new MultiClampInterop.MulticlampData()
            {
                OperatingMode = vclampMode,
                ExternalCommandSensitivity = 2.5,
                ExternalCommandSensitivityUnits = MultiClampInterop.ExternalCommandSensitivityUnits.V_V
            };


            var daq = new DynamicMock(typeof(IDAQController));
            var s = new DAQOutputStream("test", daq.MockInstance as IDAQController);


            var mcd = new MultiClampDevice(mc, c, background);
            mcd.BindStream(s);

            daq.ExpectAndReturn("get_Running", true);
            daq.ExpectNoCall("ApplyStreamBackground");

            mc.FireParametersChanged(DateTimeOffset.Now, dataVClamp);

            daq.Verify();
        }

        [Test]
        public void ShouldAllowBackgroundValueChangeAfterConstruction()
        {
            const string units = "V";

            var c = new Controller();
            var mc = new FakeMulticlampCommander();

            var bg = new Measurement(2, -3, units);
            var operatingMode = MultiClampInterop.OperatingMode.VClamp;
            var background = new Dictionary<MultiClampInterop.OperatingMode, IMeasurement>() { { operatingMode, bg } };

            var mcd = new MultiClampDevice(mc, c, background);
            mcd.BindStream(new DAQOutputStream(UNUSED_NAME));

            var data = new MultiClampInterop.MulticlampData()
            {
                OperatingMode = operatingMode,
                ExternalCommandSensitivity = 2.5,
                ExternalCommandSensitivityUnits = MultiClampInterop.ExternalCommandSensitivityUnits.V_V
            };

            mc.FireParametersChanged(DateTimeOffset.Now, data);

            var newBackground = new Measurement(10, -3, units);

            mcd.Background = newBackground;

            var expected = new Measurement(newBackground.QuantityInBaseUnit / (decimal)data.ExternalCommandSensitivity, newBackground.Exponent, "V");

            Assert.That(mcd.OutputBackground, Is.EqualTo(expected));
        }

        [Test]
        public void ShouldSetBackgroundForMode()
        {
            const string units = "V";

            var c = new Controller();
            var mc = new FakeMulticlampCommander();

            var VClampBackground = new Measurement(2, -3, units);
            var operatingMode = MultiClampInterop.OperatingMode.VClamp;
            var background = new Dictionary<MultiClampInterop.OperatingMode, IMeasurement>() { { operatingMode, VClampBackground } };

            var mcd = new MultiClampDevice(mc, c, background);

            var IClampMode = MultiClampInterop.OperatingMode.IClamp;
            var IClampBackground = new Measurement(-1, "A");

            mcd.SetBackgroundForMode(IClampMode, IClampBackground);

            Assert.That(mcd.BackgroudForMode(IClampMode), Is.EqualTo(IClampBackground));
            Assert.That(mcd.BackgroudForMode(operatingMode), Is.EqualTo(VClampBackground));

        }


        [Test]
        public void ShouldUseBackgroundForMode()
        {
            const string VClampUnits = "V";
            const string IClampUnits = "A";

            Measurement VClampBackground = new Measurement(2, -3, VClampUnits);
            Measurement IClampBackground = new Measurement(-10, -3, IClampUnits);

            var c = new Controller();
            var mc = new FakeMulticlampCommander();

            var bg = new Dictionary<MultiClampInterop.OperatingMode, IMeasurement>()
                         {
                             {MultiClampInterop.OperatingMode.VClamp, VClampBackground},
                             {MultiClampInterop.OperatingMode.IClamp, IClampBackground},  
                         };

            var mcd = new MultiClampDevice(mc, c, bg);
            mcd.BindStream(new DAQOutputStream(UNUSED_NAME));

            var data = new MultiClampInterop.MulticlampData()
            {
                OperatingMode = MultiClampInterop.OperatingMode.VClamp,
                ExternalCommandSensitivity = 2.5,
                ExternalCommandSensitivityUnits = MultiClampInterop.ExternalCommandSensitivityUnits.V_V
            };

            mc.FireParametersChanged(DateTimeOffset.Now, data);

            Assert.That(mcd.OutputBackground, Is.EqualTo(MultiClampDevice.ConvertOutput(VClampBackground, mcd.CurrentDeviceOutputParameters.Data)));

            data = new MultiClampInterop.MulticlampData()
            {
                OperatingMode = MultiClampInterop.OperatingMode.IClamp,
                ExternalCommandSensitivity = 1.5,
                ExternalCommandSensitivityUnits = MultiClampInterop.ExternalCommandSensitivityUnits.A_V
            };

            mc.FireParametersChanged(DateTimeOffset.Now, data);

            Assert.That(mcd.OutputBackground, Is.EqualTo(MultiClampDevice.ConvertOutput(IClampBackground, mcd.CurrentDeviceOutputParameters.Data)));

        }


        [Test]
        public void ShouldAllowBackgroundDefinitionViaString()
        {
            const string VClampUnits = "V";
            const string IClampUnits = "A";

            Measurement VClampBackground = new Measurement(2, -3, VClampUnits);
            Measurement IClampBackground = new Measurement(-10, -3, IClampUnits);

            var c = new Controller();
            var mc = new FakeMulticlampCommander();

            var bg = new Dictionary<MultiClampInterop.OperatingMode, IMeasurement>()
                         {
                             {MultiClampInterop.OperatingMode.VClamp, VClampBackground},
                             {MultiClampInterop.OperatingMode.IClamp, IClampBackground},  
                         };

            var mcd = new MultiClampDevice(mc.SerialNumber, mc.Channel, c.Clock, c, new List<string>() { "VClamp", "IClamp" }, bg.Values);

            Assert.That(mcd.BackgroudForMode(MultiClampInterop.OperatingMode.VClamp), Is.EqualTo(VClampBackground));
            Assert.That(mcd.BackgroudForMode(MultiClampInterop.OperatingMode.IClamp), Is.EqualTo(IClampBackground));
        }

        [Test]
        public void ShouldConvertBackgroundUnitsWithMostRecentOutputParametersInVClamp()
        {
            BackgroundTest(MultiClampInterop.OperatingMode.VClamp,
                "V",
                MultiClampInterop.ExternalCommandSensitivityUnits.V_V);
        }

        private const string UNUSED_NAME = "UNUSED";
    }

    internal class FakeMulticlampCommander : IMultiClampCommander
    {
        public FakeMulticlampCommander()
        {
            SerialNumber = 0;
            Channel = 0;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
        }

        public event EventHandler<MultiClampParametersChangedArgs> ParametersChanged;

        public uint SerialNumber { get; set; }

        public uint Channel { get; set; }
        public void RequestTelegraphValue()
        {
            // pass
        }

        public void FireParametersChanged(DateTimeOffset time, MultiClampInterop.MulticlampData data)
        {
            var clock = new FakeClock(time);
            var args = new MultiClampParametersChangedArgs(clock, data);
            ParametersChanged(this, args);
        }

        

        internal class FakeClock : IClock
        {
            public FakeClock(DateTimeOffset time)
            {
                this.time = time;
            }


            protected DateTimeOffset time { get; set; }

            public DateTimeOffset Now
            {
                get { return time; }
            }
        }
    }
}
