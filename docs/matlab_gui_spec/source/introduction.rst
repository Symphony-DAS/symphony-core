.. Copyright (c) 2011 Physion Consulting LLC

============
Introduction
============

Purpose
=======
The purpose of this document is to describe the functional specification of a Matlab GUI for the Symphony data acquisition framework.


User goals and requirements
===========================

Users wish to perform combined physiology and imaging experiments, acquiring data with the Symphony data acquisition system, alone or in combination with other systems. These experiments will be performed in dim illumination and under significant time pressure. User efficiency is thus of the utmost value.

The system should:

* Allow for user-specified experiment protocol and stimulus generation
* Provide a point-and-click interface for: choosing experimental protocol, starting and stopping acquisition
* Provide point-and-click or text-box entry for experimental protocol parameters
* Display recorded data from each epoch after completion of the epoch
* Optionally display user-specified online analysis or visualization of recorded data
* Allow user to specify output path for recorded data
* Integrate with the StimGL spatial display system


The overarching goal of Symphony is to provide the researcher with complete control over experiment protocol (what type of Epochs to present, in what order, etc.) and stimulus generation. To this end, the system should allow users to write "plugins" that describe the experiment protocol. To minimize user programming overhead, these plugins will be a set of stateless Matlab functions within a "plugin folder". See :ref:`plugin-discovery-chapter` and :ref:`protocol-api-chapter`.


Dependencies
============

The system depends on the ``Symphony.Core`` API and DLLs. The system will be instantiated with a reference to a single ``Symphony.Core.Controller`` instance. A ``+symphony`` package function to configure and instantiate a ``Controller`` is provided. See :ref:`symphony-api-chapter`::

    controller = <configured Symphony.Core.Controller>;
    gui = symphony.ExperimentUI(controller);
