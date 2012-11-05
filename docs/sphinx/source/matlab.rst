.. include:: global.rst

==========================
Using Symphony from Matlab
==========================

Windows versions of Matlab include a CLR bridge that allows Matlab code to instantiate CLR classes and to call methods on those instances. Matlab users may make use of the CLR bridge to use |core| from Windows versions of Matlab [#]_. |core| supports Matlab version 7.12 (R2010b) and later.

This chapter describes particular aspects of the |core| API relevant to Matlab users.


Setup
=====

Before using Symphony from Matlab, you must add the Symphony .Net assemblies to the CLR bridge::

    % Path to installed Symphony assemblies
    symphonyPath = 'C:\Program Files\Physion\Symphony\bin';

    % Add Symphony.Core assemblies
    NET.addAssembly(fullfile(symphonyPath, 'Symphony.Core.dll'));
    NET.addAssembly(fullfile(symphonyPath, 'Symphony.ExternalDevices.dll'));
    NET.addAssembly(fullfile(symphonyPath, 'HekaDAQInterface.dll'));
    NET.addAssembly(fullfile(symphonyPath, 'Symphony.SimulationDAQController.dll'));


Converting Matlab vectors to ``IEnumerable<IMeasurement>``
==========================================================

Output data must be given to |core| as an enumerable of |measurement| instances. Creating individual |measurement| instances in Matlab is slow due to inherent performance penalties in the CLR bridge. Instead, you can convert a Matlab vector to an ``IEnumerable<IMeasurement>`` in a single call::

    measurementList = Measurement.FromArray(matlabVector, units);

The resulting ``measurementList`` will contain |measurement| elements with the given units.


Converting ``IEnumerable<IMeasurement>`` to Matlab vectors
==========================================================

Given an ``IEnumerable<IMeasurement>``, such as the ``Data`` of an ``IInputData``, the ``ToQuantityArray`` method will return a Matlab vector (using double-precision floating point values)::
    
    measurements = inputData.Data;
    matlabVector = measurements.ToQuantityArray()
    
``ToQuantityArray`` will throw an exception if the ``BaseUnit``s are not homogenous in the list. Because Matlab does not track units of quantities, ``ToQuantityArray`` converts the measurement ``Quantities`` to their value in the ``BaseUnit``. For example, a |measurement| of 1 mV (a ``BaseUnit`` of "V") will have a quantity of 0.001 in the returned quantity vector.

To retrieve the (single) ``BaseUnit`` for the measurements, you can use the ``Units`` method::

    unitString = measurements.Units()

Again, ``Units`` will throw an exception if the |measurement| elements of ``measurements`` do not all have the same ``BaseUnit``.

Using Matlab functions as .Net delegates
========================================

Wherever |core| takes a .Net delegate (e.g. as a unit conversion ``ConvertProc``, or a ``DelegatedStimulus`` ``BlockRenderer`` or ``DurationCalculator``), you may supply a Matlab function handle. This example registers a unit conversion function from Matlab::

    function outMeasurement = convert_proc(inMeasurement)
        import Symphony.Core.*;
        
        % calculate a newQuantity from inMeasurement...
        
        outMeasurement = Measurement(newQuantity, newUnits);
    end
    
    Converters.Register(fromUnits, toUnits, @convert_proc)

.. note:: Be ware that using a Matlab function in place of a .Net delegate will incur a significant performance penalty due to inherent limitations in the CLR bridge.

.. [#] Matlab is a registered trademark of The MathWorks, Inc.