.. include:: global.rst

=============
Configuration
=============

This chapter describes configuration of the Symphony system. In particular, this chapter describes programatic construction of the :ref:`input-output-pipelines`.


Basic |core| pipeline setup
===========================

To build a |core| pipeline, start with the endpoints: a |controller| and a |hwbridge|. In this example, we create a ``HekaDAQBridge`` and a |controller|::

    Logging.ConfigureConsole();
    var daq = HekaDAQBridge.AvailableControllers().First()
    var controller = Controller(daq, daq); // use daq as the DAQ Bridge and canonical clock
    
Now, we can fill in |device| instances. In this example, we add a single ``UnitConvertingExternalDevice`` to the input and output pipelines, binding each to the ``ANALOG_OUT.1`` and ``ANALOG_IN.1`` streams of the DAQ controller respectively::

    var devOut = new UnitConvertingExternalDevice("Device_OUT_1", "Manufacturer", controller,
                                                 new Measurement(0, "V"))
                                                           {
                                                               MeasurementConversionTarget = "V",
                                                               Clock = daq
                                                           };
    devOut.BindStream((IDAQOutputStream)daq.GetStreams("ANALOG_OUT.1").First());
                                        
    var devIn = new UnitConvertingExternalDevice("Device_IN_1", "Manufacturer", controller,
                                                   new Measurement(0, "V"))
                                                   {
                                                       MeasurementConversionTarget = "V",
                                                       Clock = daq
                                                   };

    devIn.BindStream((IDAQOutputStream)daq.GetStreams("ANALOG_IN.1").First());
