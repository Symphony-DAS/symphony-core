.. include:: global.rst

.. _symphony-core-chapter:

=======================
Symphony.Core Framework
=======================

Symphony is a modern data acquisition system for physiology, built on the Windows .Net platform. The core functionality of Symphony is provided by the |core| framework. This chapter describes the basic concepts used by |core|:

* `Data Model`_
* `Input and Output Pipelines`_
* `Measurements and Units`_
* `Canonical Time`_

.. _data-model:

Data Model
==========

This section describes the object data model used by |core| to represent experimental data. The two fundamental classes in the Symphony data model are :ref:`Epochs <epoch-section>` and :ref:`Epoch Groups <epoch-group-section>`. ``Symphony.Core.Epoch`` instances represent "trials", discrete (though possibly contiguous) regions of the experimental timeline during which a stimulus is presented to and/or a measurement is made from the experimental preparation. |epochs| describe the stimuli to present to and the responses to record during the ``Epoch's`` region of the timeline.

|epochs| are grouped into |groups| to represent the larger structure of an experiment. For example, |epochs| may be grouped into |groups| to represent "control", "drug" and "wash" conditions in a classical physiology experiment. Each |epoch| belongs to one, and only one, |group|. |groups| may be further grouped into containing |groups| in a hierarchical relationship.

Each |group| is associated with the "source" of its data—the biological subject of the |epochs| contained in that |group|. Symphony does not explicitly represent the source, but |groups| may store an identifier of the source to be stored with the data for later reconciliation.

The following sections describe these core classes in more detail:

- :ref:`Epochs <epoch-section>`
- :ref:`Epoch Groups <epoch-group-section>`

Finally, this section finishes with a description of Symphony's representation of data in stimuli and responses, the ``IIOData`` interface and its subclasses ``IOutputData`` (for stimulus data) and  ``IInputData`` (for response data).

.. _epoch-group-section:

Epoch Groups
************

|groups| contain one or more |epochs|. An |group| may be used to represent a block of |epochs| that have a similar protocol or condition, or may be used to mark logical boundaries in an experiment.

Attributes
----------

|groups| have a ``label``, a user-specified identifier for the |group| and a start and end time (expressed as time in UTC plus a timezone offset). In addition, |groups| may have a list of ``keyword`` tags, and a dictionary (string-to-value map) of additional metadata ``properties``. The Symphony |group| also has a string attribute for storing the identifier of the ``Source``—the biological subject—of the data collected in the |group|. Presumably, this identifier will be used to link the data with the subject's record in an other database or data management system.


.. _epoch-section:

Epochs
******

An |epoch| is the fundamental unit of Symphony's acquisition system. An |epoch| represents a logical region of the experimental timeline in which stimuli are presented and contiguous measurement(s) are made. In a trial-based experiment, an |epoch| is one trial. 

The |controller| maintains a queue of Epochs and presents them in sequence. The |responses| from each |epoch| are persisted with that |epoch|, along with the parameters of the stimulus, protocol and relevant metadata.

Symphony assumes that each |epoch| is described by a "protocol" that describes which stimuli to present and which measurements (responses) to record. The protocol for an |epoch| is stored in the ``ProtocolID`` property and the parameters of that protocol in the ``ProtocolParameters``.

A |core| client creates an |epoch|, filling in the required attributes, then passes that |epoch| to a |controller| to be "run". The |controller| will present the ``Epoch's`` stimuli and record the ``Epoch's`` designated ``Responses`` until the longest-duration |stimulus| is exhausted (for definite |epochs|) or the |controller| the ``NextEpoch`` or  ``CancelEpoch`` methods are called. "Indefinite" |epochs| do not have a pre-determined duration and will be run until ``NextEpoch`` or ``CancelEpoch`` is called on the |controller| (see `Indefinite Epochs` below).

Background
----------

Each |epoch| must describe the |measurement| to be applied to each |device| in the output pipeline when (1) no |stimulus| is provided for that |device| by the |epoch| or (2) the |stimulus| for that |device| is exhausted before the end of the |epoch|. The ``Background`` property must be an ``IDictionary`` that maps |device| to a background |measurement|.

.. note:: The |epoch| ``Background`` is different than the |device| ``Background``. The |device| ``Background`` is applied when *no* |epoch| is being run.

To set the background for a device, use the ``SetBackground`` method. This example sets a background of 1.0 V at 1000 Hz as the "stimulus" to be sent to ``device`` if no |stimulus| is provided in this |epoch| or if the |stimulus| for ``device`` is exhausted before the end of the |epoch|::

    ExternalDevice device;
    IMeasurement background = new Measurement(1.0, "V");
    IMeasurement sampleRate = new Measurement(1000, "Hz");
    
    epoch.SetBackground(device, background measurement);

Stimuli
-------

The |epoch| ``Stimuli`` property is an ``IDictionary`` that maps |device| to |stimulus|. 

TODO

Responses
---------

The |epoch| ``Responses`` property is an ``IDictionary`` that maps |device| to |response|. Responses will be recorded from each |device| present in the ``Responses`` dictionary.

To record a |response| from a particular device during an |epoch|, add a |response| for that |device| to the ``Responses`` dictionary of the |epoch|::

    epoch.Responses[device] = new Response();


