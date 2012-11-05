.. include:: global.rst

.. _external-devices-chapter:

================
External Devices
================

Symphony External Device objects represent the physical devices present in the experimental setup. The |core| framework uses External Devices to manage interaction with computer-addressable hardware and to manage unit conversions associated with a particular device (see :ref:`measurements-and-units`).

This chapter describes the External Device definitions included with Symphony (in the ``Symphony.ExternalDevices`` assembly). For information about adding new External Device definitions, see :ref:`extending-symphony-chapter`.


Device properties
=================

All |device| instances have ``Name`` and ``Manufacturer`` properties. At runtime, each |device| associated with a |controller| must have a unique ``Name``. Each |device| must also have a |clock| instance and a ``Background`` value. The ``Background`` value is a |measurement| which is pushed to the device when acquisition stops. For example, a |device| representing an amplifier might have a ``Background`` value of 0 V, indicating that 0 V will be applied to the amplifier command whenever acquisition stops.

.. note:: Device ``Background`` is different than the Epoch ``Background``. Each |epoch| must specify a background |measurement| for *all* devices. This Epoch background is applied to the device by the output pipeline when no output data is available for that device (because no |stimulus| is defined for that device or beacuse the output data for that device has been exausted before the end of the Epoch).


.. _unit-converting-device:

UnitConvertingExternalDevice
============================

The ``UnitConvertingExternalDevice`` performs a static unit conversion on data that passes through the "device". To configure a ``UnitConvertingExternalDevice``, you must set its ``MeasurementConversionTarget`` attribute. The device will use ``Converters.Convert`` to convert all |measurement| elements in the data to this target units.

This example constructs a ``UnitConvertingExternalDevice`` that converts all |measurement| data to Volts::

    var devOut = new UnitConvertingExternalDevice("Device Name", "Device Manufacturuer", controller,
                                                 new Measurement(0, "V"))
                                                           {
                                                               MeasurementConversionTarget = "V",
                                                               Clock = daq
                                                           };

.. coalecing-device:

CoalescingDevice
================

The CoalescingDevice is a special kind of ExternalDevice that needs to coalesce (combine) multiple ``IInputData`` instances into a single ``IInputData`` for processing further up the pipeline. ``CoalescingDevice`` extends ``UnitConvertingExternalDevice`` and is constructed in the same manner. Unlike a ``UnitConvertingExternalDevice``, however, ``CoalescingDevice`` may be connected to more than one ``IHardwareStream`` instances. The Coalescing device coaleces one ``IInputData`` from each stream (waiting until all streams have pushed an ``IInputData``) before sending a single ``IInputData`` onward in the pipeline.

The ``Coalesce`` property of the ``CoalescingDevice`` must be set to a function (a .Net delegate) that takes an enumerable of ``IInputData`` and produces a single ``IInputData`` as its return value.


.. _multiclamp-device:

MultiClampDevice
================

The |s.mcd| represents a Molecular Devices MultiClamp amplifier. The |mcd| uses telegraph events from the MultiClamp Commander software to determine the state of the MultiClamp amplifier. In particular, |mcd| instances determine mode and gain of the amplifier and convert outgoing |measurement| elements in the desired output current (in I-clamp or I-0 mode) or membrane voltage (in V-clamp mode) to the correct amplifier command voltage. For input |measurement| elements from the amplifier, the |mcd| converts the line voltage to the measured membrane voltage in mV (in I-clamp and I-0 mode) or holding current in pA (in V-clamp mode).

You must specify a ``Background`` value for each mode. The |mcd| instance will choose the appropriate background value, as needed, depending on the current amplifier mode.

|s.mcd| uses a helper ``Symphony.ExternalDevices.MultiClampCommander`` instance to manage telegraph inputs from the MultiClamp Commander software. Thus, to create a |mcd| device instance, you must provide the ``MultiClampCommander`` instance::

    Controller controller = ...;
    IClock clock = ...;
    var mc = new MulticlampCommander(serialNumber, channel, clock);

    var bg = new Dictionary<MultiClampInterop.OperatingMode, IMeasurement>()
                 {
                     {MultiClampInterop.OperatingMode.VClamp, VClampBackground},
                     {MultiClampInterop.OperatingMode.IClamp, IClampBackground},  
                 };

    var mcd = new MultiClampDevice(mc, controller, bg);

