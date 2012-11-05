.. Copyright (c) 2011 Physion Consulting LLC


.. _stimgl-chapter:

==================
StimGL integration
==================

The Symphony Matlab GUI should allow users to conduct experiments using the StimGL visual stimulus rendering system.

StimGL has a Matlab API and can be configured to trigger a stimulus with upon a TTL input switch. The Symphony Matlab GUI should take advantage of this existing infrastructure in interacting with StimGL. One possible point of interaction is the experiment protocol's :ref:`prepareepoch-api` function to send StimGL commands and set the StimGL system to trigger on a TTL pulse.

A protocol that wishes to present an Epoch using the StimGL system would then (1) add a stimulus to the Epoch which produces a TTL pulse at the desired time to trigger StimGL presentation and (2) add StimGL logic to the ``PrepareEpoch`` function::

    function PrepareEpoch(epoch, params, state)
        
        <send StimGL stimulus params, taken from params & state>
        <set StimGL to trigger on TTL>
        
    end