Indefinite Epochs
-----------------

The duration of an |epoch| is defined by the longest ``Duration`` of any of its stimuli. As noted above (see `Stimuli`), a |stimulus| is "indefinite" if it does not have a fixed duration. An |epoch| with one or more indefinite stimuli is also indefinite. The |epoch| ``IsIndefinite`` attribute will be ``true`` if the |epoch| is indefinite.

The |contorller| will present indefinite |epochs| until the ``NextEpoch`` or ``CancelEpoch`` methods of the |controller| are called. 

.. note:: Indefinite |epochs| may not define any responses (``Responses`` must be empty). In other words, you cannot record data during an indefinite |epoch|.



Representing Data
*****************

IIOData
-------


IInputData
----------

IOutputData
-----------





.. _input-output-pipelines:

Input and Output Pipelines
==========================

At its most fundamental, |core| manages two data pipelines. The first pipeline manages "stimulus" data (``IOutputData`` instances) as it travels from the computer to the experimental preparation. Symphony calls this pipeline the *output pipeline* because it contains data going "out" from the computer to the experimental preparation.

The second pipeline manages "response" data (``IInputData`` instances) as it travels from the experimental preparation to the computer. Symphony calls this pipeline the *input pipeline* because it contains data coming "in" form the experimental preparation to the computer.

Figure :ref:`1.1 <symphony-pipeline-figure>` shows the relationship between the experimental setup and the |core| pipelines. The input and output pipelines describe the flow of *data* between *nodes* of the pipeline. Before acquisition, the connections describing the flow of data must be specified. Each |epoch| defines the mapping from |stimulus| to |device| and from |device| to |response| objects. The |controller| must be told which |device| instances are present. These |device| instances must also be connected to appropriate |hwbridge| channels for input and output as necessary.

The *output pipeline* always begins with a |controller| that has a current |epoch| that represents the experimental trial currently being performed. Data flows from each |stimulus| object in this |epoch| to their designated |device|. From the device, data flows to a particular hardware channel (e.g. a DAC channel on a DAQ device).

.. _symphony-pipeline-figure:
.. figure:: images/symphony-pipelines.pdf
    :width: 95%

    **Relationship between physical experimental setup and |core| pipelines**. In the physical pipeline, stimulus data flows from the computer to the DAQ (e.g a Heka ITC), through an external device such as an amplifier to the experimental prep. Response data flows from the experimental preparation, through an external device such as an amplifier to the DAQ and finally to the computer. In the |core| pipeline, stimulus data flows from the computer to the |device| instance representing, e.g. an amplifier, which converts the data to the format required by that device (e.g. from a stimulus in pA to V sent to the amplifier), then to the |hwbridge| which converts the desired data (at the amplifier) to the DAQ's required input (e.g. DAQ counts). The response pathway in |core| reverses this process, producing, at the computer, data in physical units such as mV for current-clamp data and pA for voltage-clamp data.

Validation
**********

Before starting acquisition, |core| validates the input and output pipelines. Validation fails if any nodes is misconfigured or does not have the required connections. Most nodes will fail validation if any sampling rate quantities do not have "Hz" units.


.. _measurements-and-units:

Measurements and Units
======================

Numeric quantities in |core| are represented as instances of |measurement|. |measurement| includes information about the quantity and its units. To minimize unexpected numerical precision issues, |measurement| represents the quantity as a .Net ``decimal`` type. Units are represented as a ``BaseUnit`` (e.g. "V", "A", etc.) and an integer exponent. So, for example, 2.5 mV is represented as a |measurement| with a ``Quanitty`` of ``2.5m``, a ``BaseUnit`` of "V" and an ``Exponent`` of ``-3``.

Validation
**********

Nodes the the |core| input and output piplines—|device| and |hwbridge| instances—validate data's units and will stop acquisition if data passed to them has unexpected units.

Unit conversion
***************

When unit conversions are required (e.g converting DAQ counts to Volts), |core| uses the |converter| service. |converter| stores all of the available unit conversions and will select the appropriate conversion routine according to the units of the |measurement| to be converted and the desired units.

To convert a |measurement|, use ``Converters.Convert``. This example shows how to convert a |measurement| ``measurement`` to Volts::

    Converters.Convert(measurement, "V")
    
If no converter is available from ``measurement's`` units to Volts, ``Converters.Convert`` throws an exception.

For information on adding new unit converts to Symphony, see :ref:`extending-symphony-chapter`.


.. _canonical-time:

Canonical Time
==============

In many experimental configurations, data is acquired from multiple devices. Each device typically has its own, internal, clock. Although usually very accurate, these clocks are not guaranteed to be synchronized. Therefore, aligning data from multiple devices can be a challenge. To address this issue, Symphony defines a single "canonical" clock for all nodes in the input and output pipelines. All elements in |core| that produce timestamps or timestamped events require a |clock| instance from which they draw these time stamps. Assuming a particular hardware component can supply timestamps with minimal latency, it may be used as the canonical clock for the |core| pipelines. Typical |clock| instances are the computer (CPU clock) or a DAQ device's internal hardware clock.