.. Copyright (c) 2011 Physion Consulting LLC


.. _protocol-api-chapter:

===================
Protocol Plugin API
===================

A protocol plugin is responsible for describing the sequence of Epochs presented during an experiment. The protocol will likely have one or more parameters. For example, a protocol that presents a sinusoidal stimulus would likely have parameters specifying the amplitude, offset, frequency and phase of the sinusoid as well as duration of each Epoch, total number of epochs to present, etc. The value of these parameters is persisted with the data for each Epoch. 

The protocol may also require ongoing state information (such as the total number of Epochs presented) in order to decide what parameters to use for an upcoming Epoch, how many Epochs to present, etc.. Rather than requiring the user to implement a Matlab object that encapsulates this state, the system will provide a `state` ``struct`` to each function that requires state information. The protocol plugin ``NextProtocolParameters`` function may update this state structure. Unlike protocol parameters, these state parameters are *not* persisted with the data.


Protocol plugins for the system are a collection of Matlab functions within a unique file system folder (see :ref:`plugin-discovery-chapter`).

Default parameters
==================

Protocol plugins specify the default parameters as a Matlab MAT file, with struct values at the top-level of the MAT file. Symphony will create the default parameter values for a protocol like this::

    default_parameters = load('<plugins_folder>/<plugin>/DefaultParameters.mat');

When the user clicks "Save Defaults" in the protocol parameters window, the current parameters are written to this same file.


Required functions
==================

The required functions for a protocol "plugin" are described below. Each should be stateless and idempotent. In other words, these functions must always return the same values given the same input, even with multiple invocations. 

ProtocolInfo
************

Symphony stores a protocol identifier and version in the output data file. This identifier and version should uniquely identify the protocol (i.e. the code) that was used during the experiment. The protocol must provide the identifier and version as the return value of the ``ProtocolInfo`` function. It is suggested that users use reverse-domain style protocol identifiers (e.g. `org.hhmi.janelia.murphy.protocol1`) and the Subversion revision number as the protocol revision::

    function [protocolID,version] = ProtocolInfo()
        protocolID = 'org.hhmi.janelia.murphy.protocol1';
        version = svnRevision;
    end

DefaultState
************

When a protocol is activated (e.g. when the user selects the protocol from the protocol popup menu on the main UI; see :ref:`main-ui-controls-section`), the initial state is defined by the return value of ``DefaultState``. Any state fields required by subsequent functions should be set to their default values in this function. An example implementation sets the number of presented Epochs to 0::

    function state = DefaultState()
        state.numberOfPresentedEpochs = 0;
    end

ShouldContinue
**************

Symphony will continue to present Epochs sequentially until the protocol returns ``false`` from ``ShouldContinue``. The function receives the current protocol parameters and state ``structs`` as parameters. The function must have the following prototype::

    function done = ShouldContinue(params, state)


NextProtocolParameters
**********************

The protocol specifies the ``struct`` of parameters and the ``struct`` of state variables for the *next* Epoch in sequence given the current parameters and state structures. The function receives the current protocol parameters and state ``structs`` as parameters. The function must have the prototype::

    function [nextParams,nextState] = NextProtocolParameters(params, state)
    
    
EpochWithParameters
*******************

**TODO: EXAMPLE**

Symphony will call this function to construct an Epoch object with the given parameters. This function should instantiate a ``Symphony.Core.Epoch`` and add appropriate ``Symphony.Core.IStimulus`` and ``Symphony.Core.Responses`` to the Epoch. This function receives an instance of ``Symphony.Core.Controller`` as a second argument. The function receives the current protocol parameters as a first parameter. An example showing the basics of this API are shown below::

    function epoch = EpochWithParameters(params, controller)
        import Symphony.Core.*;
        
        epoch = Epoch(...);
        
        % for each desired stimulus
        stimData = stimulusDataWithParameters(params) % user-defined function
        dev = controller.ExternalDeviceWithName(devName)
        
        stim = Stimulus(...)
        
        epoch.AddStimulus(...)
        
        epoch.AddResponse(Response(controller.ExternalDeviceWithName(...), ...))
    end
        

.. _prepareepoch-api:

PrepareEpoch 
************

Because multiple Epochs may be queued by the Matlab (or future) GUIs, the protocol is given a chance to modify the Epoch immediately before being presented. The function receives the Epoch and protocol parameters and state ``structs`` *for that epoch* parameters. The function must have the following prototype::
    
    function PrepareEpoch(epoch, params, state)


EpochCompleted
**************

Upon completion of each Epoch, Symphony gives the protocol an opportunity to perform any desired online analysis, including figure display. The protocol may update the current state to reflect any accumulated online analysis or desired change in parameters given the results of that analysis. The function receives the Epoch and the  state ``struct`` *for that Epoch* as parameters. This function should not modify the Epoch. This function is called *after* the Epoch is persisted to disk and any changes made by this function will not be saved. The function must have the prototype::
    
    function nextState = EpochCompleted(epoch, state)