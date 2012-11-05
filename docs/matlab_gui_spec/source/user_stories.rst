.. Copyright (c) 2011 Physion Consulting LLC


.. _user-stories-chapter:

============
User stories
============

This chapter describes several user stories (use cases) of interaction with the system. For each, the expected interaction between the Matlab GUI, Symphony and protocol plugin(s) are specified.



Starting a new Epoch Group
==========================

Given an open Matlab GUI, when the user presses the "New Epoch Group" button:

    * The system should present a modal dialog for choosing the path for output, the label and the source for the new Epoch group.
    * System calls ``Sympony.Core.Controller.StartEpochGroup``, passing the epoch group label, source, and output path. The returned ``Persistor`` must be passed to ``Symphony.Core.Controller.RunEpoch`` below.
    * System should write a MAT file containing relevant experiment metadata (from the "Experiment" group of the Main UI) to the same containing folder as the output file specified above.
    

Selecting the experiment protocol
=================================

Given an open Matlab GUI in the "stopped" state, when the user selects a protocol from the "Protocol" popup menu in the Main GUI that is *not* the current protocol and has *not* been the current protocol during this run of the Matlab GUI:
    
    * System calls ``DefaultState()`` on the new protocol and saves the result as the *current protocol state*.
    * System automatically generates the Protocol Parameters UI window and applies the current protocol parameters.
    

Given an open Matlab GUI in the "stopped" state, when the user selects a protocol from the "Protocol" popup menu in the Main GUI that is *not* the current protocol and has been the current protocol during this run of the Matlab GUI:

        * System sets the previous protocol parameters for the newly selected protocol as the *current protocol parameters*.
        * System automatically generates the Protocol Parameters UI window and applies the current protocol parameters.
    
Given an open Matlab GUI, when the user selects a protocol from the "Protocol" popup menu in the Main GUI that *is* the current protocol, then there is no effect on system state.

Given an open Matlab GUI in the "running" state, when the user selects a protocol from the "Protocol" popup menu in the Main GUI that is *not* the current protocol, then the system should behave as if the user had pressed the "Stop" button and then selected the new protocol. 


Starting and stopping acquisition
=================================

Given an open Matlab GUI window in the "stopped" state with a selected current protocol, and valid (non-empty) Epoch Group label and output path and non-null Epoch Group source, when the user presses the "Start" button:

    * System sets the Matlab GUI state to the "running" state
    * System changes label of the Start/Stop button in the Main UI to "Stop"
    * System calls ``DefaultState()`` on the new protocol and saves the result as the *current protocol state*.
    * While ``ShouldContinue()`` returns true and while the Matlab GUI is the "running" state:
    
        * System calls ``NextProtocolParameters``, updating the current protocol parameters and state.
        * System calls ``EpochWithParameters`` to generate the next Epoch to present
        * System calls ``PrepareEpoch``
        * System calls ``Symphony.Core.Controller.RunEpoch``, passing the ``Persistor`` from above, the current Epoch and a flag indicating whether the Epoch should be persisted to disk (taken from the checkbox in the Main UI)
        * System calls ``EpochCompleted`` with the resulting epoch
        * System plots all Response objects on Epoch in the Results Display
    * System sets state to "stopped" state
    * System changes label of the Start/Stop button in the Main UI to "Start"

Given an open Matlab GUI window in the "running" state, when the user presses the "Stop" button:

    * System sets the Matlab GUI state to the "stopped" state
    * System changes label of the Start/Stop button in the Main UI to "Start"


The system should not allow the user to start acquisition with an empty epoch group label, null epoch group source or empty output path.